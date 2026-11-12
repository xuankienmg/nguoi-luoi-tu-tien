using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

[Collection("SectBuffCache")]   // SectBuff có cache tĩnh: không chạy song song với SectTests
public class SectBossTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    private readonly DateTime now = DateTime.UtcNow;
    private ServerSect Sect => new(db);
    private ServerSectBoss Boss => new(db);
    private string Day => SectData.DayOf(now);

    public SectBossTests() { db.Database.OpenConnection(); db.Database.EnsureCreated(); SectBuff.Clear(); ServerChat.ResetOnlineCacheForTests(); }
    public void Dispose() => db.Dispose();

    private async Task<Guid> NewPlayer(string name, int realm = 1, long stones = 0)
    {
        var p = await new GameService(db).CreateAsync(name);
        p.Realm = realm; p.LastTickAt = now;
        if (stones > 0) p.Add("linh_thach", stones);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return p.Id;
    }

    /// <summary>Tông có chủ là leader; members vào bằng đơn + duyệt.</summary>
    private async Task<(Guid Leader, long SectId)> NewSect(string name = "Thanh Vân", params Guid[] members)
    {
        var leader = await NewPlayer("Chủ " + name);
        await Sect.CreateAsync(leader, name, now);
        var id = (await db.Sects.AsNoTracking().SingleAsync(s => s.Name == name)).Id;
        foreach (var m in members)
        {
            await Sect.ApplyAsync(m, id, now);
            var app = await db.SectApplications.SingleAsync(a => a.PlayerId == m);
            await Sect.DecideAsync(leader, app.Id, true, now);
        }
        return (leader, id);
    }

    private Task<SectMember> Row(Guid id) => db.SectMembers.AsNoTracking().SingleAsync(m => m.PlayerId == id);

    private Task SetRef(Guid id, double value) =>
        db.SectMembers.Where(m => m.PlayerId == id).ExecuteUpdateAsync(u => u.SetProperty(m => m.BossRef, value));

    [Fact]
    public void PvpRunStopsAtMaxRounds()
    {
        var a = PvpFighter.FromStats("A", 1, new() { ["atk"] = 100, ["hp"] = 1000, ["def"] = 0 });
        var d = PvpFighter.FromStats("D", 1, new() { ["atk"] = 1, ["hp"] = 1e12, ["def"] = 0 });
        Assert.True(PvpCombat.Run(a, d, 1, 3).Actions.Count <= 6);
        Assert.True(PvpCombat.Run(a, d, 1).Actions.Count > 6);
    }

    [Fact]
    public void BossRotatesDailyAndCycles()
    {
        var d1 = SectBossData.BossOf("2026-11-11");
        var d2 = SectBossData.BossOf("2026-11-12");
        Assert.NotEqual(d1.Id, d2.Id);
        Assert.Equal(d1.Id, SectBossData.BossOf(SectBossData.NextDay(SectBossData.NextDay("2026-11-11"))).Id);   // 2 Boss luân phiên
        Assert.Equal("2026-12-01", SectBossData.NextDay("2026-11-30"));
    }

    [Fact]
    public void MaxHpIsShareOfTwoHitsPerMember()
    {
        Assert.Equal(576000, SectBossData.MaxHp([100, 300, 200, 200]));   // 60 × 2 lượt × 800
        Assert.Equal(1, SectBossData.MaxHp([]));
    }

    [Fact]
    public void ContributionScalesWithShareOfHpAndClamps()
    {
        Assert.Equal(5, SectBossData.Contribution(0, 1000));
        Assert.Equal(15, SectBossData.Contribution(10, 10000));       // 15000 × 0,1%
        Assert.Equal(30, SectBossData.Contribution(900, 1000));
    }

    [Fact]
    public void RankOrdersByDamageThenId()
    {
        var a = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var b = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var c = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var rows = new (Guid, double)[] { (a, 10), (b, 50), (c, 50) };
        Assert.Equal(0, SectBossData.Rank(rows, b));
        Assert.Equal(1, SectBossData.Rank(rows, c));
        Assert.Equal(2, SectBossData.Rank(rows, a));
        Assert.Equal(int.MaxValue, SectBossData.Rank([(a, 0)], a));   // không gây sát thương: không vào top
    }

    [Fact]
    public async Task BuffDayAddsQiPctOnlyOnThatDay()
    {
        var (leader, sid) = await NewSect();
        var tomorrow = SectBossData.NextDay(Day);
        await db.Sects.Where(s => s.Id == sid).ExecuteUpdateAsync(u => u.SetProperty(s => s.BuffDay, tomorrow));
        SectBuff.Clear();
        await SectBuff.EnsureAsync(db, leader, now);
        Assert.Equal(SectData.QiPct(1), SectBuff.QiPct(leader, now));
        Assert.Equal(SectData.QiPct(1) + SectBossData.BuffQiPct, SectBuff.QiPct(leader, now.AddDays(1)));
        Assert.Equal(SectData.QiPct(1), SectBuff.QiPct(leader, now.AddDays(2)));
    }

    [Fact]
    public async Task MaxHpScalesWithActiveMembersAndFillsMissingRefs()
    {
        var m1 = await NewPlayer("Một"); var m2 = await NewPlayer("Hai"); var m3 = await NewPlayer("Ba");
        var (leader, _) = await NewSect("Thanh Vân", m1, m2, m3);
        await SetRef(leader, 100); await SetRef(m1, 300);   // m2, m3 chưa có: lấy trung bình của người có (200)
        var dto = await Boss.StateAsync(leader, now, () => 999);
        Assert.Equal(576000, dto.MaxHp);                       // 30 × 2 × 12 × (100 + 300 + 200 + 200)
    }

    [Fact]
    public async Task SoloSectUsesSelfReference()
    {
        var (leader, _) = await NewSect();
        Assert.Equal(72000, (await Boss.StateAsync(leader, now, () => 100)).MaxHp);   // 30 × 2 × 12 × 100
    }

    [Fact]
    public async Task InactiveMembersAreNotCounted()
    {
        var m1 = await NewPlayer("Vắng");
        var (leader, _) = await NewSect("Thanh Vân", m1);
        await SetRef(leader, 100); await SetRef(m1, 100);
        await db.Players.IgnoreQueryFilters().Where(p => p.Id == m1).ExecuteUpdateAsync(u => u.SetProperty(p => p.LastTickAt, now.AddDays(-5)));
        Assert.Equal(72000, (await Boss.StateAsync(leader, now, () => 999)).MaxHp);
    }

    [Fact]
    public async Task MaxHpIsLockedForTheDay()
    {
        var (leader, _) = await NewSect();
        var first = (await Boss.StateAsync(leader, now, () => 100)).MaxHp;
        await SetRef(leader, 10_000);
        Assert.Equal(first, (await Boss.StateAsync(leader, now, () => 100)).MaxHp);
    }

    [Fact]
    public async Task TwoHitsPerDayThenRefusedAndNewDayResets()
    {
        var (leader, _) = await NewSect();
        Assert.Equal(1, (await Boss.ReserveHitAsync(leader, now, () => 100)).HitNo);
        Assert.Equal(2, (await Boss.ReserveHitAsync(leader, now, () => 100)).HitNo);
        var ex = await Assert.ThrowsAsync<GameException>(() => Boss.ReserveHitAsync(leader, now, () => 100));
        Assert.Contains("hết lượt", ex.Message);
        Assert.Equal(1, (await Boss.ReserveHitAsync(leader, now.AddDays(1), () => 100)).HitNo);
    }

    [Fact]
    public async Task OutsidersCannotHit()
    {
        var loner = await NewPlayer("Lẻ Loi");
        await Assert.ThrowsAsync<GameException>(() => Boss.ReserveHitAsync(loner, now, () => 100));
    }

    [Fact]
    public async Task RecordHitAddsDamageContributionRefAndFund()
    {
        var (leader, sid) = await NewSect();
        var r = await Boss.ReserveHitAsync(leader, now, () => 100);          // máu 72000
        var (pts, defeated) = await Boss.RecordHitAsync(leader, r.Boss, r.Sect, 30000, now);
        Assert.Equal(SectBossData.Contribution(30000, 72000), pts);
        Assert.False(defeated);
        var row = await Row(leader);
        Assert.Equal(pts, row.Contribution); Assert.Equal(pts, row.TotalContribution); Assert.Equal(30000, row.BossRef);
        Assert.Equal(pts, (await db.Sects.AsNoTracking().SingleAsync(s => s.Id == sid)).Fund);
        Assert.Equal(30000, (await db.SectBosses.AsNoTracking().SingleAsync()).Damage);
    }

    [Fact]
    public async Task DefeatSetsBuffOnceAnnouncesAndBlocksFurtherHits()
    {
        var m = await NewPlayer("Đệ Tử");
        var (leader, sid) = await NewSect("Thanh Vân", m);                   // 2 người hoạt động, máu 720000
        var r1 = await Boss.ReserveHitAsync(leader, now, () => 100);
        Assert.False((await Boss.RecordHitAsync(leader, r1.Boss, r1.Sect, 78000, now)).Defeated);
        var r2 = await Boss.ReserveHitAsync(leader, now, () => 100);
        Assert.True((await Boss.RecordHitAsync(leader, r2.Boss, r2.Sect, 78000, now)).Defeated);   // 3120 ≥ 720000
        Assert.Equal(SectBossData.NextDay(Day), (await db.Sects.AsNoTracking().SingleAsync(s => s.Id == sid)).BuffDay);
        Assert.Contains(await db.ChatMessages.Where(c => c.SectId == sid).Select(c => c.Text).ToListAsync(), t => t.Contains("bị hạ"));
        var ex = await Assert.ThrowsAsync<GameException>(() => Boss.ReserveHitAsync(m, now, () => 100));
        Assert.Contains("đã bị hạ", ex.Message);
        Assert.False((await Boss.RecordHitAsync(leader, r2.Boss, r2.Sect, 6000, now)).Defeated);   // chỉ kích hoạt một lần
    }

    [Fact]
    public async Task ChestOncePerPlayerWithTopBonus()
    {
        var m = await NewPlayer("Đệ Tử"); var idle = await NewPlayer("Lười");
        var (leader, _) = await NewSect("Thanh Vân", m, idle);               // 3 người hoạt động, máu 216000
        await Assert.ThrowsAsync<GameException>(() => Boss.ClaimAsync(leader, now));    // chưa hạ Boss
        var rm = await Boss.ReserveHitAsync(m, now, () => 100);
        await Boss.RecordHitAsync(m, rm.Boss, rm.Sect, 60000, now);
        for (var i = 0; i < 2; i++)
        {
            var r = await Boss.ReserveHitAsync(leader, now, () => 100);
            await Boss.RecordHitAsync(leader, r.Boss, r.Sect, 78000, now);
        }                                                                    // 216000 ≥ 216000: hạ Boss
        Assert.Equal(SectBossData.TopBonus[0], await Boss.ClaimAsync(leader, now));
        Assert.Equal(SectBossData.TopBonus[1], await Boss.ClaimAsync(m, now));
        Assert.Equal(2 * SectBossData.Contribution(78000, 216000) + SectBossData.TopBonus[0], (await Row(leader)).Contribution);
        var again = await Assert.ThrowsAsync<GameException>(() => Boss.ClaimAsync(leader, now));
        Assert.Contains("Đã nhận", again.Message);
        var none = await Assert.ThrowsAsync<GameException>(() => Boss.ClaimAsync(idle, now));
        Assert.Contains("chưa đánh", none.Message);
    }

    [Fact]
    public async Task PageBuffReachesQiRateOnTheNextDay()
    {
        var (leader, _) = await NewSect();
        var r1 = await Boss.ReserveHitAsync(leader, now, () => 100);         // máu 72000
        await Boss.RecordHitAsync(leader, r1.Boss, r1.Sect, 78000, now);       // hạ ngay
        await Sect.PageAsync(leader, now);
        Assert.Equal(SectData.QiPct(1) + SectBossData.BuffQiPct, SectBuff.QiPct(leader, now.AddDays(1)));
    }

    [Fact]
    public void BossAttackRisesEveryFourRoundsOfTheFight()
    {
        Assert.Equal(15, SectBossData.MaxRounds);
        foreach (var r in new[] { 0, 1, 2, 3 }) Assert.Equal(SectBossData.AtkMult, SectBossData.AtkAtRound(r), 6);
        foreach (var r in new[] { 4, 5, 6, 7 }) Assert.Equal(SectBossData.AtkMult * (1 + SectBossData.AtkRampPct), SectBossData.AtkAtRound(r), 6);
        Assert.Equal(SectBossData.AtkMult * (1 + 2 * SectBossData.AtkRampPct), SectBossData.AtkAtRound(8), 6);   // tiếp tục tăng sau mỗi 4 lượt
    }

    [Fact]
    public void BossHitsHarderInTheSecondHalfOfTheFight()
    {
        var me = PvpFighter.FromStats("Yếu", 3, new() { ["atk"] = 300, ["hp"] = 1e9, ["def"] = 200, ["speed"] = 50 }) with { Crit = 0, Dodge = 0 };
        var r = GameService.SectBossRun(me, SectBossData.BossOf(Day), 5);
        var boss = r.Actions.Where(a => !a.ByA && a.Basic > 0).ToList();
        Assert.True(boss.Count >= 6);
        var first = boss.Take(3).Average(a => (double)a.Basic); var last = boss.TakeLast(3).Average(a => (double)a.Basic);
        Assert.True(last > first * 1.5, $"{first} → {last}");   // lượt 5–8 gấp đôi lượt 1–4 (trừ khiên / phòng thủ)
    }

    [Fact]
    public async Task SimulationIsDeterministicAndBounded()
    {
        var game = new GameService(db);
        var id = await NewPlayer("Thử Chiêu");
        var (_, a) = await game.ActAsync(id, p => Task.FromResult(GameService.SectBossSim(p, Day, 1)));
        var (_, b) = await game.ActAsync(id, p => Task.FromResult(GameService.SectBossSim(p, Day, 1)));
        Assert.True(a.Damage > 0);
        Assert.Equal(a, b);
        Assert.InRange(a.Rounds, 1, SectBossData.MaxRounds);
    }

    [Fact]
    public void HugeStatsDoNotOverflow()
    {
        var me = PvpFighter.FromStats("Cự", 5, new() { ["atk"] = 1e18, ["hp"] = 1e18, ["def"] = 1e17, ["speed"] = 100, ["dodge"] = 0 });   // dodge 0: với speed 100 cả hai bên né gần như 100% nên sát thương = 0
        var r = GameService.SectBossSimulate(me, SectBossData.BossOf(Day), 7);
        Assert.True(r.Damage > 0 && double.IsFinite(r.Damage));
    }

    [Fact]
    public async Task AttackEndToEndGivesDamageContributionAndFund()
    {
        var game = new GameService(db);
        var (leader, sid) = await NewSect();
        var (_, msg) = await game.ActAsync(leader, p => game.SectBossAttackAsync(p));
        Assert.Contains("sát thương", msg);
        Assert.True((await db.Sects.AsNoTracking().SingleAsync(s => s.Id == sid)).Fund > 0);
        Assert.Equal(1, (await db.SectBossHits.AsNoTracking().SingleAsync()).Hits);
    }

    [Fact]
    public async Task ChestPaysItemsAfterDefeat()
    {
        var game = new GameService(db);
        var (leader, _) = await NewSect();
        await game.ActAsync(leader, p => game.SectBossAttackAsync(p));
        await db.SectBosses.ExecuteUpdateAsync(u => u.SetProperty(x => x.MaxHp, 1.0));   // ép Boss yếu để lượt sau chắc chắn hạ
        var (before, _) = await game.ActAsync(leader, p => Task.FromResult(""));
        var stones0 = before.Count("linh_thach"); var pills0 = before.Count("linh_thu_dan");
        var (_, msg) = await game.ActAsync(leader, p => game.SectBossAttackAsync(p));
        Assert.Contains("bị hạ", msg);
        var (after, _) = await game.ActAsync(leader, p => game.SectBossClaimAsync(p));
        Assert.True(after.Count("linh_thach") - stones0 >= SectBossData.ChestStoneMult * SectData.StoneBase(1));
        Assert.True(after.Count("linh_thu_dan") - pills0 >= SectBossData.ChestPetPills);
        await Assert.ThrowsAsync<GameException>(() => game.ActAsync(leader, p => game.SectBossClaimAsync(p)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void ChestRollsMatchRatesAndRealItems(int realm)
    {
        var rng = new Random(7); const int n = 200_000;
        var hits = new Dictionary<string, int>(); int dao = 0, huyenTien = 0;
        for (var i = 0; i < n; i++)
            foreach (var (id, qty) in SectBossData.OpenChest(realm, rng))
            {
                Assert.True(GameData.ItemById.ContainsKey(id), id);
                Assert.True(qty > 0);
                if (GameData.ItemById[id].Slot != null)
                {
                    if (id.EndsWith($"q{SectBossData.DaoQuality}")) dao++;
                    else if (id.EndsWith($"q{SectBossData.HuyenTienQuality}")) huyenTien++;
                    else Assert.Fail(id);
                }
                else hits[id] = hits.GetValueOrDefault(id) + 1;
            }
        Assert.Equal(n, hits["linh_thach"]); Assert.Equal(n, hits["linh_thu_dan"]);
        Assert.InRange(dao / (double)n, .008, .012);
        Assert.InRange(huyenTien / (double)n, .0035, .0065);
        Assert.InRange(hits.GetValueOrDefault(GameService.VipCurrency) / (double)n, .008, .012);
        Assert.Equal(realm >= 3, hits.ContainsKey("ngo_dao_dan"));
    }

    [Fact]
    public async Task StateShowsRankingAndFlags()
    {
        var m = await NewPlayer("Đệ Tử");
        var (leader, _) = await NewSect("Thanh Vân", m);
        var r = await Boss.ReserveHitAsync(m, now, () => 100);
        await Boss.RecordHitAsync(m, r.Boss, r.Sect, 70, now);
        var dto = await Boss.StateAsync(leader, now, () => 100);
        Assert.Equal(SectBossData.HitsPerDay, dto.HitsLeft);
        Assert.Single(dto.Ranking);
        Assert.Equal(70, dto.Ranking[0].Damage);
        Assert.False(dto.Ranking[0].Me);
        Assert.False(dto.Defeated); Assert.False(dto.CanClaim);
    }
}
