namespace DaprFx.Core;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class DaprTopicAttribute(string topic) : Attribute
{
    public string Topic { get; } = topic;
}
