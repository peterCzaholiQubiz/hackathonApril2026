# Architect Working Directory

This folder is reserved for the architect agent to store architectural decision records, system design documents, and technical specifications.

## Contents (to be generated)

- `architecture.md` — Component diagram, data flow, layer descriptions
- `adr/` — Architectural Decision Records (ADRs)
- `system-design.md` — Detailed system design
- `tech-stack.md` — Technology choices and rationale
- `data-flow.md` — End-to-end data flow diagrams

## Context

**Application**: Customer Portfolio Thermometer  
**Stack**: .NET 10 backend, PostgreSQL database, Angular frontend, Claude AI (claude-sonnet-4-6)  
**Purpose**: Read-only analytics layer over CRM data that computes churn/payment/margin risk scores and generates AI-powered explanations and suggested actions.

See `../initial-description.md` for the original problem statement and `../plan.md` for the full implementation plan.
