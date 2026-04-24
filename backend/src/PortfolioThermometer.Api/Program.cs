using Microsoft.EntityFrameworkCore;
using Npgsql;
using PortfolioThermometer.Api.Middleware;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Infrastructure.AzureOpenAi;
using PortfolioThermometer.Infrastructure.Data;
using PortfolioThermometer.Infrastructure.Repositories;
using PortfolioThermometer.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
var connectionString = ResolveConnectionString(builder.Configuration);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ── Domain services ───────────────────────────────────────────────────────────
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICrmImportService, CrmImportService>();
builder.Services.AddScoped<IRiskScoringEngine, RiskScoringEngine>();
builder.Services.AddScoped<IClaudeExplanationService, AzureOpenAiExplanationService>();
builder.Services.AddScoped<IPortfolioAggregationService, PortfolioAggregationService>();
builder.Services.AddScoped<IMeterReadGenerationService, MeterReadGenerationService>();
builder.Services.AddScoped<ITestDataGenerationService, TestDataGenerationService>();

// ── Azure OpenAI ──────────────────────────────────────────────────────────────
builder.Services.Configure<AzureOpenAiOptions>(
    builder.Configuration.GetSection(AzureOpenAiOptions.SectionName));
builder.Services.AddHttpClient("azureopenai");
builder.Services.AddSingleton<IAzureOpenAiClient, AzureOpenAiClient>();

// ── Controllers & API ─────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Portfolio Thermometer API", Version = "v1" });
});

// ── CORS ──────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()));

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseMiddleware<GlobalErrorHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

// ── Apply migrations on startup (dev convenience) ────────────────────────────
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();

static string ResolveConnectionString(ConfigurationManager configuration)
{
    var databaseUrl = configuration["DATABASE_URL"];

    if (!string.IsNullOrWhiteSpace(databaseUrl))
        return ToNpgsqlConnectionString(databaseUrl);

    return configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Database connection is not configured. Set DATABASE_URL or ConnectionStrings:DefaultConnection.");
}

static string ToNpgsqlConnectionString(string databaseUrl)
{
    if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri))
        return databaseUrl;

    if (!string.Equals(uri.Scheme, "postgres", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(uri.Scheme, "postgresql", StringComparison.OrdinalIgnoreCase))
    {
        return databaseUrl;
    }

    var userInfo = uri.UserInfo.Split(':', 2);
    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty,
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
        SslMode = SslMode.Prefer
    };

    if (!string.IsNullOrWhiteSpace(uri.Query))
    {
        var queryParameters = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var queryParameter in queryParameters)
        {
            var parts = queryParameter.Split('=', 2);
            if (parts.Length != 2)
                continue;

            var key = Uri.UnescapeDataString(parts[0]);
            var value = Uri.UnescapeDataString(parts[1]);

            if (string.Equals(key, "sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SslMode>(value, true, out var sslMode))
            {
                builder.SslMode = sslMode;
                continue;
            }

            try
            {
                builder[key] = value;
            }
            catch (ArgumentException)
            {
                // Ignore URL parameters that Npgsql does not support directly.
            }
        }
    }

    return builder.ConnectionString;
}
