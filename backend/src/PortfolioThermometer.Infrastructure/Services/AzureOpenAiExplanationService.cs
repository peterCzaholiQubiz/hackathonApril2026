using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.AzureOpenAi;
using PortfolioThermometer.Infrastructure.AzureOpenAi.Prompts;
using PortfolioThermometer.Infrastructure.Data;

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
                var actions = JsonSerializer.Deserialize<List<ActionDto>>(content, JsonOptions);
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
    private sealed record ActionDto(string ActionType, string Priority, string Title, string? Description);
}
