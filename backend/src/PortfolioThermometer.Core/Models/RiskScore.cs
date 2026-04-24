namespace PortfolioThermometer.Core.Models;

public class RiskScore
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid SnapshotId { get; set; }
    public int ChurnScore { get; set; }
    public int PaymentScore { get; set; }
    public int MarginScore { get; set; }
    public int OverallScore { get; set; }
    public string HeatLevel { get; set; } = string.Empty;
    public DateTimeOffset ScoredAt { get; set; }

    // Navigation properties
    public Customer Customer { get; set; } = null!;
    public PortfolioSnapshot Snapshot { get; set; } = null!;
    public ICollection<RiskExplanation> RiskExplanations { get; set; } = new List<RiskExplanation>();
    public ICollection<SuggestedAction> SuggestedActions { get; set; } = new List<SuggestedAction>();
}
