using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Api.Common;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Api.Controllers;

[ApiController]
[Route("api/risk")]
public sealed class RiskScoresController : ControllerBase
{
    private readonly AppDbContext _db;

    public RiskScoresController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("distribution")]
    public async Task<ActionResult<ApiResponse<object>>> GetDistribution(CancellationToken ct)
    {
        var snapshot = await _db.PortfolioSnapshots
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (snapshot is null)
            return Ok(ApiResponse<object?>.Ok(null));

        var distribution = new
        {
            snapshot.TotalCustomers,
            snapshot.GreenCount,
            snapshot.YellowCount,
            snapshot.RedCount,
            snapshot.GreenPct,
            snapshot.YellowPct,
            snapshot.RedPct,
            snapshot.AvgChurnScore,
            snapshot.AvgPaymentScore,
            snapshot.AvgMarginScore
        };

        return Ok(ApiResponse<object>.Ok(distribution));
    }

    [HttpGet("top-at-risk")]
    public async Task<ActionResult<ApiResponse<object>>> GetTopAtRisk(
        [FromQuery] string type = "overall",
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        if (limit is < 1 or > 100)
            limit = 10;

        var snapshot = await _db.PortfolioSnapshots
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (snapshot is null)
            return Ok(ApiResponse<object?>.Ok(null));

        var query = _db.RiskScores
            .Include(r => r.Customer)
            .Where(r => r.SnapshotId == snapshot.Id);

        var ordered = type.ToLowerInvariant() switch
        {
            "churn" => query.OrderByDescending(r => r.ChurnScore),
            "payment" => query.OrderByDescending(r => r.PaymentScore),
            "margin" => query.OrderByDescending(r => r.MarginScore),
            _ => query.OrderByDescending(r => r.OverallScore)
        };

        var results = await ordered
            .Take(limit)
            .Select(r => new
            {
                r.CustomerId,
                r.Customer.Name,
                r.Customer.CompanyName,
                r.Customer.Segment,
                r.ChurnScore,
                r.PaymentScore,
                r.MarginScore,
                r.OverallScore,
                r.HeatLevel
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.Ok(results));
    }

    [HttpGet("groups")]
    public async Task<ActionResult<ApiResponse<object>>> GetGroups(CancellationToken ct)
    {
        var snapshot = await _db.PortfolioSnapshots
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (snapshot is null)
            return Ok(ApiResponse<object?>.Ok(null));

        var groups = await _db.RiskScores
            .Include(r => r.Customer)
            .Where(r => r.SnapshotId == snapshot.Id)
            .GroupBy(r => r.HeatLevel)
            .Select(g => new
            {
                HeatLevel = g.Key,
                Count = g.Count(),
                Customers = g.Select(r => new
                {
                    r.CustomerId,
                    r.Customer.Name,
                    r.Customer.CompanyName,
                    r.Customer.Segment,
                    r.OverallScore
                }).ToList()
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.Ok(groups));
    }
}
