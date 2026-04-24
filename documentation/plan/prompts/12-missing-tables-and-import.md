# Prompt 12 — Missing Tables & Import Steps

**Agent**: `general-purpose`  
**Phase**: 1 — Foundation (gap-fill)  
**Status**: TODO  
**Depends on**: Prompt 06 (CRM CSV Import Service)

---

Fill the gaps identified after the initial implementation of Prompt 06. Two DB tables required by the data-flow plan are absent from the schema, the import service skips them entirely, and there is a path bug that prevents reading files from subdirectories.

Read `documentation/plan/data-flow.md` (Stage 1) and `frontend/src/app/core/models/crm-schema.model.ts` (authoritative column reference) before starting.

---

## 1 — Fix the path bug in `CrmImportService`

`ErpPath()` and `ArchivePath()` are currently identical — neither includes the subdirectory, so any file outside the root of `crmDataPath` is never found.

Replace both helpers:

```csharp
// ERPSQLServer subfolder
private static string ErpPath(string crmDataPath, string fileName)
    => Path.Combine(crmDataPath, "ERPSQLServer", $"[Confidential] {fileName}");

// ArchievingSolution subfolder (note: intentional typo in folder name matches real folder)
private static string ArchivePath(string crmDataPath, string fileName)
    => Path.Combine(crmDataPath, "ArchievingSolution", $"[Confidential] {fileName}");

// ArchievingSolution/Generic subfolder
private static string ArchiveGenericPath(string crmDataPath, string fileName)
    => Path.Combine(crmDataPath, "ArchievingSolution", "Generic", $"[Confidential] {fileName}");
```

All existing call sites in `CrmImportService` that call `ErpPath` or `ArchivePath` need updating to use the correct helper. The two `Look-up Customer Data` files currently pass through `ArchivePath` but use the same path format as ERP files — fix them to call `ArchivePath`.

---

## 2 — New Core models

Create in `backend/src/PortfolioThermometer.Core/Models/`:

### `Connection.cs`

```csharp
namespace PortfolioThermometer.Core.Models;

public class Connection
{
    public Guid Id { get; set; }
    public string CrmExternalId { get; set; } = string.Empty;   // ConnectionId from CSV
    public Guid? CustomerId { get; set; }                        // nullable — some connections have no customer link
    public Customer? Customer { get; set; }
    public string Ean { get; set; } = string.Empty;
    public string? ProductType { get; set; }    // "Electricity" | "Gas"
    public string? DeliveryType { get; set; }   // "LDN" | "ODN" | "NA"
    public int? ConnectionTypeId { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
}
```

### `MeterRead.cs`

```csharp
namespace PortfolioThermometer.Core.Models;

public class MeterRead
{
    public Guid Id { get; set; }
    public string CrmExternalId { get; set; } = string.Empty;   // UsageID (ConnectionMeterReads) or EANUniqueIdentifier+StartDate (Meter Read_1-8)
    public Guid? ConnectionId { get; set; }
    public Connection? Connection { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal? Consumption { get; set; }
    public string? Unit { get; set; }           // "kWh" | "m3"
    public string? UsageType { get; set; }      // "UsageHigh" | "UsageLow"
    public string? Direction { get; set; }      // "Consumption" | "Production"
    public string? Quality { get; set; }        // "Estimated" | "Measured" | "Customer" | "Actual"
    public string? Source { get; set; }         // "ConnectionMeterReads" | "MeterRead_1-8"
    public DateTimeOffset ImportedAt { get; set; }
}
```

---

## 3 — EF Core configurations

Create in `backend/src/PortfolioThermometer.Infrastructure/Data/Configurations/`:

### `ConnectionConfiguration.cs`

```csharp
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
```

### `MeterReadConfiguration.cs`

```csharp
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
        builder.Property(m => m.StartDate).HasColumnName("start_date");
        builder.Property(m => m.EndDate).HasColumnName("end_date");
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
```

---

## 4 — Update `AppDbContext`

Add the two new `DbSet` properties and apply the new configurations:

