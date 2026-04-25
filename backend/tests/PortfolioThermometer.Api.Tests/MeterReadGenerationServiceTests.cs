using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;
using PortfolioThermometer.Infrastructure.Services;
using Xunit;

namespace PortfolioThermometer.Api.Tests;

public sealed class MeterReadGenerationServiceTests
{
    [Fact]
    public async Task GenerateYearlyAsync_GeneratesRowsForEligibleCustomersAndSkipsInvalidSelections()
    {
        await using var db = CreateDbContext();
        var year = 2025;
        var missingCustomerId = Guid.NewGuid();
        var electricityCustomerOne = CreateCustomer();
        var electricityCustomerTwo = CreateCustomer();
        var gasOnlyCustomer = CreateCustomer();

        db.Customers.AddRange(electricityCustomerOne, electricityCustomerTwo, gasOnlyCustomer);
        db.Connections.AddRange(
            CreateConnection(electricityCustomerOne.Id, "Electricity"),
            CreateConnection(electricityCustomerTwo.Id, "Electricity"),
            CreateConnection(gasOnlyCustomer.Id, "Gas"));
        await db.SaveChangesAsync();

        var service = new MeterReadGenerationService(db);

        var response = await service.GenerateYearlyAsync(
            new GenerateYearlyMeterReadsRequest(
                [electricityCustomerOne.Id, electricityCustomerTwo.Id, gasOnlyCustomer.Id, missingCustomerId],
                year,
                50),
            CancellationToken.None);

        response.Year.Should().Be(year);
        response.RequestedCustomerCount.Should().Be(4);
        response.EligibleCustomerCount.Should().Be(2);
        response.ProducerCustomerCount.Should().Be(1);
        response.ProcessedConnectionCount.Should().Be(2);
        response.ConsumptionRowsGenerated.Should().Be(17_520);
        response.ProductionRowsGenerated.Should().BeGreaterThan(0);
        response.ReducedConsumptionHourCount.Should().BeGreaterThan(0);
        response.SkippedCustomers.Should().BeEquivalentTo(
            new[]
            {
                new MeterReadGenerationSkippedCustomer(gasOnlyCustomer.Id, "No electricity connections found."),
                new MeterReadGenerationSkippedCustomer(missingCustomerId, "Customer not found."),
            },
            options => options.WithoutStrictOrdering());

        var producerCustomerId = response.ProducerCustomerIds.Should().ContainSingle().Subject;
        var producerConnectionId = await db.Connections
            .Where(connection => connection.CustomerId == producerCustomerId)
            .Select(connection => connection.Id)
            .SingleAsync();

        var noonRead = await db.MeterReads.SingleAsync(read =>
            read.ConnectionId == producerConnectionId &&
            read.Direction == "Production" &&
            read.StartDate == new DateTimeOffset(year, 6, 21, 12, 0, 0, TimeSpan.Zero));

        noonRead.Consumption.Should().BeGreaterThan(0m);

        var hasNightProduction = await db.MeterReads.AnyAsync(read =>
            read.ConnectionId == producerConnectionId &&
            read.Direction == "Production" &&
            read.StartDate == new DateTimeOffset(year, 6, 21, 2, 0, 0, TimeSpan.Zero));

        hasNightProduction.Should().BeFalse();

        var generatedRows = await db.MeterReads.CountAsync(read => read.Source == "GeneratedYearly");
        generatedRows.Should().Be(response.ConsumptionRowsGenerated + response.ProductionRowsGenerated);
    }

