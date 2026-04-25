using FluentAssertions;
using PortfolioThermometer.Api.Configuration;
using Xunit;

namespace PortfolioThermometer.Api.Tests.Configuration;

public sealed class AzureOpenAiApiKeyResolverTests
{
    [Fact]
    public void Resolve_PrefersEnvironmentApiKey()
    {
        var resolved = AzureOpenAiApiKeyResolver.Resolve("env-key", "config-key");

        resolved.Should().Be("env-key");
    }

    [Fact]
    public void Resolve_FallsBackToConfiguredApiKey()
    {
        var resolved = AzureOpenAiApiKeyResolver.Resolve(null, "config-key");

        resolved.Should().Be("config-key");
    }

    [Fact]
    public void Resolve_ReturnsEmpty_WhenBothValuesAreMissing()
    {
        var resolved = AzureOpenAiApiKeyResolver.Resolve("   ", null);

        resolved.Should().BeEmpty();
    }
}