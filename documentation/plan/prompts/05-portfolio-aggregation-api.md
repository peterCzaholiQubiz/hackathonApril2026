# Prompt 05 — Portfolio Aggregation & API Endpoints

**Agent**: `general-purpose`  
**Phase**: 2 — Risk Engine  
**Status**: TODO  
**Depends on**: Prompt 04 (risk scoring engine)

---

Implement the portfolio aggregation service and all REST API endpoints for the
Customer Portfolio Thermometer.

Read documentation/plan.md (Backend section, API Endpoints table).

FILES TO CREATE:

1. backend/src/PortfolioThermometer.Infrastructure/Services/PortfolioAggregationService.cs
   - Reads all risk_scores for a snapshot
   - Computes green/yellow/red counts and percentages
   - Computes per-segment breakdown as JSONB (e.g. {"enterprise":{"green":5,"yellow":2,"red":1}})
   - Computes avg_churn_score, avg_payment_score, avg_margin_score
   - Writes to portfolio_snapshots table

2. backend/src/PortfolioThermometer.Api/Controllers/PortfolioController.cs
   GET /api/portfolio/current   — latest portfolio_snapshot
   GET /api/portfolio/history   — list of all snapshots ordered by created_at DESC
   GET /api/portfolio/segments  — segment_breakdown JSON from latest snapshot

3. backend/src/PortfolioThermometer.Api/Controllers/CustomersController.cs
   GET /api/customers                        — paginated list
     query params: segment, heatLevel, search, sortBy, sortDir, page (default 1), pageSize (default 20, max 100)
   GET /api/customers/{id}                   — full detail with contracts + invoices + payments
   GET /api/customers/{id}/risk              — risk_scores + risk_explanations + suggested_actions (latest snapshot)
   GET /api/customers/{id}/interactions      — interaction history ordered by interaction_date DESC
   GET /api/customers/{id}/complaints        — complaint history ordered by created_date DESC

4. backend/src/PortfolioThermometer.Api/Controllers/RiskScoresController.cs
   GET /api/risk/distribution               — histogram buckets (0-9, 10-19, ... 90-100) with counts
   GET /api/risk/top-at-risk               — query params: type (churn|payment|margin|overall), limit (default 10)
   GET /api/risk/groups                    — customers grouped by heat_level with counts and customer summaries

5. backend/src/PortfolioThermometer.Api/Controllers/ImportController.cs
   POST /api/import/trigger                — starts the pipeline async, returns 202 Accepted with a run ID
   GET  /api/import/status                 — returns status of last run (pending/running/complete/failed)

All responses must use the consistent envelope:
{
  "success": true,
  "data": { ... },
  "error": null,
  "meta": { "total": 80, "page": 1, "pageSize": 20 }  // for paginated responses
}

Return 404 with success:false and error message when customer ID not found.
Do not expose stack traces or connection strings in error responses.

Reference: documentation/plan.md (API Endpoints section)
