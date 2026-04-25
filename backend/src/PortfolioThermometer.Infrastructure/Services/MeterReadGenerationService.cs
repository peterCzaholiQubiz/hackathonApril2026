using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;
using System.Text;

namespace PortfolioThermometer.Infrastructure.Services;

public sealed class MeterReadGenerationService(AppDbContext db) : IMeterReadGenerationService
{
    private const string StandaloneGeneratedSource = "Generated";
    private const string YearlyGeneratedSource = "GeneratedYearly";

    private sealed record ProfileParams(
        double AnnualKwh,
        double PeakHourFactor,
        double OffPeakHourFactor,
        bool HasProduction,
        int Index);

    private sealed record ConnectionTarget(
        Guid CustomerId,
        Guid ConnectionId,
        string? Segment,
        int? ConnectionTypeId,
        int Index);

    private sealed record ConnectionGenerationProfile(
        double AnnualConsumptionKwh,
        double AnnualProductionKwh,
        int HoursInYear,
        double PeakHourFactor,
        double OffPeakHourFactor,
        double DaytimeWeight,
        int PhaseShift);

    private sealed record YearlyConnectionGenerationResult(
        List<MeterRead> Rows,
        int ConsumptionRowsGenerated,
        int ProductionRowsGenerated,
        int ReducedConsumptionHourCount);

    private static readonly IReadOnlyDictionary<ConsumptionProfile, ProfileParams> Profiles =
        new Dictionary<ConsumptionProfile, ProfileParams>
        {
            [ConsumptionProfile.LowConsumer] = new(2_500, 1.2, 0.8, false, 0),
            [ConsumptionProfile.HighConsumer] = new(12_000, 1.2, 0.8, false, 1),
            [ConsumptionProfile.LowDaytime] = new(6_000, 0.4, 2.2, false, 2),
            [ConsumptionProfile.HighDaytime] = new(8_000, 1.8, 0.2, false, 3),
            [ConsumptionProfile.SolarProducer] = new(4_500, 1.2, 0.8, true, 4),
            [ConsumptionProfile.Industrial] = new(80_000, 1.05, 0.95, false, 5),
        };

