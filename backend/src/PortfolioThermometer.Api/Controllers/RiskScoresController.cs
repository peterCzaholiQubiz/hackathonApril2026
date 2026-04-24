using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Api.Common;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Api.Controllers;

[ApiController]
[Route("api/risk")]
public sealed class RiskScoresController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RiskScoresController> _logger;
    private static readonly object StatusLock = new();
    private static RiskRunStatus _lastStatus = new(false, null, null, null, null);

    public RiskScoresController(
        AppDbContext db,
        IServiceScopeFactory scopeFactory,
        ILogger<RiskScoresController> logger)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [HttpPost("trigger")]
    public ActionResult<ApiResponse<object>> TriggerRiskScoring()
    {
        if (!RiskRunGuard.TryStart())
            return Conflict(ApiResponse<object>.Fail("Risk scoring is already in progress."));

        lock (StatusLock)
        {
            _lastStatus = new RiskRunStatus(true, DateTimeOffset.UtcNow, null, null, null);
        }

        _ = Task.Run(async () =>
        {
            AppDbContext? db = null;
            Guid? snapshotId = null;
            string? error = null;

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var scoringEngine = scope.ServiceProvider.GetRequiredService<IRiskScoringEngine>();
                var aggregationService = scope.ServiceProvider.GetRequiredService<IPortfolioAggregationService>();
                var explanationService = scope.ServiceProvider.GetRequiredService<IClaudeExplanationService>();

                _logger.LogInformation("Starting risk scoring pipeline");

                var snapshot = new PortfolioSnapshot
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTimeOffset.MinValue
                };

                db.PortfolioSnapshots.Add(snapshot);
                await db.SaveChangesAsync(CancellationToken.None);
                snapshotId = snapshot.Id;

                var scores = await scoringEngine.ScoreAllCustomersAsync(snapshot.Id, CancellationToken.None);
                _logger.LogInformation("Scored {Count} customers for snapshot {SnapshotId}", scores.Count, snapshot.Id);

                await aggregationService.RefreshSnapshotAsync(snapshot.Id, CancellationToken.None);
                _logger.LogInformation("Snapshot refreshed: {SnapshotId}", snapshot.Id);

                await explanationService.GenerateExplanationsAsync(scores, CancellationToken.None);
                _logger.LogInformation("Explanations generated for snapshot {SnapshotId}", snapshot.Id);
                snapshot.CreatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Risk scoring pipeline failed");

                error = ex.Message;

                if (db is not null && snapshotId.HasValue)
                {
                    try
                    {
                        await using var cleanupScope = _scopeFactory.CreateAsyncScope();
                        var cleanupDb = cleanupScope.ServiceProvider.GetRequiredService<AppDbContext>();
                        await RiskPipelineCleanup.CleanupSnapshotAsync(cleanupDb, snapshotId.Value, CancellationToken.None);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(cleanupEx, "Failed to clean up risk snapshot {SnapshotId}", snapshotId);
                    }
                }
            }
            finally
            {
                lock (StatusLock)
                {
                    _lastStatus = new RiskRunStatus(
                        false,
                        _lastStatus.StartedAt,
                        DateTimeOffset.UtcNow,
                        error is null ? snapshotId : null,
                        error);
                }

                RiskRunGuard.Complete();
            }
        }, CancellationToken.None);

        return Accepted(ApiResponse<object>.Ok(new { message = "Risk scoring started." }));
    }

    [HttpGet("status")]
    public ActionResult<ApiResponse<RiskRunStatus>> GetStatus()
    {
        return Ok(ApiResponse<RiskRunStatus>.Ok(_lastStatus));
    }

    [HttpGet("distribution")]
    public async Task<ActionResult<ApiResponse<object>>> GetDistribution(CancellationToken ct)
    {
        var snapshot = await _db.PortfolioSnapshots
            .Where(s => s.CreatedAt > DateTimeOffset.MinValue)
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
            .Where(s => s.CreatedAt > DateTimeOffset.MinValue)
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
            .Where(s => s.CreatedAt > DateTimeOffset.MinValue)
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

    public sealed record RiskRunStatus(
        bool IsRunning,
        DateTimeOffset? StartedAt,
        DateTimeOffset? CompletedAt,
        Guid? SnapshotId,
        string? LastError);
}
