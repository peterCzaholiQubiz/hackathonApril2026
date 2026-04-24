namespace PortfolioThermometer.Infrastructure.AzureOpenAi.Prompts;

public static class SuggestedActionPrompt
{
    public static string Build(
        string customerName,
        string? segment,
        string churnExplanation,
        string paymentExplanation,
        string marginExplanation,
        int overallScore,
        string heatLevel)
    {
        var safeName = Sanitize(customerName);
        var safeSegment = Sanitize(segment ?? "unknown");
        var safeChurn = Sanitize(churnExplanation);
        var safePayment = Sanitize(paymentExplanation);
        var safeMargin = Sanitize(marginExplanation);

        return $$"""
            You are an advisory AI assistant for a portfolio management system.

            Customer: "{{safeName}}" (segment: {{safeSegment}})
            Overall risk score: {{overallScore}}/100, Heat level: {{heatLevel}}

            Risk analysis across all three dimensions:
            - Churn risk: {{safeChurn}}
            - Payment risk: {{safePayment}}
            - Margin risk: {{safeMargin}}

            Based on all three risk dimensions above, suggest 1-3 specific, concrete actions
            for the account manager. Actions must be tailored to this customer's situation,
            not generic advice.

            Action types (use exactly one of): outreach, discount, review, escalate, upsell
            Priorities (use exactly one of): high, medium, low

            Respond ONLY with a valid JSON array in this exact format:
            [{ "action_type": "outreach", "priority": "high", "title": "Action title", "description": "Specific description" }]
            """;
    }

    private static string Sanitize(string value) =>
        value.Replace("\"", "'").Replace("\n", " ").Replace("\r", " ")[..Math.Min(value.Length, 300)];
}
