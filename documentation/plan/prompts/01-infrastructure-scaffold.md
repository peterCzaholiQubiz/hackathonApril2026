# Prompt 01 — Infrastructure Scaffold

**Agent**: `general-purpose`  
**Phase**: 1 — Foundation  
**Status**: DONE (docker-compose.yml, .env.example, database/init.sql already created)

---

Scaffold the infrastructure for the Customer Portfolio Thermometer:

1. Create docker-compose.yml at the repo root with:
   - postgres:16 service (port 5432, health check)
   - backend service (.NET 10, port 8080, depends on postgres)
   - frontend service (Angular dev server, port 4200, depends on backend)
   - shared network and named volume for postgres data

2. Create .env.example with:
   POSTGRES_PASSWORD=
   POSTGRES_DB=portfolio_thermometer
   ANTHROPIC_API_KEY=
   CRM_CONNECTION_STRING=

3. Create database/init.sql with the full PostgreSQL schema from documentation/plan.md
   (all 10 tables: customers, contracts, invoices, payments, complaints, interactions,
   risk_scores, risk_explanations, suggested_actions, portfolio_snapshots)
   Include indexes on all FK columns and unique constraints on crm_external_id columns.

Reference: documentation/plan.md (Data Model section)
