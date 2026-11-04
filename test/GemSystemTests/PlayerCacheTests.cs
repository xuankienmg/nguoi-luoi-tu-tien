using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>
/// Cache cây nhân vật trong RAM (PlayerCache, chế độ Server): lần đồng bộ sau không đọc lại Player / Inventory / Equipment / Pets.
/// Chạy trên SQLite: PostgreSQL dùng cột hệ thống xmin làm concurrency token; ở đây XminSqliteContext mô phỏng bằng cột Version
/// tăng mỗi lần UPDATE (phần ánh xạ xmin của Npgsql chỉ được kiểm qua migration / model snapshot, chưa chạy trên Postgres thật).
/// </summary>
public class PlayerCacheTests : IDisposable
{
    private readonly SqliteConnection conn = new("Data Source=:memory:");
    private readonly SqlLog sql = new();
    private readonly FakeClock clock = new();
    private readonly PlayerCache cache;

    public PlayerCacheTests()
    {
        conn.Open();
        cache = new PlayerCache(clock);
        using var db = NewDb();
        db.Database.EnsureCreated();
    }

    public void Dispose() => conn.Dispose();

    private GameDbContext NewDb() => new XminSqliteContext(new DbContextOptionsBuilder<GameDbContext>()
        .UseSqlite(conn).AddInterceptors(sql, new XminBump()).Options);

    private static readonly ItemDef Gear = GameData.AllItems.First(i => i.Slot is { } s && GameData.Realms[0].EquipSlots.Contains(s) && GameData.UpgradeGear(i.Id) is null);

    /// <summary>Nhân vật chưa chọn khởi đầu: Tick chỉ dời mốc thời gian, nên số vật phẩm không đổi ngẫu nhiên.</summary>
    private async Task<Guid> AddPlayer()
    {
        using var db = NewDb();
        var p = new Player { Name = "Lâm Phong", StarterChosen = false, ManualVersion = 1, LbSecret = "x" };
        p.Add("linh_thach", 1000);
        p.Add("tu_khi_dan", 5);
        p.Equipment.Add(new EquippedItem { PlayerId = p.Id, Slot = Gear.Slot!, ItemId = Gear.Id });
        p.Pets.Add(new Pet { PlayerId = p.Id, DefId = PetData.Definitions[0].Id });
        db.Players.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<(Player p, StateDto state)> Act(Guid id, Func<Player, GameDbContext, (string?, string?)> action, bool transaction = false)
    {
        using var db = NewDb();
        var game = new GameService(db, playerCache: cache);
        await using var tx = transaction ? await db.Database.BeginTransactionAsync() : null;   // như OnlineBoundary
        var (p, _) = await game.ActAsync(id, p => Task.FromResult(action(p, db)));
        var state = await game.BuildStateAsync(p);
        if (tx is not null) await tx.CommitAsync();
        return (p, state);
    }

    private Task<(Player p, StateDto state)> Sync(Guid id) => Act(id, (_, _) => (null, null));

    private decimal CountInDb(Guid id, string item)
    {
        using var db = NewDb();
        return db.Inventory.AsNoTracking().Where(i => i.PlayerId == id && i.ItemId == item).Select(i => (decimal?)i.Count).SingleOrDefault() ?? 0;
    }

    [Fact]
    public async Task SecondSyncDoesNotReadThePlayerTree()
    {
        var id = await AddPlayer();
        await Sync(id);
        sql.Clear();

        var (p, state) = await Sync(id);

        Assert.Empty(sql.Selects(t => t is "Players" or "Inventory" or "Equipment" or "Pets"));
        Assert.Empty(sql.Selects(t => t is "Logs"));   // nhật ký: Id mới nhất và số dòng giữ trong cache
        Assert.Contains(sql.Commands, c => c.Contains("UPDATE \"Players\""));   // vẫn ghi mốc thời gian
        Assert.Equal(1000, state.Inventory["linh_thach"]);
        Assert.Equal(Gear.Id, state.Equipment[Gear.Slot!]);
        Assert.Single(p.Pets);
    }

    [Fact]
    public async Task ExternalWriteIsDetectedReloadedAndNotDuplicated()
    {
        var id = await AddPlayer();
        await Sync(id);   // cây đã vào cache với Version cũ

        // nơi khác (instance cũ khi deploy rolling, sửa tay trong database) ghi vào nhân vật: PostgreSQL đổi xmin
        using (var other = NewDb())
        {
            await other.Inventory.Where(i => i.PlayerId == id && i.ItemId == "linh_thach").ExecuteUpdateAsync(s => s.SetProperty(i => i.Count, i => i.Count + 500));
            await other.Players.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Version, x => x.Version + 1));
        }

