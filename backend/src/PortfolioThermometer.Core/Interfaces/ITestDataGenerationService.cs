namespace PortfolioThermometer.Core.Interfaces;

public interface ITestDataGenerationService
{
    Task<TestDataGenerationResult> GenerateAsync(int customerCount, CancellationToken ct = default);
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
