namespace PortfolioThermometer.Core.Models;

public class MeterRead
{
    public Guid Id { get; set; }
    public string CrmExternalId { get; set; } = string.Empty;   // UsageID (ConnectionMeterReads) or EANUniqueIdentifier+StartDate (Meter Read_1-8)
    public Guid? ConnectionId { get; set; }
    public Connection? Connection { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public decimal? Consumption { get; set; }
    public string? Unit { get; set; }           // "kWh" | "m3"
    public string? UsageType { get; set; }      // "UsageHigh" | "UsageLow"
    public string? Direction { get; set; }      // "Consumption" | "Production"
    public string? Quality { get; set; }        // "Estimated" | "Measured" | "Customer" | "Actual"
    public string? Source { get; set; }         // "ConnectionMeterReads" | "MeterRead_1-8"
    public DateTimeOffset ImportedAt { get; set; }
}
