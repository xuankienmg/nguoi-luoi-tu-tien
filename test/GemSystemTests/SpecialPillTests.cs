using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>Đan tiêu hao linh thảo cao giai: Bổ Nguyên Cực phẩm, Hộ Đạo, Hỗn Nguyên Chiến Ý, Bàn Đào Trường Sinh.</summary>
public class SpecialPillTests : IDisposable
{
    private sealed class Roll(double value) : Random { public override double NextDouble() => value; }
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    private async Task<(GameService Game, Player Player)> Setup(int realm)
    {
        db.Database.OpenConnection(); db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Linh đan");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Realm = realm; p.Stage = 0;
        p.ExploreReadyAt = DateTime.UtcNow.AddHours(1);
        return (game, p);
    }

    private static bool IconExists(string icon) =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", icon)) || File.Exists(Path.Combine("..", "..", "..", "..", "..", "wwwroot", icon));

    [Theory]
    [InlineData("bo_nguyen_dan_cuc", "kien_moc_mieu", 120)]
    [InlineData("ho_dao_dan", "bat_tu_phuong_hoa", 2000)]
    [InlineData("chien_y_dan", "hon_nguyen_dao_lien", 2000)]
    [InlineData("ban_dao_truong_sinh_dan", "am_duong_ban_dao", 5000)]
    public void RecipeUsesHighHerbsAndSellsBelowCost(string id, string herb, int sell)
    {
        var item = GameData.ItemById[id];
        var brew = Assert.Single(GameData.Brews, b => b.Out == id);
        Assert.True(brew.In.ContainsKey(herb));
        Assert.Equal(sell, item.Sell);
        Assert.True(sell < brew.In["linh_thach"]);
        Assert.True(IconExists(item.Icon!));
    }

    [Fact]
    public void ChienYHasOneIconPerQuality()
    {
        Assert.Equal("img/dan/chien_y_dan_thuong.png", GameData.ItemById["chien_y_dan_thuong"].Icon);
        foreach (var s in new[] { "", "_trung", "_thuong" }) Assert.True(IconExists(GameData.ItemById["chien_y_dan" + s].Icon!));
    }

    [Fact]
    public async Task BoNguyenCucGivesThreeHours()
    {
        var (game, p) = await Setup(5);
        var state = await game.BuildStateAsync(p);
        p.Qi = 0; p.Add(SpecialPillData.BoNguyenCuc, 1);
        game.UseItem(p, SpecialPillData.BoNguyenCuc, 1);
        Assert.Equal(Math.Min((double)state.QiReq, state.Rate * 10800), (double)p.Qi, 0);
    }

    [Fact]
    public async Task HoDaoAbsorbsOneFailedBreakthrough()
    {
        var (game, p) = await Setup(5);
        p.Qi = GameData.Realms[5].QiReq(0);
        foreach (var (id, qty) in GameData.Realms[5].StageCost(0)) p.Add(id, qty * 3);
        p.Add(SpecialPillData.HoDao, 2);
        game.UseItem(p, SpecialPillData.HoDao, 1);
        Assert.Throws<GameException>(() => game.UseItem(p, SpecialPillData.HoDao, 1));   // chỉ một hiệu lực
        var cap = p.Qi;
        game.Advance(p, new Roll(.999));
        Assert.Equal(cap, p.Qi);
        Assert.True(p.DebuffUntil <= DateTime.UtcNow);
        Assert.False(SpecialPillData.HoDaoActive(p));
        Assert.Equal(1, p.Count(SpecialPillData.HoDao));
    }

    [Fact]
    public async Task HoDaoNeedsHoaThan()
    {
        var (game, p) = await Setup(3);
        p.Add(SpecialPillData.HoDao, 1);
        Assert.Throws<GameException>(() => game.UseItem(p, SpecialPillData.HoDao, 1));
    }

    [Fact]
    public async Task BanDaoCapsAtTenAndRaisesHpAtk()
    {
        var (game, p) = await Setup(6);
        var before = GameService.Stats(p);
        p.Add(SpecialPillData.BanDao, 15);
        game.UseItem(p, SpecialPillData.BanDao, 0);
        Assert.Equal(10, SpecialPillData.BanDaoUsed(p));
        Assert.Equal(5, p.Count(SpecialPillData.BanDao));
        Assert.Throws<GameException>(() => game.UseItem(p, SpecialPillData.BanDao, 1));
        var after = GameService.Stats(p);
        Assert.True(after.Hp > before.Hp && after.Atk > before.Atk);
        Assert.Equal(before.Def, after.Def);
    }

    [Fact]
    public async Task ChienYBoostsExploreButNotArenaOrPower()
    {
        var (game, p) = await Setup(6);
        var power = GameService.Power(p);
        var before = GameService.Stats(p);
        p.Add(SupportPills.ChienY + "_thuong", 1);
        game.UseItem(p, SupportPills.ChienY + "_thuong", 1);
        var after = GameService.Stats(p);
        Assert.True(after.Atk > before.Atk && after.Def > before.Def);
        Assert.Equal(before.Atk, GameService.Stats(p, buffs: false).Atk);
        Assert.Equal(power, GameService.Power(p));
    }

    public void Dispose() => db.Dispose();
}
