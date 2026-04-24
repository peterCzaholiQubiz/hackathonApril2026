using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Infrastructure.Data.Configurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("contracts");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(c => c.CrmExternalId).HasColumnName("crm_external_id").HasMaxLength(100).IsRequired();
        builder.Property(c => c.ContractType).HasColumnName("contract_type").HasMaxLength(50);
        builder.Property(c => c.StartDate).HasColumnName("start_date");
        builder.Property(c => c.EndDate).HasColumnName("end_date");
        builder.Property(c => c.MonthlyValue).HasColumnName("monthly_value").HasColumnType("decimal(12,2)");
        builder.Property(c => c.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired().HasDefaultValue("EUR");
        builder.Property(c => c.Status).HasColumnName("status").HasMaxLength(20);
        builder.Property(c => c.AutoRenew).HasColumnName("auto_renew").IsRequired().HasDefaultValue(false);
        builder.Property(c => c.ImportedAt).HasColumnName("imported_at").IsRequired().HasDefaultValueSql("NOW()");

        builder.HasIndex(c => c.CrmExternalId).IsUnique().HasDatabaseName("uq_contracts_crm_id");
        builder.HasIndex(c => c.CustomerId).HasDatabaseName("idx_contracts_customer_id");
        builder.HasIndex(c => c.Status).HasDatabaseName("idx_contracts_status");
        builder.HasIndex(c => c.EndDate).HasDatabaseName("idx_contracts_end_date");

        builder.HasOne(c => c.Customer)
            .WithMany(cu => cu.Contracts)
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
