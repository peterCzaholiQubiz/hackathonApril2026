# Implementation Plan: Customer Portfolio Thermometer

## Overview

A read-only analytics layer that imports CRM data into a local PostgreSQL database, computes risk scores (churn, payment, margin) using deterministic rules, enriches those scores with natural language explanations and suggested actions via the Claude API, and presents everything through an Angular dashboard with a portfolio heatmap, customer detail views, and action panels.

## Stack
- **Backend**: .NET 10 (ASP.NET Core Web API)
- **Database**: PostgreSQL 16
- **Frontend**: Angular (latest)
- **AI**: Azure OpenAI — deployment `gpt-4o`

## CRM CSV Data Source

The system consumes real pseudonymised energy-sector CRM data from the `crm-data/` folder at the repo root. All field values that could identify a person, organisation, or asset have been replaced with stable hash tokens — joins across files still work, but original values cannot be recovered.

### Folder layout

```
crm-data/
├── ArchievingSolution/
│   ├── [Confidential] Look-up Customer Data_1.csv   # invoice archive index (part 1)
│   ├── [Confidential] Look-up Customer Data_2.csv   # invoice archive index (part 2)
│   └── Generic/
│       ├── [Confidential] Contract Price.csv         # contract-specific tariff lines
│       ├── [Confidential] Meter Read_1.csv           # meter reading history (1 of 8)
│       │   … Meter Read_2 through Meter Read_8.csv
│       ├── [Confidential] Price Proposition.csv      # catalogue/proposition tariffs
│       ├── [Confidential] Timeslices - CaptarCode.csv
│       ├── [Confidential] Timeslices - ConnectionType.csv
│       ├── [Confidential] Timeslices - EnergyDeliveryStatus.csv
│       ├── [Confidential] Timeslices - PhysicalStatus.csv
│       ├── [Confidential] Timeslices - Profile.csv
│       ├── [Confidential] Timeslices - ResidentialFunction.csv
│       └── [Confidential] Timeslices - UsageType.csv
└── ERPSQLServer/
    ├── [Confidential] Organizations.csv              # master party table (customers, brokers, collectives)
    ├── [Confidential] OrganizationTypes.csv          # lookup: 1=HeadOrg 2=Customer 5=Collective 6=Company 7=Broker
    ├── [Confidential] Connections.csv                # energy connection points (EAN codes)
    ├── [Confidential] ConnectionTypes.csv            # lookup: CHP, Biomass, WindTurbine, MainConnection …
    ├── [Confidential] Contracts.csv                  # contract master (Customer + Period contracts)
    ├── [Confidential] ProductTypes.csv               # lookup: 1=Electricity 2=Gas
    ├── [Confidential] OrganizationContacts.csv       # org-level interaction/contact history
    ├── [Confidential] ConnectionContacts.csv         # connection-level interaction history
    ├── [Confidential] LastConnectionContacts.csv     # most-recent contact per connection
    ├── [Confidential] Contract-Customer-Connection-BrokerDebtor.csv   # flattened reporting join
    ├── [Confidential] ConnectionMeterReads.csv       # aggregated meter reads per connection
    ├── [Confidential] [ValueAQuery] ASU001.csv       # annual standard usage (SJV) per EAN
    ├── [Confidential] [ValueAQuery] CPY001.csv       # time-sliced connection properties
    ├── [Confidential] [ValueAQuery] DQE - Captars.csv              # network tariff history per EAN
    ├── [Confidential] [ValueAQuery] DQE - Prijzen v5 met Organization.csv   # price component history
    └── [Confidential] [ValueAQuery] ERPMRE.csv       # detailed meter-read event fact table
```

All files: UTF-8 with BOM, comma-delimited. End-dates of `9999-12-31` or `99991231` mean open-ended.

TypeScript interfaces for every file are in `frontend/src/app/core/models/crm-schema.model.ts`.

### CSV → Domain model mapping

| Domain entity | Primary CSV source | Key join columns |
|---|---|---|
| `customers` | `Organizations.csv` (OrganizationTypeId = 2) | `OrganizationId` → `crm_external_id` |
| `contracts` | `Contracts.csv` | `ContractId` → `crm_external_id`; `CurrentAgreedAmount` → `monthly_value` |
| `contract_prices` | `Contract Price.csv` + `Price Proposition.csv` | `ContractUniqueIdentifier` → contract FK |
| `connections` | `Connections.csv` | `ConnectionId`, `EAN`, `ProductType` (Electricity/Gas) |
| `meter_reads` | `ConnectionMeterReads.csv` + `Meter Read_1-8.csv` | `EAN` / `ConnectionId` |
| `interactions` | `OrganizationContacts.csv`, `ConnectionContacts.csv` | `OrganizationId` / `ConnectionId`; `Subject` → channel |
| `invoices` (archive) | `Look-up Customer Data_1.csv` + `_2.csv` | `Customer number`, `Debtor number`, `Invoice number` |

