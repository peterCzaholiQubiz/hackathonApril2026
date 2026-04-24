using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Core.Interfaces;

public interface ICrmImportService
{
    Task<ImportResult> ImportAllAsync(string crmDataPath, CancellationToken ct);
}
