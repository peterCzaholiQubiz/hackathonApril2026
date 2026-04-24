using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;
using PortfolioThermometer.Infrastructure.Services;
using System.Text;
using Xunit;

namespace PortfolioThermometer.Infrastructure.Tests.Services;

/// <summary>
/// Tests for CrmImportService — connections and meter reads import logic.
/// All tests drive behaviour through ImportAllAsync with minimal CSV fixtures.
/// </summary>
public sealed class CrmImportServiceTests : IDisposable
{
    private readonly string _tempDir;

    public CrmImportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Infrastructure helpers
    // ──────────────────────────────────────────────────────────────────────────

    private AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private CrmImportService CreateService(AppDbContext db)
        => new(db, NullLoggerFactory.Instance);

    /// <summary>
    /// Sets up the expected directory/file layout that CrmImportService uses:
    ///   {root}/ERPSQLServer/[Confidential] {fileName}
    ///   {root}/ArchievingSolution/[Confidential] {fileName}
    ///   {root}/ArchievingSolution/Generic/[Confidential] {fileName}
    /// </summary>
    private string ErpDir() => Path.Combine(_tempDir, "ERPSQLServer");
    private string ArchiveDir() => Path.Combine(_tempDir, "ArchievingSolution");
    private string ArchiveGenericDir() => Path.Combine(_tempDir, "ArchievingSolution", "Generic");

    private void WriteErpCsv(string fileName, string content)
    {
        Directory.CreateDirectory(ErpDir());
        File.WriteAllText(Path.Combine(ErpDir(), $"[Confidential] {fileName}"), content);
    }

    private void WriteArchiveCsv(string fileName, string content)
    {
        Directory.CreateDirectory(ArchiveDir());
        File.WriteAllText(Path.Combine(ArchiveDir(), $"[Confidential] {fileName}"), content);
    }

    private void WriteArchiveGenericCsv(string fileName, string content)
    {
        Directory.CreateDirectory(ArchiveGenericDir());
        File.WriteAllText(Path.Combine(ArchiveGenericDir(), $"[Confidential] {fileName}"), content);
    }

