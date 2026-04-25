using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioThermometer.Infrastructure.Data;
using PortfolioThermometer.Infrastructure.Services;
using Xunit;

namespace PortfolioThermometer.Infrastructure.Tests.Services;

public sealed class CrmSampleDatasetTests
{
    [Fact]
    public async Task Sample100Dataset_ImportsLinkedData()
    {
        await using var db = CreateDb();
        var service = new CrmImportService(db, NullLoggerFactory.Instance);

        var samplePath = Path.Combine(FindRepoRoot(), "crm-data", "Sample-100");

        Directory.Exists(samplePath).Should().BeTrue("the reduced CRM sample should be generated before validation");

        var result = await service.ImportAllAsync(samplePath, CancellationToken.None);

        var importedCustomers = await db.Customers.CountAsync();
        var importedContracts = await db.Contracts.CountAsync();
        var importedInvoices = await db.Invoices.CountAsync();
        var importedInteractions = await db.Interactions.CountAsync();
        var importedConnections = await db.Connections.CountAsync();

        importedCustomers.Should().Be(100);
        importedContracts.Should().BeGreaterThan(0);
        importedInvoices.Should().BeGreaterThan(0);
        importedInteractions.Should().BeGreaterThan(0);
        importedConnections.Should().BeGreaterThan(0);

        result.CustomersImported.Should().Be(100);
        result.ContractsImported.Should().BeGreaterThan(0);
        result.InvoicesImported.Should().BeGreaterThan(0);
        result.InteractionsImported.Should().BeGreaterThan(0);
        result.ConnectionsImported.Should().BeGreaterThan(0);

        importedCustomers.Should().Be(result.CustomersImported);
        importedContracts.Should().Be(result.ContractsImported);
        importedInvoices.Should().Be(result.InvoicesImported);
        importedInteractions.Should().Be(result.InteractionsImported);
        importedConnections.Should().Be(result.ConnectionsImported);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "README.md")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root from the test output directory.");
    }
}
