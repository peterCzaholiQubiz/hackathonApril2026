using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Api.Common;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Api.Controllers;

[ApiController]
[Route("api/portfolio")]
public sealed class PortfolioController : ControllerBase
{
    private readonly AppDbContext _db;

    public PortfolioController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<PortfolioSnapshot>>> GetCurrent(CancellationToken ct)
    {
        var snapshot = await _db.PortfolioSnapshots
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (snapshot is null)
            return Ok(ApiResponse<PortfolioSnapshot?>.Ok(null));

        return Ok(ApiResponse<PortfolioSnapshot>.Ok(snapshot));
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PortfolioSnapshot>>>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var total = await _db.PortfolioSnapshots.CountAsync(ct);
        var snapshots = await _db.PortfolioSnapshots
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var meta = new ApiMeta { Total = total, Page = page, PageSize = pageSize };
        return Ok(ApiResponse<IReadOnlyList<PortfolioSnapshot>>.Ok(snapshots, meta));
    }

    [HttpGet("segments")]
    public async Task<ActionResult<ApiResponse<object>>> GetSegments(CancellationToken ct)
    {
        var snapshot = await _db.PortfolioSnapshots
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (snapshot is null)
            return Ok(ApiResponse<object?>.Ok(null));

        return Ok(ApiResponse<object>.Ok(new { segmentBreakdown = snapshot.SegmentBreakdown }));
    }
}
