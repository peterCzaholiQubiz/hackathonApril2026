using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StatusController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<StatusController> _logger;

    public StatusController(AppDbContext db, ILogger<StatusController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("check")]
    public async Task<IActionResult> Check(CancellationToken ct)
    {
        try
        {
            var connectionString = _db.Database.GetConnectionString()
                ?? _db.Database.GetDbConnection().ConnectionString;

            _logger.LogInformation(
                "Status check DB connection string: {ConnectionString}",
                RedactConnectionString(connectionString));

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

    private static string RedactConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "<empty>";

        try
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            foreach (var key in new[] { "Password", "Pwd" })
            {
                if (builder.ContainsKey(key))
                    builder[key] = "***";
            }

            return builder.ConnectionString;
        }
        catch
        {
            return "<unparseable connection string>";
        }
    }
}