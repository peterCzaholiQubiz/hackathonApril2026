using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Infrastructure.Data.Configurations;

public class RiskScoreConfiguration : IEntityTypeConfiguration<RiskScore>
{
    public void Configure(EntityTypeBuilder<RiskScore> builder)
    {
        builder.ToTable("risk_scores");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(r => r.SnapshotId).HasColumnName("snapshot_id").IsRequired();
        builder.Property(r => r.ChurnScore).HasColumnName("churn_score").IsRequired();
        builder.Property(r => r.PaymentScore).HasColumnName("payment_score").IsRequired();
        builder.Property(r => r.MarginScore).HasColumnName("margin_score").IsRequired();
        builder.Property(r => r.OverallScore).HasColumnName("overall_score").IsRequired();
        builder.Property(r => r.HeatLevel).HasColumnName("heat_level").HasMaxLength(10).IsRequired();
        builder.Property(r => r.ScoredAt).HasColumnName("scored_at").IsRequired().HasDefaultValueSql("NOW()");

        builder.HasCheckConstraint("chk_churn_score_range", "churn_score BETWEEN 0 AND 100");
        builder.HasCheckConstraint("chk_payment_score_range", "payment_score BETWEEN 0 AND 100");
        builder.HasCheckConstraint("chk_margin_score_range", "margin_score BETWEEN 0 AND 100");
        builder.HasCheckConstraint("chk_overall_score_range", "overall_score BETWEEN 0 AND 100");
        builder.HasCheckConstraint("chk_heat_level_values", "heat_level IN ('green', 'yellow', 'red')");

        builder.HasIndex(r => r.CustomerId).HasDatabaseName("idx_risk_scores_customer_id");
        builder.HasIndex(r => r.SnapshotId).HasDatabaseName("idx_risk_scores_snapshot_id");
        builder.HasIndex(r => r.HeatLevel).HasDatabaseName("idx_risk_scores_heat_level");
        builder.HasIndex(r => r.OverallScore).HasDatabaseName("idx_risk_scores_overall_score").IsDescending();

        builder.HasOne(r => r.Customer)
            .WithMany(c => c.RiskScores)
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Snapshot)
            .WithMany(s => s.RiskScores)
            .HasForeignKey(r => r.SnapshotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
