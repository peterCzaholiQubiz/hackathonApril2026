using Microsoft.AspNetCore.Mvc;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Api.Controllers;

[ApiController]
[Route("api/meter-reads")]
public sealed class MeterReadController(IMeterReadGenerationService generator) : ControllerBase
{
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
}
