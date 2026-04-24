# hackathonApril2026

A portfolio thermometer application with an ASP.NET Core backend and Angular frontend.

---

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0+ |
| Node.js / npm | 18+ |
| Docker & Docker Compose | any recent version (optional) |
| PostgreSQL | 16 (or use Docker) |

---

## Quick start — Docker Compose (all services)

This is the easiest way to run the full stack (PostgreSQL + backend + frontend) together.

```bash
# 1. Copy and fill in the required env vars
cp .env.example .env
# Edit .env: set POSTGRES_PASSWORD and ANTHROPIC_API_KEY

# 2. Start everything
docker compose up --build
```

| Service | URL |
|---------|-----|
| Frontend | http://localhost:4200 |
| Backend API | http://localhost:8080 |
| PostgreSQL | localhost:5432 |

Stop with `docker compose down` (add `-v` to also remove the database volume).

---

## Running locally (without Docker)

### 1. Database

Start a local PostgreSQL 16 instance and ensure the defaults match `appsettings.json`:

```
Host=localhost; Port=5432; Database=portfolio_thermometer; Username=postgres; Password=admin
```

Or override them via environment variables / `appsettings.Development.json`.

### 2. Backend (ASP.NET Core — port 8080)

```bash
cd backend

# Restore dependencies
dotnet restore PortfolioThermometer.sln

# Set your Anthropic API key (or add it to appsettings.Development.json)
$env:Anthropic__ApiKey = "sk-..."       # PowerShell
# export Anthropic__ApiKey="sk-..."     # bash/zsh

# Run the API
dotnet run --project src/PortfolioThermometer.Api/PortfolioThermometer.Api.csproj
```

The API will be available at **http://localhost:8080**.

In development, imports default to the reduced sample dataset at `crm-data\Sample-100`. To regenerate that flat sample from the full archive:

```bash
python scripts/build_crm_sample.py --customer-count 100
```

You can still point the import endpoint at another dataset folder under the configured CRM root by sending `crmdatapath` (for example `All` or `Sample-100`) in the `POST /api/import/trigger` request body.

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

## Running tests

```bash
# Backend tests
cd backend
dotnet test PortfolioThermometer.sln

# Frontend tests
cd frontend
npm test
```
