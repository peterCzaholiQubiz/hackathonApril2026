using Microsoft.AspNetCore.Mvc;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StatusController : ControllerBase
{
    private readonly AppDbContext _db;

    public StatusController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("check")]
    public async Task<IActionResult> Check(CancellationToken ct)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(ct);

            if (!canConnect)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    api = "ok",
                    database = "unavailable"
                });
            }

            return Ok(new
            {
                api = "ok",
                database = "ok"
            });
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                api = "ok",
                database = "unavailable"
            });
        }
    }
}