namespace PortfolioThermometer.Infrastructure.AzureOpenAi.Prompts;

public static class RiskExplanationPrompt
{
    public static string Build(
        string customerName,
        string? segment,
        string riskType,
        int riskScore,
        string heatLevel,
        int churnScore,
        int paymentScore,
        int marginScore)
    {
        var safeName = Sanitize(customerName);
        var safeSegment = Sanitize(segment ?? "unknown");

        return $$"""
            You are an advisory AI assistant for a portfolio management system.

            Analyze the {{riskType}} risk for customer "{{safeName}}" (segment: {{safeSegment}}).

            Risk scores:
            - {{riskType}} score: {{riskScore}}/100
            - Churn: {{churnScore}}/100
            - Payment: {{paymentScore}}/100
            - Margin: {{marginScore}}/100
            - Overall heat level: {{heatLevel}}

            Provide a 2-3 sentence advisory explanation for why this {{riskType}} risk score was assigned,
            citing the specific signals that likely contributed. Use advisory tone: phrases like
            "may indicate", "consider", "suggests", "could reflect". Do not be prescriptive.

            Respond ONLY with valid JSON in this exact format:
            { "explanation": "2-3 sentence advisory explanation here", "confidence": "high" }
            The confidence field must be exactly: high, medium, or low.
            """;
    }

    private static string Sanitize(string value) =>
        value.Replace("\"", "'").Replace("\n", " ").Replace("\r", " ")[..Math.Min(value.Length, 100)];
}