### Risk scoring signals from real data

| Risk dimension | CSV signals |
|---|---|
| **Churn** | `Contracts.csv` EndDate proximity; `OrganizationContacts.csv` contact frequency/subject="Cancellation"; `ConnectionContacts.csv` interaction count trend |
| **Payment** | `Look-up Customer Data` invoice count; `ConnectionMeterReads` consumption vs billed amount gaps |
| **Margin** | `Contract Price.csv` vs `Price Proposition.csv` deviation; `Contracts.csv` CurrentAgreedAmount trend across periods; `DQE - Captars.csv` tariff classification |

---

## Requirements

- Read-only against CRM source data; all writes go to a local PostgreSQL analytics database
- Three risk dimensions: churn risk, payment risk, margin behavior risk
- Each risk score has a human-readable explanation generated by Azure OpenAI (gpt-4o)
- Suggested actions per customer and per segment
- Portfolio-level heat overview (green/yellow/red distribution)
- Demonstrable end-to-end on a single laptop (Docker Compose for DB + backend + frontend)

## Architecture Overview

### Component Diagram (Textual)

```
[CRM Source DB / CSV]
        |
        | (one-way read/import)
        v
[.NET 10 Backend API]
  ├── CrmImportService         -- reads CRM data, writes to local PG
  ├── RiskScoringEngine        -- deterministic rule-based scoring
  ├── AzureOpenAiExplanationService -- calls Azure OpenAI for NL explanations + actions
  ├── PortfolioAggregationService -- computes portfolio-level stats
  └── REST API Controllers     -- serves data to frontend
        |
        v
[PostgreSQL (local analytics)]
  ├── customers, contracts, invoices, payments, complaints, interactions
  ├── risk_scores, risk_explanations, suggested_actions
  └── portfolio_snapshots
        |
        v
[Angular Frontend]
  ├── Dashboard (heatmap)
  ├── Customer Detail (risk breakdown)
  ├── Risk Group Filter
  └── Actions Panel
```

### Data Flow

1. **Import**: CrmImportService reads from CRM source (DB connection string or CSV files) and upserts into local PostgreSQL tables. Never writes back to CRM.
2. **Score**: RiskScoringEngine reads imported data, computes numeric risk scores (0-100) per customer per dimension, writes to `risk_scores`.
3. **Explain**: AzureOpenAiExplanationService sends scoring context to Azure OpenAI (gpt-4o), receives NL explanations and actions, writes to `risk_explanations` and `suggested_actions`.
4. **Aggregate**: PortfolioAggregationService computes segment-level and portfolio-level heat distributions, writes to `portfolio_snapshots`.
5. **Serve**: REST API controllers expose all computed data to the Angular frontend.
6. **Display**: Angular app fetches data, renders heatmap dashboard, detail views, filters, and action panels.

---

## Data Model (PostgreSQL Schema)

### Table: `customers`

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| crm_external_id | VARCHAR(100) UNIQUE | Original CRM ID |
| name | VARCHAR(255) | |
| company_name | VARCHAR(255) | |
| email | VARCHAR(255) | |
| phone | VARCHAR(50) | |
| segment | VARCHAR(50) | e.g. "enterprise", "mid-market", "smb" |
| account_manager | VARCHAR(255) | |
| onboarding_date | DATE | |
| is_active | BOOLEAN | |
| imported_at | TIMESTAMPTZ | |
| updated_at | TIMESTAMPTZ | |

### Table: `contracts`

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| customer_id | UUID FK -> customers | |
| crm_external_id | VARCHAR(100) UNIQUE | |
| contract_type | VARCHAR(50) | "subscription", "one-time", "retainer" |
| start_date | DATE | |
| end_date | DATE | nullable for open-ended |
| monthly_value | DECIMAL(12,2) | |
| currency | VARCHAR(3) | |
| status | VARCHAR(20) | "active", "expired", "cancelled" |
| auto_renew | BOOLEAN | |
| imported_at | TIMESTAMPTZ | |

