using PortfolioThermometer.Core.Models;

namespace PortfolioThermometer.Core.Interfaces;

public interface IClaudeExplanationService
{
    Task GenerateExplanationsAsync(IReadOnlyList<RiskScore> scores, CancellationToken ct);

    Task<IReadOnlyList<SuggestedAction>> GenerateSuggestedActionsAsync(Guid customerId, CancellationToken ct);
}
