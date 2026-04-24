# Risk Model Upgrade Plan

## Objective

Upgrade the current deterministic risk scoring engine to a hybrid, interpretable, and production-ready model while keeping full compatibility with the existing backend API contracts and frontend dashboard expectations.

## Scope

- Keep existing outputs and fields in `RiskScore`: `ChurnScore`, `PaymentScore`, `MarginScore`, `OverallScore`, `HeatLevel`.
- Preserve existing API behavior for:
  - `/api/risk/distribution`
  - `/api/risk/top-at-risk`
  - `/api/risk/groups`
- Extend scoring quality using a two-layer design:
  - Layer A: deterministic signal engine (enhanced existing rules)
  - Layer B: calibration model (probability correction and reliability)
- Remain non-intrusive to CRM sources (read-only ingestion).

Out of scope for MVP:

- Replacing the current frontend UX structure
- Introducing fully black-box models that cannot be explained
- Real-time streaming infrastructure

## Current-State Baseline

The current scoring logic is deterministic and implemented in `RiskScoringEngine`.

- Sub-scores are bounded to 0-100.
- Overall score uses weighted composition:
  - `overall = round(churn * 0.40 + payment * 0.35 + margin * 0.25)`
- Heat mapping:
  - `0-39 => green`
  - `40-69 => yellow`
  - `70-100 => red`

This baseline remains the compatibility anchor throughout the upgrade.

## Target Architecture

### Layer A: Deterministic Signal Layer

Continue using deterministic business signals from:

- `Customer` (tenure, segment)
- `Contract` (expiry, auto-renew, monthly value trend, duration)
- `Invoice` and `Payment` (overdue behavior, lateness trend, partial payments)
- `Complaint` and `Interaction` (severity, billing complaints, negative sentiment, outbound inactivity)

Output from Layer A:

- Raw sub-scores (`rawChurn`, `rawPayment`, `rawMargin`)
- Derived overall raw score (`rawOverall`)
- Signal contributions per dimension for explainability

### Layer B: Statistical Calibration Layer

Apply calibration on top of Layer A score outputs to improve probability reliability and ranking quality.

MVP calibration options:

- Platt scaling (logistic)
- Isotonic regression

Calibration happens per dimension and/or overall score:

- `calibratedProbability = calibrator(rawScore, featureContext)`
- `calibratedScore = round(calibratedProbability * 100)`

Compatibility rule:

- Persist calibrated values into existing `RiskScore` fields.
- Heat-level thresholding remains unchanged for API stability.

## Feature Strategy

### Core Feature Groups

1. Customer profile and tenure
2. Contract lifecycle and value trends
3. Billing and payment reliability
4. Complaint and interaction behavior
5. Market exposure features from electricity/gas median series

### Market-Aware Features (Phase 2)

- Monthly median price delta (current month vs trailing 12-month median)
- Volatility proxy (rolling standard deviation over selected windows)
- Seasonality mismatch signal (customer usage period vs high-volatility periods)
- Gas/electricity specific pressure indicators by contract/product type

### Leakage Controls

- Snapshot-time cutoff: no features after `scoredAt` reference point
- Label window isolation for supervised/proxy labels
- Strict temporal train/validation/test splits
- No direct usage of post-event statuses in pre-event scoring

## Labeling Strategy

Because complete ground-truth labels may be sparse, use phased labeling:

1. Supervised labels where available:
   - churn event proxies from contract cancellations/non-renewals
   - payment risk from severe overdue and persistent lateness
   - margin deterioration from value decline and billing pressure indicators
2. Weak supervision / proxy labeling for missing segments
3. Confidence tagging of labels for training diagnostics

## Data Split and Validation

- Use rolling temporal splits by snapshot month.
- Recommended baseline:
  - Train: earliest 60-70%
  - Validation: next 15-20%
  - Test: latest 15-20%
- Evaluate stability across:
  - customer segment
  - contract type
  - electricity vs gas related portfolios

## Implementation Plan by Phase

## Phase 0 - Baseline Freeze and Instrumentation

Deliverables:

- Freeze current deterministic output as benchmark.
- Add comparison harness to evaluate new model against baseline.
- Define reproducible snapshot selection rules.

Acceptance criteria:

- Current tests pass unchanged.
- Baseline scoring outputs reproducible for the same input snapshot.

## Phase 1 - Feature Extraction Foundation

