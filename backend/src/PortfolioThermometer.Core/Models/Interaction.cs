namespace PortfolioThermometer.Core.Models;

public class Interaction
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CrmExternalId { get; set; } = string.Empty;
    public DateOnly? InteractionDate { get; set; }
    public string? Channel { get; set; }
    public string? Direction { get; set; }
    public string? Summary { get; set; }
    public string? Sentiment { get; set; }
    public DateTimeOffset ImportedAt { get; set; }

    // Navigation properties
    public Customer Customer { get; set; } = null!;
}