### Table: `invoices`

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| customer_id | UUID FK -> customers | |
| crm_external_id | VARCHAR(100) UNIQUE | |
| invoice_number | VARCHAR(50) | |
| issued_date | DATE | |
| due_date | DATE | |
| amount | DECIMAL(12,2) | |
| currency | VARCHAR(3) | |
| status | VARCHAR(20) | "paid", "unpaid", "overdue", "partial" |
| imported_at | TIMESTAMPTZ | |

### Table: `payments`

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| invoice_id | UUID FK -> invoices | |
| customer_id | UUID FK -> customers | |
| crm_external_id | VARCHAR(100) UNIQUE | |
| payment_date | DATE | |
| amount | DECIMAL(12,2) | |
| days_late | INTEGER | 0 = on time, negative = early |
| imported_at | TIMESTAMPTZ | |

### Table: `complaints`

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| customer_id | UUID FK -> customers | |
| crm_external_id | VARCHAR(100) UNIQUE | |
| created_date | DATE | |
| resolved_date | DATE | nullable |
| category | VARCHAR(50) | "billing", "service", "product", "other" |
| severity | VARCHAR(20) | "low", "medium", "high", "critical" |
| description | TEXT | |
| imported_at | TIMESTAMPTZ | |

### Table: `interactions`

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| customer_id | UUID FK -> customers | |
| crm_external_id | VARCHAR(100) UNIQUE | |
| interaction_date | DATE | |
| channel | VARCHAR(30) | "email", "phone", "meeting", "chat" |
| direction | VARCHAR(10) | "inbound", "outbound" |
| summary | TEXT | |
| sentiment | VARCHAR(20) | nullable; "positive", "neutral", "negative" |
| imported_at | TIMESTAMPTZ | |

### Table: `risk_scores`

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| customer_id | UUID FK -> customers | |
| snapshot_id | UUID FK -> portfolio_snapshots | |
| churn_score | INTEGER | 0-100 |
| payment_score | INTEGER | 0-100 |
| margin_score | INTEGER | 0-100 |
| overall_score | INTEGER | weighted composite 0-100 |
| heat_level | VARCHAR(10) | "green", "yellow", "red" |
| scored_at | TIMESTAMPTZ | |

### Table: `risk_explanations`

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| risk_score_id | UUID FK -> risk_scores | |
| customer_id | UUID FK -> customers | |
| risk_type | VARCHAR(20) | "churn", "payment", "margin", "overall" |
| explanation | TEXT | AI-generated NL explanation |
| confidence | VARCHAR(10) | "high", "medium", "low" |
| generated_at | TIMESTAMPTZ | |
| model_used | VARCHAR(50) | Azure OpenAI deployment name, e.g. "gpt-4o" |

### Table: `suggested_actions`

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| risk_score_id | UUID FK -> risk_scores | |
| customer_id | UUID FK -> customers | |
| action_type | VARCHAR(30) | "outreach", "discount", "review", "escalate", "upsell" |
| priority | VARCHAR(10) | "high", "medium", "low" |
| title | VARCHAR(255) | Short action title |
| description | TEXT | Claude-generated detailed action |
| generated_at | TIMESTAMPTZ | |

### Table: `portfolio_snapshots`

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| created_at | TIMESTAMPTZ | |
| total_customers | INTEGER | |
| green_count | INTEGER | |
| yellow_count | INTEGER | |
| red_count | INTEGER | |
| green_pct | DECIMAL(5,2) | |
| yellow_pct | DECIMAL(5,2) | |
| red_pct | DECIMAL(5,2) | |
| avg_churn_score | DECIMAL(5,2) | |
| avg_payment_score | DECIMAL(5,2) | |
| avg_margin_score | DECIMAL(5,2) | |
| segment_breakdown | JSONB | Per-segment heat counts |

---

## Backend (.NET 10) Project Structure

```
backend/
├── PortfolioThermometer.sln
├── src/
│   ├── PortfolioThermometer.Api/
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Controllers/
│   │   │   ├── PortfolioController.cs
│   │   │   ├── CustomersController.cs
│   │   │   ├── RiskScoresController.cs
│   │   │   └── ImportController.cs
│   ├── PortfolioThermometer.Core/
│   │   ├── Models/          -- Customer, Contract, Invoice, Payment, Complaint, Interaction, RiskScore, RiskExplanation, SuggestedAction, PortfolioSnapshot
│   │   ├── Enums/           -- HeatLevel, RiskType, ActionType
│   │   └── Interfaces/      -- ICrmImportService, IRiskScoringEngine, IClaudeExplanationService, IPortfolioAggregationService, ICustomerRepository
│   ├── PortfolioThermometer.Infrastructure/
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/
│   │   │   └── Migrations/
│   │   ├── Repositories/
│   │   ├── Services/
│   │   │   ├── CrmImportService.cs
│   │   │   ├── RiskScoringEngine.cs
│   │   │   ├── AzureOpenAiExplanationService.cs
│   │   │   └── PortfolioAggregationService.cs
│   │   └── AzureOpenAi/
│   │       ├── AzureOpenAiClient.cs
│   │       └── Prompts/
│   └── PortfolioThermometer.Seeder/
└── tests/
    ├── PortfolioThermometer.Core.Tests/
    ├── PortfolioThermometer.Infrastructure.Tests/
    └── PortfolioThermometer.Api.Tests/
```

