using System.Text.Json.Serialization;

namespace PortfolioThermometer.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConsumptionProfile
{
    LowConsumer,
    HighConsumer,
    LowDaytime,
    HighDaytime,
    SolarProducer,
    Industrial,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GenerationPeriod { ThreeMonths, SixMonths, OneYear, TwoYears }

public record GenerateMeterReadsRequest(
    Guid CustomerId,
    ConsumptionProfile Profile,
    GenerationPeriod Period
);

public record DailyMeterReadSummary(
    string Date,
    decimal ConsumptionHigh,
    decimal ConsumptionLow,
    decimal TotalConsumption,
    decimal Production
);

public record GenerateMeterReadsResponse(
    Guid CustomerId,
    ConsumptionProfile Profile,
    GenerationPeriod Period,
    int TotalHourlyRowsGenerated,
    IReadOnlyList<DailyMeterReadSummary> DailySummary
);
