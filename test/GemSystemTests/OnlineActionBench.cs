using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;
using VanDao.Online;
using Xunit;

namespace GemSystemTests;

/// <summary>
/// Đo số lượt đi về DB và thời gian xử lý (CPU) của từng hành động online, đi đúng đường của OnlineBoundary + Run/RunCraft
/// (khóa → idempotency → ActAsync → BuildStateAsync → lưu OnlineRequests → COMMIT), trên Postgres thật.
/// Chỉ chạy khi đặt VANDAO_BENCH_PG (DB thử, sẽ bị xóa / tạo lại); kết quả ghi vào VANDAO_BENCH_OUT (mặc định bench.txt cạnh test).
/// Ước tính trên máy chủ thật: lượt × ~50 ms (độ trễ tới Supabase Singapore) + CPU.
/// </summary>
public class OnlineActionBench
{
    private const double Rtt = 50;
    private readonly PlayerCache cache = new();
    private readonly ServerLeaderboardCache lbCache = new();
    private readonly ServerLogCache logCache = new();
    private readonly Guid owner = Guid.NewGuid();

    [Fact]
    public async Task Measure_common_actions()
    {
        if (Environment.GetEnvironmentVariable("VANDAO_BENCH_PG") is not { Length: > 0 } cs) return;
        await using (var setup = Db(cs)) { await setup.Database.EnsureDeletedAsync(); await setup.Database.EnsureCreatedAsync(); }
        var pid = await SeedAsync(cs);
        var gear = GameData.AllItems.Where(i => i.Slot is not null && i.MinRealm <= 3 && GameData.UpgradeGear(i.Id) is null).ToList();
        var weapon = gear.First(i => i.Slot == "weapon");
        var brew = GameData.Brews.First(b => GameData.ItemById[b.Out].MinRealm <= 3);

        var actions = new (string Name, bool Post, Func<GameService, Player, object?> Act)[] {
            ("sync (GET)", false, (g, p) => null),
            ("equip-best", true, (g, p) => g.EquipBest(p)),
            ("equip", true, (g, p) => g.Equip(p, weapon.Id)),
            ("open chest", true, (g, p) => g.UseItem(p, "ruong_thuong", 1)),
            ("sell gear", true, (g, p) => g.Sell(p, p.Inventory.First(i => i.Count > 1 && GameData.ItemById.TryGetValue(i.ItemId, out var d) && d.Slot is not null).ItemId, 1)),
            ("brew", true, (g, p) => g.Brew(p, brew.Out, 1)),
            ("fuse quick", true, (g, p) => g.QuickFuseGear(p)),
            ("forge strike", true, (g, p) => g.ForgeStrike(p, "weapon")),
            ("login claim", true, (g, p) => g.ClaimDailyLogin(p)),   // một lần mỗi ngày: chỉ lượt đầu thành công
            ("sync (GET)", false, (g, p) => null),
        };
        var report = new StringBuilder();
        report.AppendLine($"{"action",-14} {"rt",3} {"sql",4} {"tx",3} {"cpu ms",8}  {"≈ server ms",11}  result / steps");
        // lượt 0: process lạnh (JIT, EF biên dịch truy vấn); in lần thành công cuối cùng của mỗi hành động (hành động một lần/ngày: lượt 0)
        var last = new Dictionary<int, (DbTiming Timing, double Cpu, string Result, string Steps)>();
        for (var round = 0; round < 3; round++)
            for (var i = 0; i < actions.Length; i++)
            {
                var r = await RequestAsync(cs, pid, actions[i].Post, actions[i].Act);
                if (r.Result == "ok" || !last.ContainsKey(i) || last[i].Result != "ok") last[i] = r;
            }
        for (var i = 0; i < actions.Length; i++)
        {
            var (t, cpu, result, steps) = last[i];
            report.AppendLine($"{actions[i].Name,-14} {t.RoundTrips,3} {t.Commands,4} {t.TxOps,3} {cpu,8:0.0}  {t.RoundTrips * Rtt + cpu,11:0}  {result} | {steps}");
        }
        var path = Environment.GetEnvironmentVariable("VANDAO_BENCH_OUT") is { Length: > 0 } o ? o : Path.Combine(AppContext.BaseDirectory, "bench.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllTextAsync(path, report.ToString());
    }

    private async Task<(DbTiming Timing, double Cpu, string Result, string Steps)> RequestAsync(string cs, Guid pid, bool post, Func<GameService, Player, object?> act)
    {
        await using var db = Db(cs);
        db.RequestOwnerId = owner;
        db.Timing = new DbTiming(); var clock = new StepClock(db.Timing);
        var sw = Stopwatch.StartNew();
        var game = new GameService(db, board: new ServerLeaderboard(db, lbCache), serverMarket: new ServerMarket(db), serverArena: new ServerArena(db),
            logCache: logCache, donateStore: new DonateStore(db), playerCache: cache, serverChat: new ServerChat(db));
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Database.AutoSavepointsEnabled = Environment.GetEnvironmentVariable("VANDAO_BENCH_AUTOSAVEPOINT") == "1";   // như OnlineBoundary (1 = đo kiểu cũ)
        await OnlineBoundary.LockAsync(db, owner, pid, default); clock.Mark("lock");
        var requestId = Guid.NewGuid();
        if (post) await OnlineBoundary.RecentRequestsAsync(db, owner, requestId, DateTime.UtcNow.AddSeconds(-1), default);
        clock.Mark("guard");
        string result = "ok";
        try
        {
            var (p, _) = await game.ActAsync(pid, p => Task.FromResult(act(game, p)));
            await game.BuildStateAsync(p);
        }
        catch (GameException e) { result = "game error: " + e.Message[..Math.Min(40, e.Message.Length)]; }
        clock.Mark("app");
        if (result == "ok")
        {
            if (post) db.OnlineRequests.Add(new OnlineRequest { OwnerId = owner, RequestId = requestId, Fingerprint = "f", Response = new string('x', 2000), Status = 200 });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            cache.Committed(db);
        }
        else { await tx.RollbackAsync(); cache.Abandoned(db); }
        clock.Mark("commit");
        var cpu = sw.Elapsed.TotalMilliseconds - db.Timing.CommandMs - db.Timing.TxMs - db.Timing.OpenMs;
        return (db.Timing, cpu, result, string.Join(" ", clock.Steps.Select(s => $"{s.Name}={s.Trips}")));
    }

    /// <summary>Nhân vật giữa game: cảnh giới 3, túi nhiều trang bị trùng, nguyên liệu luyện đan, đan tu vi.</summary>
    private async Task<Guid> SeedAsync(string cs)
    {
        await using var db = Db(cs);
        db.RequestOwnerId = owner;
        var game = new GameService(db, board: new ServerLeaderboard(db, lbCache), playerCache: null);
        var p = await game.CreateAsync("Thử Tốc Độ");
        if (PetData.StarterEggs.Keys.FirstOrDefault() is { } egg) try { game.ChooseStarter(p, SkillData.Elements[0].Id, egg); } catch (GameException) { }
        p.Realm = 3; p.Stage = 0;
        p.Add("linh_thach", 50_000_000);
        p.Add("ruong_thuong", 50);
        p.Add(ForgeData.Iron, 50);
        foreach (var g in GameData.AllItems.Where(i => i.Slot is not null && i.MinRealm <= 3 && GameData.UpgradeGear(i.Id) is null).Take(40))
            p.Add(g.Id, 12);
        foreach (var b in GameData.Brews.Take(3)) foreach (var (item, n) in b.In) p.Add(item, n * 20);
        await game.SaveAsync(p);
        return p.Id;
    }

    private static GameDbContext Db(string cs) => new(new DbContextOptionsBuilder<GameDbContext>().UseNpgsql(cs, OnlineSchema.Npgsql)
        .AddInterceptors(new DbTimingCommandInterceptor(), new DbTimingConnectionInterceptor(), new DbTimingTransactionInterceptor()).Options);
}
