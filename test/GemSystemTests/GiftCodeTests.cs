using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class GiftCodeTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    private async Task<(GameService Game, Player Player)> Setup()
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Nhập code");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        return (game, p);
    }

    [Fact]
    public async Task OnlineWelcomeCodeGivesEveryItemOnce()
    {
        var (game, p) = await Setup();
        var before = GameData.GiftCodes["NGUOILUOITUTIENONLINE"].ToDictionary(x => x.Item, x => p.Count(x.Item));
        game.RedeemCode(p, " nguoiluoitutienonline ");
        foreach (var (item, qty) in GameData.GiftCodes["NGUOILUOITUTIENONLINE"])
            Assert.Equal(before[item] + qty, p.Count(item));
        Assert.Equal(100_000m, p.Count("linh_thach") - before["linh_thach"]);
        Assert.Equal(5m, p.Count(GameData.PetSpinTicket) - before[GameData.PetSpinTicket]);
        Assert.Throws<GameException>(() => game.RedeemCode(p, "NGUOILUOITUTIENONLINE"));
    }

    [Fact]
    public async Task ThanhSamGivesTheThanhSamOutfitChestOnce()
    {
        var (game, p) = await Setup();
        Assert.Equal("Rương Trang Phục · Thanh Sam Nữ Đệ Tử", GameData.ItemById["ruong_skin_luyen_khi_nu"].Name);
        var before = p.Count("ruong_skin_luyen_khi_nu");
        game.RedeemCode(p, "thanhsam");
        Assert.Equal(before + 1, p.Count("ruong_skin_luyen_khi_nu"));
        Assert.Throws<GameException>(() => game.RedeemCode(p, "THANHSAM"));
        Assert.Equal(before + 1, p.Count("ruong_skin_luyen_khi_nu"));
    }

    [Fact]
    public async Task FullBagRejectsCodeWithoutGivingAnything()
    {
        var (game, p) = await Setup();
        var gifts = GameData.GiftCodes["NGUOILUOITUTIENONLINE"];
        var before = gifts.ToDictionary(x => x.Item, x => p.Count(x.Item));
        var need = gifts.Count(x => p.Count(x.Item) == 0);
        var filler = GameData.AllItems.Where(i => i.Slot is null && !GemData.IsGemOrStone(i.Id) && p.Count(i.Id) == 0 && gifts.All(g => g.Item != i.Id))
            .Take(GameService.BagCap(p) - GameService.BagUsed(p) - need + 1).ToList();
        foreach (var i in filler) p.Add(i.Id, 1);
        Assert.Throws<GameException>(() => game.RedeemCode(p, "NGUOILUOITUTIENONLINE"));
        foreach (var (item, _) in gifts) Assert.Equal(before[item], p.Count(item));
        Assert.DoesNotContain("NGUOILUOITUTIENONLINE", p.Redeemed);
    }

    public void Dispose() => db.Dispose();
}