```csharp
public DbSet<Connection> Connections => Set<Connection>();
public DbSet<MeterRead> MeterReads => Set<MeterRead>();
```

In `OnModelCreating`:

```csharp
modelBuilder.ApplyConfiguration(new ConnectionConfiguration());
modelBuilder.ApplyConfiguration(new MeterReadConfiguration());
```

---

## 5 — EF Core migration

After updating the model, generate a new migration:

```
dotnet ef migrations add AddConnectionsAndMeterReads \
  --project backend/src/PortfolioThermometer.Infrastructure \
  --startup-project backend/src/PortfolioThermometer.Api
```

---

## 6 — Update `init.sql`

Append to `database/init.sql` so the raw SQL initialisation is consistent with the EF model:

```sql
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS connections (
    id                UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    crm_external_id   VARCHAR(100) NOT NULL,
    customer_id       UUID         REFERENCES customers (id) ON DELETE SET NULL,
    ean               VARCHAR(50)  NOT NULL,
    product_type      VARCHAR(20),   -- 'Electricity' | 'Gas'
    delivery_type     VARCHAR(10),   -- 'LDN' | 'ODN' | 'NA'
    connection_type_id INTEGER,
    imported_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_connections_crm_id UNIQUE (crm_external_id)
);

CREATE INDEX IF NOT EXISTS idx_connections_customer_id ON connections (customer_id);
CREATE INDEX IF NOT EXISTS idx_connections_ean         ON connections (ean);

-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS meter_reads (
    id               UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    crm_external_id  VARCHAR(200)  NOT NULL,
    connection_id    UUID          REFERENCES connections (id) ON DELETE SET NULL,
    start_date       DATE,
    end_date         DATE,
    consumption      DECIMAL(15,4),
    unit             VARCHAR(10),   -- 'kWh' | 'm3'
    usage_type       VARCHAR(20),   -- 'UsageHigh' | 'UsageLow'
    direction        VARCHAR(20),   -- 'Consumption' | 'Production'
    quality          VARCHAR(20),   -- 'Estimated' | 'Measured' | 'Customer' | 'Actual'
    source           VARCHAR(30),   -- 'ConnectionMeterReads' | 'MeterRead_1-8'
    imported_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_meter_reads_crm_id UNIQUE (crm_external_id)
);

CREATE INDEX IF NOT EXISTS idx_meter_reads_connection_id ON meter_reads (connection_id);
CREATE INDEX IF NOT EXISTS idx_meter_reads_start_date    ON meter_reads (start_date);
```

---

## 7 — Add import steps to `CrmImportService`

### Step A — Connections (insert after contracts, before interactions)

Source: `ERPSQLServer/[Confidential] Connections.csv`  
Column reference: `Connection` interface in `crm-schema.model.ts`

Key mapping:

| CSV column | Domain field | Notes |
|---|---|---|
| `ConnectionId` | `crm_external_id` | string |
| `EAN` | `ean` | skip row if blank |
| `ProductType` | `product_type` | "Electricity" \| "Gas" |
| `DeliveryType` | `delivery_type` | |
| `ConnectionTypeId` | `connection_type_id` | integer |

Link `customer_id` via `Contract-Customer-Connection-BrokerDebtor.csv` join rows already loaded: `ConnectionId → CustomerNumber → debtorRefToCustomerId`.

Build a `connectionIdToDbId` dictionary (string ConnectionId → Guid) to pass into the meter read step.

Skip rows with a blank `EAN`.

```csharp
private async Task<(int Count, Dictionary<string, Guid> ConnectionIdToDbId)> ImportConnectionsAsync(
    string crmDataPath,
    List<JoinRow> joinRows,
    Dictionary<string, Guid> debtorRefToCustomerId,
    CancellationToken ct)
```

### Step B — Meter reads (insert after connections)

Two sources, import both into the same `meter_reads` table:

#### Source 1 — `ERPSQLServer/[Confidential] ConnectionMeterReads.csv`

Column reference: `ConnectionMeterRead` interface in `crm-schema.model.ts`

