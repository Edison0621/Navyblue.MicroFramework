namespace DaprFx.Core;

public sealed class ServiceClientRegistration(Type interfaceType, string[] appIds, ServiceClientOptions options)
{
    public Type InterfaceType { get; } = interfaceType;
    public string[] AppIds { get; } = appIds;
    public ServiceClientOptions Options { get; } = options;
}