## API Endpoints

### PortfolioController (`/api/portfolio`)
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/portfolio/current` | Latest portfolio snapshot with heat distribution |
| GET | `/api/portfolio/history` | List of snapshots over time |
| GET | `/api/portfolio/segments` | Heat breakdown per customer segment |

### CustomersController (`/api/customers`)
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/customers` | Paginated list; query params: `segment`, `heatLevel`, `sortBy`, `page`, `pageSize` |
| GET | `/api/customers/{id}` | Full customer detail with contracts, invoices, payments |
| GET | `/api/customers/{id}/risk` | Risk scores + explanations + suggested actions |
| GET | `/api/customers/{id}/interactions` | Interaction history |
| GET | `/api/customers/{id}/complaints` | Complaint history |

### RiskScoresController (`/api/risk`)
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/risk/distribution` | Distribution of scores across customers |
| GET | `/api/risk/top-at-risk?type=churn&limit=10` | Top N customers by risk type |
| GET | `/api/risk/groups` | Customers grouped by heat level with counts |

### ImportController (`/api/import`)
| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/import/trigger` | Trigger full import + scoring + explanation pipeline |
| GET | `/api/import/status` | Status of last import run |

---

## Risk Scoring Logic

### Heat Level Thresholds
- **Green**: overall_score 0–39
- **Yellow**: overall_score 40–69
- **Red**: overall_score 70–100

### Overall Score (Weighted Composite)
`overall_score = (churn_score * 0.40) + (payment_score * 0.35) + (margin_score * 0.25)`

### Churn Risk Signals
| Signal | Weight |
|--------|--------|
| Contract expiring within 90 days, no auto-renew | +25 |
| Declining interaction frequency (last 90d vs prior 90d) | +20 |
| Recent high-severity complaints (last 180 days) | +20 |
| Negative sentiment in recent interactions (last 90 days) | +15 |
| No outbound contact in 60+ days | +10 |
| Customer tenure < 12 months | +10 |

### Payment Risk Signals
| Signal | Weight |
|--------|--------|
| Average days late > 30 in last 6 months | +30 |
| More than 2 overdue invoices currently | +25 |
| Payment trend worsening (last 3 months vs prior 3) | +20 |
| Partial payments in last 6 months | +15 |
| Any invoice > 90 days overdue | +10 |

### Margin Behavior Risk Signals
| Signal | Weight |
|--------|--------|
| Contract value declining vs prior contract | +30 |
| Discount requests (billing complaints) | +25 |
| Reduced active contracts vs 12 months ago | +20 |
| Below-segment average contract value | +15 |
| Short contract durations (< 6 months) | +10 |

---

## AI Integration (Azure OpenAI)

- Deployment: `gpt-4o` (configurable)
- Endpoint: environment variable `AZURE_OPENAI_ENDPOINT`
- API key: environment variable `AZURE_OPENAI_API_KEY`
- API version: `2024-02-01`
- Batch size: 10 customers per batch
- Concurrency: max 5 parallel calls via `SemaphoreSlim`
- Fallback: placeholder explanation with confidence "low" if API unavailable
- Caching: only regenerate when risk scores change

### Explanation Prompt
Sends customer signals + risk score per dimension. Azure OpenAI returns JSON: `{ "explanation": "...", "confidence": "high|medium|low" }`

### Actions Prompt
Sends all three risk explanations + scores. Azure OpenAI returns JSON array of actions: `[{ "action_type": "...", "priority": "...", "title": "...", "description": "..." }]`

---

## Frontend (Angular) Structure

