namespace DaprFx.EventBus;

public static class OutboxRetryPolicy
{
    public static bool ShouldMoveToDeadLetter(int nextAttempt, int maxRetryCount)
        => nextAttempt > Math.Max(0, maxRetryCount);

    public static TimeSpan CalculateDelay(int baseDelaySeconds, int nextAttempt)
    {
        var safeBase = Math.Max(1, baseDelaySeconds);
        var safeAttempt = Math.Max(1, nextAttempt);
        var seconds = Math.Pow(safeBase, safeAttempt);
        return TimeSpan.FromSeconds(seconds);
    }
}
