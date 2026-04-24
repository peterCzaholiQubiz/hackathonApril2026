namespace PortfolioThermometer.Core.Models;

public class Contract
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CrmExternalId { get; set; } = string.Empty;
    public string? ContractType { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal? MonthlyValue { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? Status { get; set; }
    public bool AutoRenew { get; set; } = false;
    public DateTimeOffset ImportedAt { get; set; }

    // Navigation properties
    public Customer Customer { get; set; } = null!;
}
