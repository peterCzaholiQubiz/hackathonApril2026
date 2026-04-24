# Prompt 10 — Code Review

**Agent**: `ecc:code-reviewer`  
**Phase**: 4 — Polish  
**Status**: DONE  
**Depends on**: All previous prompts complete

---

Review the Customer Portfolio Thermometer codebase for quality, security, and correctness.

Read the following files before reviewing:
- documentation/plan.md (architecture and requirements)
- documentation/plan/architecture.md (layer descriptions and principles)

REVIEW SCOPE:

Backend (backend/src/):
- Verify no business logic leaks into controllers (should delegate to services/interfaces)
- Check EF Core queries for N+1 patterns (use Include() and projection where needed)
- Verify all async methods use CancellationToken and await correctly (no .Result or .Wait())
- Check error handling: no silent catch blocks, no empty catch, errors propagate or log
- Verify consistent use of the API response envelope across all controllers
- Check that CrmImportService has no write operations against the external CRM source
- Verify ANTHROPIC_API_KEY is read from configuration, never hardcoded

Frontend (frontend/src/):
- Check for Observable subscriptions without unsubscribe (use async pipe or takeUntilDestroyed)
- Verify no direct DOM manipulation (should use Angular bindings)
- Check that error interceptor handles all HTTP error codes (400, 401, 404, 500)
- Verify loading states are shown for all async data-fetching components
- Check that AI-generated content is labeled as "AI Advisory" in the UI

Cross-cutting:
- Verify no secrets or credentials committed anywhere in the repo
- Check .gitignore includes .env, *.user, bin/, obj/, node_modules/
- Verify docker-compose.yml reads secrets from environment variables, not hardcoded values

Report only CRITICAL and HIGH severity issues. For each finding, provide:
- Severity: CRITICAL | HIGH
- File path and line number
- Description of the issue
- Suggested fix (concise)

Reference: documentation/plan/architecture.md, documentation/plan/adr/
