# Prompt 11 — Security Review

**Agent**: `ecc:security-reviewer`  
**Phase**: 4 — Polish  
**Status**: DONE  
**Depends on**: All previous prompts complete

---

Security review the Customer Portfolio Thermometer before the hackathon demo.

Read documentation/plan/adr/003-ai-integration.md for the Claude API integration context.

FILES TO REVIEW:

1. backend/src/PortfolioThermometer.Infrastructure/Claude/ClaudeApiClient.cs
   - Is the Anthropic API URL hardcoded or configurable? (SSRF risk if configurable without validation)
   - Is the API key read from configuration, never from user input?
   - Are response bodies size-limited before parsing?

2. backend/src/PortfolioThermometer.Api/Controllers/ImportController.cs
   - Is there any rate limiting or guard on POST /api/import/trigger?
     (Prevent accidental or repeated triggering during demo)
   - Does the status endpoint leak internal error messages or stack traces?

3. backend/src/PortfolioThermometer.Infrastructure/Services/CrmImportService.cs
   - Is the CRM connection string validated at startup (not at request time)?
   - Are there any write operations (INSERT/UPDATE/DELETE) against the CRM source?
     This would violate the read-only constraint.
   - Is the connection opened with read-only permissions where supported?

4. backend/src/PortfolioThermometer.Api/Program.cs
   - CORS: is it locked to localhost:4200 only in development? Not wildcard (*)?
   - Does the global error handler strip stack traces from production error responses?

5. frontend/src/app/core/interceptors/error.interceptor.ts
   - Does it log full error details to the browser console in production?
     (May expose internal URLs or error messages)
   - Does it handle 401/403 responses without exposing auth details?

6. docker-compose.yml + .env.example
   - Are all secrets injected via environment variables?
   - Is .env in .gitignore?

CHECK FOR:
- Hardcoded API keys, passwords, or connection strings anywhere in the codebase
- SQL injection in any raw query strings
- Error responses that leak stack traces, connection strings, or internal paths
- CORS misconfiguration
- Read-only constraint violations in CrmImportService

Report findings by severity (CRITICAL / HIGH / MEDIUM) with file:line references.
CRITICAL issues must be fixed before the demo.

Reference: documentation/plan/architecture.md (Integration Points section)
