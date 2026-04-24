namespace PortfolioThermometer.Core.Models;

public class Connection
{
    public Guid Id { get; set; }
    public string CrmExternalId { get; set; } = string.Empty;   // ConnectionId from CSV
    public Guid? CustomerId { get; set; }                        // nullable — some connections have no customer link
    public Customer? Customer { get; set; }
    public string Ean { get; set; } = string.Empty;
    public string? ProductType { get; set; }    // "Electricity" | "Gas"
    public string? DeliveryType { get; set; }   // "LDN" | "ODN" | "NA"
    public int? ConnectionTypeId { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
}
