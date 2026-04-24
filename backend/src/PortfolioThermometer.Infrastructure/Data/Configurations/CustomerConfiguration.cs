using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Infrastructure.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.CrmExternalId).HasColumnName("crm_external_id").HasMaxLength(100).IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(c => c.CompanyName).HasColumnName("company_name").HasMaxLength(255);
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(255);
        builder.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(50);
        builder.Property(c => c.Segment).HasColumnName("segment").HasMaxLength(50);
        builder.Property(c => c.AccountManager).HasColumnName("account_manager").HasMaxLength(255);
        builder.Property(c => c.OnboardingDate).HasColumnName("onboarding_date");
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValue(true);
        builder.Property(c => c.ImportedAt).HasColumnName("imported_at").IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("NOW()");

        builder.HasIndex(c => c.CrmExternalId).IsUnique().HasDatabaseName("uq_customers_crm_id");
        builder.HasIndex(c => c.Segment).HasDatabaseName("idx_customers_segment");
        builder.HasIndex(c => c.IsActive).HasDatabaseName("idx_customers_is_active");
    }
}
