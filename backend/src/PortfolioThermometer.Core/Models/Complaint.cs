namespace PortfolioThermometer.Core.Models;

public class Complaint
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CrmExternalId { get; set; } = string.Empty;
    public DateOnly? CreatedDate { get; set; }
    public DateOnly? ResolvedDate { get; set; }
    public string? Category { get; set; }
    public string? Severity { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset ImportedAt { get; set; }

    // Navigation properties
    public Customer Customer { get; set; } = null!;
}
