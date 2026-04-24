namespace DaprFx.Core;

public interface IStateStore<T> where T : class
{
    Task<T?> GetAsync(string key, CancellationToken ct = default);
    Task SaveAsync(string key, T value, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
