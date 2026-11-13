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
        Assert.Equal("Thiên Phẩm - Luyện Đan Đế Sư", Alchemy.RankName(10));
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
        var ok = (int)craft.Outputs!.Sum(o => o.Qty);
        Assert.InRange(ok, 1, 49);                              // có lò hỏng (cấp 1, đan bậc 1: 30% thất bại)
        Assert.Equal(ok, Alchemy.Have(p, "tu_khi_dan") - 1);    // +1 viên khởi đầu
        Assert.Equal((int)Math.Round((ok + (50 - ok) * Alchemy.FailExpRate) * Alchemy.ExpPerPill(1)), p.AlchemyExp);
    }

    [Fact]
    public void FailChanceDropsWithRankAboveTier()
    {
        // cân bằng 11/2026: 45% khi cấp = bậc, −5%/cấp vượt, sàn 15%
        Assert.Equal(.45, Alchemy.FailChance(3, 3), 6);
        Assert.Equal(.40, Alchemy.FailChance(4, 3), 6);
        Assert.Equal(.25, Alchemy.FailChance(7, 3), 6);
        Assert.Equal(.15, Alchemy.FailChance(10, 1), 6);
        Assert.Equal(.45, Alchemy.FailChance(1, 1), 6);
        // Đại Thừa trở lên: 95% khi cấp = bậc, −5%/cấp vượt, sàn 80%
        Assert.Equal(.95, Alchemy.FailChance(8, 8), 6);
        Assert.Equal(.85, Alchemy.FailChance(10, 8), 6);
        Assert.Equal(.80, Alchemy.FailChance(11, 8), 6);
        Assert.Equal(.95, Alchemy.FailChance(10, 10), 6);
        Assert.Equal(.90, Alchemy.FailChance(11, 10), 6);
    }

    [Fact]
    public void FailChanceNeverBelowOldValuesAndDropsWithRank()
    {
        for (int t = 1; t <= Alchemy.MaxRank; t++)
            for (int r = t; r < Alchemy.MaxRank; r++)
                Assert.True(Alchemy.FailChance(r + 1, t) <= Alchemy.FailChance(r, t));
        for (int t = 1; t <= Alchemy.MaxRank; t++)
            Assert.True(Alchemy.FailChance(Alchemy.MaxRank, t) >= (t >= Alchemy.TopTier ? .80 : .15));
    }

    [Fact]
    public async Task AllFailedBrewConsumesMaterialsWithoutPill()
    {
        var (game, p) = await Setup();
        p.Realm = 0; p.AlchemyExp = 1;
        p.Add("tu_linh_thao", 10); p.Add("bach_diep_thao", 10); p.Add("linh_thach", 1000);
        var seed = Enumerable.Range(0, 1000).First(s => new Random(s).NextDouble() < Alchemy.FailChance(1, 1));
        var (_, _, craft) = game.Brew(p, "tu_khi_dan", 1, new Random(seed));
        Assert.EndsWith(":fail", craft.Recipe);
        Assert.Empty(craft.Outputs!);
        Assert.Equal(1, Alchemy.Have(p, "tu_khi_dan"));         // chỉ viên khởi đầu
        Assert.Equal(1 + (int)Math.Round(Alchemy.FailExpRate * Alchemy.ExpPerPill(1)), p.AlchemyExp);
    }

    [Fact]
    public void LevelExpRisesGentlyFromRankFive()
    {
        // cân bằng 11/2026: EXP mỗi viên ×0,4 → số viên mỗi cấp ×2,5
        for (var r = 1; r < 5; r++) Assert.Equal(25, (Alchemy.ExpAt(r + 1) - Alchemy.ExpAt(r)) / Alchemy.ExpPerPill(r));   // 25 viên/cấp
        var pills = Enumerable.Range(5, 5).Select(r => (double)(Alchemy.ExpAt(r + 1) - Alchemy.ExpAt(r)) / Alchemy.ExpPerPill(r));
        Assert.Equal(new[] { 27.5, 32.5, 37.5, 43.75, 50 }, pills);                                                        // ×1.1 → ×2.0
        Assert.Equal(4000, Alchemy.ExpAt(5));     // bảng EXP giữ nguyên: người cũ không tụt cấp
        Assert.Equal(12910, Alchemy.ExpAt(6));
    }

    [Fact]
    public void LowerTierPillsGiveLessExp()
    {
        Assert.Equal(1, Alchemy.TierExpMult(3, 3), 6);
        Assert.Equal(1, Alchemy.TierExpMult(3, 5), 6);
        Assert.Equal(.5, Alchemy.TierExpMult(4, 3), 6);
        Assert.Equal(.25, Alchemy.TierExpMult(5, 3), 6);
        var p = new Player { AlchemyExp = Alchemy.ExpAt(3) };   // cấp 3, luyện 10 viên bậc 1 → 25% EXP
        Alchemy.Gain(p, 1, 10);
        Assert.Equal(Alchemy.ExpAt(3) + (int)Math.Round(10 * Alchemy.ExpPerPill(1) * .25), p.AlchemyExp);
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
