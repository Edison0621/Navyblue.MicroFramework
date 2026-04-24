namespace DaprFx.Configuration;

internal static class DaprConfigurationChangeDetector
{
    public static bool HasChanged(
        IDictionary<string, string?> current,
        IDictionary<string, string?> latest)
    {
        if (current.Count != latest.Count)
        {
            return true;
        }

        foreach (var item in current)
        {
            if (!latest.TryGetValue(item.Key, out var value))
            {
                return true;
            }

            if (!string.Equals(item.Value, value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
