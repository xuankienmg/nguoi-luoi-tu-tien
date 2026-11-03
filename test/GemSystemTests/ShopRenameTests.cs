using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class ShopRenameTests : IDisposable
{
    private static readonly DateTimeOffset Day1 = new(2026, 11, 2, 10, 0, 0, TimeSpan.FromHours(7));
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    private async Task<(GameService Game, Player Player)> Setup()
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Đạo hữu");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Add("linh_thach", 1_000_000_000_000 - p.Count("linh_thach"));
        return (game, p);
    }

    [Fact]
    public void StoreSellsRenameCardFor100kAndCardIsTradable()
    {
        Assert.Equal("Tạp Hóa bà Dĩm", GameData.Shop.Name);
        var item = Assert.Single(GameData.Shop.Fixed);
        Assert.Equal(GameData.RenameCard, item.Item);
        Assert.Equal(100_000, item.Price);
        Assert.True(GameService.Tradable(GameData.ItemById[GameData.RenameCard]));
        Assert.False(GameService.Tradable(GameData.ItemById["linh_thach"]));
        Assert.False(GameService.Tradable(GameData.ItemById["thuong_pham_linh_thach"]));
        Assert.False(GameService.Tradable(GameData.ItemById[GameData.SpinTicket]));
        Assert.False(GameService.Tradable(GameData.ItemById[GameData.PetSpinTicket]));
        Assert.False(GameService.Tradable(GameData.ItemById["thuong_pham_linh_thach"], canSellTickets: true));   // tiền donate: admin cũng không treo bán
        Assert.True(GameService.Tradable(GameData.ItemById[GameData.SpinTicket], canSellTickets: true));
    }

    [Fact]
    public void DailyStockIsTenDistinctItemsSharedByDayAndChangesNextDay()
    {
        var today = GameService.ShopDaily(Day1);
        Assert.Equal(10, today.Count);
        Assert.Equal(10, today.Distinct().Count());
        Assert.All(today, id => Assert.Contains(GameService.ShopDailyPool, d => d.Id == id));
        Assert.All(GameService.ShopDailyPool, d =>
        {
            Assert.Null(d.Slot); Assert.True(GameService.ShopStockable(d));
            Assert.NotEqual("chest", d.Type);
            Assert.True(GameData.BreakPillRealm(d.Id) < 0, d.Id);
            Assert.True(GemData.GemLevel(d.Id) <= 8, d.Id);                                // Tinh Thạch cấp 10 không bán
        });
        Assert.Contains(GameService.ShopDailyPool, d => GemData.GemLevel(d.Id) == 8);
        Assert.Equal(today, GameService.ShopDaily(Day1.AddHours(1)));                    // 11h59 vẫn đợt sáng
        Assert.NotEqual(today, GameService.ShopDaily(Day1.AddHours(2)));                 // 12h VN: đợt chiều
        Assert.NotEqual(today, GameService.ShopDaily(Day1.AddHours(14)));                // 0h ngày hôm sau
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 450)]
    [InlineData(2, 4_050)]
    [InlineData(5, 2_952_450)]
    [InlineData(8, 2_152_336_050)]
    public void GemPriceIsFixedPerLevelAndIgnoresRealm(int level, long price)
    {
        var gem = GemProgression.Id("hoa", level);
        Assert.Equal(price, GameService.ShopDailyPrice(new Player { Realm = 0 }, gem));
        Assert.Equal(price, GameService.ShopDailyPrice(new Player { Realm = 9 }, gem));
    }

    [Fact]
    public void ShopMarkupDependsOnItemKind()
    {
        var p = new Player { Realm = 5 };
        foreach (var (type, markup) in new[] { ("egg", 600d), ("petgear", 100d), ("herb", 30d) })
        {
            var item = GameService.ShopDailyPool.First(d => d.Type == type);
            Assert.Equal((long)Math.Ceiling(GameData.SellPrice(item) * markup), GameService.ShopDailyPrice(p, item.Id));
        }
        Assert.Equal(3_000_000, GameService.ShopDailyPrice(p, "dai_thua_dan"));          // đan phương 2.000.000 linh thạch × 1,5
        Assert.Equal(900_000, GameService.ShopDailyPrice(p, "hop_the_dan"));
        Assert.Equal(30, GameService.ShopDailyPrice(p, "tu_khi_dan"));
        var other = GameService.ShopDailyPool.First(d => GemData.GemLevel(d.Id) < 0 && d.Type is not ("egg" or "herb" or "petgear") && !GameData.PillChain.Contains(d.Id));
        Assert.Equal((long)Math.Ceiling(GameData.SellPrice(other) * 3d), GameService.ShopDailyPrice(p, other.Id));
    }

    [Theory]
    [InlineData("hoa_than_dan", 37_500)]       // đan phương 25.000 × 1,5
    [InlineData("do_kiep_dan", 9_000_000)]
    [InlineData("chan_tien_dan", 27_000_000)]
    [InlineData("thu_linh_dan", 45_000)]       // đan hỗ trợ: đan phương 30.000 × 1,5
    [InlineData("be_quan_dan", 180_000)]
    public void HighPillQualitiesCostTwoAndFourTimesTheBasePill(string pill, long basePrice)
    {
        var p = new Player { Realm = 9 };
        Assert.Equal(basePrice, GameService.ShopDailyPrice(p, pill));
        Assert.Equal(basePrice * 2, GameService.ShopDailyPrice(p, pill + "_trung"));
        Assert.Equal(basePrice * 4, GameService.ShopDailyPrice(p, pill + "_thuong"));
    }

    [Fact]
    public void EveryHighPillQualityIsPricierThanTheLowerOne()
    {
        var p = new Player { Realm = 9 };
        foreach (var pill in GameData.PillChain.Concat(SupportPills.ById.Keys).Where(id => GameData.ItemById[id].Tier >= GameService.ShopPillTierFrom))
        {
            var prices = new[] { pill, pill + "_trung", pill + "_thuong" }.Select(id => GameService.ShopDailyPrice(p, id)).ToArray();
            Assert.True(prices[0] < prices[1] && prices[1] < prices[2], $"{pill}: {string.Join(" / ", prices)}");
        }
    }

    [Fact]
    public void PillsBelowHoaThanKeepTheirPrices()
    {
        var p = new Player { Realm = 3 };
        Assert.Equal(4_500, GameService.ShopDailyPrice(p, "ket_anh_dan"));
        Assert.Equal((long)Math.Ceiling(GameData.SellPrice(GameData.ItemById["ket_anh_dan_trung"]) * 3d), GameService.ShopDailyPrice(p, "ket_anh_dan_trung"));
        Assert.Equal((long)Math.Ceiling(GameData.SellPrice(GameData.ItemById["tat_hanh_dan"]) * 3d), GameService.ShopDailyPrice(p, "tat_hanh_dan"));
    }

    [Theory]
    [InlineData("common", 10)]
    [InlineData("uncommon", 7)]
    [InlineData("rare", 5)]
    [InlineData("epic", 3)]
    [InlineData("legendary", 2)]
    [InlineData("mythic", 1)]
    [InlineData("supreme", 1)]
    public void DailyLimitDependsOnRarity(string rarity, int limit)
    {
        var item = GameData.AllItems.First(d => d.Rarity == rarity);
        Assert.Equal(limit, GameService.ShopDailyLimit(item.Id));
    }

    [Fact]
    public async Task DailyItemsCanBeBoughtUpToTheirLimitPerDay()
    {
        var (game, p) = await Setup();
        foreach (var item in GameService.ShopDaily(Day1))
        {
            var offer = GameService.ShopFor(p, Day1).Offers.Single(o => o.Item == item && o.Daily);
            Assert.Equal(GameService.ShopDailyPrice(p, item), offer.Price);
            Assert.Equal(GameService.ShopDailyLimit(item), offer.Limit);
            Assert.InRange(offer.Limit, 1, 10);
            var stones = p.Count("linh_thach");
            var have = p.Count(item);
            Assert.Throws<GameException>(() => game.ShopBuy(p, "ba_diem", item, offer.Limit + 1, at: Day1));   // quá phần trong ngày
            game.ShopBuy(p, "ba_diem", item, offer.Limit, at: Day1);
            Assert.Equal(stones - offer.Price * offer.Limit, p.Count("linh_thach"));
            Assert.Equal(have + offer.Limit, p.Count(item));
            Assert.Equal(offer.Limit, GameService.ShopFor(p, Day1).Offers.Single(o => o.Item == item && o.Daily).Bought);
            Assert.Throws<GameException>(() => game.ShopBuy(p, "ba_diem", item, at: Day1));
            Assert.Equal(have + offer.Limit, p.Count(item));
        }
        Assert.DoesNotContain(GameService.ShopFor(p, Day1.AddDays(1)).Offers, o => o.Bought > 0);
    }

    [Fact]
    public async Task PartialPurchasesAddUpWithinTheDay()
    {
        var (game, p) = await Setup();
        var item = GameService.ShopDaily(Day1).First(id => GameService.ShopDailyLimit(id) >= 3);
        game.ShopBuy(p, "ba_diem", item, 1, at: Day1);
        game.ShopBuy(p, "ba_diem", item, 2, at: Day1);
        Assert.Equal(3, GameService.ShopFor(p, Day1).Offers.Single(o => o.Item == item && o.Daily).Bought);
    }

    [Fact]
    public async Task RenameCardHasNoDailyLimitButNeedsStones()
    {
        var (game, p) = await Setup();
        game.ShopBuy(p, "ba_diem", GameData.RenameCard, at: Day1);
        game.ShopBuy(p, "ba_diem", GameData.RenameCard, at: Day1);
        Assert.Equal(2, p.CountInt(GameData.RenameCard));
        p.Add("linh_thach", 50_000 - p.Count("linh_thach"));
        Assert.Throws<GameException>(() => game.ShopBuy(p, "ba_diem", GameData.RenameCard, at: Day1));
        Assert.Equal(2, p.CountInt(GameData.RenameCard));
        var notToday = GameService.ShopDailyPool.First(d => !GameService.ShopDaily(Day1).Contains(d.Id));
        Assert.Throws<GameException>(() => game.ShopBuy(p, "ba_diem", notToday.Id, at: Day1));
    }

    [Fact]
    public async Task RenameNeedsAndConsumesOneCard()
    {
        var (game, p) = await Setup();
        var old = p.Name;
        Assert.Throws<GameException>(() => game.Rename(p, "Tên Mới"));
        Assert.Equal(old, p.Name);
        p.Add(GameData.RenameCard, 2);
        game.Rename(p, "Tên Mới");
        Assert.Equal("Tên Mới", p.Name);
        Assert.Equal(1, p.CountInt(GameData.RenameCard));
        Assert.Throws<GameException>(() => game.Rename(p, "Tên Mới"));
        Assert.Equal(1, p.CountInt(GameData.RenameCard));
    }

    public void Dispose() => db.Dispose();
}
