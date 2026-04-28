using Microsoft.Extensions.Hosting;
using UserService.Models;
using UserService.Repositories;
using UserService.Security;

namespace UserService;

/// <summary>
/// Retries seeding the demo merchant admin until Dapr/Redis is reachable (sidecar often becomes ready after the app process).
/// </summary>
public sealed class DemoAdminSeedHostedService(IServiceProvider services, ILogger<DemoAdminSeedHostedService> logger) : BackgroundService
{
    private static readonly Guid DemoAdminId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= 90 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                if (attempt > 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }

                using var scope = services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

                var existing = await repo.GetAsync(DemoAdminId, stoppingToken);
                if (existing is not null)
                {
                    return;
                }

                var ids = await repo.GetIdsAsync(stoppingToken);
                var admin = new UserRecord(
                    DemoAdminId,
                    "admin",
                    "admin@example.com",
                    UserPasswordHasher.Hash("admin123"),
                    "active",
                    ["admin"],
                    null);
                await repo.SaveAsync(admin, stoppingToken);
                ids.Add(DemoAdminId);
                await repo.SaveIdsAsync(ids, stoppingToken);
                logger.LogInformation("Seeded demo admin user after {Attempt} attempt(s).", attempt);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Demo admin seed attempt {Attempt}/90; will retry.", attempt);
            }
        }

        logger.LogError("Demo admin seed failed after 90 attempts; login may fail until UserService state is available.");
    }
}
