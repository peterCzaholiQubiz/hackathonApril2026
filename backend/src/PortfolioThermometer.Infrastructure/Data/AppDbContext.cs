using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data.Configurations;

namespace PortfolioThermometer.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<Interaction> Interactions => Set<Interaction>();
    public DbSet<PortfolioSnapshot> PortfolioSnapshots => Set<PortfolioSnapshot>();
    public DbSet<RiskScore> RiskScores => Set<RiskScore>();
    public DbSet<RiskExplanation> RiskExplanations => Set<RiskExplanation>();
    public DbSet<SuggestedAction> SuggestedActions => Set<SuggestedAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new ContractConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new ComplaintConfiguration());
        modelBuilder.ApplyConfiguration(new InteractionConfiguration());
        modelBuilder.ApplyConfiguration(new PortfolioSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new RiskScoreConfiguration());
        modelBuilder.ApplyConfiguration(new RiskExplanationConfiguration());
        modelBuilder.ApplyConfiguration(new SuggestedActionConfiguration());
    }
}
