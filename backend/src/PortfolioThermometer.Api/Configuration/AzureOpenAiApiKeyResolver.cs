namespace PortfolioThermometer.Api.Configuration;

public static class AzureOpenAiApiKeyResolver
{
    public static string Resolve(string? environmentApiKey, string? configuredApiKey)
        => string.IsNullOrWhiteSpace(environmentApiKey)
            ? configuredApiKey ?? string.Empty
            : environmentApiKey;
}