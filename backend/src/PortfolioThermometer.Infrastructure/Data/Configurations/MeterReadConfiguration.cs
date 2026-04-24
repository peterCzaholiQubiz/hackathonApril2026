using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Infrastructure.Data.Configurations;

public class MeterReadConfiguration : IEntityTypeConfiguration<MeterRead>
{
    public void Configure(EntityTypeBuilder<MeterRead> builder)
    {
        builder.ToTable("meter_reads");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.CrmExternalId).HasColumnName("crm_external_id").HasMaxLength(200).IsRequired();
        builder.Property(m => m.ConnectionId).HasColumnName("connection_id");
        builder.Property(m => m.StartDate).HasColumnName("start_date").HasColumnType("timestamptz");
        builder.Property(m => m.EndDate).HasColumnName("end_date").HasColumnType("timestamptz");
        builder.Property(m => m.Consumption).HasColumnName("consumption").HasPrecision(15, 4);
        builder.Property(m => m.Unit).HasColumnName("unit").HasMaxLength(10);
        builder.Property(m => m.UsageType).HasColumnName("usage_type").HasMaxLength(20);
        builder.Property(m => m.Direction).HasColumnName("direction").HasMaxLength(20);
        builder.Property(m => m.Quality).HasColumnName("quality").HasMaxLength(20);
        builder.Property(m => m.Source).HasColumnName("source").HasMaxLength(30);
        builder.Property(m => m.ImportedAt).HasColumnName("imported_at");

        builder.HasIndex(m => m.CrmExternalId).IsUnique().HasDatabaseName("uq_meter_reads_crm_id");
        builder.HasIndex(m => m.ConnectionId).HasDatabaseName("idx_meter_reads_connection_id");
        builder.HasIndex(m => m.StartDate).HasDatabaseName("idx_meter_reads_start_date");

        builder.HasOne(m => m.Connection)
               .WithMany()
               .HasForeignKey(m => m.ConnectionId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
