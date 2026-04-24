using System.Threading;

namespace PortfolioThermometer.Api.Common;

internal static class RiskRunGuard
{
    private static int _isRunning;

    public static bool TryStart()
        => Interlocked.CompareExchange(ref _isRunning, 1, 0) == 0;

    public static void Complete()
        => Interlocked.Exchange(ref _isRunning, 0);
}
