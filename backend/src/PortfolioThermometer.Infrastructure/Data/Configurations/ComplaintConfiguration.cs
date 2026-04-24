using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Infrastructure.Data.Configurations;

public class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> builder)
    {
        builder.ToTable("complaints");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(c => c.CrmExternalId).HasColumnName("crm_external_id").HasMaxLength(100).IsRequired();
        builder.Property(c => c.CreatedDate).HasColumnName("created_date");
        builder.Property(c => c.ResolvedDate).HasColumnName("resolved_date");
        builder.Property(c => c.Category).HasColumnName("category").HasMaxLength(50);
        builder.Property(c => c.Severity).HasColumnName("severity").HasMaxLength(20);
        builder.Property(c => c.Description).HasColumnName("description");
        builder.Property(c => c.ImportedAt).HasColumnName("imported_at").IsRequired().HasDefaultValueSql("NOW()");

        builder.HasIndex(c => c.CrmExternalId).IsUnique().HasDatabaseName("uq_complaints_crm_id");
        builder.HasIndex(c => c.CustomerId).HasDatabaseName("idx_complaints_customer_id");
        builder.HasIndex(c => c.Severity).HasDatabaseName("idx_complaints_severity");
        builder.HasIndex(c => c.CreatedDate).HasDatabaseName("idx_complaints_created_date");
        builder.HasIndex(c => c.ResolvedDate).HasDatabaseName("idx_complaints_resolved_date");

        builder.HasOne(c => c.Customer)
            .WithMany(cu => cu.Complaints)
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
