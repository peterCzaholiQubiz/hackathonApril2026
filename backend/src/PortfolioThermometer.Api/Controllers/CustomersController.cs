using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Api.Common;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Customer>>>> GetCustomers(
        [FromQuery] string? segment,
        [FromQuery] string? heatLevel,
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 20;
        if (page < 1) page = 1;

        var query = _db.Customers
            .Include(c => c.RiskScores.OrderByDescending(r => r.ScoredAt).Take(1))
            .Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(segment))
            query = query.Where(c => c.Segment == segment);

        if (!string.IsNullOrWhiteSpace(heatLevel))
            query = query.Where(c => c.RiskScores.OrderByDescending(r => r.ScoredAt).Take(1).Any(r => r.HeatLevel == heatLevel));

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || (c.CompanyName != null && c.CompanyName.Contains(search)));

        var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.ToLowerInvariant(), descending) switch
        {
            ("name", false) => query.OrderBy(c => c.Name),
            ("name", true) => query.OrderByDescending(c => c.Name),
            ("segment", false) => query.OrderBy(c => c.Segment),
            ("segment", true) => query.OrderByDescending(c => c.Segment),
            ("onboarding", false) => query.OrderBy(c => c.OnboardingDate),
            ("onboarding", true) => query.OrderByDescending(c => c.OnboardingDate),
            _ => query.OrderBy(c => c.Name)
        };

        var total = await query.CountAsync(ct);
        var customers = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var meta = new ApiMeta { Total = total, Page = page, PageSize = pageSize };
        return Ok(ApiResponse<IReadOnlyList<Customer>>.Ok(customers, meta));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<Customer>>> GetCustomer(Guid id, CancellationToken ct)
    {
        var customer = await _db.Customers
            .Include(c => c.Contracts)
            .Include(c => c.Invoices)
            .Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (customer is null)
            return NotFound(ApiResponse<Customer>.Fail($"Customer {id} not found."));

        return Ok(ApiResponse<Customer>.Ok(customer));
    }

    [HttpGet("{id:guid}/risk")]
    public async Task<ActionResult<ApiResponse<object>>> GetCustomerRisk(Guid id, CancellationToken ct)
    {
        var exists = await _db.Customers.AnyAsync(c => c.Id == id, ct);
        if (!exists)
            return NotFound(ApiResponse<object>.Fail($"Customer {id} not found."));

        var latestScore = await _db.RiskScores
            .Include(r => r.RiskExplanations)
            .Include(r => r.SuggestedActions)
            .Where(r => r.CustomerId == id)
            .OrderByDescending(r => r.ScoredAt)
            .FirstOrDefaultAsync(ct);

        return Ok(ApiResponse<object?>.Ok(latestScore));
    }

    [HttpGet("{id:guid}/interactions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Interaction>>>> GetCustomerInteractions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var exists = await _db.Customers.AnyAsync(c => c.Id == id, ct);
        if (!exists)
            return NotFound(ApiResponse<IReadOnlyList<Interaction>>.Fail($"Customer {id} not found."));

        var total = await _db.Interactions.CountAsync(i => i.CustomerId == id, ct);
        var interactions = await _db.Interactions
            .Where(i => i.CustomerId == id)
            .OrderByDescending(i => i.InteractionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var meta = new ApiMeta { Total = total, Page = page, PageSize = pageSize };
        return Ok(ApiResponse<IReadOnlyList<Interaction>>.Ok(interactions, meta));
    }

    [HttpGet("{id:guid}/complaints")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Complaint>>>> GetCustomerComplaints(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var exists = await _db.Customers.AnyAsync(c => c.Id == id, ct);
        if (!exists)
            return NotFound(ApiResponse<IReadOnlyList<Complaint>>.Fail($"Customer {id} not found."));

        var total = await _db.Complaints.CountAsync(c => c.CustomerId == id, ct);
        var complaints = await _db.Complaints
            .Where(c => c.CustomerId == id)
            .OrderByDescending(c => c.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var meta = new ApiMeta { Total = total, Page = page, PageSize = pageSize };
        return Ok(ApiResponse<IReadOnlyList<Complaint>>.Ok(complaints, meta));
    }
}
