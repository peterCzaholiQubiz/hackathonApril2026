using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PortfolioThermometer.Infrastructure.AzureOpenAi;

public sealed class AzureOpenAiClient : IAzureOpenAiClient
{
    private static readonly int[] RetryDelaysMs = [1000, 2000, 4000];
    private static readonly HashSet<HttpStatusCode> RetryStatusCodes =
    [
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.ServiceUnavailable
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureOpenAiOptions _options;
    private readonly ILogger<AzureOpenAiClient> _logger;

    public AzureOpenAiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAiOptions> options,
        ILogger<AzureOpenAiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> CompleteAsync(string prompt, CancellationToken ct)
    {
        var url = BuildUrl();
        var requestBody = new
        {
            model = _options.Deployment,
            max_tokens = _options.MaxTokens,
            response_format = new { type = "json_object" },
            messages = new[] { new { role = "user", content = prompt } }
        };

        var client = _httpClientFactory.CreateClient("azureopenai");

        for (var attempt = 0; attempt <= RetryDelaysMs.Length; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("api-key", _options.ApiKey);
            request.Content = JsonContent.Create(requestBody);

            using var response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                return ParseContent(json);
            }

            if (attempt < RetryDelaysMs.Length && RetryStatusCodes.Contains(response.StatusCode))
            {
                _logger.LogWarning(
                    "Azure OpenAI returned {StatusCode}, retrying in {Delay}ms (attempt {Attempt}/{Max})",
                    (int)response.StatusCode, RetryDelaysMs[attempt], attempt + 1, RetryDelaysMs.Length);
                await Task.Delay(RetryDelaysMs[attempt], ct);
                continue;
            }

            _logger.LogError("Azure OpenAI request failed with {StatusCode} after {Attempt} attempt(s)",
                (int)response.StatusCode, attempt + 1);
            return null;
        }

        return null;
    }

    private string BuildUrl() =>
        $"{_options.Endpoint.TrimEnd('/')}/openai/deployments/{_options.Deployment}" +
        $"/chat/completions?api-version={_options.ApiVersion}";

    private static string? ParseContent(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
            choices.GetArrayLength() == 0)
            return null;

        return choices[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }
}
