using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Infrastructure.Data.Configurations;

public class SuggestedActionConfiguration : IEntityTypeConfiguration<SuggestedAction>
{
    public void Configure(EntityTypeBuilder<SuggestedAction> builder)
    {
        builder.ToTable("suggested_actions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.RiskScoreId).HasColumnName("risk_score_id").IsRequired();
        builder.Property(s => s.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(s => s.ActionType).HasColumnName("action_type").HasMaxLength(30).IsRequired();
        builder.Property(s => s.Priority).HasColumnName("priority").HasMaxLength(10).IsRequired();
        builder.Property(s => s.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        builder.Property(s => s.Description).HasColumnName("description");
        builder.Property(s => s.GeneratedAt).HasColumnName("generated_at").IsRequired().HasDefaultValueSql("NOW()");

        builder.HasCheckConstraint("chk_action_type_values", "action_type IN ('outreach', 'discount', 'review', 'escalate', 'upsell')");
        builder.HasCheckConstraint("chk_priority_values", "priority IN ('high', 'medium', 'low')");

        builder.HasIndex(s => s.RiskScoreId).HasDatabaseName("idx_suggested_actions_risk_score_id");
        builder.HasIndex(s => s.CustomerId).HasDatabaseName("idx_suggested_actions_customer_id");
        builder.HasIndex(s => s.Priority).HasDatabaseName("idx_suggested_actions_priority");

        builder.HasOne(s => s.RiskScore)
            .WithMany(rs => rs.SuggestedActions)
            .HasForeignKey(s => s.RiskScoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Customer)
            .WithMany(c => c.SuggestedActions)
            .HasForeignKey(s => s.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
