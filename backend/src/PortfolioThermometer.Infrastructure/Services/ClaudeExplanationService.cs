using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Infrastructure.Services;

/// <summary>
/// Generates natural-language risk explanations and suggested actions
/// using the Claude API (model: claude-sonnet-4-6).
///
/// Implements batching (10 per batch), concurrency (5 parallel), and
/// fallback to placeholder explanations if the API is unavailable.
/// </summary>
public sealed class ClaudeExplanationService : IClaudeExplanationService
{
    private const int BatchSize = 10;
    private const int MaxConcurrency = 5;
    private const string ModelId = "claude-sonnet-4-6";

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClaudeExplanationService> _logger;

    public ClaudeExplanationService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ClaudeExplanationService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task GenerateExplanationsAsync(IReadOnlyList<RiskScore> scores, CancellationToken ct)
    {
        var semaphore = new SemaphoreSlim(MaxConcurrency);
        var batches = scores
            .Chunk(BatchSize)
            .ToList();

        var tasks = batches.Select(batch => ProcessBatchAsync(batch, semaphore, ct));
        await Task.WhenAll(tasks);
    }

    private async Task ProcessBatchAsync(
        RiskScore[] batch,
        SemaphoreSlim semaphore,
        CancellationToken ct)
    {
        await semaphore.WaitAsync(ct);
        try
        {
            foreach (var score in batch)
            {
                await GenerateForScoreAsync(score, ct);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task GenerateForScoreAsync(RiskScore score, CancellationToken ct)
    {
        // Skip if explanations already exist for this score
        var alreadyExists = await _db.RiskExplanations
            .AnyAsync(e => e.RiskScoreId == score.Id, ct);
        if (alreadyExists) return;

        var customer = await _db.Customers.FindAsync([score.CustomerId], ct);
        if (customer is null) return;

        foreach (var riskType in new[] { "churn", "payment", "margin", "overall" })
        {
            var explanation = await GenerateExplanationAsync(score, customer, riskType, ct);
            _db.RiskExplanations.Add(explanation);
        }

        var actions = await GenerateActionsAsync(score, customer, ct);
        _db.SuggestedActions.AddRange(actions);

        await _db.SaveChangesAsync(ct);
    }

    private async Task<RiskExplanation> GenerateExplanationAsync(
        RiskScore score,
        Customer customer,
        string riskType,
        CancellationToken ct)
    {
        var scoreValue = riskType switch
        {
            "churn" => score.ChurnScore,
            "payment" => score.PaymentScore,
            "margin" => score.MarginScore,
            _ => score.OverallScore
        };

        var prompt = $"""
            Analyze the {riskType} risk for customer "{customer.Name}" (segment: {customer.Segment ?? "unknown"}).
            {riskType} risk score: {scoreValue}/100. Overall risk heat level: {score.HeatLevel}.
            Churn: {score.ChurnScore}, Payment: {score.PaymentScore}, Margin: {score.MarginScore}.

            Provide a concise, actionable explanation (2-3 sentences) for why this score was assigned.
            Respond ONLY with valid JSON with two fields: explanation (string) and confidence (high, medium, or low).
            """;

        try
        {
            var response = await CallClaudeAsync(prompt, ct);
            if (response is not null)
            {
                return new RiskExplanation
                {
                    Id = Guid.NewGuid(),
                    RiskScoreId = score.Id,
                    CustomerId = score.CustomerId,
                    RiskType = riskType,
                    Explanation = response.Explanation,
                    Confidence = response.Confidence,
                    GeneratedAt = DateTimeOffset.UtcNow,
                    ModelUsed = ModelId
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claude API call failed for customer {CustomerId}, risk type {RiskType}", score.CustomerId, riskType);
        }

        // Fallback explanation
        return new RiskExplanation
        {
            Id = Guid.NewGuid(),
            RiskScoreId = score.Id,
            CustomerId = score.CustomerId,
            RiskType = riskType,
            Explanation = $"Risk score of {scoreValue} indicates {score.HeatLevel} {riskType} risk. Detailed analysis unavailable.",
            Confidence = "low",
            GeneratedAt = DateTimeOffset.UtcNow,
            ModelUsed = "fallback"
        };
    }

    private async Task<List<SuggestedAction>> GenerateActionsAsync(
        RiskScore score,
        Customer customer,
        CancellationToken ct)
    {
        var prompt = $"""
            Customer "{customer.Name}" (segment: {customer.Segment ?? "unknown"}) risk summary:
            Churn: {score.ChurnScore}/100, Payment: {score.PaymentScore}/100, Margin: {score.MarginScore}/100.
            Overall: {score.OverallScore}/100, Heat level: {score.HeatLevel}.

            Suggest 2-3 concrete actions for the account manager.
            Action types: outreach, discount, review, escalate, upsell.
            Priorities: high, medium, low.
            Respond ONLY with a valid JSON array where each object has: action_type, priority, title, and description fields.
            """;

        try
        {
            var client = _httpClientFactory.CreateClient("ClaudeApi");
            var requestBody = new
            {
                model = ModelId,
                max_tokens = 1024,
                messages = new[] { new { role = "user", content = prompt } }
            };

            var response = await client.PostAsJsonAsync("v1/messages", requestBody, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var parsed = JsonSerializer.Deserialize<ClaudeMessageResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var content = parsed?.Content?.FirstOrDefault()?.Text ?? "[]";
                var actions = DeserializeClaudeActions(content);

                if (actions?.Count > 0)
                {
                    return actions.Select(a => new SuggestedAction
                    {
                        Id = Guid.NewGuid(),
                        RiskScoreId = score.Id,
                        CustomerId = score.CustomerId,
                        ActionType = a.ActionType,
                        Priority = a.Priority,
                        Title = a.Title,
                        Description = a.Description,
                        GeneratedAt = DateTimeOffset.UtcNow
                    }).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate actions for customer {CustomerId}", score.CustomerId);
        }

        // Fallback action
        return
        [
            new SuggestedAction
            {
                Id = Guid.NewGuid(),
                RiskScoreId = score.Id,
                CustomerId = score.CustomerId,
                ActionType = "review",
                Priority = score.HeatLevel == "red" ? "high" : "medium",
                Title = "Review customer account",
                Description = "Schedule an account review to assess current status and risks.",
                GeneratedAt = DateTimeOffset.UtcNow
            }
        ];
    }

    public async Task<IReadOnlyList<SuggestedAction>> GenerateSuggestedActionsAsync(
        Guid customerId, CancellationToken ct)
    {
        var customer = await _db.Customers.FindAsync([customerId], ct);
        if (customer is null) return [];

        var score = await _db.RiskScores
            .Include(r => r.RiskExplanations)
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.ScoredAt)
            .FirstOrDefaultAsync(ct);

        if (score is null) return [];

        var recentPayments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.PaymentDate)
            .Take(50)
            .Select(p => new { p.DaysLate })
            .ToListAsync(ct);

        var recentInteractions = await _db.Interactions
            .AsNoTracking()
            .Where(i => i.CustomerId == customerId)
            .OrderByDescending(i => i.InteractionDate)
            .Take(10)
            .Select(i => new
            {
                Date = i.InteractionDate != null ? i.InteractionDate.Value.ToString("yyyy-MM-dd") : null,
                i.Channel,
                i.Sentiment,
                i.Summary
            })
            .ToListAsync(ct);

        var churnExp = score.RiskExplanations.FirstOrDefault(e => e.RiskType == "churn")?.Explanation;
        var paymentExp = score.RiskExplanations.FirstOrDefault(e => e.RiskType == "payment")?.Explanation;
        var marginExp = score.RiskExplanations.FirstOrDefault(e => e.RiskType == "margin")?.Explanation;

        var totalPayments = recentPayments.Count;
        var latePayments = recentPayments.Count(p => p.DaysLate > 15);
        var heavilyLate = recentPayments.Count(p => p.DaysLate > 30);
        var avgDaysLate = totalPayments > 0 ? recentPayments.Average(p => p.DaysLate) : 0.0;

        var interactionLines = recentInteractions.Count > 0
            ? string.Join("\n", recentInteractions.Select(i =>
                $"  - {i.Date ?? "?"} | {i.Channel ?? "?"} | sentiment: {i.Sentiment ?? "?"} | {(i.Summary ?? string.Empty)[..Math.Min((i.Summary ?? string.Empty).Length, 80)]}"))
            : "  No recent interactions recorded.";

        var prompt = $$"""
            You are an advisory AI assistant for a portfolio management system.

            Customer: "{{customer.Name}}" (segment: {{customer.Segment ?? "unknown"}})
            Overall risk score: {{score.OverallScore}}/100 (heat: {{score.HeatLevel}})
            Breakdown — Churn: {{score.ChurnScore}}/100 | Payment: {{score.PaymentScore}}/100 | Margin: {{score.MarginScore}}/100

            Risk analysis:
            - Churn risk: {{churnExp ?? "No explanation available."}}
            - Payment risk: {{paymentExp ?? "No explanation available."}}
            - Margin risk: {{marginExp ?? "No explanation available."}}

            Payment behaviour (recent history):
            - Total payments on record: {{totalPayments}}
            - Payments > 15 days late: {{latePayments}}
            - Payments > 30 days late: {{heavilyLate}}
            - Average days late: {{avgDaysLate:F1}}

            Recent interactions (last {{recentInteractions.Count}}):
            {{interactionLines}}

            Based on all data above, suggest 1-3 specific, concrete actions for the account manager.
            Actions must be tailored to this customer's actual situation, not generic advice.

            Action types (use exactly one of): outreach, discount, review, escalate, upsell
            Priorities (use exactly one of): high, medium, low

            Respond ONLY with a valid JSON array in this exact format:
            [{ "action_type": "outreach", "priority": "high", "title": "Action title", "description": "Specific description" }]
            """;

        List<SuggestedAction> newActions;
        try
        {
            var client = _httpClientFactory.CreateClient("ClaudeApi");
            var requestBody = new
            {
                model = ModelId,
                max_tokens = 1024,
                messages = new[] { new { role = "user", content = prompt } }
            };

            var response = await client.PostAsJsonAsync("v1/messages", requestBody, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var parsed = JsonSerializer.Deserialize<ClaudeMessageResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var content = parsed?.Content?.FirstOrDefault()?.Text ?? "[]";
                var actions = DeserializeClaudeActions(content);

                if (actions?.Count > 0)
                {
                    newActions = actions.Select(a => new SuggestedAction
                    {
                        Id = Guid.NewGuid(),
                        RiskScoreId = score.Id,
                        CustomerId = score.CustomerId,
                        ActionType = a.ActionType,
                        Priority = a.Priority,
                        Title = a.Title,
                        Description = a.Description,
                        GeneratedAt = DateTimeOffset.UtcNow
                    }).ToList();
                }
                else
                {
                    newActions = [FallbackSuggestedAction(score)];
                }
            }
            else
            {
                newActions = [FallbackSuggestedAction(score)];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate suggested actions for customer {CustomerId}", customerId);
            newActions = [FallbackSuggestedAction(score)];
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var existing = _db.SuggestedActions.Where(a => a.RiskScoreId == score.Id);
            _db.SuggestedActions.RemoveRange(existing);
            _db.SuggestedActions.AddRange(newActions);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return newActions;
    }

    private static SuggestedAction FallbackSuggestedAction(RiskScore score) => new()
    {
        Id = Guid.NewGuid(),
        RiskScoreId = score.Id,
        CustomerId = score.CustomerId,
        ActionType = "review",
        Priority = score.HeatLevel == "red" ? "high" : "medium",
        Title = "Review customer account",
        Description = "Schedule an account review to assess current status and risks.",
        GeneratedAt = DateTimeOffset.UtcNow
    };

    private async Task<ClaudeExplanationResponse?> CallClaudeAsync(string prompt, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("ClaudeApi");
        var requestBody = new
        {
            model = ModelId,
            max_tokens = 512,
            messages = new[] { new { role = "user", content = prompt } }
        };

        var response = await client.PostAsJsonAsync("v1/messages", requestBody, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var parsed = JsonSerializer.Deserialize<ClaudeMessageResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var content = parsed?.Content?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(content)) return null;

        return JsonSerializer.Deserialize<ClaudeExplanationResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    // ── DTOs for Claude API deserialization ───────────────────────────────────

    private sealed class ClaudeMessageResponse
    {
        public List<ClaudeContentBlock>? Content { get; set; }
    }

    private sealed class ClaudeContentBlock
    {
        public string? Text { get; set; }
    }

    private sealed class ClaudeExplanationResponse
    {
        public string Explanation { get; set; } = string.Empty;
        public string Confidence { get; set; } = "low";
    }

    private sealed class ClaudeAction
    {
        public string ActionType { get; set; } = string.Empty;
        public string Priority { get; set; } = "medium";
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    private sealed class ClaudeActionsWrapper
    {
        public List<ClaudeAction>? Actions { get; set; }
    }

    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private static List<ClaudeAction>? DeserializeClaudeActions(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
            return JsonSerializer.Deserialize<List<ClaudeAction>>(content, SnakeCaseOptions);

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("actions", out var actionsProp) && actionsProp.ValueKind == JsonValueKind.Array)
            {
                var wrapper = JsonSerializer.Deserialize<ClaudeActionsWrapper>(content, SnakeCaseOptions);
                return wrapper?.Actions;
            }

            // Single action object
            var single = JsonSerializer.Deserialize<ClaudeAction>(content, SnakeCaseOptions);
            return single is not null ? [single] : null;
        }

        return null;
    }
}
