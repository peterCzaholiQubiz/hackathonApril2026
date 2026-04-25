using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Api.Common;
using PortfolioThermometer.Api.ViewModels;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Api.Controllers;

[ApiController]
[Route("api/portfolio")]
public sealed class PortfolioController(AppDbContext db) : ControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<PortfolioSnapshotVm?>>> GetCurrent(CancellationToken ct)
    {
        var snapshot = await db.PortfolioSnapshots
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (snapshot is null)
            return Ok(ApiResponse<PortfolioSnapshotVm?>.Ok(null));

        var activeCustomerCount = await db.Customers
            .CountAsync(customer => customer.IsActive, ct);

        var latestActiveRiskScoreIds = await db.RiskScores
            .Where(r => r.Customer.IsActive)
            .GroupBy(r => r.CustomerId)
            .Select(group => group
                .OrderByDescending(score => score.ScoredAt)
                .ThenByDescending(score => score.Id)
                .Select(score => score.Id)
                .First())
            .ToListAsync(ct);

        var latestActiveScores = await db.RiskScores
            .Where(r => latestActiveRiskScoreIds.Contains(r.Id))
            .Select(r => new
            {
                r.ChurnScore,
                r.PaymentScore,
                r.MarginScore,
                r.HeatLevel,
                CustomerSegment = r.Customer.Segment
            })
            .ToListAsync(ct);

        decimal avgChurn = latestActiveScores.Count > 0
            ? Math.Round((decimal)latestActiveScores.Average(s => s.ChurnScore), 2)
            : 0m;
        decimal avgPayment = latestActiveScores.Count > 0
            ? Math.Round((decimal)latestActiveScores.Average(s => s.PaymentScore), 2)
            : 0m;
        decimal avgMargin = latestActiveScores.Count > 0
            ? Math.Round((decimal)latestActiveScores.Average(s => s.MarginScore), 2)
            : 0m;

        int scoredTotal = latestActiveScores.Count;
        int greenCount  = latestActiveScores.Count(s => s.HeatLevel == "green");
        int yellowCount = latestActiveScores.Count(s => s.HeatLevel == "yellow");
        int redCount    = latestActiveScores.Count(s => s.HeatLevel == "red");
        decimal greenPct  = scoredTotal > 0 ? Math.Round((decimal)greenCount  / scoredTotal * 100, 2) : 0m;
        decimal yellowPct = scoredTotal > 0 ? Math.Round((decimal)yellowCount / scoredTotal * 100, 2) : 0m;
        decimal redPct    = scoredTotal > 0 ? Math.Round((decimal)redCount    / scoredTotal * 100, 2) : 0m;

        var breakdown = latestActiveScores
            .Where(s => s.CustomerSegment != null)
            .GroupBy(s => s.CustomerSegment!)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    green  = g.Count(s => s.HeatLevel == "green"),
                    yellow = g.Count(s => s.HeatLevel == "yellow"),
                    red    = g.Count(s => s.HeatLevel == "red")
                });

        var segmentBreakdownJson = breakdown.Count > 0
            ? System.Text.Json.JsonSerializer.Serialize(breakdown)
            : null;

        return Ok(ApiResponse<PortfolioSnapshotVm>.Ok(new PortfolioSnapshotVm(
            snapshot.Id, snapshot.CreatedAt, activeCustomerCount,
            greenCount, yellowCount, redCount,
            greenPct, yellowPct, redPct,
            avgChurn, avgPayment, avgMargin,
            segmentBreakdownJson)));
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PortfolioSnapshotVm>>>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var total = await db.PortfolioSnapshots.CountAsync(ct);
        var snapshots = await db.PortfolioSnapshots
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new PortfolioSnapshotVm(
                s.Id, s.CreatedAt, s.TotalCustomers,
                s.GreenCount, s.YellowCount, s.RedCount,
                s.GreenPct, s.YellowPct, s.RedPct,
                s.AvgChurnScore, s.AvgPaymentScore, s.AvgMarginScore,
                s.SegmentBreakdown))
            .ToListAsync(ct);

        var meta = new ApiMeta { Total = total, Page = page, PageSize = pageSize };
        return Ok(ApiResponse<IReadOnlyList<PortfolioSnapshotVm>>.Ok(snapshots, meta));
    }

    [HttpGet("segments")]
    public async Task<ActionResult<ApiResponse<object>>> GetSegments(CancellationToken ct)
    {
        var snapshot = await db.PortfolioSnapshots
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (snapshot is null)
            return Ok(ApiResponse<object?>.Ok(null));

        return Ok(ApiResponse<object>.Ok(new { segmentBreakdown = snapshot.SegmentBreakdown }));
    }
}
