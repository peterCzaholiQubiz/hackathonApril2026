# Prompt 07 — Azure OpenAI Integration (TDD)

**Agent**: `ecc:tdd-guide`  
**Phase**: 3 — AI + Frontend  
**Status**: DONE  
**Depends on**: Prompt 04 (risk scoring engine), Prompt 05 (API endpoints)

---

Implement the Azure OpenAI integration for the Customer Portfolio Thermometer using TDD.

Read:
- documentation/plan/adr/003-ai-integration.md (full rationale, constraints, prompt templates)
- documentation/plan.md (AI Integration section)
- backend/src/PortfolioThermometer.Core/Interfaces/IAiExplanationService.cs

FILES TO IMPLEMENT:

1. backend/src/PortfolioThermometer.Infrastructure/AzureOpenAi/AzureOpenAiOptions.cs
   - Bound from config section "AzureOpenAI"
   - Properties: Endpoint (string), ApiKey (string), Deployment (string, default "gpt-4o"),
     ApiVersion (string, default "2024-02-01"), MaxTokens (int, default 1024),
     MaxConcurrency (int, default 5), BatchSize (int, default 10)

2. backend/src/PortfolioThermometer.Infrastructure/AzureOpenAi/AzureOpenAiClient.cs
   - Uses IHttpClientFactory (named client "azureopenai")
   - POST to https://hackathoncodemonkeyopenai.openai.azure.com/openai/deployments/{Deployment}/chat/completions?api-version={ApiVersion}
   - Headers: api-key: {ApiKey}, content-type: application/json
   - Request body: { model: deployment, max_tokens, response_format: { type: "json_object" },
     messages: [{role:"user", content}] }
   - Exponential backoff retry: 3 attempts, delays 1s / 2s / 4s on 429/500/503
   - Returns the choices[0].message.content string from the response

3. backend/src/PortfolioThermometer.Infrastructure/AzureOpenAi/Prompts/RiskExplanationPrompt.cs
   Builds a prompt given customer context + risk score + contributing signals.
   Instructs the model to respond with JSON:
   { "explanation": "2-3 sentence advisory explanation", "confidence": "high|medium|low" }
   Tone must be advisory ("may indicate", "consider", "suggests") not prescriptive.

4. backend/src/PortfolioThermometer.Infrastructure/AzureOpenAi/Prompts/SuggestedActionPrompt.cs
   Builds a prompt given all three risk explanations + overall score.
   Instructs the model to respond with JSON array:
   [{ "action_type": "outreach|discount|review|escalate|upsell",
      "priority": "high|medium|low", "title": "...", "description": "..." }]
   1–3 actions per customer. Actions must be specific (not generic advice).

5. backend/src/PortfolioThermometer.Infrastructure/Services/AzureOpenAiExplanationService.cs
   - Implements IAiExplanationService
   - For each customer with a new/changed risk score: generate 4 explanations
     (churn, payment, margin, overall) + suggested actions
   - Batches of AzureOpenAiOptions.BatchSize, throttled by SemaphoreSlim(MaxConcurrency)
   - Parses JSON responses; on parse failure: fallback placeholder with confidence="low"
   - On API failure after retries: fallback placeholder with confidence="low"
   - Stores model_used = AzureOpenAiOptions.Deployment on every explanation row

TESTS (write first):
- AzureOpenAiClientTests: verify correct headers sent (api-key), retry on 429, no retry on 400,
  correct response parsing from choices[0].message.content (mock HttpMessageHandler)
- RiskExplanationPromptTests: verify prompt contains customer name, score, signals,
  advisory tone instructions, JSON format instruction
- SuggestedActionPromptTests: verify all three risk types included, JSON array instruction
- AzureOpenAiExplanationServiceTests: verify batching logic, semaphore limiting,
  fallback on API failure, fallback on JSON parse error, correct DB writes (mock dependencies)

Reference: documentation/plan/adr/003-ai-integration.md
