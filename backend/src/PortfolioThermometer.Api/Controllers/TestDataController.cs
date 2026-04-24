using Microsoft.AspNetCore.Mvc;
using PortfolioThermometer.Api.Common;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Api.Controllers;

[ApiController]
[Route("api/test-data")]
public sealed class TestDataController(
    AppDbContext db,
    IServiceScopeFactory scopeFactory,
    ITestDataGenerationService generationService,
    IPortfolioAggregationService aggregationService,
    IRiskScoringEngine scoringEngine,
    IClaudeExplanationService explanationService,
    ILogger<TestDataController> logger) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<ActionResult<ApiResponse<GenerateTestDataResponse>>> Generate(
        [FromBody] GenerateTestDataRequest request,
        CancellationToken ct)
    {
        var count = Math.Clamp(request.CustomerCount, 1, 500);

        logger.LogInformation("Generating test data for {Count} customers", count);

        var result = await generationService.GenerateAsync(count, ct);

        logger.LogInformation(
            "Test data created: {Customers} customers, {Connections} connections, {Reads} meter reads",
            result.CustomersCreated, result.ConnectionsCreated, result.MeterReadsCreated);

        if (request.RunPipeline && !RiskRunGuard.TryStart())
            return Conflict(ApiResponse<GenerateTestDataResponse>.Fail("Risk scoring is already in progress."));

        if (request.RunPipeline)
        {
            Guid? snapshotId = null;
            var snapshot = new PortfolioSnapshot
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.MinValue
            };

            try
            {
                db.PortfolioSnapshots.Add(snapshot);
                await db.SaveChangesAsync(ct);
                snapshotId = snapshot.Id;

                var scores = await scoringEngine.ScoreAllCustomersAsync(snapshot.Id, ct);
                await aggregationService.RefreshSnapshotAsync(snapshot.Id, ct);
                await explanationService.GenerateExplanationsAsync(scores, ct);

                snapshot.CreatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);

                logger.LogInformation("Pipeline completed after test data generation");
            }
            catch
            {
                if (snapshotId.HasValue)
                {
                    try
                    {
                        await using var cleanupScope = scopeFactory.CreateAsyncScope();
                        var cleanupDb = cleanupScope.ServiceProvider.GetRequiredService<AppDbContext>();
                        await RiskPipelineCleanup.CleanupSnapshotAsync(cleanupDb, snapshotId.Value, CancellationToken.None);
                    }
                    catch (Exception cleanupEx)
                    {
                        logger.LogError(cleanupEx, "Failed to clean up test-data risk snapshot {SnapshotId}", snapshotId);
                    }
                }

                throw;
            }
            finally
            {
                RiskRunGuard.Complete();
            }
        }

        return Ok(ApiResponse<GenerateTestDataResponse>.Ok(new GenerateTestDataResponse(
            result.CustomersCreated,
            result.ConnectionsCreated,
            result.MeterReadsCreated,
            result.ContractsCreated,
            result.InvoicesCreated,
            result.PaymentsCreated,
            result.ComplaintsCreated,
            result.InteractionsCreated,
            request.RunPipeline)));
    }

    public sealed record GenerateTestDataRequest(
        int CustomerCount,
        bool RunPipeline = true);

    public sealed record GenerateTestDataResponse(
        int CustomersCreated,
        int ConnectionsCreated,
        int MeterReadsCreated,
        int ContractsCreated,
        int InvoicesCreated,
        int PaymentsCreated,
        int ComplaintsCreated,
        int InteractionsCreated,
        bool PipelineRan);
}
