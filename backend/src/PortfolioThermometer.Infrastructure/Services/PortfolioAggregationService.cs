using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Infrastructure.Services;

/// <summary>
/// Computes portfolio-level heat distributions and segment breakdowns
/// for an existing PortfolioSnapshot.
/// </summary>
public sealed class PortfolioAggregationService : IPortfolioAggregationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PortfolioAggregationService> _logger;

    public PortfolioAggregationService(AppDbContext db, ILogger<PortfolioAggregationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PortfolioSnapshot> RefreshSnapshotAsync(Guid snapshotId, CancellationToken ct)
    {
        var snapshot = await _db.PortfolioSnapshots
            .FirstOrDefaultAsync(s => s.Id == snapshotId, ct)
            ?? throw new InvalidOperationException($"Snapshot {snapshotId} was not found.");

        var activeCustomerCount = await _db.Customers
            .CountAsync(customer => customer.IsActive, ct);

        var latestActiveRiskScoreIds = await _db.RiskScores
            .Where(r => r.Customer.IsActive)
            .GroupBy(r => r.CustomerId)
            .Select(group => group
                .OrderByDescending(score => score.ScoredAt)
                .ThenByDescending(score => score.Id)
                .Select(score => score.Id)
                .First())
            .ToListAsync(ct);

        var snapshotScores = await _db.RiskScores
            .Include(r => r.Customer)
            .Where(r => latestActiveRiskScoreIds.Contains(r.Id))
            .ToListAsync(ct);

        var total = snapshotScores.Count;
        var greenCount = snapshotScores.Count(s => s.HeatLevel == "green");
        var yellowCount = snapshotScores.Count(s => s.HeatLevel == "yellow");
        var redCount = snapshotScores.Count(s => s.HeatLevel == "red");

        decimal greenPct = total > 0 ? Math.Round((decimal)greenCount / total * 100, 2) : 0;
        decimal yellowPct = total > 0 ? Math.Round((decimal)yellowCount / total * 100, 2) : 0;
        decimal redPct = total > 0 ? Math.Round((decimal)redCount / total * 100, 2) : 0;

        decimal avgChurn = total > 0 ? Math.Round((decimal)snapshotScores.Average(s => s.ChurnScore), 2) : 0;
        decimal avgPayment = total > 0 ? Math.Round((decimal)snapshotScores.Average(s => s.PaymentScore), 2) : 0;
        decimal avgMargin = total > 0 ? Math.Round((decimal)snapshotScores.Average(s => s.MarginScore), 2) : 0;

        var segmentBreakdown = BuildSegmentBreakdown(snapshotScores);

        if (activeCustomerCount != total)
        {
            _logger.LogWarning(
                "Dashboard snapshot {SnapshotId} has {ScoredCustomerCount} active customers with risk scores out of {ActiveCustomerCount} active customers.",
                snapshotId,
                total,
                activeCustomerCount);
        }

        snapshot.TotalCustomers = activeCustomerCount;
        snapshot.GreenCount = greenCount;
        snapshot.YellowCount = yellowCount;
        snapshot.RedCount = redCount;
        snapshot.GreenPct = greenPct;
        snapshot.YellowPct = yellowPct;
        snapshot.RedPct = redPct;
        snapshot.AvgChurnScore = avgChurn;
        snapshot.AvgPaymentScore = avgPayment;
        snapshot.AvgMarginScore = avgMargin;
        snapshot.SegmentBreakdown = segmentBreakdown;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Portfolio snapshot refreshed: {SnapshotId}, {Total} customers, {Green} green / {Yellow} yellow / {Red} red",
            snapshotId, total, greenCount, yellowCount, redCount);

        return snapshot;
    }

    private static string BuildSegmentBreakdown(List<RiskScore> scores)
    {
        var breakdown = scores
            .Where(s => s.Customer?.Segment != null)
            .GroupBy(s => s.Customer!.Segment!)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    green = g.Count(s => s.HeatLevel == "green"),
                    yellow = g.Count(s => s.HeatLevel == "yellow"),
                    red = g.Count(s => s.HeatLevel == "red"),
                    total = g.Count()
                });

        return JsonSerializer.Serialize(breakdown);
    }
}
