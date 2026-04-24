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
                p.Amount, p.DaysLate, GetPaymentSeverity(p.DaysLate))).ToList());

        return Ok(ApiResponse<CustomerDetailVm>.Ok(vm));
    }

    [HttpGet("{id:guid}/payments")]
    public async Task<ActionResult<ApiResponse<CustomerPaymentsVm>>> GetCustomerPayments(
        Guid id,
        [FromQuery] string? severity,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 12;
        if (page < 1) page = 1;

        var normalizedSeverity = NormalizePaymentSeverity(severity);
        if (severity is not null && normalizedSeverity is null)
            return BadRequest(ApiResponse<CustomerPaymentsVm>.Fail("Severity must be one of: low, medium, high."));

        var exists = await db.Customers.AnyAsync(c => c.Id == id, ct);
        if (!exists)
            return NotFound(ApiResponse<CustomerPaymentsVm>.Fail($"Customer {id} not found."));

        var allPayments = db.Payments
            .AsNoTracking()
            .Where(p => p.CustomerId == id);

        var summary = new PaymentSummaryVm(
            await allPayments.CountAsync(p => p.DaysLate <= 15, ct),
            await allPayments.CountAsync(p => p.DaysLate >= 16 && p.DaysLate <= 30, ct),
            await allPayments.CountAsync(p => p.DaysLate > 30, ct));

        var filteredPayments = normalizedSeverity switch
        {
            "low" => allPayments.Where(p => p.DaysLate <= 15),
            "medium" => allPayments.Where(p => p.DaysLate >= 16 && p.DaysLate <= 30),
            "high" => allPayments.Where(p => p.DaysLate > 30),
            _ => allPayments,
        };

        var total = await filteredPayments.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        if (page > totalPages) page = totalPages;

        var payments = await filteredPayments
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.ImportedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PaymentVm(
                p.Id,
                p.CrmExternalId,
                p.InvoiceId,
                p.PaymentDate,
                p.Amount,
                p.DaysLate,
                GetPaymentSeverity(p.DaysLate)))
            .ToListAsync(ct);

        var vm = new CustomerPaymentsVm(normalizedSeverity, summary, payments);
        var meta = new ApiMeta { Total = total, Page = page, PageSize = pageSize };

        return Ok(ApiResponse<CustomerPaymentsVm>.Ok(vm, meta));
    }

    [HttpGet("{id:guid}/consumption")]
    public async Task<ActionResult<ApiResponse<CustomerConsumptionVm>>> GetCustomerConsumption(
        Guid id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? unit,
        CancellationToken ct = default)
    {
        var exists = await db.Customers.AnyAsync(c => c.Id == id, ct);
        if (!exists)
            return NotFound(ApiResponse<CustomerConsumptionVm>.Fail($"Customer {id} not found."));

        var range = ResolveConsumptionRange(from, to);
        if (range is null)
            return BadRequest(ApiResponse<CustomerConsumptionVm>.Fail("The from date must be on or before the to date."));

        var (resolvedFrom, resolvedTo) = range.Value;
        var fromUtc = new DateTimeOffset(resolvedFrom.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toExclusiveUtc = new DateTimeOffset(resolvedTo.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var connectionIds = await db.Connections
            .AsNoTracking()
            .Where(c => c.CustomerId == id)
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (connectionIds.Count == 0)
        {
            return Ok(ApiResponse<CustomerConsumptionVm>.Ok(new CustomerConsumptionVm(
                resolvedFrom,
                resolvedTo,
                null,
                [],
                [])));
        }

        var rows = await db.MeterReads
            .AsNoTracking()
            .Where(m =>
                m.ConnectionId.HasValue &&
                connectionIds.Contains(m.ConnectionId.Value) &&
                m.Direction == "Consumption" &&
                m.StartDate.HasValue &&
                m.StartDate.Value >= fromUtc &&
                m.StartDate.Value < toExclusiveUtc &&
                m.Consumption.HasValue &&
                !string.IsNullOrWhiteSpace(m.Unit))
            .Select(m => new
            {
                StartDate = m.StartDate!.Value,
                Consumption = m.Consumption!.Value,
                Unit = m.Unit!,
                Quality = string.IsNullOrWhiteSpace(m.Quality) ? "Unknown" : m.Quality!
            })
            .ToListAsync(ct);

        var availableUnits = rows
            .Select(r => r.Unit)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (availableUnits.Count == 0)
        {
            return Ok(ApiResponse<CustomerConsumptionVm>.Ok(new CustomerConsumptionVm(
                resolvedFrom,
                resolvedTo,
                null,
                [],
                [])));
        }

        string? selectedUnit;
        if (!string.IsNullOrWhiteSpace(unit))
        {
            selectedUnit = availableUnits.FirstOrDefault(u => string.Equals(u, unit, StringComparison.OrdinalIgnoreCase));
            if (selectedUnit is null)
                return BadRequest(ApiResponse<CustomerConsumptionVm>.Fail($"Unit '{unit}' is not available in the selected interval."));
        }
        else
        {
            selectedUnit = availableUnits[0];
        }

        var points = rows
            .Where(r => string.Equals(r.Unit, selectedUnit, StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => new DateOnly(r.StartDate.Year, r.StartDate.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var qualityBreakdown = g
                    .GroupBy(item => item.Quality, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new CustomerConsumptionQualityBreakdownVm(
                        group.Key,
                        group.Count(),
                        Math.Round(group.Sum(item => item.Consumption), 2)))
                    .OrderBy(item => item.Quality, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var quality = qualityBreakdown.Count == 1
                    ? qualityBreakdown[0].Quality
                    : "Mixed";

                return new CustomerConsumptionPointVm(
                    g.Key.ToString("yyyy-MM-dd"),
                    Math.Round(g.Sum(item => item.Consumption), 2),
                    selectedUnit,
                    quality,
                    qualityBreakdown);
            })
            .ToList();

        return Ok(ApiResponse<CustomerConsumptionVm>.Ok(new CustomerConsumptionVm(
            resolvedFrom,
            resolvedTo,
            selectedUnit,
            availableUnits,
            points)));
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

    private static (DateOnly From, DateOnly To)? ResolveConsumptionRange(DateOnly? from, DateOnly? to)
    {
        var resolvedTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var defaultFrom = new DateOnly(resolvedTo.Year, resolvedTo.Month, 1).AddMonths(-11);
        var resolvedFrom = from ?? defaultFrom;

        return resolvedFrom > resolvedTo ? null : (resolvedFrom, resolvedTo);
    }

    private static string GetPaymentSeverity(int daysLate) => daysLate switch
    {
        <= 15 => "low",
        <= 30 => "medium",
        _ => "high",
    };

    private static string? NormalizePaymentSeverity(string? severity) => severity?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "low" => "low",
        "medium" => "medium",
        "high" => "high",
        _ => null,
    };
}
