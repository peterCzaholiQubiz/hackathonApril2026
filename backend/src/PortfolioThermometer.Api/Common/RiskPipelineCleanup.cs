using Microsoft.EntityFrameworkCore;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Api.Common;

internal static class RiskPipelineCleanup
{
    public static async Task CleanupSnapshotAsync(AppDbContext db, Guid snapshotId, CancellationToken ct)
    {
        var scoreIds = await db.RiskScores
            .Where(r => r.SnapshotId == snapshotId)
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (scoreIds.Count > 0)
        {
            var explanations = await db.RiskExplanations
                .Where(e => scoreIds.Contains(e.RiskScoreId))
                .ToListAsync(ct);
            var actions = await db.SuggestedActions
                .Where(a => scoreIds.Contains(a.RiskScoreId))
                .ToListAsync(ct);
            var scores = await db.RiskScores
                .Where(r => r.SnapshotId == snapshotId)
                .ToListAsync(ct);

            db.RiskExplanations.RemoveRange(explanations);
            db.SuggestedActions.RemoveRange(actions);
            db.RiskScores.RemoveRange(scores);
        }

        var snapshot = await db.PortfolioSnapshots.FirstOrDefaultAsync(s => s.Id == snapshotId, ct);
        if (snapshot is not null)
            db.PortfolioSnapshots.Remove(snapshot);

        await db.SaveChangesAsync(ct);
    }
}
