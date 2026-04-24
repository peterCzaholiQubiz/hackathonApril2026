using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Infrastructure.Data.Configurations;

public class InteractionConfiguration : IEntityTypeConfiguration<Interaction>
{
    public void Configure(EntityTypeBuilder<Interaction> builder)
    {
        builder.ToTable("interactions");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(i => i.CrmExternalId).HasColumnName("crm_external_id").HasMaxLength(100).IsRequired();
        builder.Property(i => i.InteractionDate).HasColumnName("interaction_date");
        builder.Property(i => i.Channel).HasColumnName("channel").HasMaxLength(30);
        builder.Property(i => i.Direction).HasColumnName("direction").HasMaxLength(10);
        builder.Property(i => i.Summary).HasColumnName("summary");
        builder.Property(i => i.Sentiment).HasColumnName("sentiment").HasMaxLength(20);
        builder.Property(i => i.ImportedAt).HasColumnName("imported_at").IsRequired().HasDefaultValueSql("NOW()");

        builder.HasIndex(i => i.CrmExternalId).IsUnique().HasDatabaseName("uq_interactions_crm_id");
        builder.HasIndex(i => i.CustomerId).HasDatabaseName("idx_interactions_customer_id");
        builder.HasIndex(i => i.InteractionDate).HasDatabaseName("idx_interactions_interaction_date");
        builder.HasIndex(i => i.Direction).HasDatabaseName("idx_interactions_direction");
        builder.HasIndex(i => i.Sentiment).HasDatabaseName("idx_interactions_sentiment");

        builder.HasOne(i => i.Customer)
            .WithMany(c => c.Interactions)
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
