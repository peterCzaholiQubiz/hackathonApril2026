using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Core.Interfaces;

public interface IRiskScoringEngine
{
    Task<IReadOnlyList<RiskScore>> ScoreAllCustomersAsync(Guid snapshotId, CancellationToken ct);

    RiskScore ScoreCustomer(
        Customer customer,
        IReadOnlyList<Contract> contracts,
        IReadOnlyList<Invoice> invoices,
        IReadOnlyList<Payment> payments,
        IReadOnlyList<Complaint> complaints,
        IReadOnlyList<Interaction> interactions);
}
