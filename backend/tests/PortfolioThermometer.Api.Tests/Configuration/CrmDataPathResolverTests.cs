using FluentAssertions;
using PortfolioThermometer.Api.Configuration;
using Xunit;

namespace PortfolioThermometer.Api.Tests.Configuration;

public sealed class CrmDataPathResolverTests
{
    [Fact]
    public void Resolve_PrefersRequestedAbsolutePath()
    {
        var (contentRoot, crmRoot) = CreateTestPaths();
        var samplePath = Path.Combine(crmRoot, "Sample-100");

        var resolved = CrmDataPathResolver.Resolve(
            samplePath,
            "All",
            crmRoot,
            contentRoot);

        resolved.Should().Be(samplePath);
    }

    [Fact]
    public void Resolve_FallsBackToConfiguredRelativePath()
    {
        var (contentRoot, _) = CreateTestPaths();
        var expectedRoot = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "..", "crm-data"));
        var expectedPath = Path.Combine(expectedRoot, "Sample-100");

        var resolved = CrmDataPathResolver.Resolve(
            null,
            "Sample-100",
            "../../../crm-data",
            contentRoot);

        resolved.Should().Be(expectedPath);
    }

    [Fact]
    public void Resolve_RejectsPathsOutsideConfiguredRoot()
    {
        var (contentRoot, _) = CreateTestPaths();

        var resolved = CrmDataPathResolver.Resolve(
            "..\\..\\..\\..\\Windows",
            "Sample-100",
            "../../../crm-data",
            contentRoot);

        resolved.Should().BeNull();
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNoPathIsProvided()
    {
        var (contentRoot, _) = CreateTestPaths();

        var resolved = CrmDataPathResolver.Resolve(
            null,
            null,
            "../../../crm-data",
            contentRoot);

        resolved.Should().BeNull();
    }

    private static (string ContentRoot, string CrmRoot) CreateTestPaths()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var contentRoot = Path.Combine(repoRoot, "backend", "src", "PortfolioThermometer.Api");
        var crmRoot = Path.Combine(repoRoot, "crm-data");

        return (contentRoot, crmRoot);
    }
}
