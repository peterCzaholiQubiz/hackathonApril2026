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
    public async Task RefreshSnapshotAsync_SetsTotalCustomersFromActiveCustomersInDatabase()
    {
        var activeCustomerWithScore = CreateCustomer("cust-active-1", "Active One", isActive: true);
        var activeCustomerWithHistoricalScore = CreateCustomer("cust-active-2", "Active Two", isActive: true);
        var inactiveCustomerOne = CreateCustomer("cust-inactive-1", "Inactive One", isActive: false);
        var inactiveCustomerTwo = CreateCustomer("cust-inactive-2", "Inactive Two", isActive: false);

        var snapshot = new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var previousSnapshot = new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        _db.AddRange(
            activeCustomerWithScore,
            activeCustomerWithHistoricalScore,
            inactiveCustomerOne,
            inactiveCustomerTwo,
            snapshot,
            previousSnapshot);

        _db.RiskScores.AddRange(
            CreateRiskScore(activeCustomerWithScore.Id, snapshot.Id, 80, 60, 40, 70, "red"),
            CreateRiskScore(activeCustomerWithHistoricalScore.Id, previousSnapshot.Id, 50, 40, 30, 40, "yellow"),
            CreateRiskScore(inactiveCustomerOne.Id, snapshot.Id, 30, 20, 10, 20, "green"),
            CreateRiskScore(inactiveCustomerTwo.Id, snapshot.Id, 50, 50, 50, 50, "yellow"));

        await _db.SaveChangesAsync();

        var service = new PortfolioAggregationService(_db, NullLogger<PortfolioAggregationService>.Instance);

        var refreshed = await service.RefreshSnapshotAsync(snapshot.Id, CancellationToken.None);

        refreshed.TotalCustomers.Should().Be(2);
    }

    [Fact]
    public async Task RefreshSnapshotAsync_AveragesLatestScoresForActiveCustomersOnly()
    {
        var activeCustomer = CreateCustomer("cust-active", "Active", segment: "enterprise", isActive: true);
        var activeCustomerWithHistoricalScore = CreateCustomer("cust-active-history", "Active Historical", segment: "smb", isActive: true);
        var inactiveCustomer = CreateCustomer("cust-inactive", "Inactive", segment: "legacy", isActive: false);

        var snapshot = new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var previousSnapshot = new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        _db.AddRange(activeCustomer, activeCustomerWithHistoricalScore, inactiveCustomer, snapshot, previousSnapshot);

        _db.RiskScores.AddRange(
            CreateRiskScore(activeCustomer.Id, snapshot.Id, 80, 60, 40, 70, "red"),
            CreateRiskScore(activeCustomerWithHistoricalScore.Id, previousSnapshot.Id, 20, 40, 60, 40, "yellow"),
            CreateRiskScore(inactiveCustomer.Id, snapshot.Id, 10, 20, 30, 20, "green"));

        await _db.SaveChangesAsync();

        var service = new PortfolioAggregationService(_db, NullLogger<PortfolioAggregationService>.Instance);

        var refreshed = await service.RefreshSnapshotAsync(snapshot.Id, CancellationToken.None);

        refreshed.AvgChurnScore.Should().Be(50);
        refreshed.AvgPaymentScore.Should().Be(50);
        refreshed.AvgMarginScore.Should().Be(50);
        refreshed.RedCount.Should().Be(1);
        refreshed.GreenCount.Should().Be(0);
        refreshed.YellowCount.Should().Be(1);
        refreshed.RedPct.Should().Be(50);
        refreshed.YellowPct.Should().Be(50);
        refreshed.SegmentBreakdown.Should().Contain("enterprise");
        refreshed.SegmentBreakdown.Should().Contain("smb");
        refreshed.SegmentBreakdown.Should().NotContain("legacy");
    }

    [Fact]
    public async Task RefreshSnapshotAsync_UsesLatestScoreForEachActiveCustomer()
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

        var latestSnapshot = new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.AddRange(customer, targetSnapshot, otherSnapshot, latestSnapshot);
        _db.RiskScores.AddRange(
            new RiskScore
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                SnapshotId = targetSnapshot.Id,
                ChurnScore = 40,
                PaymentScore = 40,
                MarginScore = 40,
                OverallScore = 40,
                HeatLevel = "yellow",
                ScoredAt = DateTimeOffset.UtcNow.AddMinutes(-20)
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
            },
            new RiskScore
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                SnapshotId = latestSnapshot.Id,
                ChurnScore = 80,
                PaymentScore = 60,
                MarginScore = 40,
                OverallScore = 70,
                HeatLevel = "red",
                ScoredAt = DateTimeOffset.UtcNow
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

    private static Customer CreateCustomer(
        string crmExternalId,
        string name,
        string? segment = null,
        bool isActive = true)
    {
        return new Customer
        {
            Id = Guid.NewGuid(),
            CrmExternalId = crmExternalId,
            Name = name,
            Segment = segment,
            IsActive = isActive,
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static RiskScore CreateRiskScore(
        Guid customerId,
        Guid snapshotId,
        int churnScore,
        int paymentScore,
        int marginScore,
        int overallScore,
        string heatLevel)
    {
        return new RiskScore
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            SnapshotId = snapshotId,
            ChurnScore = churnScore,
            PaymentScore = paymentScore,
            MarginScore = marginScore,
            OverallScore = overallScore,
            HeatLevel = heatLevel,
            ScoredAt = DateTimeOffset.UtcNow
        };
    }
}
