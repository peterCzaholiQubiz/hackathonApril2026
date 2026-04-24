using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Api.Common;
using PortfolioThermometer.Api.ViewModels;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CustomerSummaryVm>>>> GetCustomers(
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

        var query = db.Customers
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

        var vms = customers.Select(c =>
        {
            var latest = c.RiskScores.MaxBy(r => r.ScoredAt);
            var risk = latest is null ? null : new RiskScoreSummaryVm(
                latest.ChurnScore, latest.PaymentScore, latest.MarginScore,
                latest.OverallScore, latest.HeatLevel, latest.ScoredAt);
            return new CustomerSummaryVm(
                c.Id, c.CrmExternalId, c.Name, c.CompanyName,
                c.Email, c.Phone, c.Segment, c.AccountManager,
                c.OnboardingDate, c.IsActive, risk);
        }).ToList();

        var meta = new ApiMeta { Total = total, Page = page, PageSize = pageSize };
        return Ok(ApiResponse<IReadOnlyList<CustomerSummaryVm>>.Ok(vms, meta));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerDetailVm>>> GetCustomer(Guid id, CancellationToken ct)
    {
        var customer = await db.Customers
            .Include(c => c.Contracts)
            .Include(c => c.Invoices)
            .Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (customer is null)
            return NotFound(ApiResponse<CustomerDetailVm>.Fail($"Customer {id} not found."));

        var vm = new CustomerDetailVm(
            customer.Id, customer.CrmExternalId, customer.Name, customer.CompanyName,
            customer.Email, customer.Phone, customer.Segment, customer.AccountManager,
            customer.OnboardingDate, customer.IsActive,
            customer.Contracts.Select(c => new ContractVm(
                c.Id, c.CrmExternalId, c.ContractType, c.StartDate, c.EndDate,
                c.MonthlyValue, c.Currency, c.Status, c.AutoRenew)).ToList(),
            customer.Invoices.Select(i => new InvoiceVm(
                i.Id, i.CrmExternalId, i.InvoiceNumber, i.IssuedDate, i.DueDate,
                i.Amount, i.Currency, i.Status)).ToList(),
            customer.Payments.Select(p => new PaymentVm(
                p.Id, p.CrmExternalId, p.InvoiceId, p.PaymentDate,
                p.Amount, p.DaysLate)).ToList());

        return Ok(ApiResponse<CustomerDetailVm>.Ok(vm));
    }

    [HttpGet("{id:guid}/risk")]
    public async Task<ActionResult<ApiResponse<RiskScoreVm?>>> GetCustomerRisk(Guid id, CancellationToken ct)
    {
        var exists = await db.Customers.AnyAsync(c => c.Id == id, ct);
        if (!exists)
            return NotFound(ApiResponse<RiskScoreVm?>.Fail($"Customer {id} not found."));

        var score = await db.RiskScores
            .Include(r => r.RiskExplanations)
            .Include(r => r.SuggestedActions)
            .Where(r => r.CustomerId == id)
            .OrderByDescending(r => r.ScoredAt)
            .FirstOrDefaultAsync(ct);

        if (score is null)
            return Ok(ApiResponse<RiskScoreVm?>.Ok(null));

        var vm = new RiskScoreVm(
            score.Id, score.CustomerId, score.SnapshotId,
            score.ChurnScore, score.PaymentScore, score.MarginScore,
            score.OverallScore, score.HeatLevel, score.ScoredAt,
            score.RiskExplanations.Select(e => new RiskExplanationVm(
                e.Id, e.RiskType, e.Explanation, e.Confidence,
                e.GeneratedAt, e.ModelUsed)).ToList(),
            score.SuggestedActions.Select(a => new SuggestedActionVm(
                a.Id, a.ActionType, a.Priority, a.Title,
                a.Description, a.GeneratedAt)).ToList());

        return Ok(ApiResponse<RiskScoreVm?>.Ok(vm));
    }

    [HttpGet("{id:guid}/interactions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InteractionVm>>>> GetCustomerInteractions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var exists = await db.Customers.AnyAsync(c => c.Id == id, ct);
        if (!exists)
            return NotFound(ApiResponse<IReadOnlyList<InteractionVm>>.Fail($"Customer {id} not found."));

        var total = await db.Interactions.CountAsync(i => i.CustomerId == id, ct);
        var interactions = await db.Interactions
            .Where(i => i.CustomerId == id)
            .OrderByDescending(i => i.InteractionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InteractionVm(
                i.Id, i.CrmExternalId, i.InteractionDate,
                i.Channel, i.Direction, i.Summary, i.Sentiment))
            .ToListAsync(ct);

        var meta = new ApiMeta { Total = total, Page = page, PageSize = pageSize };
        return Ok(ApiResponse<IReadOnlyList<InteractionVm>>.Ok(interactions, meta));
    }

    [HttpGet("{id:guid}/complaints")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ComplaintVm>>>> GetCustomerComplaints(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var exists = await db.Customers.AnyAsync(c => c.Id == id, ct);
        if (!exists)
            return NotFound(ApiResponse<IReadOnlyList<ComplaintVm>>.Fail($"Customer {id} not found."));

        var total = await db.Complaints.CountAsync(c => c.CustomerId == id, ct);
        var complaints = await db.Complaints
            .Where(c => c.CustomerId == id)
            .OrderByDescending(c => c.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ComplaintVm(
                c.Id, c.CrmExternalId, c.CreatedDate, c.ResolvedDate,
                c.Category, c.Severity, c.Description))
            .ToListAsync(ct);

        var meta = new ApiMeta { Total = total, Page = page, PageSize = pageSize };
        return Ok(ApiResponse<IReadOnlyList<ComplaintVm>>.Ok(complaints, meta));
    }
}
