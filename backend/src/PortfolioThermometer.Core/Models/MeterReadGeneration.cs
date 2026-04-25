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

public record GenerateYearlyMeterReadsRequest(
    IReadOnlyList<Guid> CustomerIds,
    int Year,
    int ProducerPercentage = 25
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

public record MeterReadGenerationSkippedCustomer(
    Guid CustomerId,
    string Reason
);

public record GenerateYearlyMeterReadsResponse(
    int Year,
    int RequestedCustomerCount,
    int EligibleCustomerCount,
    int ProducerCustomerCount,
    int ProcessedConnectionCount,
    int ConsumptionRowsGenerated,
    int ProductionRowsGenerated,
    int ReducedConsumptionHourCount,
    IReadOnlyList<Guid> ProducerCustomerIds,
    IReadOnlyList<MeterReadGenerationSkippedCustomer> SkippedCustomers
);
