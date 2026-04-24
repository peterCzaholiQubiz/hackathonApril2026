namespace PortfolioThermometer.Core.Models;

public class PortfolioSnapshot
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int TotalCustomers { get; set; }
    public int GreenCount { get; set; }
    public int YellowCount { get; set; }
    public int RedCount { get; set; }
    public decimal GreenPct { get; set; }
    public decimal YellowPct { get; set; }
    public decimal RedPct { get; set; }
    public decimal AvgChurnScore { get; set; }
    public decimal AvgPaymentScore { get; set; }
    public decimal AvgMarginScore { get; set; }
    public string? SegmentBreakdown { get; set; }

    // Navigation properties
    public ICollection<RiskScore> RiskScores { get; set; } = new List<RiskScore>();
}
