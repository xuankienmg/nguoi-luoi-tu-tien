using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Online;

/// <summary>Idempotency keys only need to outlive client retries; drop them after a day so the table stays small.</summary>
public sealed class OnlineRequestCleanup(IServiceScopeFactory scopes, ILogger<OnlineRequestCleanup> logger) : BackgroundService
{
    public static Task<int> DeleteExpiredAsync(GameDbContext db, DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddDays(-1);
        return db.OnlineRequests.Where(r => r.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do {
            try {
                using var scope = scopes.CreateScope();
                var removed = await DeleteExpiredAsync(scope.ServiceProvider.GetRequiredService<GameDbContext>(), DateTime.UtcNow, ct);
                if (removed > 0) logger.LogInformation("Removed {Count} expired online requests", removed);
            } catch (Exception e) when (e is not OperationCanceledException) {
                logger.LogWarning(e, "Online request cleanup failed");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
