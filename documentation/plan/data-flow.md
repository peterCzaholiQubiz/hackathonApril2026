# Data Flow: Customer Portfolio Thermometer

This document describes the end-to-end data flow through the five stages of the system: CRM Import, Risk Scoring, AI Explanation Generation, API Serving, and Frontend Rendering.

---

## 1. CRM Import Flow

### Source files

The CRM source is the `crm-data/` folder — pseudonymised Dutch energy-sector data exported from an ERP/CRM system. All files are UTF-8 with BOM, comma-delimited. The folder ships with the repo; no external database connection is required.

```
crm-data/ERPSQLServer/
  Organizations.csv               → customers (OrganizationTypeId=2)
  Contracts.csv                   → contracts
  Connections.csv                 → energy connection points
  ConnectionMeterReads.csv        → meter read aggregates
  OrganizationContacts.csv        → org-level interactions
  ConnectionContacts.csv          → connection-level interactions
  LastConnectionContacts.csv      → latest contact per connection
  Contract-Customer-Connection-BrokerDebtor.csv  → reporting join
  [ValueAQuery] DQE - Prijzen v5 met Organization.csv  → price history
  [ValueAQuery] DQE - Captars.csv                      → network tariffs
  [ValueAQuery] ASU001.csv                             → annual standard usage
  ProductTypes.csv / OrganizationTypes.csv / ConnectionTypes.csv  → lookups

crm-data/ArchievingSolution/
  [Confidential] Look-up Customer Data_1.csv   → invoice archive index (part 1)
  [Confidential] Look-up Customer Data_2.csv   → invoice archive index (part 2)
  Generic/Contract Price.csv                   → contract tariff lines
  Generic/Price Proposition.csv                → catalogue tariffs
  Generic/Meter Read_1.csv … Meter Read_8.csv  → full EAN meter history
  Generic/Timeslices - *.csv                   → EAN status/classification history
```

### Import sequence

```
crm-data/ CSV files (read from disk, UTF-8 BOM stripped)
        |
        | Step 1 — Lookup tables (no FK deps)
        |   ProductTypes.csv → seed product_types
        |   OrganizationTypes.csv → seed organization_types
        |   ConnectionTypes.csv → seed connection_types
        v
CrmImportService
        |
        | Step 2 — Organizations (OrganizationTypeId=2 → customers)
        |   Organizations.csv
        |   crm_external_id = OrganizationId
        |   Upsert → customers table
        |
        | Step 3 — Contracts
        |   Contracts.csv (ContractId, StartDate, EndDate, CurrentAgreedAmount)
        |   Contract Price.csv (price lines per ContractUniqueIdentifier)
        |   Price Proposition.csv (catalogue tariffs)
        |   Upsert → contracts table
        |
        | Step 4 — Connections (energy-specific)
        |   Connections.csv (EAN, ProductType, DeliveryType)
        |   Upsert → connections table
        |
        | Step 5 — Meter reads
        |   ConnectionMeterReads.csv (aggregated, per connection)
        |   Meter Read_1–8.csv (full history per EAN, scan all 8)
        |   Upsert → meter_reads table
        |
        | Step 6 — Interactions / contacts
        |   OrganizationContacts.csv  → interactions (org level)
        |   ConnectionContacts.csv    → interactions (connection level)
        |   Subject field maps: "Cancellation" → churn signal
        |   Upsert → interactions table
        |
        | Step 7 — Invoice archive index
        |   Look-up Customer Data_1.csv + _2.csv
        |   (Customer number, Debtor number, Invoice number)
        |   Upsert → invoices table
        v
PostgreSQL (CRM mirror tables)
```

### Constraints

- **Strictly read-only against CRM**: Files are read from disk. No write is ever issued against the source files.
- **Strip UTF-8 BOM**: All files may begin with `\uFEFF`; strip before parsing.
- **Open-ended dates**: `EndDate` values `9999-12-31` or `99991231` → store as `null` or `DateTime.MaxValue`.
- **Idempotent upserts**: Records are matched by `crm_external_id`. Re-importing the same data produces the same result.
- **Ordering**: Customers → Contracts → Connections → Meter reads → Interactions → Invoices (FK dependency order).
- **Transaction scope**: Each step runs in its own transaction. Failure in one step rolls back only that step.

---

## 2. Risk Scoring Flow

