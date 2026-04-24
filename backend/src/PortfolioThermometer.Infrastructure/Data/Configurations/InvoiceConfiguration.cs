using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Infrastructure.Data.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(i => i.CrmExternalId).HasColumnName("crm_external_id").HasMaxLength(100).IsRequired();
        builder.Property(i => i.InvoiceNumber).HasColumnName("invoice_number").HasMaxLength(50);
        builder.Property(i => i.IssuedDate).HasColumnName("issued_date");
        builder.Property(i => i.DueDate).HasColumnName("due_date");
        builder.Property(i => i.Amount).HasColumnName("amount").HasColumnType("decimal(12,2)");
        builder.Property(i => i.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired().HasDefaultValue("EUR");
        builder.Property(i => i.Status).HasColumnName("status").HasMaxLength(20);
        builder.Property(i => i.ImportedAt).HasColumnName("imported_at").IsRequired().HasDefaultValueSql("NOW()");

        builder.HasIndex(i => i.CrmExternalId).IsUnique().HasDatabaseName("uq_invoices_crm_id");
        builder.HasIndex(i => i.CustomerId).HasDatabaseName("idx_invoices_customer_id");
        builder.HasIndex(i => i.Status).HasDatabaseName("idx_invoices_status");
        builder.HasIndex(i => i.DueDate).HasDatabaseName("idx_invoices_due_date");

        builder.HasOne(i => i.Customer)
            .WithMany(c => c.Invoices)
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
