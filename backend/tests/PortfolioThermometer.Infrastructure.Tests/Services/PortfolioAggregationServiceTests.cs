using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;
using PortfolioThermometer.Infrastructure.Services;
using Xunit;

namespace PortfolioThermometer.Infrastructure.Tests.Services;

public sealed class PortfolioAggregationServiceTests : IDisposable
{
    private readonly AppDbContext _db;

    public PortfolioAggregationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task RefreshSnapshotAsync_UsesOnlyScoresFromRequestedSnapshot()
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            CrmExternalId = "cust-1",
            Name = "Acme",
            Segment = "enterprise",
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var targetSnapshot = new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var otherSnapshot = new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        _db.AddRange(customer, targetSnapshot, otherSnapshot);
        _db.RiskScores.AddRange(
            new RiskScore
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                SnapshotId = targetSnapshot.Id,
                ChurnScore = 80,
                PaymentScore = 60,
                MarginScore = 40,
                OverallScore = 70,
                HeatLevel = "red",
                ScoredAt = DateTimeOffset.UtcNow
            },
            new RiskScore
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                SnapshotId = otherSnapshot.Id,
                ChurnScore = 10,
                PaymentScore = 10,
                MarginScore = 10,
                OverallScore = 10,
                HeatLevel = "green",
                ScoredAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            });

        await _db.SaveChangesAsync();

        var service = new PortfolioAggregationService(_db, NullLogger<PortfolioAggregationService>.Instance);

        var refreshed = await service.RefreshSnapshotAsync(targetSnapshot.Id, CancellationToken.None);

        refreshed.TotalCustomers.Should().Be(1);
        refreshed.RedCount.Should().Be(1);
        refreshed.GreenCount.Should().Be(0);
        refreshed.YellowCount.Should().Be(0);
        refreshed.RedPct.Should().Be(100);
        refreshed.AvgChurnScore.Should().Be(80);
        refreshed.AvgPaymentScore.Should().Be(60);
        refreshed.AvgMarginScore.Should().Be(40);
        refreshed.SegmentBreakdown.Should().Contain("enterprise");
    }
}
