using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Api.Common;
using PortfolioThermometer.Api.Controllers;
using PortfolioThermometer.Api.ViewModels;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;
using Xunit;

namespace PortfolioThermometer.Api.Tests;

public sealed class CustomersControllerConsumptionTests
{
    [Fact]
    public async Task GetCustomerConsumption_ReturnsNotFound_WhenCustomerDoesNotExist()
    {
        await using var db = CreateDbContext();
        var controller = new CustomersController(db);

        var result = await controller.GetCustomerConsumption(Guid.NewGuid(), null, null, null, CancellationToken.None);

        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var payload = notFound.Value.Should().BeOfType<ApiResponse<CustomerConsumptionVm>>().Subject;
        payload.Success.Should().BeFalse();
        payload.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetCustomerConsumption_ReturnsBadRequest_WhenFromIsAfterTo()
    {
        await using var db = CreateDbContext();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            CrmExternalId = "CUST-1",
            Name = "Acme",
            IsActive = true,
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var controller = new CustomersController(db);

        var result = await controller.GetCustomerConsumption(
            customer.Id,
            new DateOnly(2026, 4, 30),
            new DateOnly(2026, 4, 1),
            null,
            CancellationToken.None);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var payload = badRequest.Value.Should().BeOfType<ApiResponse<CustomerConsumptionVm>>().Subject;
        payload.Success.Should().BeFalse();
        payload.Error.Should().Contain("from");
    }

    [Fact]
    public async Task GetCustomerConsumption_AggregatesMonthlyConsumptionAndMixedQuality()
    {
        await using var db = CreateDbContext();
        var customerId = await SeedCustomerWithConsumptionAsync(db);
        var controller = new CustomersController(db);

        var result = await controller.GetCustomerConsumption(
            customerId,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 28),
            null,
            CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ApiResponse<CustomerConsumptionVm>>().Subject;
        payload.Success.Should().BeTrue();
        payload.Data.Should().NotBeNull();

        var data = payload.Data!;
        data.SelectedUnit.Should().Be("kWh");
        data.AvailableUnits.Should().BeEquivalentTo(["kWh", "m3"], options => options.WithStrictOrdering());
        data.Points.Should().HaveCount(2);

        var january = data.Points[0];
        january.Month.Should().Be("2026-01-01");
        january.Consumption.Should().Be(15m);
        january.Quality.Should().Be("Mixed");
        january.QualityBreakdown.Should().HaveCount(2);
        january.QualityBreakdown.Should().ContainSingle(q => q.Quality == "Estimated" && q.ReadCount == 1 && q.Consumption == 5m);
        january.QualityBreakdown.Should().ContainSingle(q => q.Quality == "Measured" && q.ReadCount == 1 && q.Consumption == 10m);

        var february = data.Points[1];
        february.Month.Should().Be("2026-02-01");
        february.Consumption.Should().Be(11m);
        february.Quality.Should().Be("Measured");
    }

    [Fact]
    public async Task GetCustomerConsumption_FiltersToRequestedUnit()
    {
        await using var db = CreateDbContext();
        var customerId = await SeedCustomerWithConsumptionAsync(db);
        var controller = new CustomersController(db);

        var result = await controller.GetCustomerConsumption(
            customerId,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 28),
            "m3",
            CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ApiResponse<CustomerConsumptionVm>>().Subject;
        payload.Data.Should().NotBeNull();
        var data = payload.Data!;

        data.SelectedUnit.Should().Be("m3");
        data.AvailableUnits.Should().BeEquivalentTo(["kWh", "m3"], options => options.WithStrictOrdering());
        data.Points.Should().HaveCount(1);
        data.Points[0].Month.Should().Be("2026-01-01");
        data.Points[0].Consumption.Should().Be(7m);
        data.Points[0].Quality.Should().Be("Estimated");
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"customers-consumption-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Guid> SeedCustomerWithConsumptionAsync(AppDbContext db)
    {
        var customerId = Guid.NewGuid();
        var electricityConnectionId = Guid.NewGuid();
        var gasConnectionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        db.Customers.Add(new Customer
        {
            Id = customerId,
            CrmExternalId = "CUST-1",
            Name = "Acme",
            IsActive = true,
            ImportedAt = now,
            UpdatedAt = now,
        });

        db.Connections.AddRange(
            new Connection
            {
                Id = electricityConnectionId,
                CrmExternalId = "CONN-ELEC",
                CustomerId = customerId,
                Ean = "541000000000000001",
                ProductType = "Electricity",
                ImportedAt = now,
            },
            new Connection
            {
                Id = gasConnectionId,
                CrmExternalId = "CONN-GAS",
                CustomerId = customerId,
                Ean = "541000000000000002",
                ProductType = "Gas",
                ImportedAt = now,
            });

        db.MeterReads.AddRange(
            CreateMeterRead(electricityConnectionId, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), 10m, "kWh", "Measured"),
            CreateMeterRead(electricityConnectionId, new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero), 5m, "kWh", "Estimated"),
            CreateMeterRead(electricityConnectionId, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), 11m, "kWh", "Measured"),
            CreateMeterRead(gasConnectionId, new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero), 7m, "m3", "Estimated"),
            CreateMeterRead(gasConnectionId, new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero), 4m, "m3", "Estimated", direction: "Production"),
            CreateMeterRead(Guid.NewGuid(), new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero), 99m, "kWh", "Measured"));

        await db.SaveChangesAsync();
        return customerId;
    }

    private static MeterRead CreateMeterRead(
        Guid connectionId,
        DateTimeOffset startDate,
        decimal consumption,
        string unit,
        string quality,
        string direction = "Consumption") =>
        new()
        {
            Id = Guid.NewGuid(),
            CrmExternalId = $"READ-{Guid.NewGuid():N}",
            ConnectionId = connectionId,
            StartDate = startDate,
            EndDate = startDate.AddDays(1),
            Consumption = consumption,
            Unit = unit,
            UsageType = "UsageHigh",
            Direction = direction,
            Quality = quality,
            Source = "Test",
            ImportedAt = DateTimeOffset.UtcNow,
        };
}