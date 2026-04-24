namespace PortfolioThermometer.Core.Models;

public class SuggestedAction
{
    public Guid Id { get; set; }
    public Guid RiskScoreId { get; set; }
    public Guid CustomerId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }

    // Navigation properties
    public RiskScore RiskScore { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
}
