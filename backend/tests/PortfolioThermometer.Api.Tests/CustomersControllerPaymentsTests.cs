using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using PortfolioThermometer.Api.Common;
using PortfolioThermometer.Api.Controllers;
using PortfolioThermometer.Api.ViewModels;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;
using Xunit;

namespace PortfolioThermometer.Api.Tests;

public sealed class CustomersControllerPaymentsTests
{
    [Fact]
    public async Task GetCustomerPayments_ReturnsNotFound_WhenCustomerDoesNotExist()
    {
        await using var db = CreateDbContext();
        var controller = new CustomersController(db, new Mock<IRiskScoringEngine>().Object, new Mock<IClaudeExplanationService>().Object);

        var result = await controller.GetCustomerPayments(Guid.NewGuid(), null, 1, 12, CancellationToken.None);

        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var payload = notFound.Value.Should().BeOfType<ApiResponse<CustomerPaymentsVm>>().Subject;
        payload.Success.Should().BeFalse();
        payload.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetCustomerPayments_ReturnsLatestPaymentsPagedAndSortedDescending()
    {
        await using var db = CreateDbContext();
        var customerId = await SeedCustomerWithPaymentsAsync(db);
        var controller = new CustomersController(db, new Mock<IRiskScoringEngine>().Object, new Mock<IClaudeExplanationService>().Object);

        var result = await controller.GetCustomerPayments(customerId, null, 1, 12, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ApiResponse<CustomerPaymentsVm>>().Subject;
        payload.Success.Should().BeTrue();
        payload.Meta.Should().NotBeNull();
        payload.Meta!.Page.Should().Be(1);
        payload.Meta.PageSize.Should().Be(12);
        payload.Meta.Total.Should().Be(15);

        payload.Data.Should().NotBeNull();
        var data = payload.Data!;
        data.ActiveSeverity.Should().BeNull();
        data.Summary.Low.Should().Be(5);
        data.Summary.Medium.Should().Be(4);
        data.Summary.High.Should().Be(6);
        data.Payments.Should().HaveCount(12);
        data.Payments.Select(p => p.PaymentDate).Should().BeInDescendingOrder();
        data.Payments[0].CrmExternalId.Should().Be("PAY-15");
        data.Payments[0].Severity.Should().Be("high");
        data.Payments[^1].CrmExternalId.Should().Be("PAY-4");
    }

    [Fact]
    public async Task GetCustomerPayments_FiltersBySeverity_AndKeepsAllTimeSummary()
    {
        await using var db = CreateDbContext();
        var customerId = await SeedCustomerWithPaymentsAsync(db);
        var controller = new CustomersController(db, new Mock<IRiskScoringEngine>().Object, new Mock<IClaudeExplanationService>().Object);

        var result = await controller.GetCustomerPayments(customerId, "medium", 1, 12, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ApiResponse<CustomerPaymentsVm>>().Subject;
        payload.Success.Should().BeTrue();
        payload.Meta.Should().NotBeNull();
        payload.Meta!.Total.Should().Be(4);

        payload.Data.Should().NotBeNull();
        var data = payload.Data!;
        data.ActiveSeverity.Should().Be("medium");
        data.Summary.Low.Should().Be(5);
        data.Summary.Medium.Should().Be(4);
        data.Summary.High.Should().Be(6);
        data.Payments.Should().HaveCount(4);
        data.Payments.Should().OnlyContain(p => p.Severity == "medium");
        data.Payments.Should().OnlyContain(p => p.DaysLate >= 16 && p.DaysLate <= 30);
    }

    [Fact]
    public async Task GetCustomerPayments_ReturnsBadRequest_WhenSeverityIsInvalid()
    {
        await using var db = CreateDbContext();
        var customerId = await SeedCustomerWithPaymentsAsync(db);
        var controller = new CustomersController(db, new Mock<IRiskScoringEngine>().Object, new Mock<IClaudeExplanationService>().Object);

        var result = await controller.GetCustomerPayments(customerId, "urgent", 1, 12, CancellationToken.None);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var payload = badRequest.Value.Should().BeOfType<ApiResponse<CustomerPaymentsVm>>().Subject;
        payload.Success.Should().BeFalse();
        payload.Error.Should().Contain("Severity");
    }

    [Fact]
    public async Task GetCustomerPayments_ClampsPageToAvailableRange()
    {
        await using var db = CreateDbContext();
        var customerId = await SeedCustomerWithPaymentsAsync(db);
        var controller = new CustomersController(db, new Mock<IRiskScoringEngine>().Object, new Mock<IClaudeExplanationService>().Object);

        var result = await controller.GetCustomerPayments(customerId, "medium", 99, 12, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ApiResponse<CustomerPaymentsVm>>().Subject;
        payload.Success.Should().BeTrue();
        payload.Meta.Should().NotBeNull();
        payload.Meta!.Total.Should().Be(4);
        payload.Meta.Page.Should().Be(1);

        payload.Data.Should().NotBeNull();
        var data = payload.Data!;
        data.Payments.Should().HaveCount(4);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"customers-payments-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Guid> SeedCustomerWithPaymentsAsync(AppDbContext db)
    {
        var customerId = Guid.NewGuid();
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

        var payments = Enumerable.Range(1, 15)
            .Select(index => new Payment
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                InvoiceId = Guid.NewGuid(),
                CrmExternalId = $"PAY-{index}",
                PaymentDate = new DateOnly(2026, 1, 1).AddDays(index - 1),
                Amount = 100 + index,
                DaysLate = index switch
                {
                    <= 5 => index - 2,
                    <= 9 => 15 + (index - 5) * 3,
                    _ => 30 + index,
                },
                ImportedAt = now,
            })
            .ToList();

        db.Payments.AddRange(payments);
        await db.SaveChangesAsync();

        return customerId;
    }
}