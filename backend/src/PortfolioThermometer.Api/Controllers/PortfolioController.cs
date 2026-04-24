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

        return Ok(ApiResponse<PortfolioSnapshotVm>.Ok(new PortfolioSnapshotVm(
            snapshot.Id, snapshot.CreatedAt, snapshot.TotalCustomers,
            snapshot.GreenCount, snapshot.YellowCount, snapshot.RedCount,
            snapshot.GreenPct, snapshot.YellowPct, snapshot.RedPct,
            snapshot.AvgChurnScore, snapshot.AvgPaymentScore, snapshot.AvgMarginScore,
            snapshot.SegmentBreakdown)));
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
