using Microsoft.AspNetCore.Mvc;

namespace PortfolioThermometer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StatusController : ControllerBase
{
    [HttpGet("check")]
    public IActionResult Check()
    {
        return Ok();
    }
}