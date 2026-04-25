using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.AzureOpenAi;
using PortfolioThermometer.Infrastructure.AzureOpenAi.Prompts;
using PortfolioThermometer.Infrastructure.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortfolioThermometer.Infrastructure.Services;

public sealed class AzureOpenAiExplanationService : IClaudeExplanationService
{
    private readonly AppDbContext _db;
    private readonly IAzureOpenAiClient _client;
    private readonly AzureOpenAiOptions _options;
    private readonly ILogger<AzureOpenAiExplanationService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public AzureOpenAiExplanationService(
        AppDbContext db,
        IAzureOpenAiClient client,
        IOptions<AzureOpenAiOptions> options,
        ILogger<AzureOpenAiExplanationService> logger)
    {
        _db = db;
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task GenerateExplanationsAsync(IReadOnlyList<RiskScore> scores, CancellationToken ct)
    {
        var semaphore = new SemaphoreSlim(_options.MaxConcurrency);
        var tasks = scores
            .Chunk(_options.BatchSize)
            .Select(batch => ProcessBatchAsync(batch, semaphore, ct));
        await Task.WhenAll(tasks);
    }

    private async Task ProcessBatchAsync(RiskScore[] batch, SemaphoreSlim semaphore, CancellationToken ct)
    {
        await semaphore.WaitAsync(ct);
        try
        {
            foreach (var score in batch)
                await GenerateForScoreAsync(score, ct);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task GenerateForScoreAsync(RiskScore score, CancellationToken ct)
    {
        var alreadyExists = await _db.RiskExplanations.AnyAsync(e => e.RiskScoreId == score.Id, ct);
        if (alreadyExists) return;

        var customer = await _db.Customers.FindAsync([score.CustomerId], ct);
        if (customer is null) return;

        var explanations = new Dictionary<string, RiskExplanation>();

        foreach (var riskType in new[] { "churn", "payment", "margin", "overall" })
        {
            var explanation = await GenerateExplanationAsync(score, customer, riskType, ct);
            explanations[riskType] = explanation;
            _db.RiskExplanations.Add(explanation);
        }

        var actions = await GenerateActionsAsync(score, customer, explanations, ct);
        _db.SuggestedActions.AddRange(actions);

        await _db.SaveChangesAsync(ct);
    }

    private async Task<RiskExplanation> GenerateExplanationAsync(
        RiskScore score, Customer customer, string riskType, CancellationToken ct)
    {
        var scoreValue = riskType switch
        {
            "churn" => score.ChurnScore,
            "payment" => score.PaymentScore,
            "margin" => score.MarginScore,
            _ => score.OverallScore
        };

        var prompt = RiskExplanationPrompt.Build(
            customer.Name, customer.Segment, riskType, scoreValue,
            score.HeatLevel, score.ChurnScore, score.PaymentScore, score.MarginScore);

        try
        {
            var content = await _client.CompleteAsync(prompt, ct);
            if (content is not null)
            {
                var parsed = JsonSerializer.Deserialize<ExplanationDto>(content, JsonOptions);
                if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.Explanation))
                    return CreateExplanation(score, riskType, parsed.Explanation, parsed.Confidence);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to parse {RiskType} explanation for customer {CustomerId}",
                riskType, score.CustomerId);
        }

        return FallbackExplanation(score, riskType, scoreValue);
    }

    private async Task<List<SuggestedAction>> GenerateActionsAsync(
        RiskScore score, Customer customer,
        Dictionary<string, RiskExplanation> explanations, CancellationToken ct)
    {
        var prompt = SuggestedActionPrompt.Build(
            customer.Name, customer.Segment,
            explanations.GetValueOrDefault("churn")?.Explanation ?? string.Empty,
            explanations.GetValueOrDefault("payment")?.Explanation ?? string.Empty,
            explanations.GetValueOrDefault("margin")?.Explanation ?? string.Empty,
            score.OverallScore, score.HeatLevel);

        try
        {
            var content = await _client.CompleteAsync(prompt, ct);
            if (content is not null)
            {
                var actions = DeserializeActions(content);
                if (actions?.Count > 0)
                    return actions.Select(a => CreateAction(score, a)).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse actions for customer {CustomerId}", score.CustomerId);
        }

        return [FallbackAction(score)];
    }

    private RiskExplanation CreateExplanation(
        RiskScore score, string riskType, string text, string confidence) => new()
        {
            Id = Guid.NewGuid(),
            RiskScoreId = score.Id,
            CustomerId = score.CustomerId,
            RiskType = riskType,
            Explanation = text,
            Confidence = confidence,
            GeneratedAt = DateTimeOffset.UtcNow,
            ModelUsed = _options.Deployment
        };

    private static RiskExplanation FallbackExplanation(RiskScore score, string riskType, int scoreValue) => new()
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

    private static SuggestedAction CreateAction(RiskScore score, ActionDto a) => new()
    {
        Id = Guid.NewGuid(),
        RiskScoreId = score.Id,
        CustomerId = score.CustomerId,
        ActionType = a.ActionType,
        Priority = a.Priority,
        Title = a.Title,
        Description = a.Description,
        GeneratedAt = DateTimeOffset.UtcNow
    };

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

        var interactions = recentInteractions
            .Select(i => (i.Date, i.Channel, i.Sentiment, i.Summary))
            .ToList();

        var prompt = SuggestedActionsEnhancedPrompt.Build(
            customer.Name, customer.Segment,
            score.OverallScore, score.ChurnScore, score.PaymentScore, score.MarginScore,
            score.HeatLevel,
            churnExp, paymentExp, marginExp,
            interactions,
            totalPayments, latePayments, heavilyLate, avgDaysLate);

        List<SuggestedAction> newActions;
        try
        {
            var content = await _client.CompleteAsync(prompt, ct);
            if (content is not null)
            {
                var parsed = DeserializeActions(content);
                if (parsed?.Count > 0)
                {
                    newActions = parsed.Select(a => CreateAction(score, a)).ToList();
                }
                else
                {
                    newActions = [FallbackAction(score)];
                }
            }
            else
            {
                newActions = [FallbackAction(score)];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse suggested actions for customer {CustomerId}", customerId);
            newActions = [FallbackAction(score)];
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

    private static SuggestedAction FallbackAction(RiskScore score) => new()
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

    private sealed record ExplanationDto(string Explanation, string Confidence);

    private List<ActionDto>? DeserializeActions(string content)
    {
        ActionsResponseDto response = ActionsDeserializer.Deserialize(content);

        return response?.Actions;
    }

    public class ActionDto
    {
        [JsonPropertyName("action_type")]
        public string ActionType { get; set; } = string.Empty;

        [JsonPropertyName("priority")]
        public string Priority { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class ActionsResponseDto
    {
        [JsonPropertyName("actions")]
        public List<ActionDto> Actions { get; set; } = new();
    }

    public static class ActionsDeserializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static ActionsResponseDto Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new ActionsResponseDto();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                // Bare array: [{ ... }, ...]
                var actions = JsonSerializer.Deserialize<List<ActionDto>>(json, Options) ?? [];
                return new ActionsResponseDto { Actions = actions };
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("actions", out var actionsProp) && actionsProp.ValueKind == JsonValueKind.Array)
                {
                    // Wrapped: { "actions": [{ ... }] }
                    var result = JsonSerializer.Deserialize<ActionsResponseDto>(json, Options);
                    return result ?? new ActionsResponseDto();
                }

                // Single action object: { "action_type": "...", ... }
                var single = JsonSerializer.Deserialize<ActionDto>(json, Options);
                return single is not null
                    ? new ActionsResponseDto { Actions = [single] }
                    : new ActionsResponseDto();
            }

            return new ActionsResponseDto();
        }
    }
}
