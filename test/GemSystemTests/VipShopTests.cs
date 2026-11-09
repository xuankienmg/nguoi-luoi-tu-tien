using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class VipShopTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public void Dispose() => db.Dispose();
    private const string Tp = GameService.VipCurrency;

    private async Task<(GameService Game, Player P)> Setup(int premium = 1000)
    {
        db.Database.OpenConnection(); db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("VIP");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Add(Tp, premium);
        return (game, p);
    }

    [Fact]
    public async Task SpinTicketCostsOnePremiumStone()
    {
        var (game, p) = await Setup(5);
        game.VipBuy(p, GameData.SpinTicket, 3);
        Assert.Equal(2, p.Count(Tp));
        Assert.Equal(6, p.Count(GameData.SpinTicket));   // giảm 50%: giá 1 giữ nguyên, nhận ×2
    }

    [Fact]
    public async Task GemPricesFollowLevelWithoutLimit()
    {
        var (game, p) = await Setup();
        var lv7 = GemData.GemsAtLevel(6)[0];
        Assert.Equal(5, GameService.VipOffersFor(p).Single(o => o.Item == lv7).Price);
        Assert.Equal(25, GameService.VipOffersFor(p).Single(o => o.Item == GemData.GemsAtLevel(9)[0]).Price);
        game.VipBuy(p, lv7, 5);
        game.VipBuy(p, lv7, 5);                         // không giới hạn
        Assert.Equal(1000 - 50, p.Count(Tp));
        Assert.Equal(10, GemData.GetCount(p, lv7));
    }

    [Fact]
    public async Task PetPackGivesEggsAndPillsRepeatably()
    {
        var (game, p) = await Setup(125);
        Assert.Equal(50, GameService.VipOffersFor(p).Single(o => o.Item == GameService.VipPetPackOffer).Price);
        game.VipBuy(p, GameService.VipPetPackOffer, 2);
        Assert.Equal(25, p.Count(Tp));
        Assert.Equal(4, p.Count("thu_dan_thuong_co"));
        Assert.Equal(2000, p.Count("linh_thu_dan"));
        Assert.Throws<GameException>(() => game.VipBuy(p, GameService.VipPetPackOffer, 1));
        Assert.Equal(25, p.Count(Tp));
    }

    [Fact]
    public async Task SpecialPacksAreListedFirst()
    {
        var (_, p) = await Setup();
        var offers = GameService.VipOffersFor(p);
        Assert.Equal([GameService.VipPackOffer, GameService.VipPetPackOffer], offers.Take(2).Select(o => o.Item));
    }

    [Fact]
    public async Task NotEnoughPremiumIsRejected()
    {
        var (game, p) = await Setup(0);
        Assert.Throws<GameException>(() => game.VipBuy(p, "thu_dan_thuong_co", 1));
    }

    [Fact]
    public async Task ExchangeGivesStonesByRealm()
    {
        var (game, p) = await Setup(2);
        var before = p.Count("linh_thach");
        game.VipBuy(p, GameService.VipStoneOffer, 2);
        Assert.Equal(before + 2 * 40 * KyDuyen.StoneUnit(p.Realm), p.Count("linh_thach"));   // giảm 50%: nhận ×2
    }

    [Fact]
    public async Task HerbTierCappedByRealm()
    {
        var (game, p) = await Setup();
        var allowed = GameService.VipHerbs(p);
        Assert.Contains("tu_linh_thao", allowed);
        Assert.DoesNotContain(allowed, h => GameService.HerbTier(h) > 2);
        var high = GameData.AllItems.First(d => d.Type == "herb" && GameService.HerbTier(d.Id) >= 3).Id;
        Assert.Throws<GameException>(() => game.VipBuy(p, high, 1));
        game.VipBuy(p, "tu_linh_thao", 4);
        Assert.Equal(8, p.Count("tu_linh_thao"));   // giảm 50%: mỗi Cực Phẩm nhận 2 linh thảo
    }

    [Fact]
    public async Task PackUnlocksSpeedOnceAndGivesPills()
    {
        var (game, p) = await Setup(250);
        game.VipBuy(p, GameService.VipPackOffer, 1);
        Assert.True(p.Speed2);
        Assert.True(p.Speed2On);
        Assert.Equal(10, p.Count("bo_nguyen_dan_trung"));
        Assert.Equal(10, p.Count("bo_nguyen_dan_thuong"));
        Assert.Throws<GameException>(() => game.VipBuy(p, GameService.VipPackOffer, 1));
        Assert.Equal(200, p.Count(Tp));
        Assert.True(GameService.VipOffersFor(p).Single(o => o.Item == GameService.VipPackOffer).Owned);
    }

    [Fact]
    public async Task UnknownItemIsRejected()
    {
        var (game, p) = await Setup();
        Assert.Throws<GameException>(() => game.VipBuy(p, "khong_ton_tai", 1));
    }

    [Fact]
    public async Task HerbOfferIdWithoutChoiceIsRejected()
    {
        var (game, p) = await Setup();
        var ex = Assert.Throws<GameException>(() => game.VipBuy(p, GameService.VipHerbOffer, 1));
        Assert.Equal("Chọn linh thảo muốn đổi.", ex.Message);
        Assert.Equal(1000, p.Count(Tp));
    }

    [Fact]
    public void EveryHerbHasATier()
    {
        foreach (var d in GameData.AllItems.Where(d => d.Type == "herb")) Assert.InRange(GameService.HerbTier(d.Id), 1, 5);
        Assert.Equal(1, GameService.HerbTier("bach_diep_thao"));
        Assert.Equal(1, GameService.HerbTier("nguyet_lo_hoa"));    // cùng đan phương Trúc Cơ Đan với linh thảo gốc bậc 1
        Assert.Equal(2, GameService.HerbTier("thuy_lo_diep"));
        Assert.Equal(3, GameService.HerbTier("kim_duong_lien"));
        Assert.Equal(4, GameService.HerbTier("linh_cot_chi"));
        Assert.Equal(5, GameService.HerbTier("hon_nguyen_linh_chung"));   // vượt thang linh thảo gốc: dừng ở bậc 5
    }

    [Fact]
    public async Task ExpansionHerbsAreExchangeableByRealm()
    {
        var (game, p) = await Setup();
        Assert.Contains("thuy_lo_diep", GameService.VipHerbs(p));        // bậc 2, Luyện Khí được bậc 1–2
        Assert.DoesNotContain("linh_cot_chi", GameService.VipHerbs(p));
        game.VipBuy(p, "nguyet_lo_hoa", 2);
        Assert.Equal(4, p.Count("nguyet_lo_hoa"));
        p.Realm = 3;
        Assert.Contains("linh_cot_chi", GameService.VipHerbs(p));      // bậc 4: Hóa Thần trở đi
    }

    [Fact]
    public async Task PackWithFullBagFailsAtomically()
    {
        var (game, p) = await Setup(250);
        foreach (var d in GameData.AllItems.Where(d => d.Slot is null && !GemData.IsGemOrStone(d.Id) && !GameService.VipPackItems.Any(x => x.Item == d.Id)))
        {
            if (GameService.BagUsed(p) >= GameService.BagCap(p)) break;
            if (p.Count(d.Id) == 0) p.Add(d.Id, 1);
        }
        Assert.True(GameService.BagUsed(p) >= GameService.BagCap(p));
        var tp = p.Count(Tp);
        Assert.Throws<GameException>(() => game.VipBuy(p, GameService.VipPackOffer, 1));
        Assert.False(p.Speed2);
        Assert.False(p.Speed2On);
        Assert.Equal(tp, p.Count(Tp));
        Assert.Equal(0, p.Count("bo_nguyen_dan_trung"));
        Assert.Equal(0, p.Count("bo_nguyen_dan_thuong"));
    }

    [Theory]
    [InlineData("khong_ton_tai", 1)]
    [InlineData(GameData.SpinTicket, 0)]
    [InlineData(GameData.SpinTicket, 1001)]
    [InlineData(GameService.VipHerbOffer, 1)]
    [InlineData("linh_thu_dan", 1)]
    public async Task RejectedPurchaseKeepsCurrency(string item, int n)
    {
        var (game, p) = await Setup();
        Assert.Throws<GameException>(() => game.VipBuy(p, item, n));
        Assert.Equal(1000, p.Count(Tp));
    }

    [Fact]
    public async Task QuantityIsLimitedOnlyByBalance()
    {
        var (game, p) = await Setup();
        game.VipBuy(p, GameData.SpinTicket, 1000);
        Assert.Equal(2000, p.Count(GameData.SpinTicket));
        Assert.Equal(0, p.Count(Tp));
    }

    [Fact]
    public async Task ExchangeAtMaxRealmDoesNotOverflow()
    {
        var (game, p) = await Setup(99);
        p.Realm = 9;
        var before = p.Count("linh_thach");
        game.VipBuy(p, GameService.VipStoneOffer, 99);
        Assert.Equal(before + 99m * 40 * KyDuyen.StoneUnit(9), p.Count("linh_thach"));
    }

    [Fact]
    public async Task GemLevelEightAndNinePrices()
    {
        var (_, p) = await Setup();
        Assert.Equal(10, GameService.VipOffersFor(p).Single(o => o.Item == GemData.GemsAtLevel(7)[0]).Price);
        Assert.Equal(15, GameService.VipOffersFor(p).Single(o => o.Item == GemData.GemsAtLevel(8)[0]).Price);
    }

    [Fact]
    public async Task HalfPriceRoundsUpAndDoublesQuantityForOnePremium()
    {
        var (_, p) = await Setup();
        var o = GameService.VipOffersFor(p).ToDictionary(x => x.Item);
        Assert.Equal(50, o[GameService.VipPackOffer].Price);
        Assert.Equal(10, o[GameData.RenameCard].Price);                       // 20 → 10
        Assert.Equal(15, o["thu_dan_thien"].Price);                           // 30 → 15
        Assert.Equal(25, o["thu_dan_thuong_co"].Price);                       // 50 → 25
        Assert.Equal(2, o[SupportPills.TuLinh + "_thuong"].Price);            // 3 → 1,5 làm tròn lên 2
        Assert.Equal(2, o[SupportPills.BeQuan + "_thuong"].Price);
        foreach (var x in new[] { GameData.SpinTicket, GameData.PetSpinTicket, GameService.VipHerbOffer }) { Assert.Equal(1, o[x].Price); Assert.Equal(2, o[x].Qty); }
        Assert.Equal(1, o[GameService.VipStoneOffer].Price);
        Assert.Equal(40 * KyDuyen.StoneUnit(p.Realm), o[GameService.VipStoneOffer].Qty);
    }

    [Fact]
    public async Task CatalogItemsExist()
    {
        var (_, p) = await Setup();
        foreach (var o in GameService.VipOffersFor(p).Where(o => o.Item is not (GameService.VipHerbOffer or GameService.VipPackOffer or GameService.VipPetPackOffer or GameService.VipStoneOffer)))
            Assert.True(GameData.ItemById.ContainsKey(o.Item), o.Item);
    }

    [Fact]
    public async Task SpeedToggleHalvesTripsOnlyWhenOn()
    {
        var (game, p) = await Setup(200);
        // Trận đánh gieo hạt theo ExploreReadyAt (độ dài chuyến dao động) nên cố định mốc này trước mỗi lần đo.
        var anchor = DateTime.UtcNow.AddMinutes(1);
        async Task<StateDto> Measure() { p.ExploreReadyAt = anchor; return await game.BuildStateAsync(p); }
        var normal = (await Measure()).ExploreCooldownMs;
        Assert.Throws<GameException>(() => game.SetSpeed2(p, true));          // chưa mua gói
        game.VipBuy(p, GameService.VipPackOffer, 1);
        var fast = (await Measure()).ExploreCooldownMs;
        Assert.InRange(fast, normal / 2 - 1000, normal / 2 + 1000);
        game.SetSpeed2(p, false);
        var off = await Measure();
        Assert.Equal(normal, off.ExploreCooldownMs);
        Assert.True(off.Speed2);
        Assert.False(off.Speed2On);
        game.SetSpeed2(p, true);
        Assert.True((await Measure()).Speed2On);
    }

    [Fact]
    public async Task AdminSpeedFlagsOverridePack()
    {
        var (game, p) = await Setup(200);
        var anchor = DateTime.UtcNow.AddMinutes(1);
        async Task<long> Ms() { p.ExploreReadyAt = anchor; return (await game.BuildStateAsync(p)).ExploreCooldownMs; }
        var normal = await Ms();
        game.VipBuy(p, GameService.VipPackOffer, 1);
        Assert.InRange(await Ms(), normal / 2 - 1000, normal / 2 + 1000);
        p.Admin = "fast3";
        Assert.InRange(await Ms(), normal / 3 - 1000, normal / 3 + 1000);
        p.Admin = "fast05";
        Assert.InRange(await Ms(), normal * 2 - 1000, normal * 2 + 1000);
    }

    [Fact]
    public async Task SameValueToggleKeepsTrip()
    {
        var (game, p) = await Setup(200);
        game.VipBuy(p, GameService.VipPackOffer, 1);
        var at = DateTime.UtcNow.AddSeconds(30);
        p.ExploreReadyAt = at;
        game.SetSpeed2(p, true);
        Assert.Equal(at, p.ExploreReadyAt);
        game.SetSpeed2(p, false);
        Assert.NotEqual(at, p.ExploreReadyAt);
    }
}
