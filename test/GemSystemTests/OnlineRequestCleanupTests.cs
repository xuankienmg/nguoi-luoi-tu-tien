using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Online;

namespace GemSystemTests;

public class OnlineRequestCleanupTests
{
    [Fact]
    public async Task OnlyRequestsOlderThanOneDayAreDeleted()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var owner = Guid.NewGuid();
        db.OnlineRequests.AddRange(
            new OnlineRequest { OwnerId = owner, RequestId = Guid.NewGuid(), CreatedAt = now.AddDays(-2) },
            new OnlineRequest { OwnerId = owner, RequestId = Guid.NewGuid(), CreatedAt = now.AddHours(-23) });
        await db.SaveChangesAsync();

        Assert.Equal(1, await OnlineRequestCleanup.DeleteExpiredAsync(db, now, default));
        Assert.Equal(now.AddHours(-23), Assert.Single(await db.OnlineRequests.AsNoTracking().ToListAsync()).CreatedAt);
    }
}
