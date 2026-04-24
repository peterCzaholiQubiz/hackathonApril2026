using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.AzureOpenAi;
using PortfolioThermometer.Infrastructure.Data;
using PortfolioThermometer.Infrastructure.Services;
using Xunit;

namespace PortfolioThermometer.Infrastructure.Tests.Services;

public sealed class AzureOpenAiExplanationServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IAzureOpenAiClient> _mockClient;
    private readonly AzureOpenAiOptions _options;

    public AzureOpenAiExplanationServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(dbOptions);
        _mockClient = new Mock<IAzureOpenAiClient>();
        _options = new AzureOpenAiOptions
        {
            Endpoint = "https://test.openai.azure.com/",
            ApiKey = "test-key",
            Deployment = "gpt-4o",
            ApiVersion = "2024-02-01",
            MaxTokens = 512,
            MaxConcurrency = 2,
            BatchSize = 5
        };
    }

    public void Dispose() => _db.Dispose();

    private AzureOpenAiExplanationService CreateService() =>
        new(_db, _mockClient.Object, Options.Create(_options),
            NullLogger<AzureOpenAiExplanationService>.Instance);

    private async Task<(Customer customer, RiskScore score)> SeedCustomerAndScoreAsync()
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            CrmExternalId = "c001",
            Name = "Test Corp",
            Segment = "enterprise",
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var snapshot = new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var score = new RiskScore
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            SnapshotId = snapshot.Id,
            ChurnScore = 70,
            PaymentScore = 50,
            MarginScore = 30,
            OverallScore = 60,
            HeatLevel = "yellow",
            ScoredAt = DateTimeOffset.UtcNow
        };

        _db.Customers.Add(customer);
        _db.PortfolioSnapshots.Add(snapshot);
        _db.RiskScores.Add(score);
        await _db.SaveChangesAsync();

        return (customer, score);
    }

    [Fact]
    public async Task GenerateExplanationsAsync_CreatesExplanationsAndActions_WhenApiSucceeds()
    {
        // Arrange
        var (_, score) = await SeedCustomerAndScoreAsync();

        var explanationJson = JsonSerializer.Serialize(new
        {
            explanation = "This customer shows signs of elevated risk.",
            confidence = "high"
        });

        var actionsJson = JsonSerializer.Serialize(new[]
        {
            new { action_type = "outreach", priority = "high", title = "Contact customer", description = "Schedule a call." }
        });

        _mockClient.SetupSequence(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(explanationJson)  // churn
            .ReturnsAsync(explanationJson)  // payment
            .ReturnsAsync(explanationJson)  // margin
            .ReturnsAsync(explanationJson)  // overall
            .ReturnsAsync(actionsJson);     // actions

        var service = CreateService();

        // Act
        await service.GenerateExplanationsAsync([score], CancellationToken.None);

        // Assert
        var explanations = await _db.RiskExplanations.ToListAsync();
        explanations.Should().HaveCount(4);
        explanations.Should().AllSatisfy(e => e.Confidence.Should().Be("high"));
        explanations.Should().AllSatisfy(e => e.ModelUsed.Should().Be("gpt-4o"));

        var actions = await _db.SuggestedActions.ToListAsync();
        actions.Should().HaveCount(1);
        actions[0].ActionType.Should().Be("outreach");
    }

    [Fact]
    public async Task GenerateExplanationsAsync_UsesFallback_WhenApiReturnsNull()
    {
        // Arrange
        var (_, score) = await SeedCustomerAndScoreAsync();

        _mockClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var service = CreateService();

        // Act
        await service.GenerateExplanationsAsync([score], CancellationToken.None);

        // Assert
        var explanations = await _db.RiskExplanations.ToListAsync();
        explanations.Should().HaveCount(4);
        explanations.Should().AllSatisfy(e => e.Confidence.Should().Be("low"));
        explanations.Should().AllSatisfy(e => e.ModelUsed.Should().Be("fallback"));

        var actions = await _db.SuggestedActions.ToListAsync();
        actions.Should().HaveCount(1);
        actions[0].ActionType.Should().Be("review");
    }

    [Fact]
    public async Task GenerateExplanationsAsync_UsesFallback_WhenJsonParseFailsForExplanation()
    {
        // Arrange
        var (_, score) = await SeedCustomerAndScoreAsync();

        _mockClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not valid json {{{{");

        var service = CreateService();

        // Act
        await service.GenerateExplanationsAsync([score], CancellationToken.None);

        // Assert
        var explanations = await _db.RiskExplanations.ToListAsync();
        explanations.Should().HaveCount(4);
        explanations.Should().AllSatisfy(e => e.Confidence.Should().Be("low"));
    }

    [Fact]
    public async Task GenerateExplanationsAsync_SkipsScore_WhenExplanationsAlreadyExist()
    {
        // Arrange
        var (_, score) = await SeedCustomerAndScoreAsync();

        _db.RiskExplanations.Add(new RiskExplanation
        {
            Id = Guid.NewGuid(),
            RiskScoreId = score.Id,
            CustomerId = score.CustomerId,
            RiskType = "churn",
            Explanation = "Existing explanation.",
            Confidence = "high",
            GeneratedAt = DateTimeOffset.UtcNow,
            ModelUsed = "gpt-4o"
        });
        await _db.SaveChangesAsync();

        var service = CreateService();

        // Act
        await service.GenerateExplanationsAsync([score], CancellationToken.None);

        // Assert
        _mockClient.Verify(
            c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateExplanationsAsync_ProcessesMultipleScoresConcurrently()
    {
        // Arrange
        var snapshot = new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.PortfolioSnapshots.Add(snapshot);

        var scores = Enumerable.Range(0, 6).Select(i =>
        {
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                CrmExternalId = $"c{i:000}",
                Name = $"Customer {i}",
                ImportedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            var score = new RiskScore
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                SnapshotId = snapshot.Id,
                ChurnScore = 50,
                PaymentScore = 50,
                MarginScore = 50,
                OverallScore = 50,
                HeatLevel = "yellow",
                ScoredAt = DateTimeOffset.UtcNow
            };
            _db.Customers.Add(customer);
            _db.RiskScores.Add(score);
            return score;
        }).ToList();

        await _db.SaveChangesAsync();

        _mockClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var service = CreateService();

        // Act
        await service.GenerateExplanationsAsync(scores, CancellationToken.None);

        // Assert: 6 customers × 4 explanations each = 24
        var explanations = await _db.RiskExplanations.ToListAsync();
        explanations.Should().HaveCount(24);
    }

    [Fact]
    public async Task GenerateExplanationsAsync_FallbackAction_HasHighPriority_WhenHeatLevelIsRed()
    {
        // Arrange
        var snapshot = new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            CrmExternalId = "red001",
            Name = "Red Customer",
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var score = new RiskScore
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            SnapshotId = snapshot.Id,
            ChurnScore = 90,
            PaymentScore = 85,
            MarginScore = 80,
            OverallScore = 88,
            HeatLevel = "red",
            ScoredAt = DateTimeOffset.UtcNow
        };

        _db.PortfolioSnapshots.Add(snapshot);
        _db.Customers.Add(customer);
        _db.RiskScores.Add(score);
        await _db.SaveChangesAsync();

        _mockClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var service = CreateService();

        // Act
        await service.GenerateExplanationsAsync([score], CancellationToken.None);

        // Assert
        var actions = await _db.SuggestedActions.ToListAsync();
        actions.Should().HaveCount(1);
        actions[0].Priority.Should().Be("high");
    }
}
