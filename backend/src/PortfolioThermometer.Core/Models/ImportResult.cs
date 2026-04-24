namespace PortfolioThermometer.Core.Models;

public record ImportResult(
    int CustomersImported,
    int ContractsImported,
    int InvoicesImported,
    int PaymentsImported,
    int ComplaintsImported,
    int InteractionsImported,
    int ConnectionsImported,
    DateTimeOffset ImportedAt
);
