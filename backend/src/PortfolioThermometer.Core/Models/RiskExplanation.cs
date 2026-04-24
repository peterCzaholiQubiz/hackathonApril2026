namespace PortfolioThermometer.Core.Models;

public class RiskExplanation
{
    public Guid Id { get; set; }
    public Guid RiskScoreId { get; set; }
    public Guid CustomerId { get; set; }
    public string RiskType { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Confidence { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public string ModelUsed { get; set; } = "claude-sonnet-4-6";

    // Navigation properties
    public RiskScore RiskScore { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
}