```
PostgreSQL (CRM mirror tables)
        |
        | Read: customers + contracts + invoices +
        |       payments + complaints + interactions
        v
RiskScoringEngine
        |
        | For each active customer:
        |   1. Evaluate churn signals -> churn_score (0-100, capped)
        |   2. Evaluate payment signals -> payment_score (0-100, capped)
        |   3. Evaluate margin signals -> margin_score (0-100, capped)
        |   4. Compute overall_score = (churn*0.40 + payment*0.35 + margin*0.25)
        |   5. Assign heat_level: 0-39=green, 40-69=yellow, 70-100=red
        |   6. Write risk_scores row linked to portfolio_snapshot
        v
PostgreSQL (risk_scores table)
```

### Constraints

- **Deterministic**: Given the same input data, the engine always produces the same scores. No randomness, no external calls.
- **Score capping**: Individual dimension scores are capped at 100. Signal weights are additive; if the sum exceeds 100, it is clamped.
- **Snapshot binding**: All risk scores from a single scoring run are linked to one `portfolio_snapshots` row, enabling historical comparison.
- **No AI involvement**: The scoring engine uses only explicit rules with documented signal weights. AI is never consulted for score computation.

---

## 3. AI Explanation Generation Flow

```
PostgreSQL (risk_scores + CRM mirror tables)
        |
        | Read: risk_scores + contributing signal data per customer
        v
ClaudeExplanationService
        |
        | 1. Identify customers needing (re)generation:
        |    - New risk scores without explanations
        |    - Scores that changed since last explanation
        |
        | 2. Build batches of 10 customers
        |
        | 3. For each batch (max 5 concurrent via SemaphoreSlim):
        |    a. Construct explanation prompt:
        |       - Customer context (segment, tenure, contract status)
        |       - Risk scores per dimension
        |       - Contributing signals with values
        |       - Instruction: produce JSON with explanation + confidence
        |    b. Call Claude API (claude-sonnet-4-6)
        |    c. Parse JSON response
        |    d. Write risk_explanations rows
        |
        | 4. For each batch:
        |    a. Construct actions prompt:
        |       - All three risk explanations + scores
        |       - Customer context
        |       - Instruction: produce JSON array of actions
        |    b. Call Claude API (claude-sonnet-4-6)
        |    c. Parse JSON response
        |    d. Write suggested_actions rows
        |
        | 5. On API failure: write placeholder with confidence "low"
        v
PostgreSQL (risk_explanations + suggested_actions)
```

### Constraints

- **Post-scoring only**: Explanations are generated after scores are finalized. The AI never influences scores.
- **Caching**: Explanations are only regenerated when the underlying risk scores change. If scores are identical to the previous run, existing explanations are retained.
- **Batching**: 10 customers per API call to balance latency and token usage.
- **Throttling**: Maximum 5 concurrent API calls via SemaphoreSlim to respect rate limits.
- **Retry**: Exponential backoff on transient failures (429, 500, 503). Maximum 3 retries per batch.
- **Fallback**: If all retries fail, a placeholder explanation is stored: `"Explanation temporarily unavailable. Risk score is based on: [signal list]."` with confidence `"low"`.
- **Model tracking**: Every explanation row records `model_used = "claude-sonnet-4-6"` for auditability.

---

## 4. API Serving Flow

```
Angular Frontend
        |
        | HTTP GET requests (JSON)
        v
ASP.NET Core Controllers
        |
        | Route to appropriate controller:
        |
        | PortfolioController:
        |   GET /api/portfolio/current   -> latest portfolio_snapshot
        |   GET /api/portfolio/history   -> list of snapshots (time series)
        |   GET /api/portfolio/segments  -> segment_breakdown from snapshot
        |
        | CustomersController:
        |   GET /api/customers           -> paginated list (filter by segment,
        |                                   heat level; sort; search)
        |   GET /api/customers/{id}      -> full detail with contracts,
        |                                   invoices, payments
        |   GET /api/customers/{id}/risk -> risk_scores + risk_explanations
        |                                   + suggested_actions
        |   GET /api/customers/{id}/interactions -> interaction history
        |   GET /api/customers/{id}/complaints   -> complaint history
        |
        | RiskScoresController:
        |   GET /api/risk/distribution   -> histogram of score distribution
        |   GET /api/risk/top-at-risk    -> top N by risk type
        |   GET /api/risk/groups         -> customers grouped by heat level
        |
        | ImportController:
        |   POST /api/import/trigger     -> kicks off full pipeline
        |   GET  /api/import/status      -> last run status
        |
        v
Repository Layer (EF Core)
        |
        | LINQ -> SQL via Npgsql
        v
PostgreSQL
```

### Constraints

