using DaprFx.Configuration;

namespace DaprFx.Tests;

public class DaprConfigurationRefreshBackoffTests
{
    [Fact]
    public void CalculateDelay_NoFailure_ReturnsNormalInterval()
    {
        var normal = TimeSpan.FromSeconds(5);
        var delay = DaprConfigurationRefreshBackoff.CalculateDelay(0, normal);
        Assert.Equal(normal, delay);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    public void CalculateDelay_UsesExponentialBackoff(int failures, int expectedSeconds)
    {
        var delay = DaprConfigurationRefreshBackoff.CalculateDelay(failures, TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public void CalculateDelay_IsCappedByMaxBackoff()
    {
        var delay = DaprConfigurationRefreshBackoff.CalculateDelay(
            consecutiveFailures: 10,
            normalRefreshInterval: TimeSpan.FromSeconds(5),
            maxBackoff: TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }
}
