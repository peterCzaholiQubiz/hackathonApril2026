using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Core.Interfaces;

public interface IPortfolioAggregationService
{
    Task<PortfolioSnapshot> CreateSnapshotAsync(CancellationToken ct);
}
