using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>Kim Tiên chạy thật qua GameService.Advance: Chân Tiên → Kim Tiên, lên kỳ, thất bại, cảnh giới cuối.</summary>
public class KimTienBreakthroughTests : IDisposable
{
    private const int Kt = GameData.KimTienRealm;
    private sealed class Roll(double value) : Random { public override double NextDouble() => value; }
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public KimTienBreakthroughTests() { db.Database.OpenConnection(); db.Database.EnsureCreated(); }
    public void Dispose() => db.Dispose();

    private async Task<(GameService game, Player p)> At(int realm, int stage)
    {
        var game = new GameService(db);
        var p = await game.CreateAsync("Kim Tiên Test");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Realm = realm; p.Stage = stage;
        p.Qi = GameData.Realms[realm].QiReq(stage);
        return (game, p);
    }

    [Fact]
    public async Task ChanTienPeakBreaksThroughToKimTienAndPaysTheCost()
    {
        var (game, p) = await At(Kt - 1, GameData.Realms[Kt - 1].Stages.Length - 1);
        var cost = GameData.Realms[Kt - 1].BreakCost!;
        foreach (var (id, n) in cost) p.Add(id, n);
        var before = cost.ToDictionary(c => c.Key, c => p.Count(c.Key));   // nhân vật mới đã có sẵn ít linh thạch
        var (_, kind) = game.Advance(p, new Roll(0));
        Assert.Equal("gold", kind);
        Assert.Equal(Kt, p.Realm); Assert.Equal(0, p.Stage);
        foreach (var (id, n) in cost) Assert.Equal(before[id] - n, p.Count(id));
    }

    [Fact]
    public async Task FailedKimTienBreakthroughLosesCoreAndKeepsRealm()
    {
        var (game, p) = await At(Kt - 1, GameData.Realms[Kt - 1].Stages.Length - 1);
        foreach (var (id, n) in GameData.Realms[Kt - 1].BreakCost!) p.Add(id, n);
        game.Advance(p, new Roll(0.99));
        Assert.Equal(Kt - 1, p.Realm);
        Assert.Equal(0, p.Count(GameData.KimTienCore));   // thất bại mất cả hạch Boss
        Assert.True(p.BreakCooldownUntil > DateTime.UtcNow);
    }

    [Fact]
    public async Task MissingCoreIsRefusedWithoutSpendingAnything()
    {
        var (game, p) = await At(Kt - 1, GameData.Realms[Kt - 1].Stages.Length - 1);
        p.Add(GameData.KimTienPill, 1); p.Add("linh_thach", 12_000_000);
        Assert.Throws<GameException>(() => game.Advance(p, new Roll(0)));
        Assert.Equal(1, p.Count(GameData.KimTienPill));
    }

    [Fact]
    public async Task KimTienStagesAdvanceThenTheFinalRealmRefusesFurtherBreak()
    {
        var (game, p) = await At(Kt, 0);
        for (var s = 0; s < GameData.Realms[Kt].Stages.Length - 1; s++)
        {
            p.Qi = GameData.Realms[Kt].QiReq(p.Stage);
            foreach (var (id, n) in GameData.Realms[Kt].StageCost(p.Stage)) p.Add(id, n);
            game.Advance(p, new Roll(0));
            Assert.Equal(s + 1, p.Stage);
        }
        p.Qi = GameData.Realms[Kt].QiReq(p.Stage);
        var ex = Assert.Throws<GameException>(() => game.Advance(p, new Roll(0)));
        Assert.Contains("cao nhất", ex.Message);
        Assert.Equal(Kt, p.Realm);
    }

    [Fact]
    public async Task KimTienStatsAndPowerStayFinite()
    {
        var (_, p) = await At(Kt, 2);
        var st = GameService.Stats(p);
        foreach (var v in new[] { st.Atk, st.Def, st.Hp }) Assert.True(double.IsFinite((double)v) && v > 0, "chỉ số Kim Tiên phải hữu hạn và > 0");
        Assert.True(GameService.Power(p) > 0);
        Assert.True(GameData.Realms[Kt].QiReq(2) > 0 && GameData.Realms[Kt].QiReq(2) < 1e24m);
    }
}