    /// <summary>Write the minimum set of ERP CSVs so ImportAllAsync can proceed without crashing.</summary>
    private void WriteMinimalErpFiles(
        string? extraOrgRows = null,
        string? extraJoinRows = null,
        string? extraContractRows = null,
        string? extraConnectionRows = null)
    {
        // Organizations
        WriteErpCsv("Organizations.csv",
            "OrganizationId,Name,OrganizationTypeId,DebtorReference,TransStartDate\n" +
            (extraOrgRows ?? "CUST-1,Acme Corp,2,DEB-1,2020-01-01\n"));

        // Join table
        WriteErpCsv("Contract-Customer-Connection-BrokerDebtor.csv",
            "ContractID,CustomerNumber,ConnectionId\n" +
            (extraJoinRows ?? "CONTRACT-1,DEB-1,CONN-1\n"));

        // Contracts
        WriteErpCsv("Contracts.csv",
            "ContractId,ContractType,StartDate,EndDate,CurrentAgreedAmount\n" +
            (extraContractRows ?? "CONTRACT-1,Fixed,2020-01-01,9999-12-31,500\n"));

        // Connections (may be overridden via extraConnectionRows or written separately)
        if (extraConnectionRows != null)
            WriteErpCsv("Connections.csv",
                "ConnectionId,EAN,ProductType,DeliveryType,ConnectionTypeId\n" +
                extraConnectionRows);
        else
            WriteErpCsv("Connections.csv",
                "ConnectionId,EAN,ProductType,DeliveryType,ConnectionTypeId\n" +
                "CONN-1,EAN-001,Electricity,LDN,1\n");

        // ConnectionMeterReads (empty by default)
        WriteErpCsv("ConnectionMeterReads.csv",
            "UsageID,ConnectionId,StartDate,EndDate,Consumption,Unit,UsageType,Direction,Quality\n");

        // Org contacts (empty)
        WriteErpCsv("OrganizationContacts.csv",
            "OrganizationContactId,OrganizationId,ContactDate,Subject,Report\n");

        // Connection contacts (empty)
        WriteErpCsv("ConnectionContacts.csv",
            "ConnectionContactId,ConnectionId,ContactDate,Subject,Report\n");

        // Invoice look-up files (empty)
        WriteArchiveCsv("Look-up Customer Data_1.csv",
            "Invoice number,Customer number,Amount\n");
        WriteArchiveCsv("Look-up Customer Data_2.csv",
            "Invoice number,Customer number,Amount\n");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Connection tests
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportConnectionsAsync_SkipsRowsWithBlankEan()
    {
        WriteMinimalErpFiles(
            extraConnectionRows:
                "CONN-NOEAN,,Electricity,LDN,1\n" +
                "CONN-1,EAN-001,Electricity,LDN,1\n");

        await using var db = CreateDb();
        var svc = CreateService(db);

        await svc.ImportAllAsync(_tempDir, CancellationToken.None);

        Assert.Equal(1, await db.Connections.CountAsync());
        Assert.Equal("EAN-001", (await db.Connections.FirstAsync()).Ean);
    }

    [Fact]
    public async Task ImportConnectionsAsync_LinksCustomerViaJoinTable()
    {
        WriteMinimalErpFiles(
            extraOrgRows: "CUST-1,Acme Corp,2,DEB-1,2020-01-01\n",
            extraJoinRows: "CONTRACT-1,DEB-1,CONN-1\n",
            extraConnectionRows: "CONN-1,EAN-001,Electricity,LDN,1\n");

        await using var db = CreateDb();
        var svc = CreateService(db);

        await svc.ImportAllAsync(_tempDir, CancellationToken.None);

        var conn = await db.Connections.FirstAsync();
        var customer = await db.Customers.FirstAsync();
        Assert.Equal(customer.Id, conn.CustomerId);
    }

    [Fact]
    public async Task ImportConnectionsAsync_UpsertIsIdempotent()
    {
        WriteMinimalErpFiles(
            extraConnectionRows: "CONN-1,EAN-001,Electricity,LDN,1\n");

        await using var db = CreateDb();
        var svc = CreateService(db);

        await svc.ImportAllAsync(_tempDir, CancellationToken.None);
        await svc.ImportAllAsync(_tempDir, CancellationToken.None);

        Assert.Equal(1, await db.Connections.CountAsync());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Meter read tests
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportMeterReads_ConnectionMeterReads_PrefixesCrmId()
    {
        WriteMinimalErpFiles();

        // Override ConnectionMeterReads with a real row
        WriteErpCsv("ConnectionMeterReads.csv",
            "UsageID,ConnectionId,StartDate,EndDate,Consumption,Unit,UsageType,Direction,Quality\n" +
            "USAGE-99,CONN-1,2023-01-01,2023-01-31,150.5,kWh,UsageHigh,Consumption,Measured\n");

        await using var db = CreateDb();
        var svc = CreateService(db);

        await svc.ImportAllAsync(_tempDir, CancellationToken.None);

        var mr = await db.MeterReads.FirstAsync();
        Assert.StartsWith("CMR-", mr.CrmExternalId);
        Assert.Equal("CMR-USAGE-99", mr.CrmExternalId);
    }

    [Fact]
    public async Task ImportMeterReads_ConnectionMeterReads_LoadsExistingIdsInBatches()
    {
        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] Organizations.csv"),
            "OrganizationId,Name,OrganizationTypeId,DebtorReference,TransStartDate\n" +
            "CUST-1,Acme Corp,2,DEB-1,2020-01-01\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] Contract-Customer-Connection-BrokerDebtor.csv"),
            "ContractID,CustomerNumber,ConnectionId\n" +
            "CONTRACT-1,DEB-1,CONN-1\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] Contracts.csv"),
            "ContractId,ContractType,StartDate,EndDate,CurrentAgreedAmount\n" +
            "CONTRACT-1,Fixed,2020-01-01,9999-12-31,500\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] Connections.csv"),
            "ConnectionId,EAN,ProductType,DeliveryType,ConnectionTypeId\n" +
            "CONN-1,EAN-001,Electricity,LDN,1\n");

        var csv = new StringBuilder()
            .AppendLine("UsageID,ConnectionId,StartDate,EndDate,Consumption,Unit,UsageType,Direction,Quality");

        for (var i = 0; i < 2505; i++)
        {
            csv.AppendLine($"USAGE-{i},CONN-1,2023-01-01,2023-01-31,150.5,kWh,UsageHigh,Consumption,Measured");
        }

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] ConnectionMeterReads.csv"), csv.ToString());

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] OrganizationContacts.csv"),
            "OrganizationContactId,OrganizationId,ContactDate,Subject,Report\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] ConnectionContacts.csv"),
            "ConnectionContactId,ConnectionId,ContactDate,Subject,Report\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] Look-up Customer Data_1.csv"),
            "Invoice number,Customer number,Amount\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] Look-up Customer Data_2.csv"),
            "Invoice number,Customer number,Amount\n");

        await using var db = CreateDb();
        db.MeterReads.Add(new MeterRead
        {
            Id = Guid.NewGuid(),
            CrmExternalId = "CMR-USAGE-1500",
            ImportedAt = DateTimeOffset.UtcNow,
            Source = "ConnectionMeterReads",
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);

        await svc.ImportAllAsync(_tempDir, CancellationToken.None);

        Assert.Equal(2505, await db.MeterReads.CountAsync());
        Assert.Equal(1, await db.MeterReads.CountAsync(m => m.CrmExternalId == "CMR-USAGE-1500"));
    }

    [Fact]
    public async Task ImportMeterReads_MeterReadFiles_UsesEanToLookUpConnection()
    {
        WriteMinimalErpFiles();

        // Meter Read_1 file with a row matching EAN-001
        WriteArchiveGenericCsv("Meter Read_1.csv",
            "EANUniqueIdentifier,StartDate,EndDate,Consumption,UsageType,Direction,Quality\n" +
            "EAN-001,2023-02-01,2023-02-28,200.0,UsageLow,Consumption,Estimated\n");

        await using var db = CreateDb();
        var svc = CreateService(db);

        await svc.ImportAllAsync(_tempDir, CancellationToken.None);

        var conn = await db.Connections.FirstAsync();
        var mr = await db.MeterReads.FirstAsync();
        Assert.Equal(conn.Id, mr.ConnectionId);
        Assert.Equal("MeterRead_1-8", mr.Source);
    }

    [Fact]
    public async Task ImportMeterReads_MeterReadFiles_SkipsAmbiguousDuplicateEanWithoutThrowing()
    {
        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] Organizations.csv"),
            "OrganizationId,Name,OrganizationTypeId,DebtorReference,TransStartDate\n" +
            "CUST-1,Acme Corp,2,DEB-1,2020-01-01\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] Contract-Customer-Connection-BrokerDebtor.csv"),
            "ContractID,CustomerNumber,ConnectionId\n" +
            "CONTRACT-1,DEB-1,CONN-1\n" +
            "CONTRACT-1,DEB-1,CONN-2\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] Contracts.csv"),
            "ContractId,ContractType,StartDate,EndDate,CurrentAgreedAmount\n" +
            "CONTRACT-1,Fixed,2020-01-01,9999-12-31,500\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] Connections.csv"),
            "ConnectionId,EAN,ProductType,DeliveryType,ConnectionTypeId\n" +
            "CONN-1,EAN-DUPE,Electricity,LDN,1\n" +
            "CONN-2,EAN-DUPE,Electricity,LDN,1\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] ConnectionMeterReads.csv"),
            "UsageID,ConnectionId,StartDate,EndDate,Consumption,Unit,UsageType,Direction,Quality\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] OrganizationContacts.csv"),
            "OrganizationContactId,OrganizationId,ContactDate,Subject,Report\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] ConnectionContacts.csv"),
            "ConnectionContactId,ConnectionId,ContactDate,Subject,Report\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] Look-up Customer Data_1.csv"),
            "Invoice number,Customer number,Amount\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] Look-up Customer Data_2.csv"),
            "Invoice number,Customer number,Amount\n");

        File.WriteAllText(Path.Combine(_tempDir, "[Confidential] Meter Read_1.csv"),
            "EANUniqueIdentifier,StartDate,EndDate,Consumption,UsageType,Direction,Quality\n" +
            "EAN-DUPE,2023-02-01,2023-02-28,200.0,UsageLow,Consumption,Estimated\n");

        await using var db = CreateDb();
        var svc = CreateService(db);

        var exception = await Record.ExceptionAsync(
            () => svc.ImportAllAsync(_tempDir, CancellationToken.None));

        Assert.Null(exception);
        Assert.Equal(2, await db.Connections.CountAsync());
        Assert.Empty(await db.MeterReads.ToListAsync());
    }

    [Fact]
    public async Task ImportMeterReads_SkipsRowWhenConnectionNotFound()
    {
        WriteMinimalErpFiles();

        // Override ConnectionMeterReads: row refers to a ConnectionId that won't be imported
        WriteErpCsv("ConnectionMeterReads.csv",
            "UsageID,ConnectionId,StartDate,EndDate,Consumption,Unit,UsageType,Direction,Quality\n" +
            "USAGE-X,CONN-UNKNOWN,2023-01-01,2023-01-31,50,kWh,UsageHigh,Consumption,Measured\n");

        await using var db = CreateDb();
        var svc = CreateService(db);

        await svc.ImportAllAsync(_tempDir, CancellationToken.None);

        Assert.Empty(await db.MeterReads.ToListAsync());
    }

    [Fact]
    public async Task ImportMeterReads_SkipsMissingFiles_NoException()
    {
        // No Meter Read_N files written — only the base files
        WriteMinimalErpFiles();

        await using var db = CreateDb();
        var svc = CreateService(db);

        // Should not throw
        var exception = await Record.ExceptionAsync(
            () => svc.ImportAllAsync(_tempDir, CancellationToken.None));

        Assert.Null(exception);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Path helper tests (indirect via directory structure)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PathHelpers_ErpPath_IncludesErpSqlServerSubdirectory()
    {
        // Place Organizations.csv only in ERPSQLServer — if ErpPath is wrong, import returns 0 customers.
        WriteMinimalErpFiles(extraOrgRows: "CUST-ERP,ERP Corp,2,DEB-ERP,2021-01-01\n");

        await using var db = CreateDb();
        var svc = CreateService(db);

        await svc.ImportAllAsync(_tempDir, CancellationToken.None);

        // Customer was imported → ErpPath correctly resolved ERPSQLServer subdirectory
        Assert.True(await db.Customers.AnyAsync(), "Customer should be imported via ERPSQLServer path");
    }

    [Fact]
    public async Task PathHelpers_ArchivePath_IncludesArchievingSolutionSubdirectory()
    {
        WriteMinimalErpFiles(extraOrgRows: "CUST-1,Acme Corp,2,DEB-1,2020-01-01\n");

        // Place an invoice row in the ArchievingSolution files
        WriteArchiveCsv("Look-up Customer Data_1.csv",
            "Invoice number,Customer number,Amount\n" +
            "INV-ARCHIVE-1,DEB-1,999\n");

        await using var db = CreateDb();
        var svc = CreateService(db);

        await svc.ImportAllAsync(_tempDir, CancellationToken.None);

        // Invoice was imported → ArchivePath correctly resolved ArchievingSolution subdirectory
        Assert.True(await db.Invoices.AnyAsync(), "Invoice should be imported via ArchievingSolution path");
    }
}
