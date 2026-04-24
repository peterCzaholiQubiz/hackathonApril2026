namespace PortfolioThermometer.Core.Models;

public class Invoice
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CrmExternalId { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal? Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? Status { get; set; }
    public DateTimeOffset ImportedAt { get; set; }

    // Navigation properties
    public Customer Customer { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
