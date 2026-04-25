using Microsoft.AspNetCore.Mvc;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Api.Controllers;

[ApiController]
[Route("api/meter-reads")]
public sealed class MeterReadController(IMeterReadGenerationService generator) : ControllerBase
{
    private const int MaxSelectedCustomers = 25;

    [HttpPost("generate")]
    public async Task<ActionResult<GenerateMeterReadsResponse>> Generate(
        [FromBody] GenerateMeterReadsRequest request,
        CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty)
            return BadRequest("CustomerId is required.");

        var result = await generator.GenerateAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("generate-yearly")]
    public async Task<ActionResult<GenerateYearlyMeterReadsResponse>> GenerateYearly(
        [FromBody] GenerateYearlyMeterReadsRequest request,
        CancellationToken ct)
    {
        var rawCustomerIds = request.CustomerIds ?? [];
        var uniqueCustomerIds = rawCustomerIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (uniqueCustomerIds.Count == 0)
            return BadRequest("At least one customer must be selected.");

        if (uniqueCustomerIds.Count != rawCustomerIds.Count)
            return BadRequest("CustomerIds cannot contain duplicate or empty values.");

        if (uniqueCustomerIds.Count > MaxSelectedCustomers)
            return BadRequest($"Select up to {MaxSelectedCustomers} customers per request.");

        if (request.Year < 2000 || request.Year > 2100)
            return BadRequest("Year must be between 2000 and 2100.");

        if (request.ProducerPercentage < 0 || request.ProducerPercentage > 100)
            return BadRequest("ProducerPercentage must be between 0 and 100.");

        var result = await generator.GenerateYearlyAsync(request, ct);
        return Ok(result);
    }
}
