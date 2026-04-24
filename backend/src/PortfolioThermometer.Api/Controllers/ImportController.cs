using Microsoft.AspNetCore.Mvc;
using PortfolioThermometer.Api.Common;
using PortfolioThermometer.Api.Configuration;
using PortfolioThermometer.Core.Interfaces;

namespace PortfolioThermometer.Api.Controllers;

[ApiController]
[Route("api/import")]
public sealed class ImportController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImportController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    // Tracks the status of the last import run (in-memory for simplicity)
    private static ImportStatus _lastStatus = new(false, null, null, null);

    public ImportController(
        IServiceScopeFactory scopeFactory,
        ILogger<ImportController> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    [HttpPost("trigger")]
    public ActionResult<ApiResponse<object>> TriggerImport(
        [FromBody] TriggerImportRequest request,
        CancellationToken ct)
    {
        var crmDataPath = CrmDataPathResolver.Resolve(
            request.CrmDataPath,
            _configuration["CrmDataPath"],
            _configuration["CrmDataRoot"],
            _environment.ContentRootPath);

        if (string.IsNullOrWhiteSpace(crmDataPath))
            return BadRequest(ApiResponse<object>.Fail("CrmDataPath must resolve inside the configured CRM data root."));

        if (!Directory.Exists(crmDataPath))
        {
            _logger.LogWarning("CRM data directory not found: {Path}", crmDataPath);
            return BadRequest(ApiResponse<object>.Fail("CRM data directory not found."));
        }

        if (_lastStatus.IsRunning)
            return Conflict(ApiResponse<object>.Fail("An import is already in progress."));

        _lastStatus = new ImportStatus(true, DateTimeOffset.UtcNow, null, null);


        _ = Task.Run(async () =>
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var importService = scope.ServiceProvider.GetRequiredService<ICrmImportService>();
            var aggregationService = scope.ServiceProvider.GetRequiredService<IPortfolioAggregationService>();
            var scoringEngine = scope.ServiceProvider.GetRequiredService<IRiskScoringEngine>();
            var explanationService = scope.ServiceProvider.GetRequiredService<IClaudeExplanationService>();

            try
            {
                _logger.LogInformation("Starting full import pipeline from {Path}", crmDataPath);

                var importResult = await importService.ImportAllAsync(crmDataPath, CancellationToken.None);
                _logger.LogInformation("Import complete: {Customers} customers", importResult.CustomersImported);

                var snapshot = await aggregationService.CreateSnapshotAsync(CancellationToken.None);
                _logger.LogInformation("Snapshot created: {Id}", snapshot.Id);

                var scores = await scoringEngine.ScoreAllCustomersAsync(snapshot.Id, CancellationToken.None);
                _logger.LogInformation("Scored {Count} customers", scores.Count);

                await explanationService.GenerateExplanationsAsync(scores, CancellationToken.None);
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

    public sealed record TriggerImportRequest(string? CrmDataPath);
}