    [Fact]
    public async Task GenerateYearlyAsync_ReplacesGeneratedRowsWithoutRemovingImportedRows()
    {
        await using var db = CreateDbContext();
        var year = 2026;
        var customer = CreateCustomer();
        var connection = CreateConnection(customer.Id, "Electricity");
        var importedAt = DateTimeOffset.UtcNow;

        db.Customers.Add(customer);
        db.Connections.Add(connection);
        db.MeterReads.AddRange(
            new MeterRead
            {
                Id = Guid.NewGuid(),
                CrmExternalId = "OLD-GENERATED-ROW",
                ConnectionId = connection.Id,
                StartDate = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
                EndDate = new DateTimeOffset(year, 1, 1, 1, 0, 0, TimeSpan.Zero),
                Consumption = 1.5m,
                Unit = "kWh",
                UsageType = "UsageLow",
                Direction = "Consumption",
                Quality = "Estimated",
                Source = "GeneratedYearly",
                ImportedAt = importedAt,
            },
            new MeterRead
            {
                Id = Guid.NewGuid(),
                CrmExternalId = "IMPORTED-ROW",
                ConnectionId = connection.Id,
                StartDate = new DateTimeOffset(year - 1, 1, 1, 0, 0, 0, TimeSpan.Zero),
                EndDate = new DateTimeOffset(year - 1, 1, 1, 1, 0, 0, TimeSpan.Zero),
                Consumption = 5m,
                Unit = "kWh",
                UsageType = "UsageLow",
                Direction = "Consumption",
                Quality = "Measured",
                Source = "ConnectionMeterReads",
                ImportedAt = importedAt,
            });
        await db.SaveChangesAsync();

        var service = new MeterReadGenerationService(db);
        var request = new GenerateYearlyMeterReadsRequest([customer.Id], year, 0);

        var firstResponse = await service.GenerateYearlyAsync(request, CancellationToken.None);
        var generatedCountAfterFirstRun = await db.MeterReads.CountAsync(read => read.Source == "GeneratedYearly");

        generatedCountAfterFirstRun.Should().Be(firstResponse.ConsumptionRowsGenerated);
        (await db.MeterReads.AnyAsync(read => read.CrmExternalId == "OLD-GENERATED-ROW")).Should().BeFalse();
        (await db.MeterReads.AnyAsync(read => read.CrmExternalId == "IMPORTED-ROW")).Should().BeTrue();

        var secondResponse = await service.GenerateYearlyAsync(request, CancellationToken.None);
        var generatedCountAfterSecondRun = await db.MeterReads.CountAsync(read => read.Source == "GeneratedYearly");

        secondResponse.ConsumptionRowsGenerated.Should().Be(firstResponse.ConsumptionRowsGenerated);
        secondResponse.ProductionRowsGenerated.Should().Be(0);
        generatedCountAfterSecondRun.Should().Be(generatedCountAfterFirstRun);
        (await db.MeterReads.CountAsync(read => read.Source == "ConnectionMeterReads")).Should().Be(1);
    }

    [Fact]
    public async Task GenerateYearlyAsync_SkipsConnectionsThatAlreadyHaveImportedReadsForTheYear()
    {
        await using var db = CreateDbContext();
        var year = 2026;
        var customer = CreateCustomer();
        var connection = CreateConnection(customer.Id, "Electricity");

        db.Customers.Add(customer);
        db.Connections.Add(connection);
        db.MeterReads.Add(new MeterRead
        {
            Id = Guid.NewGuid(),
            CrmExternalId = "IMPORTED-ROW",
            ConnectionId = connection.Id,
            StartDate = new DateTimeOffset(year - 1, 12, 31, 23, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(year, 1, 1, 1, 0, 0, TimeSpan.Zero),
            Consumption = 5m,
            Unit = "kWh",
            UsageType = "UsageLow",
            Direction = "Consumption",
            Quality = "Measured",
            Source = "ConnectionMeterReads",
            ImportedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = new MeterReadGenerationService(db);

        var response = await service.GenerateYearlyAsync(
            new GenerateYearlyMeterReadsRequest([customer.Id], year, 0),
            CancellationToken.None);

        response.EligibleCustomerCount.Should().Be(0);
        response.ProcessedConnectionCount.Should().Be(0);
        response.ConsumptionRowsGenerated.Should().Be(0);
        response.ProductionRowsGenerated.Should().Be(0);
        response.SkippedCustomers.Should().ContainSingle(item =>
            item.CustomerId == customer.Id &&
            item.Reason == "Existing non-generated meter reads already cover the selected year.");
        (await db.MeterReads.CountAsync(read => read.Source == "GeneratedYearly")).Should().Be(0);
        (await db.MeterReads.CountAsync(read => read.Source == "ConnectionMeterReads")).Should().Be(1);
    }

    [Fact]
    public async Task GenerateYearlyAsync_UsesLeapYearHourCount()
    {
        await using var db = CreateDbContext();
        var customer = CreateCustomer();
        var connection = CreateConnection(customer.Id, "Electricity");

        db.Customers.Add(customer);
        db.Connections.Add(connection);
        await db.SaveChangesAsync();

        var service = new MeterReadGenerationService(db);

        var response = await service.GenerateYearlyAsync(
            new GenerateYearlyMeterReadsRequest([customer.Id], 2024, 0),
            CancellationToken.None);

        response.ConsumptionRowsGenerated.Should().Be(8_784);
        response.ProductionRowsGenerated.Should().Be(0);
        (await db.MeterReads.CountAsync(read => read.Source == "GeneratedYearly")).Should().Be(8_784);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"meter-generation-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static Customer CreateCustomer() =>
        new()
        {
            Id = Guid.NewGuid(),
            CrmExternalId = $"CUST-{Guid.NewGuid():N}",
            Name = "Customer",
            Segment = "company",
            IsActive = true,
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static Connection CreateConnection(Guid customerId, string productType) =>
        new()
        {
            Id = Guid.NewGuid(),
            CrmExternalId = $"CONN-{Guid.NewGuid():N}",
            CustomerId = customerId,
            Ean = $"541{Random.Shared.NextInt64(100_000_000_000_000L, 999_999_999_999_999L)}",
            ProductType = productType,
            DeliveryType = "LDN",
            ConnectionTypeId = 2,
            ImportedAt = DateTimeOffset.UtcNow,
        };
}
