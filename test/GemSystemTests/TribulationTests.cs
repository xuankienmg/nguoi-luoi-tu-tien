using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;
using Xunit.Abstractions;

namespace GemSystemTests;

/// <summary>Thiên Kiếp: số đạo lôi, tất định theo máu/thủ, ±% tỉ lệ, chỉ áp dụng khi đột phá đại cảnh giới.</summary>
public class TribulationTests(ITestOutputHelper output) : IDisposable
{
    private readonly GameDbContext db = Open();
    private static GameDbContext Open()
    {
        var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection(); db.Database.EnsureCreated();
        return db;
    }
    public void Dispose() => db.Dispose();

    private async Task<(GameService game, Player p)> At(int realm, int stage)
    {
        var game = new GameService(db);
        var p = await game.CreateAsync("Thiên Kiếp Test");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Realm = realm; p.Stage = stage;
        p.Qi = GameData.Realms[realm].QiReq(stage);
        return (game, p);
    }

    private static int Peak(int realm) => GameData.Realms[realm].Stages.Length - 1;

    [Theory]
    [InlineData(1, 3)]
    [InlineData(3, 3)]
    [InlineData(4, 5)]
    [InlineData(7, 7)]
    [InlineData(10, 9)]
    [InlineData(20, 9)]
    public void BoltCountGrowsWithTargetRealm(int target, int bolts) => Assert.Equal(bolts, TribulationData.Bolts(target));

    [Fact]
    public void StrongHeroWithstandsAllBoltsAndGainsMaxBonus()
    {
        var next = GameData.BaseStats(GameData.Realms[4], 0);
        var r = TribulationData.Simulate(4, next, new CombatStats(1, 1_000_000_000, 1_000_000));
        Assert.Equal(r.Total, r.Survived);
        Assert.Equal(TribulationData.MinBonus + TribulationData.BonusSpan, r.ChanceBonus, 3);
        Assert.Equal(TribulationData.MaxMitigation, r.Mitigation, 3);
    }

    [Fact]
    public void FragileHeroFallsAtFirstBoltAndLosesChance()
    {
        var next = GameData.BaseStats(GameData.Realms[4], 0);
        var r = TribulationData.Simulate(4, next, new CombatStats(1, 1, 0));
        Assert.Equal(0, r.Survived);
        Assert.Equal(0, r.Mitigation);
        Assert.Equal(TribulationData.MinBonus, r.ChanceBonus, 3);
    }

    [Fact]
    public void BoltsGetStrongerEachStrike()
    {
        var next = GameData.BaseStats(GameData.Realms[7], 0);
        var r = TribulationData.Simulate(7, next, new CombatStats(1, 1, 0));
        for (var i = 1; i < r.Damage.Length; i++) Assert.True(r.Damage[i] > r.Damage[i - 1]);
    }

    [Fact]
    public async Task OnlyMajorRealmBreakthroughFacesTribulation()
    {
        var (_, mid) = await At(1, 0);
        Assert.Null(GameService.Tribulation(mid));

        var (game, peak) = await At(1, Peak(1));
        var trib = GameService.Tribulation(peak);
        Assert.NotNull(trib);
        Assert.Equal(TribulationData.Bolts(2), trib!.Total);
        var state = await game.BuildStateAsync(peak);
        Assert.Equal(trib.Survived, state.Break.Tribulation!.Survived);
    }

    [Fact]
    public async Task LastRealmHasNoTribulation()
    {
        var last = GameData.Realms.Count - 1;
        var (_, p) = await At(last, Peak(last));
        Assert.Null(GameService.Tribulation(p));
    }

    /// <summary>Bảng cân bằng: nhân vật mới (chỉ có chỉ số gốc + linh thú khởi đầu) ở đỉnh phong mỗi cảnh giới.</summary>
    [Fact]
    public async Task PrintBaselineTable()
    {
        for (var realm = 0; realm < GameData.Realms.Count - 1; realm++)
        {
            var (_, p) = await At(realm, Peak(realm));
            var t = GameService.Tribulation(p)!;
            output.WriteLine($"{GameData.Realms[realm].Name,-12} → {GameData.Realms[realm + 1].Name,-12} hp {t.Hp,10} mit {t.Mitigation:P0} chịu {t.Survived}/{t.Total} bonus {t.ChanceBonus:+0.###;-0.###} dmg[{string.Join(",", t.Damage)}]");
        }
    }
}
