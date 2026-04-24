using DaprFx.EventBus;

namespace DaprFx.Tests;

public class OutboxRetryPolicyTests
{
    [Theory]
    [InlineData(1, 5, false)]
    [InlineData(5, 5, false)]
    [InlineData(6, 5, true)]
    [InlineData(1, 0, true)]
    public void ShouldMoveToDeadLetter_ReturnsExpected(int nextAttempt, int maxRetries, bool expected)
    {
        var result = OutboxRetryPolicy.ShouldMoveToDeadLetter(nextAttempt, maxRetries);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(2, 1, 2)]
    [InlineData(2, 2, 4)]
    [InlineData(2, 3, 8)]
    [InlineData(1, 3, 1)]
    [InlineData(0, 3, 1)]
    public void CalculateDelay_ReturnsExponentialBackoffInSeconds(int baseDelay, int attempt, double expectedSeconds)
    {
        var delay = OutboxRetryPolicy.CalculateDelay(baseDelay, attempt);
        Assert.Equal(expectedSeconds, delay.TotalSeconds);
    }
}
