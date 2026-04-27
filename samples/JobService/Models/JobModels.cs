namespace JobService.Models;

public sealed record JobRun(string Id, string JobName, string Status, string Message, DateTimeOffset StartedAt);
