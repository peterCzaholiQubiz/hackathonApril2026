using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PortfolioThermometer.Infrastructure.AzureOpenAi;
using Xunit;

namespace PortfolioThermometer.Infrastructure.Tests.AzureOpenAi;

public sealed class AzureOpenAiClientTests
{
    private const string TestEndpoint = "https://test.openai.azure.com/";
    private const string TestApiKey = "test-api-key-123";
    private const string TestDeployment = "gpt-4o";
    private const string TestApiVersion = "2024-02-01";

    private static AzureOpenAiOptions DefaultOptions => new()
    {
        Endpoint = TestEndpoint,
        ApiKey = TestApiKey,
        Deployment = TestDeployment,
        ApiVersion = TestApiVersion,
        MaxTokens = 512,
        MaxConcurrency = 5,
        BatchSize = 10
    };

    private static string BuildSuccessResponse(string content) =>
        JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content } }
            }
        });

    private (AzureOpenAiClient client, Mock<HttpMessageHandler> handler) CreateClient(
        AzureOpenAiOptions? options = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handler.Object);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("azureopenai")).Returns(httpClient);

        var opts = Options.Create(options ?? DefaultOptions);
        var client = new AzureOpenAiClient(factory.Object, opts, NullLogger<AzureOpenAiClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task CompleteAsync_SendsApiKeyHeader()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var (client, handler) = CreateClient();

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildSuccessResponse("{\"explanation\":\"test\",\"confidence\":\"high\"}"), Encoding.UTF8, "application/json")
            });

        // Act
        await client.CompleteAsync("test prompt", CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.TryGetValues("api-key", out var values).Should().BeTrue();
        values!.Should().Contain(TestApiKey);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsContent_WhenSuccessful()
    {
        // Arrange
        var expectedContent = "{\"explanation\":\"test explanation\",\"confidence\":\"high\"}";
        var (client, handler) = CreateClient();

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildSuccessResponse(expectedContent), Encoding.UTF8, "application/json")
            });

        // Act
        var result = await client.CompleteAsync("test prompt", CancellationToken.None);

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task CompleteAsync_RetriesOn429_ThenSucceeds()
    {
        // Arrange
        var callCount = 0;
        var (client, handler) = CreateClient();

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 2)
                    return new HttpResponseMessage(HttpStatusCode.TooManyRequests);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BuildSuccessResponse("{\"ok\":true}"), Encoding.UTF8, "application/json")
                };
            });

        // Act
        var result = await client.CompleteAsync("test prompt", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task CompleteAsync_DoesNotRetryOn400()
    {
        // Arrange
        var callCount = 0;
        var (client, handler) = CreateClient();

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            });

        // Act
        var result = await client.CompleteAsync("test prompt", CancellationToken.None);

        // Assert
        result.Should().BeNull();
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsNull_WhenAllRetriesExhausted()
    {
        // Arrange
        var callCount = 0;
        var (client, handler) = CreateClient();

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            });

        // Act
        var result = await client.CompleteAsync("test prompt", CancellationToken.None);

        // Assert
        result.Should().BeNull();
        callCount.Should().Be(4); // initial + 3 retries
    }

    [Fact]
    public async Task CompleteAsync_ReturnsNull_WhenResponseHasNoChoices()
    {
        // Arrange
        var (client, handler) = CreateClient();
        var emptyChoices = JsonSerializer.Serialize(new { choices = Array.Empty<object>() });

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(emptyChoices, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await client.CompleteAsync("test prompt", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
