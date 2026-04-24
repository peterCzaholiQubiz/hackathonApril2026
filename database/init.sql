-- ============================================================
-- Customer Portfolio Thermometer — Analytics Database Schema
-- PostgreSQL 16
-- ============================================================

-- Enable pgcrypto for gen_random_uuid()
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ============================================================
-- CRM MIRROR TABLES (populated by CrmImportService, read-only source)
-- ============================================================

CREATE TABLE IF NOT EXISTS customers (
    id               UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    crm_external_id  VARCHAR(100) NOT NULL,
    name             VARCHAR(255) NOT NULL,
    company_name     VARCHAR(255),
    email            VARCHAR(255),
    phone            VARCHAR(50),
    segment          VARCHAR(50),   -- 'enterprise', 'mid-market', 'smb'
    account_manager  VARCHAR(255),
    onboarding_date  DATE,
    is_active        BOOLEAN      NOT NULL DEFAULT TRUE,
    imported_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_customers_crm_id UNIQUE (crm_external_id)
);

CREATE INDEX IF NOT EXISTS idx_customers_segment   ON customers (segment);
CREATE INDEX IF NOT EXISTS idx_customers_is_active ON customers (is_active);

-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS contracts (
    id               UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id      UUID         NOT NULL REFERENCES customers (id) ON DELETE CASCADE,
    crm_external_id  VARCHAR(100) NOT NULL,
    contract_type    VARCHAR(50),   -- 'subscription', 'one-time', 'retainer'
    start_date       DATE,
    end_date         DATE,          -- NULL for open-ended contracts
    monthly_value    DECIMAL(12,2),
    currency         VARCHAR(3)   NOT NULL DEFAULT 'EUR',
    status           VARCHAR(20),   -- 'active', 'expired', 'cancelled'
    auto_renew       BOOLEAN      NOT NULL DEFAULT FALSE,
    imported_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_contracts_crm_id UNIQUE (crm_external_id)
);

CREATE INDEX IF NOT EXISTS idx_contracts_customer_id ON contracts (customer_id);
CREATE INDEX IF NOT EXISTS idx_contracts_status      ON contracts (status);
CREATE INDEX IF NOT EXISTS idx_contracts_end_date    ON contracts (end_date);

-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS invoices (
    id               UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id      UUID         NOT NULL REFERENCES customers (id) ON DELETE CASCADE,
    crm_external_id  VARCHAR(100) NOT NULL,
    invoice_number   VARCHAR(50),
    issued_date      DATE,
    due_date         DATE,
    amount           DECIMAL(12,2),
    currency         VARCHAR(3)   NOT NULL DEFAULT 'EUR',
    status           VARCHAR(20),   -- 'paid', 'unpaid', 'overdue', 'partial'
    imported_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_invoices_crm_id UNIQUE (crm_external_id)
);

CREATE INDEX IF NOT EXISTS idx_invoices_customer_id ON invoices (customer_id);
CREATE INDEX IF NOT EXISTS idx_invoices_status      ON invoices (status);
CREATE INDEX IF NOT EXISTS idx_invoices_due_date    ON invoices (due_date);

-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS payments (
    id               UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    invoice_id       UUID         NOT NULL REFERENCES invoices (id) ON DELETE CASCADE,
    customer_id      UUID         NOT NULL REFERENCES customers (id) ON DELETE CASCADE,
    crm_external_id  VARCHAR(100) NOT NULL,
    payment_date     DATE,
    amount           DECIMAL(12,2),
    days_late        INTEGER      NOT NULL DEFAULT 0, -- negative = early
    imported_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_payments_crm_id UNIQUE (crm_external_id)
);

CREATE INDEX IF NOT EXISTS idx_payments_customer_id  ON payments (customer_id);
CREATE INDEX IF NOT EXISTS idx_payments_invoice_id   ON payments (invoice_id);
CREATE INDEX IF NOT EXISTS idx_payments_payment_date ON payments (payment_date);

-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS complaints (
    id               UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id      UUID         NOT NULL REFERENCES customers (id) ON DELETE CASCADE,
    crm_external_id  VARCHAR(100) NOT NULL,
    created_date     DATE,
    resolved_date    DATE,          -- NULL = unresolved
    category         VARCHAR(50),   -- 'billing', 'service', 'product', 'other'
    severity         VARCHAR(20),   -- 'low', 'medium', 'high', 'critical'
    description      TEXT,
    imported_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_complaints_crm_id UNIQUE (crm_external_id)
);

CREATE INDEX IF NOT EXISTS idx_complaints_customer_id   ON complaints (customer_id);
CREATE INDEX IF NOT EXISTS idx_complaints_severity      ON complaints (severity);
CREATE INDEX IF NOT EXISTS idx_complaints_created_date  ON complaints (created_date);
CREATE INDEX IF NOT EXISTS idx_complaints_resolved_date ON complaints (resolved_date);

-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS interactions (
    id               UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id      UUID         NOT NULL REFERENCES customers (id) ON DELETE CASCADE,
    crm_external_id  VARCHAR(100) NOT NULL,
    interaction_date DATE,
    channel          VARCHAR(30),   -- 'email', 'phone', 'meeting', 'chat'
    direction        VARCHAR(10),   -- 'inbound', 'outbound'
    summary          TEXT,
    sentiment        VARCHAR(20),   -- 'positive', 'neutral', 'negative' (nullable)
    imported_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_interactions_crm_id UNIQUE (crm_external_id)
);

