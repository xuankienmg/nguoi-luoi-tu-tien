using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;
using VanDao.Online;
using Xunit;

namespace GemSystemTests;

/// <summary>OnlineBoundary gộp lệnh để bớt lượt đi về Supabase (11/2026): khóa + kiểm tra nhân vật, idempotency + giới hạn tốc độ.</summary>
public class OnlineRoundTripTests
{
    [Fact]
    public void Header_shows_round_trips_per_step_and_transaction_ops()
    {
        var t = new DbTiming { Commands = 3, CommandMs = 90, Opens = 1, TxOps = 1, TxMs = 30 };
        Assert.Equal("lock;dur=45.0;desc=\"1 rt\", total;dur=150.0, db;dur=90.0;desc=\"3 sql\", tx;dur=30.0;desc=\"1 tx\", conn;dur=0.0;desc=\"1 open\"",
            t.Header(("lock", 45, 1), ("total", 150, -1)));
    }

    [Fact]
    public async Task Transaction_interceptor_counts_commit_and_savepoint()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        await using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection)
            .AddInterceptors(new DbTimingCommandInterceptor(), new DbTimingTransactionInterceptor()).Options);
        db.Database.EnsureCreated();
        db.Timing = new DbTiming();
        var clock = new StepClock(db.Timing);
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1");
            clock.Mark("a");
            await tx.CreateSavepointAsync("s");
            await tx.RollbackToSavepointAsync("s");
            await tx.CommitAsync();
            clock.Mark("b");
        }
        Assert.Equal(3, db.Timing.TxOps);
        Assert.Equal([1, 3], clock.Steps.Select(s => s.Trips));
    }

    [Fact]
    public async Task Recent_requests_returns_previous_with_response_and_counts_recent()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        await using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        await CheckRecentRequests(db);
    }

    /// <summary>Chạy với Postgres thật: đặt VANDAO_TEST_PG (connection string tới DB thử, sẽ bị xóa / tạo lại). Không đặt thì bỏ qua.</summary>
    [Fact]
    public async Task Postgres_lock_checks_owner_and_serializes_same_account()
    {
        if (Pg() is not { } cs) return;
        var owner = Guid.NewGuid(); var other = Guid.NewGuid(); var playerId = Guid.NewGuid();
        await using (var setup = Db(cs))
        {
            await setup.Database.EnsureDeletedAsync(); await setup.Database.EnsureCreatedAsync();
            setup.Players.Add(new Player { Id = playerId, OwnerId = owner, Name = "Thử" });
            await setup.SaveChangesAsync();
            await CheckRecentRequests(setup);
        }

        await using var a = Db(cs);
        await using var txA = await a.Database.BeginTransactionAsync();
        Assert.True(await OnlineBoundary.LockAsync(a, owner, playerId, default));
        Assert.True(await OnlineBoundary.LockAsync(a, owner, null, default));

        await using (var c = Db(cs))   // tài khoản khác: không chờ, không sở hữu nhân vật
        {
            await using var txC = await c.Database.BeginTransactionAsync();
            Assert.False(await OnlineBoundary.LockAsync(c, other, playerId, default));
            Assert.False(await OnlineBoundary.LockAsync(c, other, Guid.NewGuid(), default));
        }

        await using var b = Db(cs);    // cùng tài khoản: chờ tới khi a commit
        await using var txB = await b.Database.BeginTransactionAsync();
        var waiting = OnlineBoundary.LockAsync(b, owner, playerId, default);
        await Task.Delay(400);
        Assert.False(waiting.IsCompleted);
        await txA.CommitAsync();
        Assert.True(await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    /// <summary>OnlineBoundary tắt savepoint tự động của EF: dòng trùng khóa (yêu cầu song song) không được làm hỏng transaction.</summary>
    [Fact]
    public async Task Postgres_duplicate_row_keeps_transaction_usable_without_auto_savepoints()
    {
        if (Pg() is not { } cs) return;
        await using (var setup = Db(cs)) { await setup.Database.EnsureDeletedAsync(); await setup.Database.EnsureCreatedAsync(); }
        await using (var first = Db(cs))
        {
            first.Sects.AddRange(new Sect { Id = 7, Name = "Bảy", NameKey = "bay" }, new Sect { Id = 8, Name = "Tám", NameKey = "tam" });
            first.SectBosses.Add(new SectBoss { SectId = 7, Day = "2026-11-26", BossId = "a", MaxHp = 1 });
            await first.SaveChangesAsync();
        }
        await using var db = Db(cs);
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Database.AutoSavepointsEnabled = false;
        db.SectBosses.Add(new SectBoss { SectId = 7, Day = "2026-11-26", BossId = "b", MaxHp = 2 });
        Assert.False(await ServerSectBoss.TrySaveAsync(db));
        db.ChangeTracker.Clear();
        db.SectBosses.Add(new SectBoss { SectId = 8, Day = "2026-11-26", BossId = "c", MaxHp = 3 });
        Assert.True(await ServerSectBoss.TrySaveAsync(db));
        await tx.CommitAsync();
        await using var check = Db(cs);
        Assert.Equal(["a", "c"], await check.SectBosses.OrderBy(x => x.SectId).Select(x => x.BossId).ToListAsync());
    }

    /// <summary>Mặc nhanh chỉ thử một bản trong các bản sao giống nhau: túi 30 món trùng vẫn mặc đúng 1, còn 29.</summary>
    [Fact]
    public async Task Equip_best_with_many_identical_copies_equips_one()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        await using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Thử Mặc");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        var slot = GameData.Realms[p.Realm].EquipSlots.First(s => p.Equipment.All(e => e.Slot != s));
        var gear = GameData.AllItems.First(i => i.Slot == slot && i.MinRealm <= p.Realm && GameData.UpgradeGear(i.Id) is null);
        p.Add(gear.Id, 30);
        game.EquipBest(p);
        Assert.Equal(gear.Id, p.Equipment.Single(e => e.Slot == slot).ItemId);
        Assert.Equal(29, p.Count(gear.Id));
        var (msg, _) = game.EquipBest(p);   // lần hai: không còn gì tốt hơn
        Assert.Contains("đã là tốt nhất", msg);
    }

    private static async Task CheckRecentRequests(GameDbContext db)
    {
        var owner = Guid.NewGuid(); var key = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.OnlineRequests.AddRange(
            new OnlineRequest { OwnerId = owner, RequestId = key, Fingerprint = "fp", Response = "packed", Status = 200, CreatedAt = now.AddMinutes(-5) },
            new OnlineRequest { OwnerId = owner, RequestId = Guid.NewGuid(), Response = "other", CreatedAt = now.AddMilliseconds(-200) },
            new OnlineRequest { OwnerId = owner, RequestId = Guid.NewGuid(), Response = "other", CreatedAt = now.AddMilliseconds(-300) },
            new OnlineRequest { OwnerId = owner, RequestId = Guid.NewGuid(), CreatedAt = now.AddSeconds(-5) },
            new OnlineRequest { OwnerId = Guid.NewGuid(), RequestId = Guid.NewGuid(), CreatedAt = now });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var since = now.AddSeconds(-1);

        var (previous, recent) = await OnlineBoundary.RecentRequestsAsync(db, owner, key, since, default);
        Assert.NotNull(previous);
        Assert.Equal(("fp", "packed", 200), (previous.Fingerprint, previous.Response, previous.Status));
        Assert.Equal(2, recent);

        var (none, recent2) = await OnlineBoundary.RecentRequestsAsync(db, owner, Guid.NewGuid(), since, default);
        Assert.Null(none);
        Assert.Equal(2, recent2);
    }

    private static string? Pg() => Environment.GetEnvironmentVariable("VANDAO_TEST_PG") is { Length: > 0 } s ? s : null;

    private static GameDbContext Db(string cs) => new(new DbContextOptionsBuilder<GameDbContext>().UseNpgsql(cs).Options);
}
