namespace PortfolioThermometer.Api.ViewModels;

public sealed record CustomerSummaryVm(
    Guid Id,
    string CrmExternalId,
    string Name,
    string? CompanyName,
    string? Email,
    string? Phone,
    string? Segment,
    string? AccountManager,
    DateOnly? OnboardingDate,
    bool IsActive,
    RiskScoreSummaryVm? LatestRisk);

public sealed record RiskScoreSummaryVm(
    int ChurnScore,
    int PaymentScore,
    int MarginScore,
    int OverallScore,
    string HeatLevel,
    DateTimeOffset ScoredAt);

public sealed record CustomerDetailVm(
    Guid Id,
    string CrmExternalId,
    string Name,
    string? CompanyName,
    string? Email,
    string? Phone,
    string? Segment,
    string? AccountManager,
    DateOnly? OnboardingDate,
    bool IsActive,
    IReadOnlyList<ContractVm> Contracts,
    IReadOnlyList<InvoiceVm> Invoices,
    IReadOnlyList<PaymentVm> Payments);

public sealed record ContractVm(
    Guid Id,
    string CrmExternalId,
    string? ContractType,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal? MonthlyValue,
    string Currency,
    string? Status,
    bool AutoRenew);

public sealed record InvoiceVm(
    Guid Id,
    string CrmExternalId,
    string? InvoiceNumber,
    DateOnly? IssuedDate,
    DateOnly? DueDate,
    decimal? Amount,
    string Currency,
    string? Status);

public sealed record PaymentVm(
    Guid Id,
    string CrmExternalId,
    Guid InvoiceId,
    DateOnly? PaymentDate,
    decimal? Amount,
    int DaysLate);

public sealed record InteractionVm(
    Guid Id,
    string CrmExternalId,
    DateOnly? InteractionDate,
    string? Channel,
    string? Direction,
    string? Summary,
    string? Sentiment);

public sealed record ComplaintVm(
    Guid Id,
    string CrmExternalId,
    DateOnly? CreatedDate,
    DateOnly? ResolvedDate,
    string? Category,
    string? Severity,
    string? Description);

public sealed record RiskScoreVm(
    Guid Id,
    Guid CustomerId,
    Guid SnapshotId,
    int ChurnScore,
    int PaymentScore,
    int MarginScore,
    int OverallScore,
    string HeatLevel,
    DateTimeOffset ScoredAt,
    IReadOnlyList<RiskExplanationVm> RiskExplanations,
    IReadOnlyList<SuggestedActionVm> SuggestedActions);

public sealed record RiskExplanationVm(
    Guid Id,
    string RiskType,
    string Explanation,
    string Confidence,
    DateTimeOffset GeneratedAt,
    string ModelUsed);

public sealed record SuggestedActionVm(
    Guid Id,
    string ActionType,
    string Priority,
    string Title,
    string? Description,
    DateTimeOffset GeneratedAt);

public sealed record CustomerConsumptionVm(
    DateOnly From,
    DateOnly To,
    string? SelectedUnit,
    IReadOnlyList<string> AvailableUnits,
    IReadOnlyList<CustomerConsumptionPointVm> Points);

public sealed record CustomerConsumptionPointVm(
    string Month,
    decimal Consumption,
    string Unit,
    string Quality,
    IReadOnlyList<CustomerConsumptionQualityBreakdownVm> QualityBreakdown);

public sealed record CustomerConsumptionQualityBreakdownVm(
    string Quality,
    int ReadCount,
    decimal Consumption);

public sealed record PortfolioSnapshotVm(
    Guid Id,
    DateTimeOffset CreatedAt,
    int TotalCustomers,
    int GreenCount,
    int YellowCount,
    int RedCount,
    decimal GreenPct,
    decimal YellowPct,
    decimal RedPct,
    decimal AvgChurnScore,
    decimal AvgPaymentScore,
    decimal AvgMarginScore,
    string? SegmentBreakdown);
