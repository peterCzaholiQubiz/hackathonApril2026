namespace PortfolioThermometer.Core.Models;

public class Payment
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid CustomerId { get; set; }
    public string CrmExternalId { get; set; } = string.Empty;
    public DateOnly? PaymentDate { get; set; }
    public decimal? Amount { get; set; }
    public int DaysLate { get; set; } = 0;
    public DateTimeOffset ImportedAt { get; set; }

    // Navigation properties
    public Invoice Invoice { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
}
