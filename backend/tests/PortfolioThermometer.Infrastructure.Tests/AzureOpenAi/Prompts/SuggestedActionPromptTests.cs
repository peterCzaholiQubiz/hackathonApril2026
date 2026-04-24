using FluentAssertions;
using PortfolioThermometer.Infrastructure.AzureOpenAi.Prompts;
using Xunit;

namespace PortfolioThermometer.Infrastructure.Tests.AzureOpenAi.Prompts;

public sealed class SuggestedActionPromptTests
{
    [Fact]
    public void Build_ContainsCustomerName()
    {
        var prompt = SuggestedActionPrompt.Build(
            "Acme Corp", "enterprise",
            "Churn risk is high due to low engagement.",
            "Payment delays suggest cash flow issues.",
            "Margin has declined significantly.",
            85, "red");

        prompt.Should().Contain("Acme Corp");
    }

    [Fact]
    public void Build_ContainsAllThreeRiskExplanations()
    {
        var churn = "Churn risk is high due to low engagement.";
        var payment = "Payment delays suggest cash flow issues.";
        var margin = "Margin has declined significantly.";

        var prompt = SuggestedActionPrompt.Build(
            "Acme Corp", "enterprise", churn, payment, margin, 85, "red");

        prompt.Should().Contain(churn);
        prompt.Should().Contain(payment);
        prompt.Should().Contain(margin);
    }

    [Fact]
    public void Build_ContainsAllActionTypes()
    {
        var prompt = SuggestedActionPrompt.Build(
            "Test Corp", "smb", "x", "y", "z", 70, "yellow");

        prompt.Should().Contain("outreach");
        prompt.Should().Contain("discount");
        prompt.Should().Contain("review");
        prompt.Should().Contain("escalate");
        prompt.Should().Contain("upsell");
    }

    [Fact]
    public void Build_ContainsJsonArrayInstruction()
    {
        var prompt = SuggestedActionPrompt.Build(
            "Test Corp", null, "x", "y", "z", 50, "green");

        prompt.Should().Contain("JSON array");
        prompt.Should().Contain("action_type");
        prompt.Should().Contain("priority");
        prompt.Should().Contain("title");
        prompt.Should().Contain("description");
    }

    [Fact]
    public void Build_ContainsOverallScore()
    {
        var prompt = SuggestedActionPrompt.Build(
            "Test Corp", "enterprise", "x", "y", "z", 92, "red");

        prompt.Should().Contain("92");
    }

    [Fact]
    public void Build_UsesUnknownWhenSegmentIsNull()
    {
        var prompt = SuggestedActionPrompt.Build(
            "No Segment Corp", null, "x", "y", "z", 50, "green");

        prompt.Should().Contain("unknown");
    }
}
