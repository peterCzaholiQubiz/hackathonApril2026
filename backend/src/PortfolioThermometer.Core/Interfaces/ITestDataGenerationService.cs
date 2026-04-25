namespace PortfolioThermometer.Core.Interfaces;

public interface ITestDataGenerationService
{
    Task<TestDataGenerationResult> GenerateAsync(int customerCount, int atRiskCount = 0, CancellationToken ct = default);
    Task<ActivityGenerationResult> GenerateActivitiesAsync(IReadOnlyList<Guid> customerIds, CancellationToken ct = default);
}

public sealed record TestDataGenerationResult(
    int CustomersCreated,
    int ConnectionsCreated,
    int MeterReadsCreated,
    int ContractsCreated,
    int InvoicesCreated,
    int PaymentsCreated,
    int ComplaintsCreated,
    int InteractionsCreated);

public sealed record ActivityGenerationResult(
    int ComplaintsCreated,
    int InteractionsCreated);