- **Read-heavy**: All GET endpoints are pure reads against the analytics database. The only write-triggering endpoint is `POST /api/import/trigger`.
- **Pagination**: Customer list endpoints use offset pagination with configurable `page` and `pageSize` parameters (default 20, max 100).
- **Response envelope**: All responses follow a consistent structure with data payload and metadata (total count for paginated results).
- **CORS**: Configured to allow requests only from the Angular frontend origin.
- **Error handling**: Consistent error response format with status code, error message, and request correlation ID. No internal details leaked.
- **No authentication**: Hackathon scope. The system runs on a local network or single machine. Authentication is out of scope but the architecture allows middleware insertion.

---

## 5. Frontend Rendering Flow

```
User opens browser
        |
        v
Angular SPA loads (static assets from container or dev server)
        |
        | On route navigation:
        v
+-- / (Dashboard) ------------------------------------------------+
|   1. portfolio.service.ts -> GET /api/portfolio/current          |
|   2. portfolio.service.ts -> GET /api/portfolio/segments         |
|   3. risk.service.ts      -> GET /api/risk/top-at-risk?limit=10  |
|   4. portfolio.service.ts -> GET /api/portfolio/history          |
|                                                                  |
|   Renders:                                                       |
|   - Donut chart: green/yellow/red distribution                   |
|   - Stacked bar chart: per-segment breakdown                     |
|   - Table: top 10 at-risk customers                              |
|   - Line chart: portfolio health trend over snapshots            |
+------------------------------------------------------------------+

+-- /customers (Customer List) ------------------------------------+
|   1. customer.service.ts -> GET /api/customers?segment=&heat=&   |
|                              page=1&pageSize=20                  |
|   Renders:                                                       |
|   - Filterable, sortable table with heat badges                  |
|   - Pagination controls                                          |
|   - Click row -> navigate to /customers/:id                      |
+------------------------------------------------------------------+

+-- /customers/:id (Customer Detail) ------------------------------+
|   1. customer.service.ts -> GET /api/customers/{id}              |
|   2. risk.service.ts     -> GET /api/customers/{id}/risk         |
|   3. customer.service.ts -> GET /api/customers/{id}/interactions |
|   4. customer.service.ts -> GET /api/customers/{id}/complaints   |
|                                                                  |
|   Renders:                                                       |
|   - Three risk gauges (churn, payment, margin)                   |
|   - AI-generated explanation panel (labeled "AI Advisory")       |
|   - Suggested actions with priority badges                       |
|   - Interaction + complaint timeline                             |
|   - Contract and invoice summary                                 |
+------------------------------------------------------------------+

+-- /risk-groups (Risk Groups) ------------------------------------+
|   1. risk.service.ts -> GET /api/risk/groups                     |
|   2. risk.service.ts -> GET /api/risk/distribution               |
|                                                                  |
|   Renders:                                                       |
|   - Customers grouped by dominant risk type                      |
|   - Distribution histogram                                       |
+------------------------------------------------------------------+
```

### Constraints

- **Parallel requests**: Independent API calls on a single route are fired in parallel (e.g., dashboard issues 4 requests concurrently).
- **Loading states**: Every data-dependent section shows a skeleton loader until its request resolves. No blank panels.
- **Error states**: Failed requests display a user-friendly error with a retry option. The rest of the page remains functional.
- **AI label transparency**: All Claude-generated content is visually labeled as "AI-generated advisory" with an info tooltip explaining that scores are rule-based and explanations are AI-generated.
- **No client-side caching of risk data**: Data is always fetched fresh to reflect the latest import run.
- **Responsive**: The dashboard targets 1024px minimum but degrades gracefully to 768px for tablet demos.

---

## End-to-End Pipeline Sequence

The full pipeline executes when a user triggers an import:

```
1.  User clicks "Run Import" in Angular
2.  Angular -> POST /api/import/trigger
3.  ImportController starts pipeline asynchronously, returns 202 Accepted
4.  CrmImportService reads CRM source, upserts to PostgreSQL        [~5-10s]
5.  RiskScoringEngine scores all active customers                    [~1-2s]
6.  PortfolioAggregationService computes snapshot                    [~1s]
7.  ClaudeExplanationService generates explanations + actions        [~60-120s for 100 customers]
8.  Import status updated to "complete"
9.  Angular polls GET /api/import/status until complete
10. Angular refreshes dashboard data
```

Total pipeline time for 100 customers: approximately 1-2 minutes, dominated by Claude API calls in step 7. The UI remains responsive throughout, showing progress via the import status endpoint.
