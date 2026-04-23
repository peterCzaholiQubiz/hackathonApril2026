# Prompt 06 — Seed Data Generator

**Agent**: `general-purpose`  
**Phase**: 2 — Risk Engine  
**Status**: TODO  
**Depends on**: Prompt 02 (backend scaffold)

---

Implement the seed data generator using Bogus for the Customer Portfolio Thermometer.

Read documentation/plan.md for the full data model.

FILE: backend/src/PortfolioThermometer.Seeder/SeedDataGenerator.cs

Generate 80 customers with realistic Dutch/European energy sector CRM data.
Mix must produce realistic risk score distributions when run through the scoring engine:

GREEN PROFILE (24 customers — 30%):
- Active contracts with auto_renew=true, end_date > 180 days away
- Payments consistently on time (days_late <= 5), no overdue invoices
- Regular outbound interactions in last 30 days, positive sentiment
- No complaints, or only low-severity resolved complaints
- Customer tenure > 24 months
- Contract values stable or growing

YELLOW PROFILE (32 customers — 40%):
- Contract expiring in 91–180 days, or auto_renew=false
- Some late payments (avg days_late 10–30), 1–2 overdue invoices
- Declining interaction frequency (fewer interactions last 90 days vs prior)
- 1–2 medium-severity complaints in last 6 months
- Tenure 12–24 months

RED PROFILE (24 customers — 30%):
- Contract expiring within 90 days with auto_renew=false, OR expired
- Multiple overdue invoices (3+), avg days_late > 30, partial payments
- No outbound contact in 60+ days, negative sentiment interactions
- High/critical severity complaints in last 6 months, unresolved
- Contract value declining vs prior contract

DATA DETAILS:
- Use Dutch company names and personal names (Bogus locale: nl)
- Segments: 20% enterprise, 40% mid-market, 40% smb
- Each customer has: 1–3 contracts, 12–24 invoices (last 2 years), payments for paid invoices,
  0–5 complaints, 4–18 interactions
- crm_external_id format: "CRM-{5-digit-number}" e.g. "CRM-10042"
- Currencies: EUR
- Contract monthly values: enterprise €5000–€50000, mid-market €1000–€5000, smb €200–€1000

The seeder should:
1. Clear existing seed data (WHERE crm_external_id LIKE 'CRM-%')
2. Insert all 80 customers with their related records in a single transaction
3. Print a summary: "Seeded 80 customers: 24 green, 32 yellow, 24 red profiles"

Reference: documentation/plan.md (Data Model section, Phase 1 task 5)
