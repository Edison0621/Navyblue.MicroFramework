namespace DaprFx.Configuration;

public sealed class DaprConfigurationRegistration(string configStore, IReadOnlyList<string> keys)
{
    public string ConfigStore { get; } = configStore;
    public IReadOnlyList<string> Keys { get; } = keys;
}
