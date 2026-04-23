# Prompt 07 — Claude API Integration (TDD)

**Agent**: `ecc:tdd-guide`  
**Phase**: 3 — AI + Frontend  
**Status**: TODO  
**Depends on**: Prompt 04 (risk scoring engine), Prompt 05 (API endpoints)

---

Implement the Claude API integration for the Customer Portfolio Thermometer using TDD.

Read:
- documentation/plan/adr/003-ai-integration.md (full rationale, constraints, prompt templates)
- documentation/plan.md (AI Integration section)
- backend/src/PortfolioThermometer.Core/Interfaces/IClaudeExplanationService.cs

FILES TO IMPLEMENT:

1. backend/src/PortfolioThermometer.Infrastructure/Claude/ClaudeApiOptions.cs
   - Bound from config section "Anthropic"
   - Properties: ApiKey (string), Model (string, default "claude-sonnet-4-6"),
     MaxTokens (int, default 1024), MaxConcurrency (int, default 5), BatchSize (int, default 10)

2. backend/src/PortfolioThermometer.Infrastructure/Claude/ClaudeApiClient.cs
   - Uses IHttpClientFactory (named client "claude")
   - POST to https://api.anthropic.com/v1/messages
   - Headers: x-api-key, anthropic-version: 2023-06-01, content-type: application/json
   - Request body: { model, max_tokens, messages: [{role:"user", content}] }
   - Exponential backoff retry: 3 attempts, delays 1s / 2s / 4s on 429/500/503
   - Returns the content[0].text string from the response

3. backend/src/PortfolioThermometer.Infrastructure/Claude/Prompts/RiskExplanationPrompt.cs
   Builds a prompt given customer context + risk score + contributing signals.
   Instructs Claude to respond with JSON:
   { "explanation": "2-3 sentence advisory explanation", "confidence": "high|medium|low" }
   Tone must be advisory ("may indicate", "consider", "suggests") not prescriptive.

4. backend/src/PortfolioThermometer.Infrastructure/Claude/Prompts/SuggestedActionPrompt.cs
   Builds a prompt given all three risk explanations + overall score.
   Instructs Claude to respond with JSON array:
   [{ "action_type": "outreach|discount|review|escalate|upsell",
      "priority": "high|medium|low", "title": "...", "description": "..." }]
   1–3 actions per customer. Actions must be specific (not generic advice).

5. backend/src/PortfolioThermometer.Infrastructure/Services/ClaudeExplanationService.cs
   - Implements IClaudeExplanationService
   - For each customer with a new/changed risk score: generate 4 explanations
     (churn, payment, margin, overall) + suggested actions
   - Batches of ClaudeApiOptions.BatchSize, throttled by SemaphoreSlim(MaxConcurrency)
   - Parses JSON responses; on parse failure: fallback placeholder with confidence="low"
   - On API failure after retries: fallback placeholder with confidence="low"
   - Stores model_used = ClaudeApiOptions.Model on every explanation row

TESTS (write first):
- ClaudeApiClientTests: verify correct headers sent, retry on 429, no retry on 400,
  correct response parsing (mock HttpMessageHandler)
- RiskExplanationPromptTests: verify prompt contains customer name, score, signals,
  advisory tone instructions, JSON format instruction
- SuggestedActionPromptTests: verify all three risk types included, JSON array instruction
- ClaudeExplanationServiceTests: verify batching logic, semaphore limiting,
  fallback on API failure, fallback on JSON parse error, correct DB writes (mock dependencies)

Reference: documentation/plan/adr/003-ai-integration.md
