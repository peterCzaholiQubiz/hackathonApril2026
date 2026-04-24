using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Infrastructure.Data.Configurations;

public class PortfolioSnapshotConfiguration : IEntityTypeConfiguration<PortfolioSnapshot>
{
    public void Configure(EntityTypeBuilder<PortfolioSnapshot> builder)
    {
        builder.ToTable("portfolio_snapshots");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(p => p.TotalCustomers).HasColumnName("total_customers").IsRequired().HasDefaultValue(0);
        builder.Property(p => p.GreenCount).HasColumnName("green_count").IsRequired().HasDefaultValue(0);
        builder.Property(p => p.YellowCount).HasColumnName("yellow_count").IsRequired().HasDefaultValue(0);
        builder.Property(p => p.RedCount).HasColumnName("red_count").IsRequired().HasDefaultValue(0);
        builder.Property(p => p.GreenPct).HasColumnName("green_pct").HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0m);
        builder.Property(p => p.YellowPct).HasColumnName("yellow_pct").HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0m);
        builder.Property(p => p.RedPct).HasColumnName("red_pct").HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0m);
        builder.Property(p => p.AvgChurnScore).HasColumnName("avg_churn_score").HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0m);
        builder.Property(p => p.AvgPaymentScore).HasColumnName("avg_payment_score").HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0m);
        builder.Property(p => p.AvgMarginScore).HasColumnName("avg_margin_score").HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0m);
        builder.Property(p => p.SegmentBreakdown).HasColumnName("segment_breakdown").HasColumnType("jsonb");

        builder.HasIndex(p => p.CreatedAt).HasDatabaseName("idx_portfolio_snapshots_created_at").IsDescending();
    }
}
