using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Infrastructure.Services;

/// <summary>
/// Deterministic rule-based risk scoring engine.
/// Implements all signal weights from plan.md.
/// </summary>
public sealed class RiskScoringEngine : IRiskScoringEngine
{
    private readonly AppDbContext _db;
    private readonly ILogger<RiskScoringEngine> _logger;

    public RiskScoringEngine(AppDbContext db, ILogger<RiskScoringEngine> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RiskScore>> ScoreAllCustomersAsync(Guid snapshotId, CancellationToken ct)
    {
        var customers = await _db.Customers
            .Where(c => c.IsActive)
            .Include(c => c.Contracts)
            .Include(c => c.Invoices)
            .Include(c => c.Payments)
            .Include(c => c.Complaints)
            .Include(c => c.Interactions)
            .ToListAsync(ct);

        var scores = new List<RiskScore>(customers.Count);

        foreach (var customer in customers)
        {
            var score = ScoreCustomer(
                customer,
                customer.Contracts.ToList(),
                customer.Invoices.ToList(),
                customer.Payments.ToList(),
                customer.Complaints.ToList(),
                customer.Interactions.ToList());

            score.SnapshotId = snapshotId;
            scores.Add(score);
        }

        _db.RiskScores.AddRange(scores);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Scored {Count} customers for snapshot {SnapshotId}", scores.Count, snapshotId);
        return scores;
    }

    public RiskScore ScoreCustomer(
        Customer customer,
        IReadOnlyList<Contract> contracts,
        IReadOnlyList<Invoice> invoices,
        IReadOnlyList<Payment> payments,
        IReadOnlyList<Complaint> complaints,
        IReadOnlyList<Interaction> interactions)
    {
        var now = DateOnly.FromDateTime(DateTime.UtcNow);

        var churnScore = Math.Min(100, ComputeChurnScore(customer, contracts, complaints, interactions, now));
        var paymentScore = Math.Min(100, ComputePaymentScore(invoices, payments, now));
        var marginScore = Math.Min(100, ComputeMarginScore(customer, contracts, complaints));

        var overallScore = (int)Math.Round(
            (churnScore * 0.40) + (paymentScore * 0.35) + (marginScore * 0.25));

        var heatLevel = overallScore switch
        {
            <= 39 => "green",
            <= 69 => "yellow",
            _ => "red"
        };

        return new RiskScore
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            SnapshotId = Guid.Empty, // set by caller
            ChurnScore = churnScore,
            PaymentScore = paymentScore,
            MarginScore = marginScore,
            OverallScore = overallScore,
            HeatLevel = heatLevel,
            ScoredAt = DateTimeOffset.UtcNow
        };
    }

    // ── Churn risk signals ────────────────────────────────────────────────────

    private static int ComputeChurnScore(
        Customer customer,
        IReadOnlyList<Contract> contracts,
        IReadOnlyList<Complaint> complaints,
        IReadOnlyList<Interaction> interactions,
        DateOnly now)
    {
        var score = 0;

        // +25: Contract expiring within 90 days, no auto-renew
        var expiringSoon = contracts.Any(c =>
            c.Status == "active" &&
            !c.AutoRenew &&
            c.EndDate.HasValue &&
            c.EndDate.Value >= now &&
            c.EndDate.Value <= now.AddDays(90));
        if (expiringSoon) score += 25;

        // +20: Declining interaction frequency (last 90d vs prior 90d)
        var recent90 = interactions.Count(i => i.InteractionDate >= now.AddDays(-90));
        var prior90 = interactions.Count(i =>
            i.InteractionDate >= now.AddDays(-180) &&
            i.InteractionDate < now.AddDays(-90));
        if (recent90 < prior90) score += 20;

        // +20: Recent high-severity complaints (last 180 days)
        var highSeverityRecent = complaints.Any(c =>
            c.Severity is "high" or "critical" &&
            c.CreatedDate >= now.AddDays(-180));
        if (highSeverityRecent) score += 20;

        // +15: Negative sentiment in recent interactions (last 90 days)
        var negativeRecent = interactions.Any(i =>
            i.Sentiment == "negative" &&
            i.InteractionDate >= now.AddDays(-90));
        if (negativeRecent) score += 15;

        // +10: No outbound contact in 60+ days
        var lastOutbound = interactions
            .Where(i => i.Direction == "outbound")
            .Max(i => i.InteractionDate);
        if (lastOutbound == null || lastOutbound < now.AddDays(-60)) score += 10;

        // +10: Customer tenure < 12 months
        if (customer.OnboardingDate.HasValue &&
            customer.OnboardingDate.Value > now.AddMonths(-12))
            score += 10;

        return score;
    }

