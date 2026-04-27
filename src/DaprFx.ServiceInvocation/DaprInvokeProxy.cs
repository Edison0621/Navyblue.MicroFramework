using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Dapr.Client;
using DaprFx.Core;

namespace DaprFx.ServiceInvocation;

internal class DaprInvokeProxy : DispatchProxy
{
    private static readonly ConcurrentDictionary<string, CircuitBreakerState> CircuitStates = new();
    private DaprClient? _daprClient;
    private string? _appId;
    private InvocationPolicyOptions _policy = new();

    public void Initialize(DaprClient daprClient, string appId, InvocationPolicyOptions? invocationPolicyOptions = null)
    {
        _daprClient = daprClient;
        _appId = appId;
        _policy = invocationPolicyOptions ?? new InvocationPolicyOptions();
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        ArgumentNullException.ThrowIfNull(_daprClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(_appId);

        if (targetMethod.ReturnType != typeof(Task) &&
            !(targetMethod.ReturnType.IsGenericType && targetMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
        {
            throw new NotSupportedException($"Method {targetMethod.Name} must return Task or Task<T>.");
        }

        var invokeAttr = targetMethod.GetCustomAttribute<DaprInvokeAttribute>();
        if (invokeAttr is null)
        {
            throw new NotSupportedException($"Method {targetMethod.Name} does not define DaprInvokeAttribute.");
        }

        var parameters = targetMethod.GetParameters();
        var path = invokeAttr.Path;
        object? requestBody = null;
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameterName = parameters[i].Name ?? $"arg{i}";
            var placeholder = $"{{{parameterName}}}";
            if (path.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Replace(placeholder, Uri.EscapeDataString(Convert.ToString(args?[i]) ?? string.Empty), StringComparison.OrdinalIgnoreCase);
            }
            else if (requestBody is null)
            {
                requestBody = args?[i];
            }
        }

        var resultType = targetMethod.ReturnType == typeof(Task) ? typeof(VoidResult) : targetMethod.ReturnType.GetGenericArguments()[0];
        var method = typeof(DaprInvokeProxy).GetMethod(nameof(InvokeAsyncCore), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(resultType);
        var task = method.Invoke(this, [path, requestBody])!;
        if (targetMethod.ReturnType == typeof(Task))
        {
            return ((Task<VoidResult>)task).ContinueWith(_ => { }, TaskScheduler.Default);
        }

        return task;
    }

    private async Task<TResult?> InvokeAsyncCore<TResult>(string path, object? requestBody)
    {
        EnsureCircuitClosed(path);
        using var client = DaprClient.CreateInvokeHttpClient(_appId);
        Exception? lastException = null;

        for (var attempt = 0; attempt <= _policy.MaxRetries; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_policy.TimeoutSeconds));
                HttpResponseMessage response;
                if (requestBody is null)
                {
                    response = await client.GetAsync(path, cts.Token);
                }
                else
                {
                    response = await client.PostAsJsonAsync(path, requestBody, cts.Token);
                }

                response.EnsureSuccessStatusCode();
                ResetCircuit(path);
                if (typeof(TResult) == typeof(VoidResult))
                {
                    return (TResult?)(object?)VoidResult.Instance;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                return await JsonSerializer.DeserializeAsync<TResult>(stream, cancellationToken: cts.Token);
            }
            catch (Exception ex) when (attempt < _policy.MaxRetries)
            {
                lastException = ex;
                RegisterFailure(path);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)));
            }
            catch (Exception ex)
            {
                lastException = ex;
                RegisterFailure(path);
                break;
            }
        }

        throw new HttpRequestException($"Failed invoking service {_appId}{path} after {_policy.MaxRetries + 1} attempts.", lastException);
    }

    private void EnsureCircuitClosed(string path)
    {
        if (!CircuitStates.TryGetValue(BuildCircuitKey(path), out var state))
        {
            return;
        }

        if (state.OpenUntilUtc > DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException($"Circuit breaker is open for {_appId}{path} until {state.OpenUntilUtc:O}.");
        }
    }

    private void RegisterFailure(string path)
    {
        var key = BuildCircuitKey(path);
        CircuitStates.AddOrUpdate(
            key,
            _ => new CircuitBreakerState(1, DateTimeOffset.MinValue),
            (_, current) =>
            {
                var failureCount = current.FailureCount + 1;
                if (failureCount >= _policy.FailureThreshold)
                {
                    return new CircuitBreakerState(0, DateTimeOffset.UtcNow.AddSeconds(_policy.OpenSeconds));
                }

                return new CircuitBreakerState(failureCount, DateTimeOffset.MinValue);
            });
    }

    private void ResetCircuit(string path) => CircuitStates.TryRemove(BuildCircuitKey(path), out _);

    private string BuildCircuitKey(string path) => $"{_appId}:{path}";

    private sealed record CircuitBreakerState(int FailureCount, DateTimeOffset OpenUntilUtc);

    private sealed class VoidResult
    {
        public static readonly VoidResult Instance = new();
    }
}