| CSV column | Domain field | Notes |
|---|---|---|
| `UsageID` | `crm_external_id` | prefix `"CMR-"` to avoid collision with source 2 |
| `ConnectionId` | → `connection_id` | look up in `connectionIdToDbId`; skip row if not found |
| `StartDate` | `start_date` | |
| `EndDate` | `end_date` | `9999-12-31` → null |
| `Consumption` | `consumption` | decimal |
| `Unit` | `unit` | |
| `UsageType` | `usage_type` | |
| `Direction` | `direction` | |
| `Quality` | `quality` | |
| — | `source` | constant `"ConnectionMeterReads"` |

#### Source 2 — `ArchievingSolution/Generic/[Confidential] Meter Read_{n}.csv` (files 1–8)

Column reference: `MeterRead` interface in `crm-schema.model.ts`

| CSV column | Domain field | Notes |
|---|---|---|
| `EANUniqueIdentifier + StartDate` | `crm_external_id` | e.g. `"MR-{EAN}-{StartDate}"` |
| EAN → connections.ean | `connection_id` | look up connection by `ean`; skip if not found |
| `StartDate` | `start_date` | |
| `EndDate` | `end_date` | |
| `Consumption` | `consumption` | |
| — | `unit` | null (not in source) |
| `UsageType` | `usage_type` | |
| `Direction` | `direction` | |
| `Quality` | `quality` | |
| — | `source` | constant `"MeterRead_1-8"` |

Read each file line-by-line (they are large). Build the list of 8 paths with:

```csharp
var meterReadPaths = Enumerable.Range(1, 8)
    .Select(n => ArchiveGenericPath(crmDataPath, $"Meter Read_{n}.csv"))
    .Where(File.Exists)
    .ToArray();
```

#### EAN lookup map

Before importing meter reads, build a lookup from EAN string to connection DB id:

```csharp
var eanToConnectionId = await _db.Connections
    .Where(c => c.Ean != string.Empty)
    .ToDictionaryAsync(c => c.Ean, c => c.Id, ct);
```

### Wire up in `ImportAllAsync`

```csharp
var (connectionsImported, connectionIdToDbId) = await ImportConnectionsAsync(
    crmDataPath, joinRows, debtorRefToCustomerId, ct);

var meterReadsImported = await ImportMeterReadsAsync(
    crmDataPath, connectionIdToDbId, ct);
```

Update `ImportResult` to include the new counts (add `ConnectionsImported` and `MeterReadsImported` to the `ImportResult` record in `Core/Models/ImportResult.cs`).

---

## 8 — Tests to write first (TDD)

```
ImportConnectionsAsync_SkipsRowsWithBlankEan
ImportConnectionsAsync_LinksCustomerViaJoinTable
ImportConnectionsAsync_UpsertIsIdempotent
ImportMeterReads_ConnectionMeterReads_PrefixesCrmId
ImportMeterReads_MeterReadFiles_UsesEanToLookUpConnection
ImportMeterReads_SkipsRowWhenConnectionNotFound
ImportMeterReads_SkipsMissingFiles_NoException
PathHelpers_ErpPath_IncludesErpSqlServerSubdirectory
PathHelpers_ArchivePath_IncludesArchievingSolutionSubdirectory
```

---

## Implementation notes

- **Do not abort on missing Meter Read files**: files 1–8 may not all exist. Use `File.Exists` before reading.
- **Large files**: read Meter Read files with `StreamReader` line-by-line via `CsvReader.ReadMultipleAsync` (already accepts `IEnumerable<string>`).
- **Idempotent upserts**: connections use `crm_external_id` as the dedup key; meter reads use the prefixed `crm_external_id`. Skip rows whose key already exists.
- **FK nullability**: both `connection_id` (on meter_reads) and `customer_id` (on connections) are nullable — a connection without a matched customer, or a meter read without a matched connection, is still persisted.
- **No changes to risk scoring engine**: `RiskScoringEngine` does not need to be updated in this prompt — that is a separate concern (adding meter-based margin signals).
