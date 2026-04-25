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
    int DaysLate,
    string Severity);

public sealed record CustomerPaymentsVm(
    string? ActiveSeverity,
    PaymentSummaryVm Summary,
    IReadOnlyList<PaymentVm> Payments);

public sealed record PaymentSummaryVm(
    int Low,
    int Medium,
    int High);

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
    decimal Production,
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

// --- Risk Dimension Groups ---

public sealed record HeatBandVm(
    int Count,
    decimal Pct,
    decimal TotalMonthlyValue);

public sealed record HeatSummaryVm(
    int TotalCustomers,
    HeatBandVm Green,
    HeatBandVm Yellow,
    HeatBandVm Red);

public sealed record RiskItemActionVm(
    string ActionType,
    string Priority,
    string Title,
    string? Description);

public sealed record RiskDimensionItemVm(
    Guid CustomerId,
    string Name,
    string? CompanyName,
    string? Segment,
    int ChurnScore,
    int PaymentScore,
    int MarginScore,
    int OverallScore,
    string HeatLevel,
    decimal MonthlyContractValue,
    string? Explanation,
    string? Confidence,
    RiskItemActionVm? TopAction);

public sealed record RiskDimensionGroupVm(
    string Dimension,
    string Label,
    decimal AvgScore,
    int TotalFlagged,
    decimal TotalMonthlyValue,
    IReadOnlyList<RiskDimensionItemVm> Items);

public sealed record RiskDimensionGroupsResponseVm(
    HeatSummaryVm HeatSummary,
    IReadOnlyList<RiskDimensionGroupVm> Dimensions);

// --- Customer Scatter Data (all customers for heatmap) ---

public sealed record CustomerScatterPointVm(
    Guid CustomerId,
    string Name,
    string? CompanyName,
    string? Segment,
    int ChurnScore,
    int PaymentScore,
    int MarginScore,
    int OverallScore,
    string HeatLevel,
    decimal MonthlyContractValue);

// --- Portfolio Energy Heatmap ---

public sealed record EnergyHeatmapCellVm(
    int Year,
    int Month,
    decimal Total);
