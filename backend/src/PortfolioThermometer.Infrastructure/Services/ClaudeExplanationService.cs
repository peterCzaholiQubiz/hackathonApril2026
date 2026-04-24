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
                var actions = JsonSerializer.Deserialize<List<ClaudeAction>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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
}
