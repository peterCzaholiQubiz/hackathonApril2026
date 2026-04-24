using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Core.Interfaces;

public interface ICrmImportService
{
    Task<ImportResult> ImportAllAsync(CancellationToken ct);
}
