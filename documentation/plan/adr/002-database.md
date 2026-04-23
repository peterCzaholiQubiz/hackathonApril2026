# ADR-002: PostgreSQL as the Analytics Database

## Status

Accepted

## Date

2026-04-10

## Context

The system requires a local analytics database that:

- Stores imported CRM data (customers, contracts, invoices, payments, complaints, interactions)
- Stores computed risk scores, AI-generated explanations, and portfolio snapshots
- Supports relational queries with joins across 10 tables
- Supports JSONB for flexible segment breakdown storage
- Runs in a Docker container on a single laptop
- Integrates well with Entity Framework Core

The CRM source is read-only. All writes happen exclusively in this analytics database. The data volume is modest: 50-100 customers for the hackathon demo, scalable to low thousands.

## Decision

Use PostgreSQL 16 running in a Docker container as the local analytics database.

## Consequences

### Positive

- **Relational strength**: The data model is heavily relational (customers -> contracts -> invoices -> payments, customers -> risk_scores -> explanations). PostgreSQL handles complex joins efficiently.
- **JSONB support**: The `portfolio_snapshots.segment_breakdown` column uses JSONB for flexible per-segment heat counts without requiring a separate table.
- **EF Core integration**: The Npgsql provider for EF Core is the most mature PostgreSQL provider in the .NET ecosystem, supporting migrations, LINQ translation, and bulk operations.
- **Docker simplicity**: The official `postgres:16` image requires only an environment variable for the password. Health checks are straightforward. No licensing concerns.
- **Standards compliance**: PostgreSQL's SQL compliance means queries and constraints behave predictably. CTEs, window functions, and aggregate queries needed for portfolio statistics work without vendor-specific workarounds.
- **Zero cost**: Open source, no licensing even for commercial use.

### Negative

- **Operational overhead vs. SQLite**: SQLite would require zero container setup, but lacks concurrent write support and JSONB capabilities needed for this workload.
- **Memory footprint**: PostgreSQL in Docker uses more memory than an embedded database, though the default configuration is adequate for demo-scale data.
- **Schema migration coordination**: EF Core migrations must be applied before the application starts. Docker Compose health checks and startup ordering handle this.

### Alternatives Considered

| Alternative | Why Not Chosen |
|-------------|----------------|
| **SQLite** | No separate container needed, but poor concurrent write support. Lacks JSONB. EF Core SQLite provider has limitations with complex migrations. Not suitable if the demo is extended beyond single-user. |
| **SQL Server (Docker)** | Heavier image (~1.5GB), requires license acceptance. No technical advantage over PostgreSQL for this workload. Team is comfortable with PostgreSQL. |
| **MySQL/MariaDB** | Viable, but PostgreSQL's JSONB, CTE support, and Npgsql provider maturity give it an edge. No team preference for MySQL. |
| **MongoDB** | The data model is relational. Document storage would require denormalization and lose referential integrity. Joins across risk scores, explanations, and customers would be awkward. |
