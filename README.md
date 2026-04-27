# Customer Portfolio Thermometer

A smart analytics platform for energy companies that transforms raw CRM data into actionable portfolio intelligence — classifying customers by risk level (🟢 green / 🟡 yellow / 🔴 red) across three risk dimensions: **Churn**, **Payment**, and **Margin**.

> Built during Hackathon April 2026 — read-only, non-intrusive layer on top of existing CRM data.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Tech Stack](#tech-stack)
  - [Backend](#backend)
  - [Frontend](#frontend)
  - [AI Integration](#ai-integration)
  - [Infrastructure & DevOps](#infrastructure--devops)
- [Key Features](#key-features)
- [API Reference](#api-reference)
- [Data Model & CRM Integration](#data-model--crm-integration)
- [Risk Scoring Engine](#risk-scoring-engine)
- [Prerequisites](#prerequisites)
- [Quick Start — Docker Compose](#quick-start--docker-compose)
- [Running Locally (without Docker)](#running-locally-without-docker)
- [Running Tests](#running-tests)

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│  Angular 19 SPA (port 4200)                                  │
│  Dashboard · Customer List · Risk Groups · Energy Heatmap    │
└────────────────────────┬─────────────────────────────────────┘
                         │ HTTP / REST
┌────────────────────────▼─────────────────────────────────────┐
│  ASP.NET Core Web API  (port 8080)                           │
│  PortfolioThermometer.Api                                    │
│  ├─ CRM Import  ──► CSV → Domain Models → PostgreSQL         │
│  ├─ Risk Scoring Engine  (churn / payment / margin)          │
│  ├─ Portfolio Aggregation  (snapshots, heat levels)          │
│  └─ AI Explanation Service  ──► Azure OpenAI GPT-4o          │
└────────────────────────┬─────────────────────────────────────┘
                         │
            ┌────────────▼────────────┐
            │  PostgreSQL 16           │
            │  (EF Core migrations)    │
            └─────────────────────────┘
```

---

## Tech Stack

### Backend

| Technology | Version | Role |
|---|---|---|
| **.NET / ASP.NET Core** | 10.0 | Web API framework |
| **Entity Framework Core** | 9.x (Npgsql) | ORM & database migrations |
| **PostgreSQL** | 16 | Persistent data store |
| **Npgsql** | latest | .NET PostgreSQL driver |
| **Swagger / OpenAPI** | Swashbuckle | API documentation (available in dev at `/swagger`) |

**Project structure:**

```
backend/
├── src/
│   ├── PortfolioThermometer.Api          # Controllers, middleware, DI wiring
│   ├── PortfolioThermometer.Core         # Domain models, interfaces, enums
│   ├── PortfolioThermometer.Infrastructure  # EF Core, repositories, services
│   └── PortfolioThermometer.Seeder       # Data seeding utilities
└── tests/                                # xUnit test projects
```

**Core domain models:** `Customer`, `Contract`, `Connection`, `MeterRead`, `Invoice`, `Payment`, `Complaint`, `Interaction`, `RiskScore`, `RiskExplanation`, `SuggestedAction`, `PortfolioSnapshot`.

### Frontend

| Technology | Version | Role |
|---|---|---|
| **Angular** | 19 | SPA framework |
| **TypeScript** | ~5.6 | Type-safe application code |
| **Chart.js** | ^4.0 | Charts and visualisations |
| **ng2-charts** | ^7.0 | Angular wrapper for Chart.js |
| **Angular CDK** | ^19.0 | UI component primitives |
| **RxJS** | ~7.8 | Reactive async data streams |
| **SCSS** | — | Component-level styling |

**Feature modules:**

| Module | Description |
|---|---|
| `dashboard` | Portfolio heatmap, risk trend, segment breakdown, top-at-risk widget, energy heatmap |
| `customer-list` | Searchable/filterable customer table with heat indicators |
| `customer-detail` | Per-customer risk scores, explanations, suggested actions, meter reads |
| `risk-groups` | Dimension-level risk groups (churn / payment / margin) with AI explanations |
| `meter-reads` | Energy consumption heatmap by month/year |
| `presentation` | Full-screen hackathon demo mode |

### AI Integration

The platform uses **Azure OpenAI** (deployment: `gpt-4o`) to generate human-readable explanations and recommended actions for each customer's risk profile.

| Component | File | Purpose |
|---|---|---|
| `AzureOpenAiClient` | `Infrastructure/AzureOpenAi/` | Low-level HTTP client for the Azure OpenAI REST API |
| `AzureOpenAiExplanationService` | `Infrastructure/Services/` | Implements `IClaudeExplanationService`; generates per-customer risk explanations and suggested actions after scoring |
| `ClaudeExplanationService` | `Infrastructure/Services/` | Alternative Anthropic Claude–based explanation provider |
| `RiskExplanationPrompt` | `Infrastructure/AzureOpenAi/Prompts/` | System + user prompt for explaining a risk score |
| `SuggestedActionPrompt` | `Infrastructure/AzureOpenAi/Prompts/` | Prompt for generating a single suggested action |
| `SuggestedActionsEnhancedPrompt` | `Infrastructure/AzureOpenAi/Prompts/` | Enhanced prompt with portfolio context for richer action recommendations |

**AI pipeline (triggered via `POST /api/risk/trigger`):**
1. Rule-based scoring engine produces numeric scores (0–100) for each customer.
2. `AzureOpenAiExplanationService` batches high-risk customers and calls GPT-4o with structured prompts.
3. Natural language explanations and prioritised suggested actions are stored in PostgreSQL and surfaced in the UI.

**Configuration:**

```json
// appsettings.json → AzureOpenAi section
{
  "AzureOpenAi": {
    "Endpoint": "https://<resource>.openai.azure.com/",
    "Deployment": "gpt-4o",
    "ApiVersion": "2024-02-01"
  }
}
```

Set the API key via the `OpenAI_Key` environment variable (or Docker Compose env).

### Infrastructure & DevOps

- **Docker Compose** — single `docker compose up --build` starts PostgreSQL, backend, and frontend.
- **Database migrations** — applied automatically on startup in the Development environment via EF Core's `MigrateAsync`.
- **CORS** — configurable allowed origins via `appsettings.json → Cors:AllowedOrigins`; defaults to `http://localhost:4200`.
- **Global error handler** — `GlobalErrorHandlerMiddleware` returns consistent JSON error responses.

---

## Key Features

- 📊 **Portfolio Heatmap** — visual green/yellow/red distribution across the entire customer base.
- 📈 **Risk Trend** — historical snapshot timeline to track portfolio health over time.
- 🏷️ **Segment Breakdown** — heat distribution per customer segment (SME, residential, large business…).
- ⚠️ **Top-at-Risk** — ranked list of customers with the highest churn, payment, or margin risk.
- 🔥 **Energy Heatmap** — month-by-year consumption grid (kWh electricity or m³ gas).
- 🤖 **AI Explanations** — plain-language reasoning for each customer's risk flag generated by GPT-4o.
- 💡 **Suggested Actions** — prioritised recommendations (proactive outreach, contract review, monitoring…).
- 📋 **Customer Detail** — full per-customer view: scores, explanation, contracts, meter reads, interactions.
- 🔄 **CRM Import** — on-demand import of CSV data from the `crm-data/` folder without touching the source system.
- 🧪 **Test Data Generator** — synthetic data endpoint for demos and offline development.

---

## API Reference

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/import/trigger` | Import CRM CSV data into the database |
| `POST` | `/api/risk/trigger` | Run risk scoring + AI explanation pipeline (async) |
| `GET` | `/api/risk/status` | Poll status of the running/last risk pipeline |
| `GET` | `/api/risk/distribution` | Overall heat distribution (green/yellow/red counts & %) |
| `GET` | `/api/risk/top-at-risk` | Top N customers by risk dimension (`overall`, `churn`, `payment`, `margin`) |
| `GET` | `/api/risk/groups` | Customers grouped by heat level |
| `GET` | `/api/risk/dimension-groups` | Full dimension-group view with AI explanations and suggested actions |
| `GET` | `/api/risk/scatter-data` | All customers as scatter points (churn × payment × margin) |
| `GET` | `/api/portfolio/current` | Latest portfolio snapshot with aggregated scores |
| `GET` | `/api/portfolio/history` | Paginated snapshot history |
| `GET` | `/api/portfolio/segments` | Segment breakdown from latest snapshot |
| `GET` | `/api/portfolio/energy-heatmap` | Monthly energy consumption heatmap (`unit=kWh\|m3`, `direction=Consumption\|Production`) |
| `GET` | `/api/customers` | Customer list with latest risk scores |
| `GET` | `/api/customers/{id}` | Customer detail (scores, contracts, interactions, meter reads) |
| `GET` | `/api/meterreads` | Meter read history |
| `GET` | `/api/status` | API health check |

Interactive docs available at **`http://localhost:8080/swagger`** in development.

---

## Data Model & CRM Integration

The system imports pseudonymised energy-sector CRM data from the `crm-data/` folder.

### CSV → Domain Mapping

| Domain Entity | Primary CSV Source | Key Join |
|---|---|---|
| `customers` | `Organizations.csv` (OrganizationTypeId = 2) | `OrganizationId` → `crm_external_id` |
| `contracts` | `Contracts.csv` | `ContractId` → `crm_external_id`; `CurrentAgreedAmount` → `monthly_value` |
| `connections` | `Connections.csv` | `ConnectionId`, EAN, ProductType |
| `meter_reads` | `ConnectionMeterReads.csv` + `Meter Read_1-8.csv` | EAN / `ConnectionId` |
| `interactions` | `OrganizationContacts.csv`, `ConnectionContacts.csv` | `OrganizationId` / `ConnectionId` |
| `invoices` | `Look-up Customer Data_1.csv` + `_2.csv` | `Customer number`, `Invoice number` |

In development, imports default to the reduced `crm-data/Sample-100` dataset (100 customers). To regenerate the sample:

```bash
python scripts/build_crm_sample.py --customer-count 100
```

To import a different dataset folder, pass `crmdatapath` in the request body:

```bash
curl -X POST http://localhost:8080/api/import/trigger \
  -H "Content-Type: application/json" \
  -d '{ "crmdatapath": "All" }'
```

---

## Risk Scoring Engine

The deterministic rule-based engine (`RiskScoringEngine`) computes three scores (0–100) per customer:

| Dimension | Key Signals |
|---|---|
| **Churn Score** | Contract end-date proximity, cancellation contacts, declining interaction frequency |
| **Payment Score** | Overdue invoices, payment gaps vs billed amount, invoice-to-payment ratio |
| **Margin Score** | Contract price vs catalogue price deviation, monthly value trend, tariff classification |

**Heat level thresholds:**

| Level | Overall Score |
|---|---|
| 🟢 Green | < 30 |
| 🟡 Yellow | 30 – 59 |
| 🔴 Red | ≥ 60 |

Customers with a dimension score ≥ 40 are flagged in the relevant risk group.

---

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 10.0+ |
| Node.js / npm | 18+ |
| Docker & Docker Compose | any recent version (optional) |
| PostgreSQL | 16 (or use Docker) |

---

## Quick Start — Docker Compose

The easiest way to run the full stack (PostgreSQL + backend + frontend) in one command.

```bash
# 1. Copy and configure environment variables
cp .env.example .env
# Edit .env: set POSTGRES_PASSWORD and OpenAI_Key (Azure OpenAI API key)

# 2. Start everything
docker compose up --build
```

| Service | URL |
|---|---|
| Frontend | http://localhost:4200 |
| Backend API | http://localhost:8080 |
| Swagger UI | http://localhost:8080/swagger |
| PostgreSQL | localhost:5432 |

Stop with `docker compose down` (add `-v` to also remove the database volume).

---

## Running Locally (without Docker)

### 1. Database

Start a local PostgreSQL 16 instance and ensure it matches the defaults in `appsettings.json`:

```
Host=localhost; Port=5432; Database=portfolio_thermometer; Username=postgres; Password=admin
```

Or override via environment variables or `appsettings.Development.json`.

### 2. Backend (ASP.NET Core — port 8080)

```bash
cd backend

# Restore dependencies
dotnet restore PortfolioThermometer.sln

# Set your Azure OpenAI API key
$env:OpenAI_Key = "your-azure-openai-key"   # PowerShell
# export OpenAI_Key="your-azure-openai-key"  # bash/zsh

# Run the API (applies EF Core migrations automatically in Development)
dotnet run --project src/PortfolioThermometer.Api/PortfolioThermometer.Api.csproj
```

The API will be available at **http://localhost:8080** and Swagger UI at **http://localhost:8080/swagger**.

### 3. Frontend (Angular — port 4200)

```bash
cd frontend

# Install dependencies (first time only)
npm install

# Start the dev server
npm start
```

The app will be available at **http://localhost:4200** and proxies API calls to `http://localhost:8080`.

---

## Running Tests

```bash
# Backend unit & integration tests
cd backend
dotnet test PortfolioThermometer.sln

# Frontend tests
cd frontend
npm test
```
