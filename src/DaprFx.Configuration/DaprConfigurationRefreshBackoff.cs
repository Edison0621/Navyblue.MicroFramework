namespace DaprFx.Configuration;

internal static class DaprConfigurationRefreshBackoff
{
    public static TimeSpan CalculateDelay(
        int consecutiveFailures,
        TimeSpan normalRefreshInterval,
        TimeSpan? maxBackoff = null)
    {
        if (consecutiveFailures <= 0)
        {
            return normalRefreshInterval;
        }

        var max = maxBackoff ?? TimeSpan.FromMinutes(2);
        var multiplier = Math.Pow(2, Math.Min(10, consecutiveFailures - 1));
        var next = TimeSpan.FromMilliseconds(normalRefreshInterval.TotalMilliseconds * multiplier);
        return next <= max ? next : max;
    }
}
