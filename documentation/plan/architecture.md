# Architecture: Customer Portfolio Thermometer

## High-Level Component Diagram

```
+----------------------------------------------+
|       CRM Source — crm-data/ (Read-Only)     |
|  Pseudonymised Dutch energy-sector CSV files |
|  ERPSQLServer/: Organizations, Contracts,    |
|    Connections, Contacts, MeterReads,        |
|    Prices, Captars, ASU, lookup tables       |
|  ArchievingSolution/: Invoice archive index, |
|    Contract/Proposition prices, Meter Read   |
|    1–8, Timeslices (CaptarCode, Profile …)   |
+--------------------+--------------------------+
                     |
                     | one-way file read (UTF-8 BOM stripped)
                     | NEVER writes back
              v
+------------------------------------------------------+
|               .NET 10 Backend (ASP.NET Core)         |
|                                                      |
|  +------------------+    +------------------------+  |
|  | CrmImportService |    | RiskScoringEngine      |  |
|  | (reads CRM,      |--->| (deterministic rules,  |  |
|  |  upserts to PG)  |    |  churn/payment/margin) |  |
|  +------------------+    +----------+-------------+  |
|                                     |                |
|                                     v                |
|  +------------------------------+  +--------------+  |
|  | AzureOpenAiExplanationService|  | Portfolio    |  |
|  | (calls Azure OpenAI gpt-4o   |  | Aggregation  |  |
|  |  for NL explanations+actions)|  | Service      |  |
|  +------------------------------+  +--------------+  |
|                                                      |
|  +------------------------------------------------+  |
|  |           REST API Controllers                  |  |
|  |  PortfolioController  CustomersController       |  |
|  |  RiskScoresController ImportController          |  |
|  +------------------------------------------------+  |
+----------------------------+-------------------------+
                             |
                             | EF Core (Npgsql)
                             v
+------------------------------------------------------+
|              PostgreSQL 16 (Local Analytics DB)       |
|                                                      |
|  CRM Mirror Tables:                                  |
|    customers, contracts, invoices, payments,          |
|    complaints, interactions                           |
|                                                      |
|  Computed Tables:                                    |
|    risk_scores, risk_explanations,                   |
|    suggested_actions, portfolio_snapshots             |
+------------------------------------------------------+
                             |
                             | JSON over HTTP (REST)
                             v
+------------------------------------------------------+
|              Angular Frontend (SPA)                  |
|                                                      |
|  +-------------+  +------------------+               |
|  | Dashboard   |  | Customer Detail  |               |
|  | - Heatmap   |  | - Risk Gauges    |               |
|  | - Segments  |  | - Explanations   |               |
|  | - Top Risk  |  | - Actions Panel  |               |
|  | - Trend     |  | - Timeline       |               |
|  +-------------+  +------------------+               |
|                                                      |
|  +------------------+  +------------------+          |
|  | Customer List    |  | Risk Groups      |          |
|  | - Filters        |  | - By dimension   |          |
|  | - Pagination     |  | - By heat level  |          |
|  +------------------+  +------------------+          |
+------------------------------------------------------+

External Service:
+------------------------------------------------------+
|              Azure OpenAI                             |
|  Deployment: gpt-4o                                  |
|  - Explanation generation                            |
|  - Suggested action generation                       |
|  - Called from AzureOpenAiExplanationService         |
|  - Batched (10/batch), throttled (5 concurrent)      |
+------------------------------------------------------+
```

## Layer Descriptions

### API Layer (`PortfolioThermometer.Api`)

The outermost layer. Contains ASP.NET Core controllers that expose REST endpoints for the Angular frontend. Responsibilities:

- HTTP request/response handling, routing, content negotiation
- Input validation and error response formatting
- CORS configuration for the Angular origin
- Dependency injection wiring for all services and repositories
- No business logic; delegates entirely to Core interfaces

### Core Layer (`PortfolioThermometer.Core`)

The domain layer with zero external dependencies. Contains:

