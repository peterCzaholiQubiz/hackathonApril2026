namespace PortfolioThermometer.Infrastructure.AzureOpenAi;

public interface IAzureOpenAiClient
{
    Task<string?> CompleteAsync(string prompt, CancellationToken ct);
}