```
frontend/src/app/
├── core/
│   ├── services/    -- portfolio.service.ts, customer.service.ts, risk.service.ts, import.service.ts
│   ├── models/      -- customer.model.ts, risk-score.model.ts, risk-explanation.model.ts, suggested-action.model.ts, portfolio-snapshot.model.ts
│   └── interceptors/error.interceptor.ts
├── features/
│   ├── dashboard/
│   │   ├── portfolio-heatmap/   -- donut chart green/yellow/red
│   │   ├── segment-breakdown/   -- stacked bar chart per segment
│   │   ├── top-at-risk/         -- top 10 red customers table
│   │   └── risk-trend/          -- line chart portfolio health over time
│   ├── customer-detail/
│   │   ├── risk-breakdown/      -- three gauges (churn/payment/margin)
│   │   ├── explanation-panel/   -- Claude-generated NL text
│   │   ├── actions-panel/       -- suggested actions with priority badges
│   │   └── customer-timeline/   -- interactions + complaints timeline
│   ├── customer-list/
│   │   └── customer-filters/    -- segment, heat level, search
│   └── risk-groups/             -- customers grouped by dominant risk type
└── shared/
    ├── components/  -- heat-badge, risk-gauge, score-bar, loading-skeleton
    ├── pipes/       -- heat-color.pipe.ts, risk-label.pipe.ts
    └── directives/  -- tooltip.directive.ts
```

### Routes
| Path | Component |
|------|-----------|
| `/` | DashboardComponent |
| `/customers` | CustomerListComponent |
| `/customers/:id` | CustomerDetailComponent |
| `/risk-groups` | RiskGroupsComponent |

---

## Full Directory Layout

```
hackathonApril2026/
├── README.md
├── docker-compose.yml
├── .env.example
├── backend/
│   ├── PortfolioThermometer.sln
│   ├── src/
│   └── tests/
├── frontend/
│   ├── angular.json
│   ├── package.json
│   └── src/
├── database/
│   ├── init.sql
│   └── seed-data.sql
└── documentation/
    ├── initial-description.md
    ├── plan.md  (this file)
    └── architect/
        └── README.md
```

---

## Phase Plan

### Phase 1: Foundation (Days 1–2)
- Docker Compose setup (PostgreSQL + backend + frontend containers)
- Database schema (`database/init.sql`)
- .NET solution scaffold with EF Core + initial migration
- Domain models and enums
- **CRM CSV import** — `CrmImportService` reads `crm-data/` CSV files into PostgreSQL (replaces Bogus seed data)
- Basic CRUD API endpoints (customers list + detail)
- Angular scaffold with stub components

**Deliverable**: Backend imports real CRM CSV data. Angular shows customer list.

### Phase 2: Risk Engine + API (Days 3–4)
- Deterministic risk scoring engine with all signals
- Portfolio aggregation service (heat distribution, segment breakdown)
- Risk and portfolio API endpoints (pagination, filtering, sorting)
- Import pipeline trigger endpoint
- Unit tests for scoring rules

**Deliverable**: Trigger import → customers scored → portfolio snapshot created.

### Phase 3: Claude AI + Full Frontend (Days 5–7)
- Claude API client with retry and fallback
- Explanation and action generation services
- Explanation generation integrated into import pipeline
- Angular dashboard (heatmap, segment chart, top-at-risk, trend)
- Angular customer detail (gauges, explanation panel, actions, timeline)
- Customer list with filters

**Deliverable**: Full working application with AI-generated explanations.

### Phase 4: Polish + Demo Readiness (Day 8)
- Visual styling (intentional color palette, typography, depth)
- Error handling and loading states
- Risk groups view
- Demo seed data tuning
- Documentation
- Docker Compose finalization (single `docker-compose up`)

**Deliverable**: Demo-ready. One-command startup.

---

## Testing Strategy

- Unit tests: risk scoring signals, import service, Claude prompt/response parsing, API controllers
- Integration tests: full pipeline (import → score → query)
- E2E tests (Angular): dashboard load, customer detail, filtering, import trigger
- Coverage target: 80%+ on Core and Infrastructure projects

---

## Risks and Mitigations

| Risk | Mitigation |
|------|-----------|
| Azure OpenAI rate limits during batch generation | Semaphore (5 concurrent), batch size 10, retry with backoff, cache explanations |
| Scoring rules produce unrealistic distributions | Tune seed data generator; integration test asserts all three heat levels present |
| API key/endpoint exposure | Environment variables only; never committed; `.env.example` with placeholder |
| PostgreSQL Docker issues across laptops | Health checks in Compose; manual setup documented as fallback |
| Angular charting library compatibility | Evaluate `ng2-charts` vs `ngx-charts` early in Phase 3 |
