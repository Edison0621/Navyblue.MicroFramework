namespace DaprFx.Core;

public interface IIdempotencyStore
{
    Task<bool> TryBeginAsync(string key, CancellationToken cancellationToken = default);
    Task CompleteAsync(string key, CancellationToken cancellationToken = default);
}
