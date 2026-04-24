using Microsoft.AspNetCore.Mvc;
using PortfolioThermometer.Api.Common;
using PortfolioThermometer.Core.Interfaces;

namespace PortfolioThermometer.Api.Controllers;

[ApiController]
[Route("api/import")]
public sealed class ImportController : ControllerBase
{
    private readonly ICrmImportService _importService;
    private readonly IRiskScoringEngine _scoringEngine;
    private readonly IClaudeExplanationService _explanationService;
    private readonly IPortfolioAggregationService _aggregationService;
    private readonly ILogger<ImportController> _logger;

    // Tracks the status of the last import run (in-memory for simplicity)
    private static ImportStatus _lastStatus = new(false, null, null, null);

    public ImportController(
        ICrmImportService importService,
        IRiskScoringEngine scoringEngine,
        IClaudeExplanationService explanationService,
        IPortfolioAggregationService aggregationService,
        ILogger<ImportController> logger)
    {
        _importService = importService;
        _scoringEngine = scoringEngine;
        _explanationService = explanationService;
        _aggregationService = aggregationService;
        _logger = logger;
    }

    [HttpPost("trigger")]
    public async Task<ActionResult<ApiResponse<object>>> TriggerImport(CancellationToken ct)
    {
        if (_lastStatus.IsRunning)
            return Conflict(ApiResponse<object>.Fail("An import is already in progress."));

        _lastStatus = new ImportStatus(true, DateTimeOffset.UtcNow, null, null);

        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Starting full import pipeline");

                var importResult = await _importService.ImportAllAsync(CancellationToken.None);
                _logger.LogInformation("Import complete: {Customers} customers", importResult.CustomersImported);

                var snapshot = await _aggregationService.CreateSnapshotAsync(CancellationToken.None);
                _logger.LogInformation("Snapshot created: {Id}", snapshot.Id);

                var scores = await _scoringEngine.ScoreAllCustomersAsync(snapshot.Id, CancellationToken.None);
                _logger.LogInformation("Scored {Count} customers", scores.Count);

                await _explanationService.GenerateExplanationsAsync(scores, CancellationToken.None);
                _logger.LogInformation("Explanations generated");

                _lastStatus = new ImportStatus(false, _lastStatus.StartedAt, DateTimeOffset.UtcNow, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Import pipeline failed");
                _lastStatus = new ImportStatus(false, _lastStatus.StartedAt, DateTimeOffset.UtcNow, ex.Message);
            }
        }, CancellationToken.None);

        return Accepted(ApiResponse<object>.Ok(new { message = "Import pipeline started." }));
    }

    [HttpGet("status")]
    public ActionResult<ApiResponse<ImportStatus>> GetStatus()
    {
        return Ok(ApiResponse<ImportStatus>.Ok(_lastStatus));
    }

    public sealed record ImportStatus(
        bool IsRunning,
        DateTimeOffset? StartedAt,
        DateTimeOffset? CompletedAt,
        string? LastError);
}
