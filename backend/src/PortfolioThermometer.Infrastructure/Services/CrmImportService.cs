using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Csv;
using PortfolioThermometer.Infrastructure.Data;
using System.Globalization;

namespace PortfolioThermometer.Infrastructure.Services;

public sealed class CrmImportService : ICrmImportService
{
    private const int CustomerImportLimit = 1000;
    private const int ExistingMeterReadLookupBatchSize = 1000;

    private readonly AppDbContext _db;
    private readonly ILogger _logger;

    public CrmImportService(AppDbContext db, ILoggerFactory loggerFactory)
    {
        _db = db;
        _logger = loggerFactory.CreateLogger<CrmImportService>();
    }

    public async Task<ImportResult> ImportAllAsync(string crmDataPath, CancellationToken ct)
    {
        _logger.LogInformation("Starting CRM import from {Path}", crmDataPath);

        var joinRows = await LoadJoinTableAsync(crmDataPath, ct);

        var (customersImported, orgIdToCustomerId, debtorRefToCustomerId) = await ImportCustomersAsync(crmDataPath, ct);

        var contractsImported = await ImportContractsAsync(crmDataPath, joinRows, debtorRefToCustomerId, ct);

        var (connectionsImported, connectionIdToDbId) = await ImportConnectionsAsync(
            crmDataPath, joinRows, debtorRefToCustomerId, ct);

        var interactionsImported = await ImportInteractionsAsync(crmDataPath, joinRows, orgIdToCustomerId, debtorRefToCustomerId, ct);

        var invoicesImported = await ImportInvoicesAsync(crmDataPath, debtorRefToCustomerId, ct);

        _logger.LogInformation(
            "CRM import complete. Customers={C}, Contracts={Co}, Interactions={I}, Invoices={Inv}, Connections={Cn}",
            customersImported, contractsImported, interactionsImported, invoicesImported, connectionsImported);

        return new ImportResult(
            customersImported,
            contractsImported,
            invoicesImported,
            PaymentsImported: 0,
            ComplaintsImported: 0,
            interactionsImported,
            ConnectionsImported: connectionsImported,
            DateTimeOffset.UtcNow);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Step 1: Join table — ContractID → CustomerNumber, ConnectionId → CustomerNumber
    // ──────────────────────────────────────────────────────────────────────────

    private sealed record JoinRow(string ContractId, string CustomerNumber, string ConnectionId);

    private async Task<List<JoinRow>> LoadJoinTableAsync(string crmDataPath, CancellationToken ct)
    {
        var path = ErpPath(crmDataPath, "Contract-Customer-Connection-BrokerDebtor.csv");
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

    private async Task<(int Count, Dictionary<string, Guid> OrgIdToCustomerId, Dictionary<string, Guid> DebtorRefToCustomerId)> ImportCustomersAsync(
        string crmDataPath, CancellationToken ct)
    {
        var path = ErpPath(crmDataPath, "Organizations.csv");
        _logger.LogInformation("Importing customers from Organizations.csv...");

        var rows = await CsvReader.ReadAllAsync(path, ct);

        var customerRows = rows
            .Where(r => Get(r, "OrganizationTypeId") == "2")
            .Take(CustomerImportLimit)
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
        var debtorRefToCustomerId = new Dictionary<string, Guid>(incomingIds.Count);

        foreach (var row in customerRows)
        {
            var orgId = Get(row, "OrganizationId");
            if (string.IsNullOrEmpty(orgId))
                continue;

            var debtorRef = Get(row, "DebtorReference");
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
                if (!string.IsNullOrEmpty(debtorRef))
                    debtorRefToCustomerId[debtorRef] = customer.Id;
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
                if (!string.IsNullOrEmpty(debtorRef))
                    debtorRefToCustomerId[debtorRef] = c.Id;
                added++;
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Upserted {Total} customers ({New} new).", orgIdToCustomerId.Count, added);

        return (added, orgIdToCustomerId, debtorRefToCustomerId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Step 3: Contracts from Contracts.csv, linked via join table
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<int> ImportContractsAsync(
        string crmDataPath,
        List<JoinRow> joinRows,
        Dictionary<string, Guid> debtorRefToCustomerId,
        CancellationToken ct)
    {
        var path = ErpPath(crmDataPath, "Contracts.csv");
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

            if (!debtorRefToCustomerId.TryGetValue(customerNumber, out var customerId))
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
        string crmDataPath,
        List<JoinRow> joinRows,
        Dictionary<string, Guid> orgIdToCustomerId,
        Dictionary<string, Guid> debtorRefToCustomerId,
        CancellationToken ct)
    {
        int added = 0;

        // ConnectionId → CustomerNumber (to resolve connection contacts → customer)
        var connectionToCustomerNumber = joinRows
            .Where(j => !string.IsNullOrEmpty(j.ConnectionId))
            .GroupBy(j => j.ConnectionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CustomerNumber, StringComparer.OrdinalIgnoreCase);

        added += await ImportOrgContactsAsync(crmDataPath, orgIdToCustomerId, ct);
        added += await ImportConnectionContactsAsync(crmDataPath, connectionToCustomerNumber, debtorRefToCustomerId, ct);

        _logger.LogInformation("Imported {Count} interactions total.", added);
        return added;
    }

    private async Task<int> ImportOrgContactsAsync(
        string crmDataPath,
        Dictionary<string, Guid> orgIdToCustomerId,
        CancellationToken ct)
    {
        var path = ErpPath(crmDataPath, "OrganizationContacts.csv");
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
        string crmDataPath,
        Dictionary<string, string> connectionToCustomerNumber,
        Dictionary<string, Guid> debtorRefToCustomerId,
        CancellationToken ct)
    {
        var path = ErpPath(crmDataPath, "ConnectionContacts.csv");
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

            if (!debtorRefToCustomerId.TryGetValue(customerNumber, out var customerId))
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
        string crmDataPath,
        Dictionary<string, Guid> debtorRefToCustomerId,
        CancellationToken ct)
    {
        var paths = new[]
        {
            ArchivePath(crmDataPath, "Look-up Customer Data_1.csv"),
            ArchivePath(crmDataPath, "Look-up Customer Data_2.csv"),
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

            existing.Add(invoiceNumber);

            var customerNumber = Get(row, "Customer number");
            if (!debtorRefToCustomerId.TryGetValue(customerNumber, out var customerId))
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
    // Step 6: Connections from Connections.csv
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<(int Count, Dictionary<string, Guid> ConnectionIdToDbId)> ImportConnectionsAsync(
        string crmDataPath,
        List<JoinRow> joinRows,
        Dictionary<string, Guid> debtorRefToCustomerId,
        CancellationToken ct)
    {
        var path = ErpPath(crmDataPath, "Connections.csv");
        _logger.LogInformation("Importing connections from Connections.csv...");

        var rows = await CsvReader.ReadAllAsync(path, ct);

        // ConnectionId → CustomerNumber from join table
        var connectionToCustomerNumber = joinRows
            .Where(j => !string.IsNullOrEmpty(j.ConnectionId))
            .GroupBy(j => j.ConnectionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CustomerNumber, StringComparer.OrdinalIgnoreCase);

        var incomingIds = rows
            .Select(r => Get(r, "ConnectionId"))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet();

        var existing = await _db.Connections
            .Where(c => incomingIds.Contains(c.CrmExternalId))
            .ToDictionaryAsync(c => c.CrmExternalId, ct);

        var now = DateTimeOffset.UtcNow;
        int added = 0;
        var connectionIdToDbId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var connectionId = Get(row, "ConnectionId");
            if (string.IsNullOrEmpty(connectionId))
                continue;

            var ean = Get(row, "EAN");
            if (string.IsNullOrEmpty(ean))
                continue; // skip rows with blank EAN

            Guid? customerId = null;
            if (connectionToCustomerNumber.TryGetValue(connectionId, out var custNum) &&
                debtorRefToCustomerId.TryGetValue(custNum, out var resolvedCustomerId))
            {
                customerId = resolvedCustomerId;
            }

            if (existing.TryGetValue(connectionId, out var conn))
            {
                conn.Ean = ean;
                conn.ProductType = Get(row, "ProductType");
                conn.DeliveryType = Get(row, "DeliveryType");
                var connTypeStr = Get(row, "ConnectionTypeId");
                conn.ConnectionTypeId = int.TryParse(connTypeStr, out var ct2) ? ct2 : (int?)null;
                conn.CustomerId = customerId;
                connectionIdToDbId[connectionId] = conn.Id;
            }
            else
            {
                var newConn = new Connection
                {
                    Id = Guid.NewGuid(),
                    CrmExternalId = connectionId,
                    Ean = ean,
                    ProductType = NullIfEmpty(Get(row, "ProductType")),
                    DeliveryType = NullIfEmpty(Get(row, "DeliveryType")),
                    ConnectionTypeId = int.TryParse(Get(row, "ConnectionTypeId"), out var ctId) ? ctId : (int?)null,
                    CustomerId = customerId,
                    ImportedAt = now,
                };
                _db.Connections.Add(newConn);
                connectionIdToDbId[connectionId] = newConn.Id;
                added++;
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Upserted {Total} connections ({New} new).", connectionIdToDbId.Count, added);
        return (added, connectionIdToDbId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Step 7: Meter reads from ConnectionMeterReads.csv + Meter Read_1-8 files
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<int> ImportMeterReadsAsync(
        string crmDataPath,
        Dictionary<string, Guid> connectionIdToDbId,
        CancellationToken ct)
    {
        int added = 0;
        var now = DateTimeOffset.UtcNow;

        // Build EAN → connection DB id lookup, skipping ambiguous EANs that map to multiple connections.
        var connectionsWithEan = await _db.Connections
            .Where(c => c.Ean != string.Empty)
            .Select(c => new { c.Ean, c.Id })
            .ToListAsync(ct);

        var connectionGroupsByEan = connectionsWithEan
            .GroupBy(c => c.Ean, StringComparer.Ordinal)
            .ToList();

        var ambiguousEans = connectionGroupsByEan
            .Where(g => g.Skip(1).Any())
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (ambiguousEans.Count > 0)
        {
            _logger.LogWarning(
                "Skipping meter reads for {Count} ambiguous EAN values because they map to multiple connections.",
                ambiguousEans.Count);
        }

        var eanToConnectionId = connectionGroupsByEan
            .Where(g => !ambiguousEans.Contains(g.Key))
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        // Source 1: ConnectionMeterReads.csv
        var cmrPath = ErpPath(crmDataPath, "ConnectionMeterReads.csv");
        _logger.LogInformation("Importing meter reads from ConnectionMeterReads.csv...");

        var cmrRows = await CsvReader.ReadAllAsync(cmrPath, ct);

        var cmrIncomingIds = cmrRows
            .Select(r => $"CMR-{Get(r, "UsageID")}")
            .Where(id => id != "CMR-")
            .ToArray();

        var cmrExisting = await LoadExistingMeterReadIdsAsync(cmrIncomingIds, ct);

        foreach (var row in cmrRows)
        {
            var usageId = Get(row, "UsageID");
            if (string.IsNullOrEmpty(usageId))
                continue;

            var crmId = $"CMR-{usageId}";
            if (cmrExisting.Contains(crmId))
                continue;

            var connectionStrId = Get(row, "ConnectionId");
            if (!connectionIdToDbId.TryGetValue(connectionStrId, out var connectionDbId))
                continue; // skip if connection not found

            var endDateRaw = Get(row, "EndDate");
            var endDate = IsOpenEndedDate(endDateRaw) ? (DateTimeOffset?)null : DateOnlyToOffset(ParseDateOnly(endDateRaw));

            _db.MeterReads.Add(new MeterRead
            {
                Id = Guid.NewGuid(),
                CrmExternalId = crmId,
                ConnectionId = connectionDbId,
                StartDate = DateOnlyToOffset(ParseDateOnly(Get(row, "StartDate"))),
                EndDate = endDate,
                Consumption = ParseDecimal(Get(row, "Consumption")),
                Unit = NullIfEmpty(Get(row, "Unit")),
                UsageType = NullIfEmpty(Get(row, "UsageType")),
                Direction = NullIfEmpty(Get(row, "Direction")),
                Quality = NullIfEmpty(Get(row, "Quality")),
                Source = "ConnectionMeterReads",
                ImportedAt = now,
            });
            cmrExisting.Add(crmId);
            added++;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Imported {Count} meter reads from ConnectionMeterReads.", added);

        // Source 2: Meter Read_1 through _8 (may not all exist)
        var meterReadPaths = Enumerable.Range(1, 8)
            .Select(n => ArchiveGenericPath(crmDataPath, $"Meter Read_{n}.csv"))
            .Where(File.Exists)
            .ToArray();

        var skippedAmbiguousMeterReads = 0;

        foreach (var meterReadPath in meterReadPaths)
        {
            _logger.LogInformation("Importing meter reads from {File}...", meterReadPath);
            var mrRows = await CsvReader.ReadAllAsync(meterReadPath, ct);
            var mrExistingIds = await LoadExistingMeterReadIdsAsync(
                mrRows.Select(row =>
                {
                    var ean = Get(row, "EANUniqueIdentifier");
                    var startDateRaw = Get(row, "StartDate");
                    return string.IsNullOrEmpty(ean) || string.IsNullOrEmpty(startDateRaw)
                        ? string.Empty
                        : $"MR-{ean}-{startDateRaw}";
                }),
                ct);

            foreach (var row in mrRows)
            {
                var ean = Get(row, "EANUniqueIdentifier");
                var startDateRaw = Get(row, "StartDate");
                if (string.IsNullOrEmpty(ean) || string.IsNullOrEmpty(startDateRaw))
                    continue;

                var crmId = $"MR-{ean}-{startDateRaw}";

                if (mrExistingIds.Contains(crmId))
                    continue;

                if (ambiguousEans.Contains(ean))
                {
                    skippedAmbiguousMeterReads++;
                    continue;
                }

                if (!eanToConnectionId.TryGetValue(ean, out var connectionDbId))
                    continue; // skip if EAN not found

                _db.MeterReads.Add(new MeterRead
                {
                    Id = Guid.NewGuid(),
                    CrmExternalId = crmId,
                    ConnectionId = connectionDbId,
                    StartDate = DateOnlyToOffset(ParseDateOnly(startDateRaw)),
                    EndDate = DateOnlyToOffset(ParseDateOnly(Get(row, "EndDate"))),
                    Consumption = ParseDecimal(Get(row, "Consumption")),
                    Unit = null,
                    UsageType = NullIfEmpty(Get(row, "UsageType")),
                    Direction = NullIfEmpty(Get(row, "Direction")),
                    Quality = NullIfEmpty(Get(row, "Quality")),
                    Source = "MeterRead_1-8",
                    ImportedAt = now,
                });
                mrExistingIds.Add(crmId);
                added++;
            }

            await _db.SaveChangesAsync(ct);
        }

        if (skippedAmbiguousMeterReads > 0)
        {
            _logger.LogWarning(
                "Skipped {Count} meter reads from Meter Read_1-8 because the EAN matched multiple connections.",
                skippedAmbiguousMeterReads);
        }

        _logger.LogInformation("Imported {Count} total meter reads.", added);
        return added;
    }

    private async Task<HashSet<string>> LoadExistingMeterReadIdsAsync(
        IEnumerable<string> crmExternalIds,
        CancellationToken ct)
    {
        var distinctIds = crmExternalIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var existingIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var batch in distinctIds.Chunk(ExistingMeterReadLookupBatchSize))
        {
            var batchIds = batch;
            var existingBatch = await _db.MeterReads
                .AsNoTracking()
                .Where(m => batchIds.Contains(m.CrmExternalId))
                .Select(m => m.CrmExternalId)
                .ToListAsync(ct);

            existingIds.UnionWith(existingBatch);
        }

        return existingIds;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static string ErpPath(string crmDataPath, string fileName)
        => Path.Combine(crmDataPath, $"[Confidential] {fileName}");

    private static string ArchivePath(string crmDataPath, string fileName)
        => Path.Combine(crmDataPath, $"[Confidential] {fileName}");

    private static string ArchiveGenericPath(string crmDataPath, string fileName)
        => Path.Combine(crmDataPath, $"[Confidential] {fileName}");

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

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

        // CSV exports include a time component (e.g. "2006-02-09 22:15:29.280")
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return DateOnly.FromDateTime(dt);

        return null;
    }

    private static bool IsOpenEndedDate(string? raw)
        => raw is not null && (raw.StartsWith("9999-12-31") || raw == "99991231");

    private static DateTimeOffset? DateOnlyToOffset(DateOnly? d)
        => d.HasValue ? new DateTimeOffset(d.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;

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
