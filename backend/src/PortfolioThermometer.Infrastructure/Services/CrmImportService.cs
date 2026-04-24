using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Csv;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Infrastructure.Services;

public sealed class CrmImportService : ICrmImportService
{
    private readonly AppDbContext _db;
    private readonly ILogger _logger;
    private readonly string _crmDataPath;

    public CrmImportService(AppDbContext db, ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _db = db;
        _logger = loggerFactory.CreateLogger<CrmImportService>();
        _crmDataPath = configuration["CrmDataPath"] ?? "/data/crm-data";
    }

    public async Task<ImportResult> ImportAllAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting CRM import from {Path}", _crmDataPath);

        var joinRows = await LoadJoinTableAsync(ct);

        var (customersImported, orgIdToCustomerId) = await ImportCustomersAsync(ct);

        var contractsImported = await ImportContractsAsync(joinRows, orgIdToCustomerId, ct);

        var interactionsImported = await ImportInteractionsAsync(joinRows, orgIdToCustomerId, ct);

        var invoicesImported = await ImportInvoicesAsync(orgIdToCustomerId, ct);

        _logger.LogInformation(
            "CRM import complete. Customers={C}, Contracts={Co}, Interactions={I}, Invoices={Inv}",
            customersImported, contractsImported, interactionsImported, invoicesImported);

        return new ImportResult(
            customersImported,
            contractsImported,
            invoicesImported,
            PaymentsImported: 0,
            ComplaintsImported: 0,
            interactionsImported,
            DateTimeOffset.UtcNow);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Step 1: Join table — ContractID → CustomerNumber, ConnectionId → CustomerNumber
    // ──────────────────────────────────────────────────────────────────────────

    private sealed record JoinRow(string ContractId, string CustomerNumber, string ConnectionId);