- **Domain models**: Customer, Contract, Invoice, Payment, Complaint, Interaction, RiskScore, RiskExplanation, SuggestedAction, PortfolioSnapshot
- **Enums**: HeatLevel (Green/Yellow/Red), RiskType (Churn/Payment/Margin/Overall), ActionType
- **Interfaces**: ICrmImportService, IRiskScoringEngine, IAiExplanationService, IPortfolioAggregationService, ICustomerRepository
- **Scoring constants**: signal weights, heat level thresholds, composite weights

This layer defines what the system does. It has no knowledge of databases, HTTP, or external APIs.

### Infrastructure Layer (`PortfolioThermometer.Infrastructure`)

Implements all Core interfaces with concrete technology:

- **Data**: EF Core DbContext, entity configurations, migrations (Npgsql provider)
- **Repositories**: PostgreSQL-backed data access implementing Core repository interfaces
- **Services**:
  - `CrmImportService` — reads from CRM source (connection string or CSV), upserts to local PG
  - `RiskScoringEngine` — deterministic rule evaluation, writes risk_scores
  - `AzureOpenAiExplanationService` — HTTP client to Azure OpenAI, prompt construction, response parsing
  - `PortfolioAggregationService` — computes snapshot statistics from risk_scores
- **AzureOpenAi**: API client wrapper, prompt templates, retry/fallback logic

### Frontend Layer (Angular SPA)

Standalone single-page application served statically. Communicates exclusively through the REST API. Contains:

- **Core module**: HTTP services, TypeScript models, error interceptor
- **Feature modules**: Dashboard, CustomerDetail, CustomerList, RiskGroups
- **Shared module**: Reusable components (heat badge, risk gauge, score bar), pipes, directives
- **Routing**: Four top-level routes

## Integration Points

| Integration | Direction | Protocol | Constraints |
|-------------|-----------|----------|-------------|
| CRM Source -> Backend | One-way read | File system — `crm-data/` CSV files (UTF-8 BOM, comma-delimited) | Read-only; never mutates source files |
| Backend -> PostgreSQL | Read/write | EF Core via Npgsql | Local analytics DB; all writes happen here |
| Backend -> Azure OpenAI | Request/response | HTTPS REST | Batched (10/batch), throttled (5 concurrent), cached results |
| Backend -> Angular | Response only | REST JSON over HTTP | Backend serves data; frontend never writes to DB directly |
| Angular -> Backend | Request | REST JSON over HTTP | Import trigger is the only state-changing user action |

## Key Architectural Principles

### 1. Read-Only CRM Integration

The system never writes back to the source CRM. The CrmImportService operates strictly as a consumer: it reads records via a database connection or CSV files and upserts them into the local PostgreSQL analytics database. This is the single most important constraint, driven by the business requirement that IT managers must not perceive risk to their existing infrastructure.

### 2. Non-Intrusive Deployment

The entire system runs as a separate stack with no footprint on the CRM infrastructure. Docker Compose packages PostgreSQL, the .NET backend, and the Angular frontend into a self-contained unit. The only touchpoint with existing systems is the `crm-data/` folder, which is mounted read-only into the backend container.

### 3. Single-Laptop Deployable

All components run in Docker containers orchestrated by a single `docker-compose.yml`. No cloud services are required at runtime except Azure OpenAI (which requires internet access and a valid Azure deployment). The system is designed to start with `docker-compose up` and be demonstrable within minutes.

### 4. Deterministic Scoring, AI-Generated Explanations

Risk scores are computed by transparent, rule-based logic with documented signal weights. AI (Azure OpenAI) is used only for natural language explanation and action suggestion generation. This separation ensures that scoring is reproducible and auditable, while explanations are human-readable and contextual. If Azure OpenAI is unavailable, the system degrades gracefully with placeholder explanations.

### 5. Advisory, Not Prescriptive

Following the Q&A guidance, the system advises rather than dictates. Risk labels are presented with full transparency about which signals contributed. Explanations include confidence levels. The UI frames outputs as suggestions, not verdicts.

### 6. Clean Architecture (Dependency Inversion)

The Core layer has no outward dependencies. Infrastructure implements Core interfaces. The API layer wires everything together via dependency injection. This enables unit testing of business logic without databases or HTTP, and allows swapping infrastructure components (e.g., replacing CSV import with a different CRM connector) without touching domain code.
