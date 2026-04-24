using Dapr.Client;
using Microsoft.Extensions.Configuration;

namespace DaprFx.Configuration;

public sealed class DaprConfigurationSource(DaprClient client, string configStore, IEnumerable<string> keys) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new DaprConfigurationProvider(client, configStore, keys);
}
