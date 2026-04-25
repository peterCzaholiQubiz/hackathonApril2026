using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Core.Interfaces;

public interface IMeterReadGenerationService
{
    Task<GenerateMeterReadsResponse> GenerateAsync(
        GenerateMeterReadsRequest request,
        CancellationToken ct = default);

    Task<GenerateYearlyMeterReadsResponse> GenerateYearlyAsync(
        GenerateYearlyMeterReadsRequest request,
        CancellationToken ct = default);
}
