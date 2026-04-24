using DaprFx.Configuration;

namespace DaprFx.Tests;

public class DaprConfigurationChangeDetectorTests
{
    [Fact]
    public void HasChanged_WhenSameContent_ReturnsFalse()
    {
        var current = new Dictionary<string, string?>
        {
            ["A"] = "1",
            ["B"] = "2"
        };
        var latest = new Dictionary<string, string?>
        {
            ["A"] = "1",
            ["B"] = "2"
        };

        var changed = DaprConfigurationChangeDetector.HasChanged(current, latest);
        Assert.False(changed);
    }

    [Fact]
    public void HasChanged_WhenValueChanged_ReturnsTrue()
    {
        var current = new Dictionary<string, string?> { ["A"] = "1" };
        var latest = new Dictionary<string, string?> { ["A"] = "2" };

        Assert.True(DaprConfigurationChangeDetector.HasChanged(current, latest));
    }

    [Fact]
    public void HasChanged_WhenKeyRemoved_ReturnsTrue()
    {
        var current = new Dictionary<string, string?> { ["A"] = "1", ["B"] = "2" };
        var latest = new Dictionary<string, string?> { ["A"] = "1" };

        Assert.True(DaprConfigurationChangeDetector.HasChanged(current, latest));
    }
}
