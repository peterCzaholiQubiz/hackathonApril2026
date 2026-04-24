using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Core.Interfaces;

public interface IPortfolioAggregationService
{
    Task<PortfolioSnapshot> RefreshSnapshotAsync(Guid snapshotId, CancellationToken ct);
}
