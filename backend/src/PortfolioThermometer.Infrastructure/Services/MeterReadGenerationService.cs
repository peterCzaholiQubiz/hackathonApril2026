using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Infrastructure.Services;

public sealed class MeterReadGenerationService(AppDbContext db) : IMeterReadGenerationService
{
    private sealed record ProfileParams(
        double AnnualKwh,
        double PeakHourFactor,
        double OffPeakHourFactor,
        bool HasProduction,
        int Index);

    private static readonly IReadOnlyDictionary<ConsumptionProfile, ProfileParams> Profiles =
        new Dictionary<ConsumptionProfile, ProfileParams>
        {
            [ConsumptionProfile.LowConsumer]   = new(2_500,   1.2,  0.8,  false, 0),
            [ConsumptionProfile.HighConsumer]  = new(12_000,  1.2,  0.8,  false, 1),
            [ConsumptionProfile.LowDaytime]    = new(6_000,   0.4,  2.2,  false, 2),
            [ConsumptionProfile.HighDaytime]   = new(8_000,   1.8,  0.2,  false, 3),
            [ConsumptionProfile.SolarProducer] = new(4_500,   1.2,  0.8,  true,  4),
            [ConsumptionProfile.Industrial]    = new(80_000,  1.05, 0.95, false, 5),
        };

    public async Task<GenerateMeterReadsResponse> GenerateAsync(
        GenerateMeterReadsRequest request,
        CancellationToken ct = default)
    {
        var profile = Profiles[request.Profile];
        var monthCount = request.Period switch
        {
            GenerationPeriod.ThreeMonths => 3,
            GenerationPeriod.SixMonths   => 6,
            GenerationPeriod.OneYear     => 12,
            GenerationPeriod.TwoYears    => 24,
            _                            => 12,
        };

        var now = DateTimeOffset.UtcNow;
        var endUtc = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var startUtc = endUtc.AddMonths(-monthCount);

        var prefix = $"GEN-{request.CustomerId:N}-";
        await db.MeterReads
            .Where(m => m.CrmExternalId.StartsWith(prefix))
            .ExecuteDeleteAsync(ct);

        var rows = new List<MeterRead>();
        var importedAt = DateTimeOffset.UtcNow;
        var baseHourly = profile.AnnualKwh / 8760.0;

        for (var h = startUtc; h < endUtc; h = h.AddHours(1))
        {
            var seasonal = SeasonalFactor(h.Month);
            var isPeak = h.Hour >= 7 && h.Hour < 22;
            var timeOfDayFactor = isPeak ? profile.PeakHourFactor : profile.OffPeakHourFactor;
            var usageType = isPeak ? "UsageHigh" : "UsageLow";
            var suffix = isPeak ? "H" : "L";
            var jitter = 1.0 + Math.Sin((h.DayOfYear * 24 + h.Hour + profile.Index) * 1.9) * 0.06;
            var consumption = (decimal)Math.Round(baseHourly * seasonal * timeOfDayFactor * jitter, 4);

            rows.Add(new MeterRead
            {
                Id = Guid.NewGuid(),
                CrmExternalId = $"GEN-{request.CustomerId:N}-{h:yyyyMMddHH}-{suffix}",
                ConnectionId = null,
                StartDate = h,
                EndDate = h.AddHours(1),
                Consumption = consumption,
                Unit = "kWh",
                UsageType = usageType,
                Direction = "Consumption",
                Quality = "Estimated",
                Source = "Generated",
                ImportedAt = importedAt,
            });

            if (profile.HasProduction && h.Hour >= 6 && h.Hour < 20)
            {
                var solarFactor = SolarProductionFactor(h.Month);
                var production = (decimal)Math.Round(baseHourly * solarFactor * jitter, 4);
                rows.Add(new MeterRead
                {
                    Id = Guid.NewGuid(),
                    CrmExternalId = $"GEN-{request.CustomerId:N}-{h:yyyyMMddHH}-P",
                    ConnectionId = null,
                    StartDate = h,
                    EndDate = h.AddHours(1),
                    Consumption = production,
                    Unit = "kWh",
                    UsageType = "UsageHigh",
                    Direction = "Production",
                    Quality = "Estimated",
                    Source = "Generated",
                    ImportedAt = importedAt,
                });
            }
        }

        await db.MeterReads.AddRangeAsync(rows, ct);
        await db.SaveChangesAsync(ct);

        var consumptionRows = rows.Where(r => r.Direction == "Consumption").ToList();
        var productionRows  = rows.Where(r => r.Direction == "Production").ToList();

        var dailySummary = consumptionRows
            .GroupBy(r => r.StartDate!.Value.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var date = g.Key;
                var high = g.Where(r => r.UsageType == "UsageHigh").Sum(r => r.Consumption ?? 0m);
                var low  = g.Where(r => r.UsageType == "UsageLow").Sum(r => r.Consumption ?? 0m);
                var prod = productionRows
                    .Where(r => r.StartDate!.Value.Date == date)
                    .Sum(r => r.Consumption ?? 0m);
                return new DailyMeterReadSummary(
                    date.ToString("yyyy-MM-dd"),
                    Math.Round(high, 2),
                    Math.Round(low, 2),
                    Math.Round(high + low, 2),
                    Math.Round(prod, 2));
            })
            .ToList();

        return new GenerateMeterReadsResponse(
            request.CustomerId,
            request.Profile,
            request.Period,
            rows.Count,
            dailySummary);
    }

    private static double SeasonalFactor(int month) => month switch
    {
        1 or 2 or 12 => 1.35,
        3 or 11      => 1.15,
        4 or 10      => 0.95,
        5 or 9       => 0.80,
        6 or 7 or 8  => 0.65,
        _            => 1.0,
    };

    private static double SolarProductionFactor(int month) => month switch
    {
        6      => 0.55,
        7      => 0.60,
        8      => 0.55,
        5 or 9 => 0.35,
        4 or 10 => 0.20,
        _      => 0.05,
    };
}
