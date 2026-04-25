using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Api.Common;
using PortfolioThermometer.Api.ViewModels;
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

    [HttpGet("dimension-groups")]
    public async Task<ActionResult<ApiResponse<RiskDimensionGroupsResponseVm?>>> GetDimensionGroups(
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        if (limit is < 1 or > 100) limit = 10;

        var snapshot = await _db.PortfolioSnapshots
            .Where(s => s.CreatedAt > DateTimeOffset.MinValue)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (snapshot is null)
            return Ok(ApiResponse<RiskDimensionGroupsResponseVm?>.Ok(null));

        // Load all risk scores for heat summary and per-dimension ordering
        var allScores = await _db.RiskScores
            .Where(r => r.SnapshotId == snapshot.Id)
            .Select(r => new { r.Id, r.CustomerId, r.ChurnScore, r.PaymentScore, r.MarginScore, r.OverallScore, r.HeatLevel })
            .ToListAsync(ct);

        if (allScores.Count == 0)
            return Ok(ApiResponse<RiskDimensionGroupsResponseVm?>.Ok(null));

        var snapshotScoreIds = allScores.Select(s => s.Id).ToList();
        var allCustomerIds = allScores.Select(s => s.CustomerId).Distinct().ToList();

        // Monthly contract value per customer (active contracts)
        var contractValueRows = await _db.Contracts
            .Where(c => allCustomerIds.Contains(c.CustomerId)
                     && c.Status == "active"
                     && c.MonthlyValue != null)
            .GroupBy(c => c.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(c => (decimal)c.MonthlyValue!) })
            .ToListAsync(ct);

        var contractValues = contractValueRows.ToDictionary(x => x.CustomerId, x => x.Total);
        decimal ContractValue(Guid id) => contractValues.TryGetValue(id, out var v) ? v : 0m;

        // Portfolio-wide heat summary
        var totalCount = allScores.Count;
        var greenCustomerIds  = allScores.Where(s => s.HeatLevel == "green").Select(s => s.CustomerId).ToList();
        var yellowCustomerIds = allScores.Where(s => s.HeatLevel == "yellow").Select(s => s.CustomerId).ToList();
        var redCustomerIds    = allScores.Where(s => s.HeatLevel == "red").Select(s => s.CustomerId).ToList();

        HeatBandVm MakeBand(List<Guid> ids) => new(
            ids.Count,
            totalCount > 0 ? Math.Round((decimal)ids.Count / totalCount * 100, 1) : 0m,
            ids.Sum(ContractValue));

        var heatSummary = new HeatSummaryVm(totalCount, MakeBand(greenCustomerIds), MakeBand(yellowCustomerIds), MakeBand(redCustomerIds));

        // Top N per dimension
        var topChurnIds   = allScores.OrderByDescending(s => s.ChurnScore).Take(limit).Select(s => s.CustomerId).ToList();
        var topPaymentIds = allScores.OrderByDescending(s => s.PaymentScore).Take(limit).Select(s => s.CustomerId).ToList();
        var topMarginIds  = allScores.OrderByDescending(s => s.MarginScore).Take(limit).Select(s => s.CustomerId).ToList();
        var relevantIds   = topChurnIds.Union(topPaymentIds).Union(topMarginIds).ToList();

        // Customer name/segment info
        var customers = await _db.Customers
            .Where(c => relevantIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.CompanyName, c.Segment })
            .ToDictionaryAsync(c => c.Id, ct);

        // Explanations scoped to current snapshot
        var explanationRows = await _db.RiskExplanations
            .Where(e => relevantIds.Contains(e.CustomerId) && snapshotScoreIds.Contains(e.RiskScoreId))
            .Select(e => new { e.CustomerId, e.RiskType, e.Explanation, e.Confidence, e.GeneratedAt })
            .ToListAsync(ct);

        var explanationLookup = explanationRows
            .GroupBy(e => (e.CustomerId, e.RiskType))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.GeneratedAt).First());

        // Top suggested action per customer (high > medium > low priority)
        var actionRows = await _db.SuggestedActions
            .Where(a => relevantIds.Contains(a.CustomerId) && snapshotScoreIds.Contains(a.RiskScoreId))
            .Select(a => new { a.CustomerId, a.ActionType, a.Priority, a.Title, a.Description, a.GeneratedAt })
            .ToListAsync(ct);

        static int PriorityWeight(string p) => p switch { "high" => 0, "medium" => 1, _ => 2 };

        var actionLookup = actionRows
            .GroupBy(a => a.CustomerId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(a => PriorityWeight(a.Priority))
                .ThenByDescending(a => a.GeneratedAt)
                .First());

        var scoreMap = allScores.ToDictionary(s => s.CustomerId);

        RiskDimensionItemVm BuildItem(Guid id, string dim)
        {
            var score = scoreMap[id];
            customers.TryGetValue(id, out var customer);
            explanationLookup.TryGetValue((id, dim), out var expl);
            actionLookup.TryGetValue(id, out var action);

            return new RiskDimensionItemVm(
                id,
                customer?.Name ?? string.Empty,
                customer?.CompanyName,
                customer?.Segment,
                score.ChurnScore,
                score.PaymentScore,
                score.MarginScore,
                score.OverallScore,
                score.HeatLevel,
                ContractValue(id),
                expl?.Explanation,
                expl?.Confidence,
                action is null ? null : new RiskItemActionVm(
                    action.ActionType, action.Priority, action.Title, action.Description));
        }

        RiskDimensionGroupVm BuildGroup(string dim, string label, List<Guid> ids)
        {
            var items = ids.Select(id => BuildItem(id, dim)).ToList();

            // Portfolio-wide average for this dimension (all customers, not just top N)
            decimal avgScore = allScores.Count == 0 ? 0m : Math.Round(
                (decimal)allScores.Sum(s => dim switch
                {
                    "churn"   => s.ChurnScore,
                    "payment" => s.PaymentScore,
                    "margin"  => s.MarginScore,
                    _         => s.OverallScore,
                }) / allScores.Count, 1);

            // All customers flagged (score >= 40) for this dimension across the full portfolio
            var flaggedCustomerIds = dim switch
            {
                "churn"   => allScores.Where(s => s.ChurnScore >= 40).Select(s => s.CustomerId).ToList(),
                "payment" => allScores.Where(s => s.PaymentScore >= 40).Select(s => s.CustomerId).ToList(),
                "margin"  => allScores.Where(s => s.MarginScore >= 40).Select(s => s.CustomerId).ToList(),
                _         => allScores.Where(s => s.OverallScore >= 40).Select(s => s.CustomerId).ToList(),
            };

            int totalFlagged = flaggedCustomerIds.Count;
            decimal totalMonthlyValue = flaggedCustomerIds.Sum(ContractValue);

            return new RiskDimensionGroupVm(dim, label, avgScore, totalFlagged, totalMonthlyValue, items);
        }

        var dimensions = new[]
        {
            BuildGroup("churn",   "Churn Risk",   topChurnIds),
            BuildGroup("payment", "Payment Risk", topPaymentIds),
            BuildGroup("margin",  "Margin Risk",  topMarginIds),
        };

        return Ok(ApiResponse<RiskDimensionGroupsResponseVm>.Ok(
            new RiskDimensionGroupsResponseVm(heatSummary, dimensions)));
    }

    public sealed record RiskRunStatus(
        bool IsRunning,
        DateTimeOffset? StartedAt,
        DateTimeOffset? CompletedAt,
        Guid? SnapshotId,
        string? LastError);
}