    private async Task<List<JoinRow>> LoadJoinTableAsync(CancellationToken ct)
    {
        var path = ErpPath("Contract-Customer-Connection-BrokerDebtor.csv");
        _logger.LogInformation("Loading join table from {File}...", path);

        var rows = await CsvReader.ReadAllAsync(path, ct);

        return rows
            .Select(r => new JoinRow(
                Get(r, "ContractID"),
                Get(r, "CustomerNumber"),
                Get(r, "ConnectionId")))
            .Where(r => !string.IsNullOrEmpty(r.ContractId) && !string.IsNullOrEmpty(r.CustomerNumber))
            .ToList();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Step 2: Customers from Organizations.csv (OrganizationTypeId == "2")
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<(int Count, Dictionary<string, Guid> OrgIdToCustomerId)> ImportCustomersAsync(
        CancellationToken ct)
    {
        var path = ErpPath("Organizations.csv");
        _logger.LogInformation("Importing customers from Organizations.csv...");

        var rows = await CsvReader.ReadAllAsync(path, ct);

        var customerRows = rows
            .Where(r => Get(r, "OrganizationTypeId") == "2")
            .ToList();

        var incomingIds = customerRows
            .Select(r => Get(r, "OrganizationId"))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet();

        var existing = await _db.Customers
            .Where(c => incomingIds.Contains(c.CrmExternalId))
            .ToDictionaryAsync(c => c.CrmExternalId, ct);

        var now = DateTimeOffset.UtcNow;
        int added = 0;
        var orgIdToCustomerId = new Dictionary<string, Guid>(incomingIds.Count);

        foreach (var row in customerRows)
        {
            var orgId = Get(row, "OrganizationId");
            if (string.IsNullOrEmpty(orgId))
                continue;

            var name = Get(row, "Name", orgId);
            var segment = MapSegment(Get(row, "OrganizationTypeId"));
            var onboarding = ParseDateOnly(Get(row, "TransStartDate"));

            if (existing.TryGetValue(orgId, out var customer))
            {
                customer.Name = name;
                customer.CompanyName = name;
                customer.Segment = segment;
                customer.OnboardingDate = onboarding;
                customer.UpdatedAt = now;
                orgIdToCustomerId[orgId] = customer.Id;
            }
            else
            {
                var c = new Customer
                {
                    Id = Guid.NewGuid(),
                    CrmExternalId = orgId,
                    Name = name,
                    CompanyName = name,
                    Segment = segment,
                    OnboardingDate = onboarding,
                    IsActive = true,
                    ImportedAt = now,
                    UpdatedAt = now,
                };
                _db.Customers.Add(c);
                orgIdToCustomerId[orgId] = c.Id;
                added++;
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Upserted {Total} customers ({New} new).", orgIdToCustomerId.Count, added);

        return (added, orgIdToCustomerId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Step 3: Contracts from Contracts.csv, linked via join table
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<int> ImportContractsAsync(
        List<JoinRow> joinRows,
        Dictionary<string, Guid> orgIdToCustomerId,
        CancellationToken ct)
    {
        var path = ErpPath("Contracts.csv");
        _logger.LogInformation("Importing contracts from Contracts.csv...");

        var rows = await CsvReader.ReadAllAsync(path, ct);

        // ContractID → CustomerNumber from join table
        var contractToCustomerNumber = joinRows
            .GroupBy(j => j.ContractId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CustomerNumber, StringComparer.OrdinalIgnoreCase);

        var incomingIds = rows
            .Select(r => Get(r, "ContractId"))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet();

        var existing = await _db.Contracts
            .Where(c => incomingIds.Contains(c.CrmExternalId))
            .ToDictionaryAsync(c => c.CrmExternalId, ct);

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.Today);
        int added = 0;

        foreach (var row in rows)
        {
            var contractId = Get(row, "ContractId");
            if (string.IsNullOrEmpty(contractId))
                continue;

            if (!contractToCustomerNumber.TryGetValue(contractId, out var customerNumber))
                continue; // not linked to any customer

            if (!orgIdToCustomerId.TryGetValue(customerNumber, out var customerId))
                continue; // customer not found

            var endDateRaw = Get(row, "EndDate");
            var endDate = IsOpenEndedDate(endDateRaw) ? (DateOnly?)null : ParseDateOnly(endDateRaw);
            var startDate = ParseDateOnly(Get(row, "StartDate"));
            var status = endDate.HasValue && endDate.Value < today ? "expired" : "active";
            var monthlyValue = ParseDecimal(Get(row, "CurrentAgreedAmount"));

            if (existing.TryGetValue(contractId, out var contract))
            {
                contract.ContractType = Get(row, "ContractType");
                contract.StartDate = startDate;
                contract.EndDate = endDate;
                contract.MonthlyValue = monthlyValue;
                contract.Status = status;
            }
            else
            {
                _db.Contracts.Add(new Contract
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    CrmExternalId = contractId,
                    ContractType = Get(row, "ContractType"),
                    StartDate = startDate,
                    EndDate = endDate,
                    MonthlyValue = monthlyValue,
                    Currency = "EUR",
                    Status = status,
                    AutoRenew = false,
                    ImportedAt = now,
                });
                added++;
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Upserted contracts ({New} new).", added);
        return added;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Step 4: Interactions from OrganizationContacts + ConnectionContacts
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<int> ImportInteractionsAsync(
        List<JoinRow> joinRows,
        Dictionary<string, Guid> orgIdToCustomerId,
        CancellationToken ct)
    {
        int added = 0;

        // ConnectionId → CustomerNumber (to resolve connection contacts → customer)
        var connectionToCustomerNumber = joinRows
            .Where(j => !string.IsNullOrEmpty(j.ConnectionId))
            .GroupBy(j => j.ConnectionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CustomerNumber, StringComparer.OrdinalIgnoreCase);

        added += await ImportOrgContactsAsync(orgIdToCustomerId, ct);
        added += await ImportConnectionContactsAsync(connectionToCustomerNumber, orgIdToCustomerId, ct);

        _logger.LogInformation("Imported {Count} interactions total.", added);
        return added;
    }

    private async Task<int> ImportOrgContactsAsync(
        Dictionary<string, Guid> orgIdToCustomerId,
        CancellationToken ct)
    {
        var path = ErpPath("OrganizationContacts.csv");
        _logger.LogInformation("Importing interactions from OrganizationContacts.csv...");

        var rows = await CsvReader.ReadAllAsync(path, ct);

        var incomingIds = rows
            .Select(r => $"OC-{Get(r, "OrganizationContactId")}")
            .Where(id => id != "OC-")
            .ToHashSet();

        var existing = await _db.Interactions
            .Where(i => incomingIds.Contains(i.CrmExternalId))
            .Select(i => i.CrmExternalId)
            .ToHashSetAsync(ct);

        var now = DateTimeOffset.UtcNow;
        int added = 0;

        foreach (var row in rows)
        {
            var contactId = Get(row, "OrganizationContactId");
            if (string.IsNullOrEmpty(contactId))
                continue;

            var orgId = Get(row, "OrganizationId");
            if (!orgIdToCustomerId.TryGetValue(orgId, out var customerId))
                continue;

            var externalId = $"OC-{contactId}";
            if (existing.Contains(externalId))
                continue;

            _db.Interactions.Add(new Interaction
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                CrmExternalId = externalId,
                InteractionDate = ParseDateOnly(Get(row, "ContactDate")),
                Channel = Get(row, "Subject"),
                Direction = "inbound",
                Summary = Get(row, "Report"),
                Sentiment = null,
                ImportedAt = now,
            });
            added++;
        }

        await _db.SaveChangesAsync(ct);
        return added;
    }

    private async Task<int> ImportConnectionContactsAsync(
        Dictionary<string, string> connectionToCustomerNumber,
        Dictionary<string, Guid> orgIdToCustomerId,
        CancellationToken ct)
    {
        var path = ErpPath("ConnectionContacts.csv");
        _logger.LogInformation("Importing interactions from ConnectionContacts.csv...");

        var rows = await CsvReader.ReadAllAsync(path, ct);

        var incomingIds = rows
            .Select(r => $"CC-{Get(r, "ConnectionContactId")}")
            .Where(id => id != "CC-")
            .ToHashSet();

        var existing = await _db.Interactions
            .Where(i => incomingIds.Contains(i.CrmExternalId))
            .Select(i => i.CrmExternalId)
            .ToHashSetAsync(ct);

        var now = DateTimeOffset.UtcNow;
        int added = 0;

        foreach (var row in rows)
        {
            var contactId = Get(row, "ConnectionContactId");
            if (string.IsNullOrEmpty(contactId))
                continue;

            var connectionId = Get(row, "ConnectionId");
            if (!connectionToCustomerNumber.TryGetValue(connectionId, out var customerNumber))
                continue;

            if (!orgIdToCustomerId.TryGetValue(customerNumber, out var customerId))
                continue;

            var externalId = $"CC-{contactId}";
            if (existing.Contains(externalId))
                continue;

            _db.Interactions.Add(new Interaction
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                CrmExternalId = externalId,
                InteractionDate = ParseDateOnly(Get(row, "ContactDate")),
                Channel = Get(row, "Subject"),
                Direction = "inbound",
                Summary = Get(row, "Report"),
                Sentiment = null,
                ImportedAt = now,
            });
            added++;
        }

        await _db.SaveChangesAsync(ct);
        return added;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Step 5: Invoices from Look-up Customer Data files
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<int> ImportInvoicesAsync(
        Dictionary<string, Guid> orgIdToCustomerId,
        CancellationToken ct)
    {
        var paths = new[]
        {
            ArchivePath("Look-up Customer Data_1.csv"),
            ArchivePath("Look-up Customer Data_2.csv"),
        };

        _logger.LogInformation("Importing invoices from Look-up Customer Data files...");

        var rows = await CsvReader.ReadMultipleAsync(paths, ct);

        var incomingIds = rows
            .Select(r => Get(r, "Invoice number"))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet();

        var existing = await _db.Invoices
            .Where(i => incomingIds.Contains(i.CrmExternalId))
            .Select(i => i.CrmExternalId)
            .ToHashSetAsync(ct);

        var now = DateTimeOffset.UtcNow;
        int added = 0;

        foreach (var row in rows)
        {
            var invoiceNumber = Get(row, "Invoice number");
            if (string.IsNullOrEmpty(invoiceNumber))
                continue;

            if (existing.Contains(invoiceNumber))
                continue;

            var customerNumber = Get(row, "Customer number");
            if (!orgIdToCustomerId.TryGetValue(customerNumber, out var customerId))
                continue;

            _db.Invoices.Add(new Invoice
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                CrmExternalId = invoiceNumber,
                InvoiceNumber = invoiceNumber,
                Status = "unknown",
                Currency = "EUR",
                ImportedAt = now,
            });
            added++;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Imported {Count} invoices.", added);
        return added;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private string ErpPath(string fileName)
        => Path.Combine(_crmDataPath, "ERPSQLServer", $"[Confidential] {fileName}");

    private string ArchivePath(string fileName)
        => Path.Combine(_crmDataPath, "ArchievingSolution", $"[Confidential] {fileName}");

    private static string Get(Dictionary<string, string> row, string key, string fallback = "")
        => row.TryGetValue(key, out var val) ? val : fallback;

    private static DateOnly? ParseDateOnly(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || IsOpenEndedDate(raw))
            return null;

        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
            return d1;
        if (DateOnly.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
            return d2;
        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, out var d3))
            return d3;

        return null;
    }

    private static bool IsOpenEndedDate(string? raw)
        => raw is "9999-12-31" or "99991231";

    private static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        raw = raw.Replace(',', '.');

        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }

    private static string? MapSegment(string? orgTypeId) => orgTypeId switch
    {
        "2" => "customer",
        "5" => "collective",
        "6" => "company",
        _ => null,
    };
}
