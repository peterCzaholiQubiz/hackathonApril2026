namespace PortfolioThermometer.Infrastructure.AzureOpenAi;

public sealed class AzureOpenAiOptions
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string Deployment { get; init; } = "gpt-4o";
    public string ApiVersion { get; init; } = "2024-02-01";
    public int MaxTokens { get; init; } = 1024;
    public int MaxConcurrency { get; init; } = 5;
    public int BatchSize { get; init; } = 10;
}
