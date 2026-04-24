namespace DaprFx.Core;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DaprInvokeAttribute(string path) : Attribute
{
    public string Path { get; } = path;
}
