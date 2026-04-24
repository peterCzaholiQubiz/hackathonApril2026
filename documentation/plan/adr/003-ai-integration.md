# ADR-003: Azure OpenAI (gpt-4o) for Risk Explanations and Suggested Actions

## Status

Accepted

## Date

2026-04-24

## Context

The Customer Portfolio Thermometer computes numeric risk scores (0-100) across three dimensions: churn, payment, and margin. These scores are derived from deterministic rules with explicit signal weights. However, numeric scores alone do not satisfy the hackathon requirement for "clear explanation per risk group why it is flagged" and "suggested actions."

The system needs to produce:

1. **Natural language explanations** of why a customer is flagged at a given risk level, citing the specific signals that contributed
2. **Suggested actions** (e.g., proactive outreach, contract adjustment, advisory offer) tailored to each customer's situation

The Q&A response from the challenge owner explicitly raised the concern about explainability and transparency: "When you flag a customer as a high risk and this is incorrect, you lose a customer. So make sure that you advise instead of tell. And be transparent in the reasoning."

### Why AI Instead of Templated Strings

Templated string approaches (e.g., "Customer has {n} overdue invoices and contract expires in {days} days") were considered but rejected for several reasons:

- **Combinatorial explosion**: Three risk dimensions with 5-6 signals each produce hundreds of unique combinations. Templated strings for each combination would be brittle and require extensive hand-authoring.
- **Lack of nuance**: Templates produce mechanical text. "Average days late is 45" is a fact, not an explanation. An LLM can synthesize multiple signals into a coherent narrative: "This customer's payment behavior has deteriorated over the past quarter, with invoices averaging 45 days late compared to 12 days in the prior period, suggesting potential cash flow pressure."
- **Action contextualization**: Suggested actions depend on the intersection of multiple risk dimensions. A customer with high churn risk AND high margin risk warrants different actions than one with only high churn risk. Templates cannot reason across dimensions without increasingly complex branching logic.
- **Advisory tone**: The challenge owner requested advisory, not prescriptive output. An LLM naturally produces hedged, contextual language ("Consider reaching out to discuss...") rather than imperative statements ("Call the customer").
- **Confidence signaling**: The LLM can assess and communicate its own confidence in the explanation based on the strength and consistency of the input signals.

### Explainability and Transparency Design

To address the Q&A concern directly:

- Risk scores are computed by deterministic rules, not by the AI. The AI never decides the score.
- The AI receives the score and the contributing signals as structured input. It explains what the rules already computed.
- Every explanation is stored with the model identifier (Azure OpenAI deployment name) and a confidence level.
- The UI displays both the numeric score with contributing signals AND the AI-generated explanation, so users can verify the reasoning.
- The system advises ("consider," "may benefit from") rather than prescribes ("must," "should immediately").

## Decision

Use Azure OpenAI with deployment `gpt-4o` to generate natural language risk explanations and suggested actions. The AI layer is strictly post-scoring: it explains and advises on scores that were already computed by deterministic rules.

### Configuration

| Setting | Environment variable | Example |
|---------|---------------------|---------|
| Azure OpenAI endpoint | `AzureOpenAI:Endpoint` | `https://hackathoncodemonkeyopenai.openai.azure.com/` |
| API key | `AzureOpenAI:ApiKey` | (see appsettings.json) |
| Deployment name | `AzureOpenAI:Deployment` | `gpt-4o` |
| API version | `AzureOpenAI:ApiVersion` | `2024-02-01` |

The API call uses the Azure OpenAI Chat Completions endpoint:
`POST https://hackathoncodemonkeyopenai.openai.azure.com/openai/deployments/gpt-4o/chat/completions?api-version=2024-02-01`

Auth header: `api-key: {AZURE_OPENAI_API_KEY}`

## Consequences

### Positive

- **High-quality natural language**: GPT-4o produces fluent, contextual explanations that read as professional advisory notes rather than templated output
- **Scalable to new signals**: Adding a new risk signal to the scoring engine does not require authoring new templates; the prompt automatically incorporates it
- **Advisory tone**: The model naturally produces the hedged, contextual language the challenge owner requested
- **Confidence signaling**: The model can express confidence levels based on signal strength and consistency
- **Structured output**: Azure OpenAI supports JSON mode (`response_format: { type: "json_object" }`), enabling reliable parsing of explanations and action arrays
- **Graceful degradation**: If the API is unavailable, the system falls back to a placeholder explanation with confidence "low"; scores remain fully functional
- **Enterprise-grade reliability**: Azure OpenAI provides SLA-backed availability and data residency guarantees

### Negative

- **External dependency**: Requires internet access and valid Azure credentials at explanation-generation time. The system cannot generate new explanations fully offline.
- **Latency**: Each batch of 10 customers takes 2-5 seconds for explanation generation. For 100 customers, the full pipeline takes approximately 1-2 minutes with 5 concurrent batches.
- **Cost**: API usage incurs per-token costs. For hackathon scale (100 customers, occasional re-runs), cost is negligible.
- **Non-determinism**: The same inputs may produce slightly different explanations across runs. This is acceptable for advisory text.
- **Trust boundary**: Users must understand that explanations are AI-generated. The UI must label them clearly and present the deterministic score alongside.

### Alternatives Considered

| Alternative | Why Not Chosen |
|-------------|----------------|
| **Anthropic Claude API** | Team has Azure OpenAI access available; Azure provides enterprise SLA and data residency. No meaningful quality difference for this use case. |
| **Templated strings** | Combinatorial explosion for multi-signal explanations. Cannot reason across dimensions. Produces mechanical output. Does not scale to new signals without manual authoring. |
| **Local LLM (Ollama/llama.cpp)** | Avoids API dependency, but requires significant GPU/CPU resources on the demo laptop. Model quality for structured JSON output is lower than GPT-4o. Setup complexity is high for a hackathon. |
| **No AI — rules-only with static text** | Satisfies the scoring requirement but not the "clear explanation" and "suggested actions" requirements. The demonstration impact would be significantly reduced. |

### Mitigations

| Risk | Mitigation |
|------|-----------|
| API rate limits | SemaphoreSlim (5 concurrent), batch size 10, exponential backoff retry |
| API unavailability | Fallback placeholder explanation with confidence "low"; scores unaffected |
| API key/endpoint exposure | Environment variables only; never committed; `.env.example` with placeholder |
| Non-determinism | Cache explanations; only regenerate when risk scores change |
| User trust | Display deterministic signals alongside AI text; label as "AI-generated advisory" |
