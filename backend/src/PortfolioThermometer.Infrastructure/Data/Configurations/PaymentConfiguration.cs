using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.InvoiceId).HasColumnName("invoice_id").IsRequired();
        builder.Property(p => p.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(p => p.CrmExternalId).HasColumnName("crm_external_id").HasMaxLength(100).IsRequired();
        builder.Property(p => p.PaymentDate).HasColumnName("payment_date");
        builder.Property(p => p.Amount).HasColumnName("amount").HasColumnType("decimal(12,2)");
        builder.Property(p => p.DaysLate).HasColumnName("days_late").IsRequired().HasDefaultValue(0);
        builder.Property(p => p.ImportedAt).HasColumnName("imported_at").IsRequired().HasDefaultValueSql("NOW()");

        builder.HasIndex(p => p.CrmExternalId).IsUnique().HasDatabaseName("uq_payments_crm_id");
        builder.HasIndex(p => p.CustomerId).HasDatabaseName("idx_payments_customer_id");
        builder.HasIndex(p => p.InvoiceId).HasDatabaseName("idx_payments_invoice_id");
        builder.HasIndex(p => p.PaymentDate).HasDatabaseName("idx_payments_payment_date");

        builder.HasOne(p => p.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Customer)
            .WithMany(c => c.Payments)
            .HasForeignKey(p => p.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
