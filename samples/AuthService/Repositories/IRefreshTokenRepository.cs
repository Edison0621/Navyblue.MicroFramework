namespace AuthService.Repositories;

public interface IRefreshTokenRepository
{
    Task SaveAsync(string refreshToken, Guid userId, CancellationToken cancellationToken);
    Task<Guid?> GetUserIdAsync(string refreshToken, CancellationToken cancellationToken);
    Task DeleteAsync(string refreshToken, CancellationToken cancellationToken);
}
