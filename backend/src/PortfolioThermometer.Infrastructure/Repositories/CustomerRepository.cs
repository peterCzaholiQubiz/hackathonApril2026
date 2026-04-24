using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Infrastructure.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _db;

    public CustomerRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Customer>> GetAllActiveAsync(CancellationToken ct)
    {
        return await _db.Customers
            .Where(c => c.IsActive)
            .Include(c => c.Contracts)
            .Include(c => c.Invoices)
            .Include(c => c.Payments)
            .Include(c => c.Complaints)
            .Include(c => c.Interactions)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.Customers
            .Include(c => c.Contracts)
            .Include(c => c.Invoices)
            .Include(c => c.Payments)
            .Include(c => c.Complaints)
            .Include(c => c.Interactions)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }
}
