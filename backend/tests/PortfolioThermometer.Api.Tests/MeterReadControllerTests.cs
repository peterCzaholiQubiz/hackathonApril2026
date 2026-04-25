using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PortfolioThermometer.Api.Controllers;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using Xunit;

namespace PortfolioThermometer.Api.Tests;

public sealed class MeterReadControllerTests
{
    [Fact]
    public async Task GenerateYearly_ReturnsBadRequest_WhenNoCustomersSelected()
    {
        var controller = new MeterReadController(new Mock<IMeterReadGenerationService>().Object);

        var result = await controller.GenerateYearly(
            new GenerateYearlyMeterReadsRequest([], 2026, 25),
            CancellationToken.None);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("At least one customer must be selected.");
    }

    [Fact]
    public async Task GenerateYearly_ReturnsBadRequest_WhenTooManyCustomersSelected()
    {
        var controller = new MeterReadController(new Mock<IMeterReadGenerationService>().Object);
        var customerIds = Enumerable.Range(0, 26).Select(_ => Guid.NewGuid()).ToArray();

        var result = await controller.GenerateYearly(
            new GenerateYearlyMeterReadsRequest(customerIds, 2026, 25),
            CancellationToken.None);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Select up to 25 customers per request.");
    }

    [Fact]
    public async Task GenerateYearly_ReturnsOk_WhenRequestIsValid()
    {
        var request = new GenerateYearlyMeterReadsRequest([Guid.NewGuid()], 2026, 50);
        var expected = new GenerateYearlyMeterReadsResponse(
            2026,
            1,
            1,
            1,
            2,
            17_520,
            4_320,
            2_100,
            [request.CustomerIds[0]],
            []);

        var service = new Mock<IMeterReadGenerationService>();
        service
            .Setup(generator => generator.GenerateYearlyAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new MeterReadController(service.Object);

        var result = await controller.GenerateYearly(request, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
        service.Verify(generator => generator.GenerateYearlyAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }
}
