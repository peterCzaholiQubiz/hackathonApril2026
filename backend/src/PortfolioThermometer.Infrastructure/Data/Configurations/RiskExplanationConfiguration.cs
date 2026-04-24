using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Infrastructure.Data.Configurations;

public class RiskExplanationConfiguration : IEntityTypeConfiguration<RiskExplanation>
{
    public void Configure(EntityTypeBuilder<RiskExplanation> builder)
    {
        builder.ToTable("risk_explanations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.RiskScoreId).HasColumnName("risk_score_id").IsRequired();
        builder.Property(r => r.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(r => r.RiskType).HasColumnName("risk_type").HasMaxLength(20).IsRequired();
        builder.Property(r => r.Explanation).HasColumnName("explanation").IsRequired();
        builder.Property(r => r.Confidence).HasColumnName("confidence").HasMaxLength(10).IsRequired();
        builder.Property(r => r.GeneratedAt).HasColumnName("generated_at").IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(r => r.ModelUsed).HasColumnName("model_used").HasMaxLength(50).IsRequired().HasDefaultValue("claude-sonnet-4-6");

        builder.HasCheckConstraint("chk_risk_type_values", "risk_type IN ('churn', 'payment', 'margin', 'overall')");
        builder.HasCheckConstraint("chk_confidence_values", "confidence IN ('high', 'medium', 'low')");

        builder.HasIndex(r => r.RiskScoreId).HasDatabaseName("idx_risk_explanations_risk_score_id");
        builder.HasIndex(r => r.CustomerId).HasDatabaseName("idx_risk_explanations_customer_id");
        builder.HasIndex(r => r.RiskType).HasDatabaseName("idx_risk_explanations_risk_type");

        builder.HasOne(r => r.RiskScore)
            .WithMany(rs => rs.RiskExplanations)
            .HasForeignKey(r => r.RiskScoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Customer)
            .WithMany(c => c.RiskExplanations)
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
