namespace PortfolioThermometer.Infrastructure.AzureOpenAi.Prompts;

public static class SuggestedActionsEnhancedPrompt
{
    public static string Build(
        string customerName,
        string? segment,
        int overallScore,
        int churnScore,
        int paymentScore,
        int marginScore,
        string heatLevel,
        string? churnExplanation,
        string? paymentExplanation,
        string? marginExplanation,
        IReadOnlyList<(string? Date, string? Channel, string? Sentiment, string? Summary)> recentInteractions,
        int totalPayments,
        int latePaymentsCount,
        int heavilyLatePaymentsCount,
        double avgDaysLate)
    {
        var safeName = Sanitize(customerName);
        var safeSegment = Sanitize(segment ?? "unknown");
        var safeChurn = Sanitize(churnExplanation ?? "No explanation available.");
        var safePayment = Sanitize(paymentExplanation ?? "No explanation available.");
        var safeMargin = Sanitize(marginExplanation ?? "No explanation available.");

        var interactionLines = recentInteractions.Count > 0
            ? string.Join("\n", recentInteractions.Select(i =>
                $"  - {i.Date ?? "?"} | {i.Channel ?? "?"} | sentiment: {i.Sentiment ?? "?"} | {Sanitize(i.Summary ?? string.Empty, 80)}"))
            : "  No recent interactions recorded.";

        return $$"""
            You are an advisory AI assistant for a portfolio management system.

            Customer: "{{safeName}}" (segment: {{safeSegment}})
            Overall risk score: {{overallScore}}/100 (heat: {{heatLevel}})
            Breakdown — Churn: {{churnScore}}/100 | Payment: {{paymentScore}}/100 | Margin: {{marginScore}}/100

            Risk analysis:
            - Churn risk: {{safeChurn}}
            - Payment risk: {{safePayment}}
            - Margin risk: {{safeMargin}}

            Payment behaviour (recent history):
            - Total payments on record: {{totalPayments}}
            - Payments > 15 days late: {{latePaymentsCount}}
            - Payments > 30 days late: {{heavilyLatePaymentsCount}}
            - Average days late: {{avgDaysLate:F1}}

            Recent interactions (last {{recentInteractions.Count}}):
            {{interactionLines}}

            Based on all data above, suggest 1-3 specific, concrete actions for the account manager.
            Actions must be tailored to this customer's actual situation, not generic advice.

            Action types (use exactly one of): outreach, discount, review, escalate, upsell
            Priorities (use exactly one of): high, medium, low

            Respond ONLY with a valid JSON array in this exact format:
            [{ "action_type": "outreach", "priority": "high", "title": "Action title", "description": "Specific description" }]
            """;
    }

    private static string Sanitize(string value, int maxLen = 300) =>
        value.Replace("\"", "'").Replace("\n", " ").Replace("\r", " ")[..Math.Min(value.Length, maxLen)];
}
