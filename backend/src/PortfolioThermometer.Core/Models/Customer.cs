namespace PortfolioThermometer.Core.Models;

public class Customer
{
    public Guid Id { get; set; }
    public string CrmExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Segment { get; set; }
    public string? AccountManager { get; set; }
    public DateOnly? OnboardingDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset ImportedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();
    public ICollection<Interaction> Interactions { get; set; } = new List<Interaction>();
    public ICollection<RiskScore> RiskScores { get; set; } = new List<RiskScore>();
    public ICollection<RiskExplanation> RiskExplanations { get; set; } = new List<RiskExplanation>();
    public ICollection<SuggestedAction> SuggestedActions { get; set; } = new List<SuggestedAction>();
}
