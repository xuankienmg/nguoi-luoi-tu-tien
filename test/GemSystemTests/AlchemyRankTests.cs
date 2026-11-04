using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class AlchemyRankTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    private async Task<(GameService Game, Player Player)> Setup()
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Luyện đan");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.ExploreReadyAt = DateTime.UtcNow.AddHours(1);
        return (game, p);
    }

    [Fact]
    public void RankNamesFollowTheTenGrades()
    {
        Assert.Equal("Nhất Phẩm - Luyện Đan Sư Sơ Cấp", Alchemy.RankName(1));
        Assert.Equal("Tứ Phẩm - Luyện Đan Sư Trung Cấp", Alchemy.RankName(4));
        Assert.Equal("Bát Phẩm - Luyện Đan Tông Sư", Alchemy.RankName(8));
        Assert.Equal("Cửu Phẩm - Luyện Đan Đại Sư", Alchemy.RankName(9));
        Assert.Equal("Thập Phẩm - Luyện Đan Đế Sư", Alchemy.RankName(10));
        for (var r = 1; r < Alchemy.MaxRank; r++) Assert.Equal(r, Alchemy.RankOf(Alchemy.ExpAt(r + 1) - 1));
    }

    [Fact]
    public void BaseOddsAreTenThirtySixtyAndRiseWithRank()
    {
        var (t, m) = Alchemy.Odds(3, 3);
        Assert.Equal(.10, t, 6); Assert.Equal(.30, m, 6);
        var (t2, m2) = Alchemy.Odds(6, 3);
        Assert.True(t2 > t && m2 > m);
        var (t3, m3) = Alchemy.Odds(10, 1);
        Assert.True(t3 + m3 < 1);
    }

    [Fact]
    public async Task BrewingSplitsQualitiesAndGainsExp()
    {
        var (game, p) = await Setup();
        p.Add("tu_linh_thao", 200); p.Add("bach_diep_thao", 100); p.Add("linh_thach", 10_000);
        var (_, _, craft) = game.Brew(p, "tu_khi_dan", 50, new Random(7));
        Assert.Equal(50, craft.Outputs!.Sum(o => o.Qty));
        Assert.Equal(50, Alchemy.Have(p, "tu_khi_dan") - 1);   // +1 viên khởi đầu
        Assert.Equal(50 * Alchemy.ExpPerPill(1), p.AlchemyExp);
        Assert.True(Alchemy.Rank(p) >= 2);
    }

    [Fact]
    public void LevelExpRisesGentlyFromRankFive()
    {
        for (var r = 1; r < 5; r++) Assert.Equal(10, (Alchemy.ExpAt(r + 1) - Alchemy.ExpAt(r)) / Alchemy.ExpPerPill(r));   // ~10 viên/cấp
        var pills = Enumerable.Range(5, 5).Select(r => (double)(Alchemy.ExpAt(r + 1) - Alchemy.ExpAt(r)) / Alchemy.ExpPerPill(r));
        Assert.Equal(new[] { 11, 13, 15, 17.5, 20 }, pills);                                                                // ×1.1 → ×2.0
        Assert.Equal(4000, Alchemy.ExpAt(5));     // tới cấp 5 giữ nguyên
        Assert.Equal(12910, Alchemy.ExpAt(6));
    }

    [Fact]
    public async Task HigherTierNeedsMatchingRank()
    {
        var (game, p) = await Setup();
        p.Realm = 0; p.AlchemyExp = 0;
        p.Add("tu_linh_thao", 20); p.Add("huyet_can_thao", 20); p.Add("nguyet_lo_hoa", 20); p.Add("linh_thach", 10_000);
        Assert.Throws<GameException>(() => game.Brew(p, "truc_co_dan", 1));
        p.AlchemyExp = Alchemy.ExpAt(2);
        game.Brew(p, "truc_co_dan", 1);
    }

    [Fact]
    public async Task BreakthroughUsesBestPillFirstAndAddsChance()
    {
        var (game, p) = await Setup();
        p.Realm = 1; p.Stage = 0;
        var cost = new Dictionary<string, int> { ["truc_co_dan"] = 2 };
        p.Add("truc_co_dan", 3); p.Add(Alchemy.VariantId("truc_co_dan", 2), 1); p.Add(Alchemy.VariantId("truc_co_dan", 1), 1);
        Assert.Equal(5, Alchemy.Have(p, "truc_co_dan"));
        Assert.Equal(.075, Alchemy.Bonus(p, cost), 6);
        Alchemy.Pay(p, cost);
        Assert.Equal(0, p.Count(Alchemy.VariantId("truc_co_dan", 2)));
        Assert.Equal(0, p.Count(Alchemy.VariantId("truc_co_dan", 1)));
        Assert.Equal(3, p.Count("truc_co_dan"));
    }

    public void Dispose() => db.Dispose();
}