Deliverables:

- Add a feature extraction service that computes deterministic feature vectors per customer.
- Introduce versioned feature schema constants.
- Persist optional feature snapshots for offline diagnostics (if enabled).

Acceptance criteria:

- Feature extraction unit tests cover edge cases (null dates, no payments, no interactions).
- No API contract changes.

## Phase 2 - Calibration Model Integration (MVP)

Deliverables:

- Implement calibration interface and first calibrator (Platt or isotonic).
- Add model metadata storage (model version, trained at, source snapshot range, metrics).
- Integrate calibrated scoring into risk pipeline while preserving `RiskScore` contract.

Acceptance criteria:

- Calibrated scores generated end-to-end from `/api/risk/trigger` flow.
- Existing distribution and top-at-risk endpoints return valid data without schema changes.

## Phase 3 - Evaluation and Monitoring

Deliverables:

- Offline evaluation pipeline and report template.
- Metrics:
  - ROC-AUC
  - PR-AUC
  - Recall at top-K
  - Calibration error (ECE/Brier)
  - Heat-level confusion matrix
- Cost-sensitive scoring evaluation (false-negative vs false-positive impact).

Acceptance criteria:

- Evaluation report generated for latest test window.
- Segment-level stability checks included.

## Phase 4 - Explainability and Governance

Deliverables:

- Top driver extraction from deterministic signal contributions and calibration context.
- Enrich explanation prompt input with top factors and confidence context.
- Drift monitoring spec (feature drift + score drift + outcome drift).
- Retraining trigger policy and rollback strategy.

Acceptance criteria:

- Every high-risk customer has deterministic top drivers available for explanation generation.
- Drift thresholds defined and documented.

## Phase 5 - Rollout and Hardening

Deliverables:

- Staged rollout:
  - shadow mode (compute-only)
  - compare mode (baseline vs calibrated)
  - cutover mode (calibrated as default)
- Operational playbook for rerun/recovery.
- Backfill plan for historical snapshots (optional).

Acceptance criteria:

- No regression in API consumers.
- Rollback to baseline can be performed with configuration only.

## Proposed Code Changes

Additions:

- `PortfolioThermometer.Core/Interfaces/IFeatureExtractor.cs`
- `PortfolioThermometer.Core/Interfaces/IRiskCalibrator.cs`
- `PortfolioThermometer.Infrastructure/Services/FeatureExtractor.cs`
- `PortfolioThermometer.Infrastructure/Services/CalibrationRiskScoringEngine.cs` (or extend existing engine cleanly)
- `PortfolioThermometer.Infrastructure/Models/ModelMetadata.cs` (if persistence needed)
- `PortfolioThermometer.Infrastructure/Services/RiskModelTrainingService.cs` (offline/batch)

Modifications:

- `RiskScoringEngine` to expose structured signal contributions or delegate to feature extractor
- Risk pipeline trigger flow to include calibration step
- Explanation generation context to include top drivers and confidence metadata

## Testing Plan

Unit tests:

- Feature extraction windows and null safety
- Sub-score boundary behavior (0 and 100 clamps)
- Calibration mapping monotonicity and bounds
- Heat threshold boundary tests (39/40 and 69/70)

Integration tests:

- End-to-end risk trigger pipeline produces scores and explanations
- API compatibility tests for distribution/group/top-at-risk endpoints
- Snapshot reproducibility tests

Regression tests:

- Baseline parity mode ensures existing deterministic outputs are available when calibration is disabled

## Delivery Timeline (Suggested)

- Week 1: Phase 0 + Phase 1
- Week 2: Phase 2
- Week 3: Phase 3 + Phase 4
- Week 4: Phase 5 and production hardening

## Risks and Mitigations

- Sparse labels
  - Mitigation: proxy-label strategy + confidence-tagged training
- Data quality inconsistencies across CRM sources
  - Mitigation: feature-level validation and anomaly counters
- Model drift after market regime shifts
  - Mitigation: scheduled drift checks and retraining triggers
- Explainability mismatch between score and narrative
  - Mitigation: deterministic top-driver handoff into explanation prompts

## Definition of Done

- Hybrid scoring active with backward-compatible outputs.
- Evaluation metrics tracked and documented by time window and segment.
- Explainability includes score drivers and confidence.
- Rollback path to deterministic baseline is tested.
- API consumers require no changes.
