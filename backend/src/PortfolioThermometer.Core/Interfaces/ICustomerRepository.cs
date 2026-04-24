using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Core.Interfaces;

public interface ICustomerRepository
{
    Task<IReadOnlyList<Customer>> GetAllActiveAsync(CancellationToken ct);
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);
}
