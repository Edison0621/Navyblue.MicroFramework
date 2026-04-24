using System.Diagnostics;

namespace DaprFx.Operations;

public sealed class ProcessRuntimeDashboardContributor : IOpsDashboardContributor
{
    private readonly Process _currentProcess = Process.GetCurrentProcess();
    private TimeSpan _lastCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
    private DateTimeOffset _lastCpuSampleAt = DateTimeOffset.UtcNow;

    public string SectionName => "runtime";

    public Task<object?> BuildAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var cpuNow = _currentProcess.TotalProcessorTime;
        var elapsedMs = (now - _lastCpuSampleAt).TotalMilliseconds;
        var cpuMs = (cpuNow - _lastCpuTime).TotalMilliseconds;

        _lastCpuSampleAt = now;
        _lastCpuTime = cpuNow;

        var cpuPercent = elapsedMs <= 0
            ? 0
            : Math.Clamp(cpuMs / (elapsedMs * Environment.ProcessorCount) * 100, 0, 100);

        return Task.FromResult<object?>(new
        {
            CpuPercent = Math.Round(cpuPercent, 2),
            MemoryMb = Math.Round(_currentProcess.WorkingSet64 / (1024d * 1024d), 2),
            ManagedMemoryMb = Math.Round(GC.GetTotalMemory(false) / (1024d * 1024d), 2),
            ThreadCount = _currentProcess.Threads.Count,
            HandleCount = _currentProcess.HandleCount,
            UptimeMinutes = Math.Round((now - _currentProcess.StartTime.ToUniversalTime()).TotalMinutes, 1)
        });
    }
}