    public async Task<GenerateMeterReadsResponse> GenerateAsync(
        GenerateMeterReadsRequest request,
        CancellationToken ct = default)
    {
        var profile = Profiles[request.Profile];
        var monthCount = request.Period switch
        {
            GenerationPeriod.ThreeMonths => 3,
            GenerationPeriod.SixMonths => 6,
            GenerationPeriod.OneYear => 12,
            GenerationPeriod.TwoYears => 24,
            _ => 12,
        };

        var now = DateTimeOffset.UtcNow;
        var endUtc = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var startUtc = endUtc.AddMonths(-monthCount);
        var prefix = $"GEN-{request.CustomerId:N}-";

        await DeleteRowsAsync(
            db.MeterReads.Where(m => m.CrmExternalId.StartsWith(prefix)),
            ct);

        var rows = new List<MeterRead>();
        var importedAt = DateTimeOffset.UtcNow;
        var baseHourly = profile.AnnualKwh / 8760.0;

        for (var h = startUtc; h < endUtc; h = h.AddHours(1))
        {
            var seasonal = SeasonalFactor(h.Month);
            var isPeak = IsPeakHour(h.Hour);
            var timeOfDayFactor = isPeak ? profile.PeakHourFactor : profile.OffPeakHourFactor;
            var usageType = isPeak ? "UsageHigh" : "UsageLow";
            var suffix = isPeak ? "H" : "L";
            var jitter = 1.0 + Math.Sin((h.DayOfYear * 24 + h.Hour + profile.Index) * 1.9) * 0.06;
            var consumption = (decimal)Math.Round(baseHourly * seasonal * timeOfDayFactor * jitter, 4);

            rows.Add(CreateMeterRead(
                null,
                $"GEN-{request.CustomerId:N}-{h:yyyyMMddHH}-{suffix}",
                h,
                consumption,
                usageType,
                "Consumption",
                StandaloneGeneratedSource,
                importedAt));

            if (profile.HasProduction && h.Hour >= 6 && h.Hour < 20)
            {
                var solarFactor = SolarProductionFactor(h.Month);
                var production = (decimal)Math.Round(baseHourly * solarFactor * jitter, 4);
                rows.Add(CreateMeterRead(
                    null,
                    $"GEN-{request.CustomerId:N}-{h:yyyyMMddHH}-P",
                    h,
                    production,
                    "UsageHigh",
                    "Production",
                    StandaloneGeneratedSource,
                    importedAt));
            }
        }

        await db.MeterReads.AddRangeAsync(rows, ct);
        await db.SaveChangesAsync(ct);

        var consumptionRows = rows.Where(r => r.Direction == "Consumption").ToList();
        var productionRows = rows.Where(r => r.Direction == "Production").ToList();

        var dailySummary = consumptionRows
            .GroupBy(r => r.StartDate!.Value.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var date = g.Key;
                var high = g.Where(r => r.UsageType == "UsageHigh").Sum(r => r.Consumption ?? 0m);
                var low = g.Where(r => r.UsageType == "UsageLow").Sum(r => r.Consumption ?? 0m);
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

    public async Task<GenerateYearlyMeterReadsResponse> GenerateYearlyAsync(
        GenerateYearlyMeterReadsRequest request,
        CancellationToken ct = default)
    {
        var requestedCustomerIds = (request.CustomerIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var selectedCustomers = await db.Customers
            .AsNoTracking()
            .Where(customer => requestedCustomerIds.Contains(customer.Id))
            .Select(customer => new
            {
                customer.Id,
                customer.Segment,
            })
            .ToListAsync(ct);

        var customerLookup = selectedCustomers.ToDictionary(customer => customer.Id);

        var electricityConnections = await db.Connections
            .AsNoTracking()
            .Where(connection =>
                connection.CustomerId.HasValue &&
                requestedCustomerIds.Contains(connection.CustomerId.Value) &&
                connection.ProductType == "Electricity")
            .OrderBy(connection => connection.CustomerId)
            .ThenBy(connection => connection.Id)
            .Select(connection => new
            {
                ConnectionId = connection.Id,
                CustomerId = connection.CustomerId!.Value,
                connection.ConnectionTypeId,
            })
            .ToListAsync(ct);

        var startUtc = new DateTimeOffset(request.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endUtc = startUtc.AddYears(1);
        var electricityConnectionIds = electricityConnections
            .Select(connection => connection.ConnectionId)
            .ToList();

        var eligibleCustomerIds = electricityConnections
            .Select(connection => connection.CustomerId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var electricityCustomerIds = electricityConnections
            .Select(connection => connection.CustomerId)
            .Distinct()
            .ToHashSet();

        var skippedCustomers = requestedCustomerIds
            .Except(eligibleCustomerIds)
            .Select(customerId => new MeterReadGenerationSkippedCustomer(
                customerId,
                !customerLookup.ContainsKey(customerId)
                    ? "Customer not found."
                    : !electricityCustomerIds.Contains(customerId)
                        ? "No electricity connections found."
                        : "Existing non-generated meter reads already cover the selected year."))
            .ToList();

        var producerCustomerIds = SelectProducerCustomers(
            eligibleCustomerIds,
            request.Year,
            request.ProducerPercentage);
        var producerCustomerSet = producerCustomerIds.ToHashSet();

        var connectionTargets = electricityConnections
            .Where(connection => customerLookup.ContainsKey(connection.CustomerId))
            .GroupBy(connection => connection.CustomerId)
            .SelectMany(group => group.Select((connection, index) => new ConnectionTarget(
                group.Key,
                connection.ConnectionId,
                customerLookup[group.Key].Segment,
                connection.ConnectionTypeId,
                index)))
            .ToList();

        var useTransaction = !string.Equals(
            db.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

        await using var transaction = useTransaction
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        await DeleteRowsAsync(
            db.MeterReads.Where(read =>
                read.ConnectionId.HasValue &&
                electricityConnectionIds.Contains(read.ConnectionId.Value) &&
                read.Source == YearlyGeneratedSource &&
                read.StartDate.HasValue &&
                read.StartDate.Value >= startUtc &&
                read.StartDate.Value < endUtc),
            ct);

        var importedAt = DateTimeOffset.UtcNow;
        var batch = new List<MeterRead>(24_000);
        var consumptionRowsGenerated = 0;
        var productionRowsGenerated = 0;
        var reducedConsumptionHourCount = 0;

        try
        {
            foreach (var target in connectionTargets)
            {
                var generated = GenerateYearlyRowsForConnection(
                    target,
                    request.Year,
                    producerCustomerSet.Contains(target.CustomerId),
                    importedAt);

                batch.AddRange(generated.Rows);
                consumptionRowsGenerated += generated.ConsumptionRowsGenerated;
                productionRowsGenerated += generated.ProductionRowsGenerated;
                reducedConsumptionHourCount += generated.ReducedConsumptionHourCount;

                if (batch.Count >= 24_000)
                    await PersistBatchAsync(batch, ct);
            }

            await PersistBatchAsync(batch, ct);

            if (transaction is not null)
                await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(CancellationToken.None);

            throw;
        }

        return new GenerateYearlyMeterReadsResponse(
            request.Year,
            requestedCustomerIds.Count,
            eligibleCustomerIds.Count,
            producerCustomerIds.Count,
            connectionTargets.Count,
            consumptionRowsGenerated,
            productionRowsGenerated,
            reducedConsumptionHourCount,
            producerCustomerIds,
            skippedCustomers);
    }

    private static YearlyConnectionGenerationResult GenerateYearlyRowsForConnection(
        ConnectionTarget target,
        int year,
        bool isProducer,
        DateTimeOffset importedAt)
    {
        var startUtc = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endUtc = startUtc.AddYears(1);
        var hoursInYear = (int)(endUtc - startUtc).TotalHours;
        var profile = BuildConnectionProfile(target, year, isProducer, hoursInYear);
        var rows = new List<MeterRead>(isProducer ? 13_500 : 8_800);
        var productionRowsGenerated = 0;
        var reducedConsumptionHourCount = 0;

        for (var hour = startUtc; hour < endUtc; hour = hour.AddHours(1))
        {
            var usageType = IsPeakHour(hour.Hour) ? "UsageHigh" : "UsageLow";
            var grossConsumption = ComputeGrossConsumption(profile, hour);
            var production = isProducer
                ? ComputeSolarProduction(profile, target.ConnectionId, year, hour)
                : 0m;

            var netConsumption = grossConsumption;
            if (production > 0m)
            {
                netConsumption = decimal.Round(
                    decimal.Max(0m, grossConsumption - production),
                    4,
                    MidpointRounding.AwayFromZero);

                if (netConsumption < grossConsumption)
                    reducedConsumptionHourCount++;
            }

            rows.Add(CreateMeterRead(
                target.ConnectionId,
                $"GBULK-C-{target.ConnectionId:N}-{hour:yyyyMMddHH}",
                hour,
                netConsumption,
                usageType,
                "Consumption",
                YearlyGeneratedSource,
                importedAt));

            if (production <= 0m)
                continue;

            rows.Add(CreateMeterRead(
                target.ConnectionId,
                $"GBULK-P-{target.ConnectionId:N}-{hour:yyyyMMddHH}",
                hour,
                production,
                usageType,
                "Production",
                YearlyGeneratedSource,
                importedAt));

            productionRowsGenerated++;
        }

        return new YearlyConnectionGenerationResult(
            rows,
            hoursInYear,
            productionRowsGenerated,
            reducedConsumptionHourCount);
    }

    private static ConnectionGenerationProfile BuildConnectionProfile(
        ConnectionTarget target,
        int year,
        bool isProducer,
        int hoursInYear)
    {
        var (minAnnualConsumption, maxAnnualConsumption) = target.Segment?.Trim().ToLowerInvariant() switch
        {
            "residential" => (2_400d, 6_400d),
            "sme" or "company" => (8_000d, 22_000d),
            "collective" => (14_000d, 32_000d),
            "corporate" or "enterprise" => (20_000d, 62_000d),
            _ => (5_500d, 18_000d),
        };

        var annualConsumption = minAnnualConsumption
            + ((maxAnnualConsumption - minAnnualConsumption) * StableUnit(target.ConnectionId, year, 11));

        if (target.ConnectionTypeId.HasValue)
            annualConsumption *= 0.9 + (target.ConnectionTypeId.Value * 0.12);

        if (target.Index > 0)
            annualConsumption *= 0.85 + (target.Index * 0.08);

        annualConsumption *= 0.9 + (StableUnit(target.ConnectionId, year, 17) * 0.2);

        var annualProduction = 0d;
        if (isProducer)
            annualProduction = annualConsumption * (0.45 + (StableUnit(target.ConnectionId, year, 19) * 0.55));

        return new ConnectionGenerationProfile(
            annualConsumption,
            annualProduction,
            hoursInYear,
            1.0 + (StableUnit(target.ConnectionId, year, 23) * 0.45),
            0.55 + (StableUnit(target.ConnectionId, year, 29) * 0.35),
            0.2 + (StableUnit(target.ConnectionId, year, 31) * 0.8),
            (int)Math.Round(StableUnit(target.ConnectionId, year, 37) * 23));
    }

    private static decimal ComputeGrossConsumption(
        ConnectionGenerationProfile profile,
        DateTimeOffset hour)
    {
        var baseHourly = profile.AnnualConsumptionKwh / profile.HoursInYear;
        var seasonal = SeasonalFactor(hour.Month);
        var timeOfDay = HourlyConsumptionFactor(hour.Hour, profile.DaytimeWeight);
        var peakFactor = IsPeakHour(hour.Hour) ? profile.PeakHourFactor : profile.OffPeakHourFactor;
        var jitter = 0.94 + (0.12 * HourlyWave(hour, profile.PhaseShift));

        return RoundPositive(baseHourly * seasonal * timeOfDay * peakFactor * jitter);
    }

    private static decimal ComputeSolarProduction(
        ConnectionGenerationProfile profile,
        Guid connectionId,
        int year,
        DateTimeOffset hour)
    {
        if (profile.AnnualProductionKwh <= 0d)
            return 0m;

        var (sunriseHour, sunsetHour) = GetSolarWindow(hour.Month);
        var hourPosition = hour.Hour + 0.5;

        if (hourPosition < sunriseHour || hourPosition >= sunsetHour)
            return 0m;

        var daylightProgress = (hourPosition - sunriseHour) / (sunsetHour - sunriseHour);
        var daylightCurve = Math.Sin(Math.PI * daylightProgress);
        if (daylightCurve <= 0d)
            return 0m;

        var baseHourly = profile.AnnualProductionKwh / 1_600d;
        var seasonal = SolarProductionFactor(hour.Month);
        var jitter = 0.92 + (0.12 * HourlyWave(hour, profile.PhaseShift + (int)Math.Round(StableUnit(connectionId, year, 41) * 10)));

        return RoundPositive(baseHourly * seasonal * daylightCurve * jitter, allowZero: true);
    }

    private static double HourlyConsumptionFactor(int hour, double daytimeWeight) => hour switch
    {
        >= 0 and < 6 => 0.48,
        >= 6 and < 9 => 0.84,
        >= 9 and < 17 => 0.72 + (0.55 * daytimeWeight),
        >= 17 and < 22 => 1.18 + (0.12 * (1d - daytimeWeight)),
        _ => 0.62,
    };

    private static bool IsPeakHour(int hour) => hour is >= 7 and < 22;

    private static double SeasonalFactor(int month) => month switch
    {
        1 or 2 or 12 => 1.35,
        3 or 11 => 1.15,
        4 or 10 => 0.95,
        5 or 9 => 0.8,
        6 or 7 or 8 => 0.65,
        _ => 1.0,
    };

    private static double SolarProductionFactor(int month) => month switch
    {
        1 or 12 => 0.12,
        2 or 11 => 0.18,
        3 or 10 => 0.35,
        4 or 9 => 0.58,
        5 or 8 => 0.82,
        6 or 7 => 1.0,
        _ => 0.1,
    };

    private static (double SunriseHour, double SunsetHour) GetSolarWindow(int month) => month switch
    {
        1 or 12 => (8d, 16d),
        2 or 11 => (7.5d, 17d),
        3 or 10 => (7d, 18d),
        4 or 9 => (6d, 19d),
        5 or 8 => (5.5d, 20d),
        6 or 7 => (5d, 21d),
        _ => (6d, 18d),
    };

    private static List<Guid> SelectProducerCustomers(
        IReadOnlyList<Guid> eligibleCustomerIds,
        int year,
        int producerPercentage)
    {
        if (eligibleCustomerIds.Count == 0 || producerPercentage <= 0)
            return [];

        var producerCount = Math.Clamp(
            (int)Math.Round(
                eligibleCustomerIds.Count * (producerPercentage / 100d),
                MidpointRounding.AwayFromZero),
            1,
            eligibleCustomerIds.Count);

        return eligibleCustomerIds
            .OrderBy(customerId => StableUnit(customerId, year, 47))
            .ThenBy(customerId => customerId)
            .Take(producerCount)
            .ToList();
    }

    private static MeterRead CreateMeterRead(
        Guid? connectionId,
        string crmExternalId,
        DateTimeOffset startDate,
        decimal consumption,
        string usageType,
        string direction,
        string source,
        DateTimeOffset importedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            CrmExternalId = crmExternalId,
            ConnectionId = connectionId,
            StartDate = startDate,
            EndDate = startDate.AddHours(1),
            Consumption = consumption,
            Unit = "kWh",
            UsageType = usageType,
            Direction = direction,
            Quality = "Estimated",
            Source = source,
            ImportedAt = importedAt,
        };

    private async Task DeleteRowsAsync(IQueryable<MeterRead> query, CancellationToken ct)
    {
        if (string.Equals(db.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal))
        {
            var rows = await query.ToListAsync(ct);
            if (rows.Count == 0)
                return;

            db.MeterReads.RemoveRange(rows);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            return;
        }

        await query.ExecuteDeleteAsync(ct);
    }

    private async Task PersistBatchAsync(List<MeterRead> batch, CancellationToken ct)
    {
        if (batch.Count == 0)
            return;

        await db.MeterReads.AddRangeAsync(batch, ct);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        batch.Clear();
    }

    private static decimal RoundPositive(double value, bool allowZero = false)
    {
        var rounded = Math.Round(value, 4, MidpointRounding.AwayFromZero);
        if (allowZero && rounded <= 0d)
            return 0m;

        return (decimal)Math.Max(0.0001d, rounded);
    }

    private static double HourlyWave(DateTimeOffset hour, int phaseShift)
    {
        var wave = Math.Sin((hour.DayOfYear * 24 + hour.Hour + phaseShift) * 0.17);
        return (wave + 1d) / 2d;
    }

    private static double StableUnit(Guid value, int year, int salt)
    {
        var input = $"{value:N}:{year}:{salt}";
        var bytes = Encoding.UTF8.GetBytes(input);
        uint hash = 2166136261;

        foreach (var current in bytes)
        {
            hash ^= current;
            hash *= 16777619;
        }

        return hash / (double)uint.MaxValue;
    }
}