        var runs = 0;
        var (p, state) = await Act(id, (p, _) => { runs++; p.Add("tu_khi_dan", 1); return ("ok", ""); }, transaction: true);

        Assert.Equal(2, runs);   // xung đột → quay về savepoint, nạp lại, chạy lại một lần
        Assert.Equal(1500, CountInDb(id, "linh_thach"));   // không mất phần nơi khác ghi
        Assert.Equal(6, CountInDb(id, "tu_khi_dan"));       // không cộng hai lần
        Assert.Equal(1500, state.Inventory["linh_thach"]);
        Assert.True(cache.Contains(id));
        Assert.Same(p, cache.TryTake(id));
    }

    [Fact]
    public async Task ConflictTwiceGivesUpAndEvicts()
    {
        var id = await AddPlayer();
        await Sync(id);
        var runs = 0;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Act(id, (p, db) =>
        {
            runs++;
            p.Add("tu_khi_dan", 1);
            // mỗi lần chạy đều có người ghi chen vào trước khi lưu
            db.Database.ExecuteSql($"UPDATE \"Players\" SET \"Version\" = \"Version\" + 1 WHERE \"Id\" = {id}");
            return (null, null);
        }));
        Assert.Equal(2, runs);
        Assert.False(cache.Contains(id));
        Assert.Equal(5, CountInDb(id, "tu_khi_dan"));
    }

    [Fact]
    public async Task FailedActionEvictsTheModifiedTree()
    {
        var id = await AddPlayer();
        await Sync(id);
        Assert.True(cache.Contains(id));

        await Assert.ThrowsAsync<GameException>(() => Act(id, (p, _) =>
        {
            p.Add("linh_thach", 99_000);   // sửa dở cây rồi mới báo lỗi, không lưu
            throw new GameException("Không đủ điều kiện.");
        }));

        Assert.False(cache.Contains(id));
        var (_, state) = await Sync(id);
        Assert.Equal(1000, state.Inventory["linh_thach"]);
    }

    [Fact]
    public async Task AddingAndRemovingRowsOnAReattachedTreeIsSaved()
    {
        var id = await AddPlayer();
        await Sync(id);
        var newPet = new Pet { DefId = PetData.Definitions[1].Id };

        await Act(id, (p, db) =>
        {
            p.Add("linh_thu_dan", 7);                                   // dòng túi đồ mới
            p.Inventory.Remove(p.Inventory.Single(i => i.ItemId == "tu_khi_dan"));   // bỏ khỏi danh sách (orphan → DELETE)
            var e = p.Equipment.Single(); p.Equipment.Remove(e); db.Equipment.Remove(e); p.Add(e.ItemId, 1);
            var old = p.Pets.Single(); p.Pets.Remove(old); db.Pets.Remove(old);
            newPet.PlayerId = p.Id; p.Pets.Add(newPet); db.Pets.Add(newPet);
            return (null, null);
        });

        using (var db = NewDb())
        {
            var saved = await db.Players.AsNoTracking().Include(x => x.Inventory).Include(x => x.Equipment).Include(x => x.Pets).SingleAsync(x => x.Id == id);
            Assert.Equal(7, saved.Inventory.Single(i => i.ItemId == "linh_thu_dan").Count);
            Assert.DoesNotContain(saved.Inventory, i => i.ItemId == "tu_khi_dan");
            Assert.Equal(1, saved.Inventory.Single(i => i.ItemId == Gear.Id).Count);
            Assert.Empty(saved.Equipment);
            Assert.Equal(newPet.Id, Assert.Single(saved.Pets).Id);
        }

        // lần sau vẫn dùng cây trong cache, khớp database và lưu tiếp được
        sql.Clear();
        var (p2, state) = await Act(id, (p, _) => { p.Add("linh_thu_dan", 1); return (null, null); });
        Assert.Empty(sql.Selects(t => t is "Players" or "Inventory" or "Equipment" or "Pets"));
        Assert.Equal(8, state.Inventory["linh_thu_dan"]);
        Assert.Equal(8, CountInDb(id, "linh_thu_dan"));
        Assert.Single(p2.Pets);
    }

    [Fact]
    public async Task IdlePlayersExpireFromTheCache()
    {
        var id = await AddPlayer();
        await Sync(id);
        clock.Advance(PlayerCache.Idle - TimeSpan.FromMinutes(1));
        Assert.NotNull(cache.TryTake(id));   // còn dùng thì gia hạn

        clock.Advance(PlayerCache.Idle + TimeSpan.FromMinutes(1));
        sql.Clear();
        await Sync(id);
        Assert.NotEmpty(sql.Selects(t => t is "Players"));   // hết hạn → đọc lại database
        Assert.True(cache.Contains(id));

        // nhân vật không ai dùng nữa cũng bị dọn khi có request khác
        var other = await AddPlayer();
        clock.Advance(PlayerCache.Idle + TimeSpan.FromMinutes(2));
        await Sync(other);
        Assert.False(cache.Contains(id));
    }

    [Fact]
    public async Task LogsFromTheCacheMatchTheDatabaseAndAreTrimmed()
    {
        var id = await AddPlayer();
        using (var db = NewDb())
        {
            for (var i = 0; i < GameService.LogKeep - 2; i++) db.Logs.Add(new LogEntry { PlayerId = id, Text = $"cũ {i}" });
            await db.SaveChangesAsync();
        }
        await Sync(id);
        for (var i = 0; i < 5; i++)
            await Act(id, (p, db) =>
            {
                var entry = new LogEntry { PlayerId = p.Id, Text = $"mới {i}" };
                p.Logs.Add(entry); db.Logs.Add(entry);
                return (null, null);
            });
        var (_, state) = await Sync(id);

        using var check = NewDb();
        var expected = check.Logs.AsNoTracking().Where(l => l.PlayerId == id).OrderByDescending(l => l.Id).Take(GameService.LogShow).Select(l => l.Text).ToList();
        Assert.Equal(expected, state.Logs.Select(l => l.Text).ToList());
        Assert.Equal("mới 4", state.Logs[0].Text);
        Assert.Equal(GameService.LogKeep, check.Logs.Count(l => l.PlayerId == id));
    }

    [Fact]
    public async Task EmptyMarketClaimDoesNotLoadThePlayer()
    {
        var id = await AddPlayer();
        using var db = NewDb();
        var game = new GameService(db, serverMarket: new ServerMarket(db), playerCache: cache);
        sql.Clear();

        Assert.False(await game.MarketClaimPendingAsync(id));
        Assert.Empty(sql.Selects(t => t is "Players" or "Inventory" or "Equipment" or "Pets"));

        db.MarketListings.Add(new MarketListing { SellerId = id, SellerName = "Lâm Phong", ItemId = "tu_khi_dan", Qty = 1, Price = 10,
            Status = "sold", Payout = 9, CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(1), ClosedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        Assert.True(await game.MarketClaimPendingAsync(id));
    }

    [Fact]
    public async Task ResetForgetsTheCachedTree()
    {
        var id = await AddPlayer();
        await Sync(id);
        using (var db = NewDb())
        {
            var game = new GameService(db, playerCache: cache);
            await game.ResetAsync(await game.LoadAsync(id));
        }
        Assert.False(cache.Contains(id));
        var (_, state) = await Sync(id);
        Assert.Equal(GameData.StartingInventory["linh_thach"], state.Inventory["linh_thach"]);
    }

    /// <summary>SQLite: cột Version đóng vai xmin của PostgreSQL (concurrency token, đổi mỗi lần UPDATE).</summary>
    private sealed class XminSqliteContext(DbContextOptions<GameDbContext> options) : GameDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);
            b.Entity<Player>().Property(p => p.Version).IsConcurrencyToken();
        }
    }

    private sealed class XminBump : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default)
        {
            e.Context!.ChangeTracker.DetectChanges();
            foreach (var entry in e.Context.ChangeTracker.Entries<Player>())
                if (entry.State is EntityState.Modified) entry.Entity.Version = entry.Property(p => p.Version).OriginalValue + 1;
            return base.SavingChangesAsync(e, result, ct);
        }
    }

    private sealed class SqlLog : DbCommandInterceptor
    {
        public readonly List<string> Commands = [];
        public void Clear() { lock (Commands) Commands.Clear(); }

        /// <summary>Các lệnh SELECT đọc ít nhất một bảng thỏa điều kiện.</summary>
        public List<string> Selects(Func<string, bool> table) =>
            Commands.Where(c => c.StartsWith("SELECT") && System.Text.RegularExpressions.Regex.Matches(c, "(?:FROM|JOIN) \"(\\w+)\"")
                .Any(m => table(m.Groups[1].Value))).ToList();

        private void Add(DbCommand c) { lock (Commands) Commands.Add(c.CommandText.TrimStart()); }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r) { Add(c); return r; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default) { Add(c); return ValueTask.FromResult(r); }
        public override InterceptionResult<object> ScalarExecuting(DbCommand c, CommandEventData e, InterceptionResult<object> r) { Add(c); return r; }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default) { Add(c); return ValueTask.FromResult(r); }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand c, CommandEventData e, InterceptionResult<int> r) { Add(c); return r; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<int> r, CancellationToken ct = default) { Add(c); return ValueTask.FromResult(r); }
    }

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }
}