CREATE INDEX IF NOT EXISTS idx_interactions_customer_id      ON interactions (customer_id);
CREATE INDEX IF NOT EXISTS idx_interactions_interaction_date ON interactions (interaction_date);
CREATE INDEX IF NOT EXISTS idx_interactions_direction        ON interactions (direction);
CREATE INDEX IF NOT EXISTS idx_interactions_sentiment        ON interactions (sentiment);

-- ============================================================
-- COMPUTED / ANALYTICS TABLES (written by backend services)
-- ============================================================

-- portfolio_snapshots must be created before risk_scores (FK dependency)
CREATE TABLE IF NOT EXISTS portfolio_snapshots (
    id                UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    total_customers   INTEGER      NOT NULL DEFAULT 0,
    green_count       INTEGER      NOT NULL DEFAULT 0,
    yellow_count      INTEGER      NOT NULL DEFAULT 0,
    red_count         INTEGER      NOT NULL DEFAULT 0,
    green_pct         DECIMAL(5,2) NOT NULL DEFAULT 0,
    yellow_pct        DECIMAL(5,2) NOT NULL DEFAULT 0,
    red_pct           DECIMAL(5,2) NOT NULL DEFAULT 0,
    avg_churn_score   DECIMAL(5,2) NOT NULL DEFAULT 0,
    avg_payment_score DECIMAL(5,2) NOT NULL DEFAULT 0,
    avg_margin_score  DECIMAL(5,2) NOT NULL DEFAULT 0,
    segment_breakdown JSONB        -- e.g. {"enterprise":{"green":5,"yellow":2,"red":1}}
);

CREATE INDEX IF NOT EXISTS idx_portfolio_snapshots_created_at ON portfolio_snapshots (created_at DESC);

-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS risk_scores (
    id            UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id   UUID        NOT NULL REFERENCES customers (id) ON DELETE CASCADE,
    snapshot_id   UUID        NOT NULL REFERENCES portfolio_snapshots (id) ON DELETE CASCADE,
    churn_score   INTEGER     NOT NULL CHECK (churn_score   BETWEEN 0 AND 100),
    payment_score INTEGER     NOT NULL CHECK (payment_score BETWEEN 0 AND 100),
    margin_score  INTEGER     NOT NULL CHECK (margin_score  BETWEEN 0 AND 100),
    overall_score INTEGER     NOT NULL CHECK (overall_score BETWEEN 0 AND 100),
    heat_level    VARCHAR(10) NOT NULL CHECK (heat_level IN ('green', 'yellow', 'red')),
    scored_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_risk_scores_customer_id   ON risk_scores (customer_id);
CREATE INDEX IF NOT EXISTS idx_risk_scores_snapshot_id   ON risk_scores (snapshot_id);
CREATE INDEX IF NOT EXISTS idx_risk_scores_heat_level    ON risk_scores (heat_level);
CREATE INDEX IF NOT EXISTS idx_risk_scores_overall_score ON risk_scores (overall_score DESC);

-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS risk_explanations (
    id             UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    risk_score_id  UUID         NOT NULL REFERENCES risk_scores (id) ON DELETE CASCADE,
    customer_id    UUID         NOT NULL REFERENCES customers (id) ON DELETE CASCADE,
    risk_type      VARCHAR(20)  NOT NULL CHECK (risk_type IN ('churn', 'payment', 'margin', 'overall')),
    explanation    TEXT         NOT NULL,
    confidence     VARCHAR(10)  NOT NULL CHECK (confidence IN ('high', 'medium', 'low')),
    generated_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    model_used     VARCHAR(50)  NOT NULL DEFAULT 'claude-sonnet-4-6'
);

CREATE INDEX IF NOT EXISTS idx_risk_explanations_risk_score_id ON risk_explanations (risk_score_id);
CREATE INDEX IF NOT EXISTS idx_risk_explanations_customer_id   ON risk_explanations (customer_id);
CREATE INDEX IF NOT EXISTS idx_risk_explanations_risk_type     ON risk_explanations (risk_type);

-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS suggested_actions (
    id             UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    risk_score_id  UUID         NOT NULL REFERENCES risk_scores (id) ON DELETE CASCADE,
    customer_id    UUID         NOT NULL REFERENCES customers (id) ON DELETE CASCADE,
    action_type    VARCHAR(30)  NOT NULL CHECK (action_type IN ('outreach', 'discount', 'review', 'escalate', 'upsell')),
    priority       VARCHAR(10)  NOT NULL CHECK (priority IN ('high', 'medium', 'low')),
    title          VARCHAR(255) NOT NULL,
    description    TEXT,
    generated_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_suggested_actions_risk_score_id ON suggested_actions (risk_score_id);
CREATE INDEX IF NOT EXISTS idx_suggested_actions_customer_id   ON suggested_actions (customer_id);
CREATE INDEX IF NOT EXISTS idx_suggested_actions_priority      ON suggested_actions (priority);
