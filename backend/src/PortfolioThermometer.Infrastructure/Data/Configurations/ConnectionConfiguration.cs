using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Infrastructure.Data.Configurations;

public class ConnectionConfiguration : IEntityTypeConfiguration<Connection>
{
    public void Configure(EntityTypeBuilder<Connection> builder)
    {
        builder.ToTable("connections");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.CrmExternalId).HasColumnName("crm_external_id").HasMaxLength(100).IsRequired();
        builder.Property(c => c.CustomerId).HasColumnName("customer_id");
        builder.Property(c => c.Ean).HasColumnName("ean").HasMaxLength(50).IsRequired();
        builder.Property(c => c.ProductType).HasColumnName("product_type").HasMaxLength(20);
        builder.Property(c => c.DeliveryType).HasColumnName("delivery_type").HasMaxLength(10);
        builder.Property(c => c.ConnectionTypeId).HasColumnName("connection_type_id");
        builder.Property(c => c.ImportedAt).HasColumnName("imported_at");

        builder.HasIndex(c => c.CrmExternalId).IsUnique().HasDatabaseName("uq_connections_crm_id");
        builder.HasIndex(c => c.Ean).HasDatabaseName("idx_connections_ean");
        builder.HasIndex(c => c.CustomerId).HasDatabaseName("idx_connections_customer_id");

        builder.HasOne(c => c.Customer)
               .WithMany()
               .HasForeignKey(c => c.CustomerId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
