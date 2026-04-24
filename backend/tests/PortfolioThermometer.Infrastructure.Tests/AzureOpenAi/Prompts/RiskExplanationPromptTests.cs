using FluentAssertions;
using PortfolioThermometer.Infrastructure.AzureOpenAi.Prompts;
using Xunit;

namespace PortfolioThermometer.Infrastructure.Tests.AzureOpenAi.Prompts;

public sealed class RiskExplanationPromptTests
{
    [Fact]
    public void Build_ContainsCustomerName()
    {
        var prompt = RiskExplanationPrompt.Build(
            "Acme Corp", "enterprise", "churn", 75, "red", 75, 40, 30);

        prompt.Should().Contain("Acme Corp");
    }

    [Fact]
    public void Build_ContainsRiskScore()
    {
        var prompt = RiskExplanationPrompt.Build(
            "Acme Corp", "enterprise", "churn", 75, "red", 75, 40, 30);

        prompt.Should().Contain("75");
    }

    [Fact]
    public void Build_ContainsAllThreeRiskScores()
    {
        var prompt = RiskExplanationPrompt.Build(
            "Acme Corp", null, "payment", 55, "yellow", 30, 55, 20);

        prompt.Should().Contain("30");  // churn
        prompt.Should().Contain("55");  // payment
        prompt.Should().Contain("20");  // margin
    }

    [Fact]
    public void Build_ContainsAdvisoryToneInstructions()
    {
        var prompt = RiskExplanationPrompt.Build(
            "Test Customer", "smb", "margin", 80, "red", 60, 45, 80);

        prompt.Should().ContainAny("may indicate", "consider", "suggests", "could reflect");
    }

    [Fact]
    public void Build_ContainsJsonFormatInstruction()
    {
        var prompt = RiskExplanationPrompt.Build(
            "Test Customer", "smb", "churn", 60, "yellow", 60, 30, 20);

        prompt.Should().ContainAny("json", "JSON");
        prompt.Should().Contain("explanation");
        prompt.Should().Contain("confidence");
    }

    [Fact]
    public void Build_UsesUnknownWhenSegmentIsNull()
    {
        var prompt = RiskExplanationPrompt.Build(
            "No Segment Corp", null, "overall", 50, "green", 50, 50, 50);

        prompt.Should().Contain("unknown");
    }

    [Fact]
    public void Build_ContainsHeatLevel()
    {
        var prompt = RiskExplanationPrompt.Build(
            "Test Corp", "enterprise", "churn", 90, "red", 90, 20, 10);

        prompt.Should().Contain("red");
    }
}
