# Prompt 04 — Risk Scoring Engine (TDD)

**Agent**: `ecc:tdd-guide`  
**Phase**: 2 — Risk Engine  
**Status**: DONE  
**Depends on**: Prompt 02 (backend scaffold)

---

Implement the RiskScoringEngine in the Customer Portfolio Thermometer backend using TDD.

Read:
- documentation/plan.md (Risk Scoring Logic section)
- backend/src/PortfolioThermometer.Core/Interfaces/IRiskScoringEngine.cs

Files to implement:
- backend/src/PortfolioThermometer.Infrastructure/Services/RiskScoringEngine.cs
- backend/tests/PortfolioThermometer.Core.Tests/RiskScoringEngineTests.cs

Scoring rules to implement:

CHURN RISK (signals are additive, cap at 100):
- Contract expiring within 90 days with auto_renew=false: +25
- Declining interaction frequency (last 90d vs prior 90d period): +20
- High/critical severity complaints in last 180 days (any = trigger): +20
- Negative sentiment interactions in last 90 days (any = trigger): +15
- No outbound interaction in last 60 days: +10
- Customer tenure < 12 months (onboarding_date): +10

PAYMENT RISK (signals are additive, cap at 100):
- Average days_late > 30 across payments in last 6 months: +30
- More than 2 invoices with status='overdue' currently: +25
- Payment trend worsening (avg days_late last 3 months > prior 3 months): +20
- Any invoice with status='partial' in last 6 months: +15
- Any invoice with due_date more than 90 days ago and status != 'paid': +10

MARGIN BEHAVIOR RISK (signals are additive, cap at 100):
- Current active contract monthly_value lower than previous contract: +30
- Any billing complaint in last 12 months: +25
- Fewer active contracts than 12 months ago: +20
- Customer monthly_value below segment average: +15
- Any contract with duration < 6 months (end_date - start_date): +10

OVERALL SCORE:
overall_score = ROUND(churn_score * 0.40 + payment_score * 0.35 + margin_score * 0.25)

HEAT LEVEL:
- green:  overall_score 0–39
- yellow: overall_score 40–69
- red:    overall_score 70–100

Write tests FIRST for each signal independently, then the weighted composite, then heat thresholds.
Include edge cases: brand new customer with no history, all-green customer, all-red customer,
customer with exactly 0 signals triggered, score exactly at threshold boundaries (39/40, 69/70).
Target 80%+ code coverage.

Reference: documentation/plan.md (Risk Scoring Logic section)
