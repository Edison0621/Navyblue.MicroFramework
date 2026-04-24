using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DaprFx.Configuration;

public sealed class DaprConfigurationProvider(DaprClient client, string configStore, IEnumerable<string> keys, TimeSpan? refreshInterval = null) : ConfigurationProvider, IDisposable
{
    private readonly DaprClient _client = client;
    private readonly string _configStore = configStore;
    private readonly IReadOnlyList<string> _keys = keys.ToList();
    private readonly TimeSpan _refreshInterval = refreshInterval ?? TimeSpan.FromSeconds(10);
    private readonly object _sync = new();
    private CancellationTokenSource? _refreshCts;
    private Task? _refreshTask;
    private int _consecutiveFailures;

    public override void Load()
    {
        try
        {
            var values = LoadSnapshot(default);
            lock (_sync)
            {
                Data = values;
            }
        }
        catch
        {
            // Allow service startup when Dapr sidecar/config store is temporarily unavailable.
            if (Data.Count == 0)
            {
                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public void StartRefreshLoop(ILogger? logger = null)
    {
        if (_refreshTask is not null)
        {
            return;
        }

        _refreshCts = new CancellationTokenSource();
        _refreshTask = Task.Run(async () =>
        {
            while (!_refreshCts.IsCancellationRequested)
            {
                var delay = _refreshInterval;
                try
                {
                    var latest = LoadSnapshot(_refreshCts.Token);
                    var changed = false;
                    lock (_sync)
                    {
                        if (DaprConfigurationChangeDetector.HasChanged(Data, latest))
                        {
                            Data = latest;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        OnReload();
                    }

                    _consecutiveFailures = 0;
                }
                catch (Exception ex) when (!_refreshCts.IsCancellationRequested)
                {
                    _consecutiveFailures++;
                    delay = DaprConfigurationRefreshBackoff.CalculateDelay(_consecutiveFailures, _refreshInterval);
                    logger?.LogWarning(ex, "Failed to refresh Dapr configuration store {ConfigStore}", _configStore);
                }

                await Task.Delay(delay, _refreshCts.Token);
            }
        }, _refreshCts.Token);
    }

    private Dictionary<string, string?> LoadSnapshot(CancellationToken cancellationToken)
    {
        var response = _client.GetConfiguration(_configStore, _keys, metadata: null, cancellationToken: cancellationToken)
            .GetAwaiter()
            .GetResult();
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in response.Items)
        {
            values[item.Key] = item.Value.Value;
        }
        return values;
    }

    public void Dispose()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
    }
}
