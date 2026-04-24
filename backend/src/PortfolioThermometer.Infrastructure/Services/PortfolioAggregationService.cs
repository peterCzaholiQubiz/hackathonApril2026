using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Infrastructure.Services;

/// <summary>
/// Computes portfolio-level heat distributions and segment breakdowns,
/// persisting the result as a PortfolioSnapshot.
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

    public async Task<PortfolioSnapshot> CreateSnapshotAsync(CancellationToken ct)
    {
        // Fetch the most recent risk score per customer
        var latestScores = await _db.RiskScores
            .Include(r => r.Customer)
            .GroupBy(r => r.CustomerId)
            .Select(g => g.OrderByDescending(r => r.ScoredAt).First())
            .ToListAsync(ct);

        var total = latestScores.Count;
        var greenCount = latestScores.Count(s => s.HeatLevel == "green");
        var yellowCount = latestScores.Count(s => s.HeatLevel == "yellow");
        var redCount = latestScores.Count(s => s.HeatLevel == "red");

        decimal greenPct = total > 0 ? Math.Round((decimal)greenCount / total * 100, 2) : 0;
        decimal yellowPct = total > 0 ? Math.Round((decimal)yellowCount / total * 100, 2) : 0;
        decimal redPct = total > 0 ? Math.Round((decimal)redCount / total * 100, 2) : 0;

        decimal avgChurn = total > 0 ? Math.Round((decimal)latestScores.Average(s => s.ChurnScore), 2) : 0;
        decimal avgPayment = total > 0 ? Math.Round((decimal)latestScores.Average(s => s.PaymentScore), 2) : 0;
        decimal avgMargin = total > 0 ? Math.Round((decimal)latestScores.Average(s => s.MarginScore), 2) : 0;

        var segmentBreakdown = BuildSegmentBreakdown(latestScores);

        var snapshot = new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            TotalCustomers = total,
            GreenCount = greenCount,
            YellowCount = yellowCount,
            RedCount = redCount,
            GreenPct = greenPct,
            YellowPct = yellowPct,
            RedPct = redPct,
            AvgChurnScore = avgChurn,
            AvgPaymentScore = avgPayment,
            AvgMarginScore = avgMargin,
            SegmentBreakdown = segmentBreakdown
        };

        _db.PortfolioSnapshots.Add(snapshot);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Portfolio snapshot created: {Total} customers, {Green} green / {Yellow} yellow / {Red} red",
            total, greenCount, yellowCount, redCount);

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