    // ── Payment risk signals ──────────────────────────────────────────────────

    private static int ComputePaymentScore(
        IReadOnlyList<Invoice> invoices,
        IReadOnlyList<Payment> payments,
        DateOnly now)
    {
        var score = 0;
        var sixMonthsAgo = now.AddMonths(-6);

        // +30: Average days late > 30 in last 6 months
        var recentPayments = payments.Where(p => p.PaymentDate >= sixMonthsAgo).ToList();
        if (recentPayments.Count > 0 && recentPayments.Average(p => p.DaysLate) > 30)
            score += 30;

        // +25: More than 2 overdue invoices currently
        var overdueCount = invoices.Count(i => i.Status == "overdue");
        if (overdueCount > 2) score += 25;

        // +20: Payment trend worsening (last 3 months vs prior 3)
        var last3 = payments
            .Where(p => p.PaymentDate >= now.AddMonths(-3))
            .Select(p => p.DaysLate)
            .DefaultIfEmpty(0)
            .Average();
        var prior3 = payments
            .Where(p => p.PaymentDate >= now.AddMonths(-6) && p.PaymentDate < now.AddMonths(-3))
            .Select(p => p.DaysLate)
            .DefaultIfEmpty(0)
            .Average();
        if (last3 > prior3) score += 20;

        // +15: Partial payments in last 6 months
        var partialInvoices = invoices.Any(i => i.Status == "partial" && i.IssuedDate >= sixMonthsAgo);
        if (partialInvoices) score += 15;

        // +10: Any invoice > 90 days overdue
        var severelyOverdue = invoices.Any(i =>
            i.Status == "overdue" &&
            i.DueDate.HasValue &&
            i.DueDate.Value < now.AddDays(-90));
        if (severelyOverdue) score += 10;

        return score;
    }

    // ── Margin behavior risk signals ──────────────────────────────────────────

    private static int ComputeMarginScore(
        Customer customer,
        IReadOnlyList<Contract> contracts,
        IReadOnlyList<Complaint> complaints)
    {
        var score = 0;
        var activeContracts = contracts.Where(c => c.Status == "active").ToList();
        var allContracts = contracts.OrderBy(c => c.StartDate).ToList();

        // +30: Contract value declining vs prior contract
        if (allContracts.Count >= 2)
        {
            var latest = allContracts[^1].MonthlyValue ?? 0;
            var previous = allContracts[^2].MonthlyValue ?? 0;
            if (latest < previous) score += 30;
        }

        // +25: Discount requests (billing complaints)
        var discountComplaints = complaints.Any(c => c.Category == "billing");
        if (discountComplaints) score += 25;

        // +20: Reduced active contracts vs 12 months ago (approximate: count < avg)
        if (activeContracts.Count == 0 && allContracts.Count > 0) score += 20;

        // +15: Below-segment average contract value (simplified: < 1000/month)
        var avgValue = activeContracts.Count > 0
            ? activeContracts.Average(c => c.MonthlyValue ?? 0)
            : 0;
        if (avgValue is > 0 and < 1000) score += 15;

        // +10: Short contract durations (< 6 months)
        var shortContracts = contracts.Any(c =>
            c.StartDate.HasValue &&
            c.EndDate.HasValue &&
            (c.EndDate.Value.DayNumber - c.StartDate.Value.DayNumber) < 180);
        if (shortContracts) score += 10;

        return score;
    }
}
