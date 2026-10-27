using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class GemRegressionTests
{
    [Theory]
    [InlineData("linh_thach")]
    [InlineData("unknown_item")]
    [InlineData("tu_khi_dan")]
    public void FusionRejectsUnrelatedItemsWithoutConsumingResources(string item)
    {
        var player = new Player();
        player.Add("linh_thach", 10000);
        player.Add(item, 2);
        var inventory = player.Inventory.ToDictionary(i => i.ItemId, i => i.Count);
        Assert.False(GemData.EvaluateFusion(new() { [item] = 2 }).IsValid);
        Assert.False(GemData.EvaluateFusion(new() { [item] = 1, ["stone_viem_so"] = 1 }).IsValid);
        Assert.Throws<GameException>(() => new GameService(null!).CraftGemFromItems(player, $"{item}:2"));
        foreach (var (id, count) in inventory) Assert.Equal(count, player.Count(id));
        Assert.Empty(GemData.GetVault(player));
    }

    [Fact]
    public void BossTokensMergeOneForOneAndAllFirstFourChaptersUseSharedToken()
    {
        var p = new Player { MapVer = 1 };
        p.Add("con_ngo_lenh", 31); p.Add("huyet_sac_lenh", 95);
        GameService.ConvertLegacy(p); GameService.ConvertLegacy(p);
        Assert.Equal(126, p.Count("con_ngo_lenh"));
        Assert.Equal(0, p.Count("huyet_sac_lenh"));
        Assert.False(GameData.ItemById.ContainsKey("huyet_sac_lenh"));
        foreach (var d in Enumerable.Range(0, 3))
        foreach (var c in Enumerable.Range(0, 4))
            Assert.Equal("con_ngo_lenh", GameData.ChapterFor(d, c).KeyItem);
    }

    [Fact]
    public void SocketedStonesFollowEachEquipmentCopyAndSurviveSaving()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var p = new Player(); db.Players.Add(p);
        var service = new GameService(db);
        var weapon = GameData.AllItems.First(i => i.Slot == "weapon" && i.Rarity == "common" && i.MinRealm == 0).Id;
        p.Add(weapon, 2); p.Add("gem_hoa_lv1", 1); p.Add("gem_quang_lv1", 1);
        service.Equip(p, weapon); service.SocketGem(p, "weapon", 0, "gem_hoa_lv1");
        service.Equip(p, weapon); // plain duplicate replaces socketed copy
        Assert.Empty(GemData.GetSocketedItems(p, "weapon"));
        Assert.Equal("gem_hoa_lv1", GemData.BuildState(p).BagSlots![weapon][0][0]);
        service.SocketGem(p, "weapon", 0, "gem_quang_lv1");
        db.SaveChanges();
        service.Unequip(p, "weapon");
        db.SaveChanges();
        Assert.Equal(0, GemData.GetSocketedBonuses(p).Atk);
        Assert.Equal(2, p.Count(weapon));
        service.Equip(p, weapon, copy: 1);
        Assert.Equal("gem_quang_lv1", GemData.GetSocketedItems(p, "weapon")[0]);
        service.Equip(p, weapon, copy: 0);
        Assert.Equal("gem_hoa_lv1", GemData.GetSocketedItems(p, "weapon")[0]);
        Assert.Equal(0, p.Count("gem_hoa_lv1")); Assert.Equal(0, p.Count("gem_quang_lv1"));
        db.SaveChanges(); db.ChangeTracker.Clear();
        var saved = db.Players.Include(x => x.Inventory).Include(x => x.Equipment).Single();
        Assert.Equal("gem_hoa_lv1", GemData.GetSocketedItems(saved, "weapon")[0]);
        Assert.Equal("gem_quang_lv1", GemData.BuildState(saved).BagSlots![weapon][0][0]);
        new GameService(db).Equip(saved, weapon);
        Assert.Equal("gem_quang_lv1", GemData.GetSocketedItems(saved, "weapon")[0]);
        Assert.Throws<GameException>(() => new GameService(db).Equip(saved, weapon, copy: 5));
    }

    [Fact]
    public void GemCategoriesRestrictEveryEquipmentSlotWithoutConsumingRejectedGems()
    {
        var service = new GameService(null!);
        Assert.All(GemData.Items, gem => Assert.Contains(gem.GemCategory, new[] { "attack", "defense" }));
        foreach (var slot in GemData.EquipSlots)
        foreach (var theme in GemProgression.Themes)
        {
            var id = GemProgression.Id(theme, 0);
            var p = new Player();
            p.Equipment.Add(new() { Slot = slot, ItemId = GameData.AllItems.First(i => i.Slot == slot && i.Rarity == "common").Id });
            p.Add(id, 1);
            var defense = theme is "bang" or "tho" or "phuc_nguyen" or "vu_tru";
            var armor = slot is "head" or "feet" or "body" or "hands" or "legs";
            if (defense == armor) {
                service.SocketGem(p, slot, 0, id);
                Assert.Equal(0, p.Count(id));
                Assert.Equal(id, GemData.GetSocketedItems(p, slot)[0]);
            } else {
                Assert.Throws<GameException>(() => service.SocketGem(p, slot, 0, id));
                Assert.Equal(1, p.Count(id));
                Assert.Empty(GemData.GetSlots(p));
            }
        }
    }

    [Fact]
    public void SellingSupportsEveryItemIncludingVaultGemsAndPremiumCurrency()
    {
        Assert.All(GameData.AllItems, item => Assert.True(item.Sell > 0));
        var p = new Player();
        var service = new GameService(null!);
        p.Add("gem_hoa_lv10", 3);
        p.Add("thuong_pham_linh_thach", 2);
        var price = GameData.ItemById["gem_hoa_lv10"].Sell;
        service.Sell(p, "gem_hoa_lv10", 1);
        Assert.Equal(2, p.Count("gem_hoa_lv10"));
        Assert.Equal(price, p.Count("linh_thach"));
        service.SellBulk(p, 9, items: "gem_hoa_lv10:2,thuong_pham_linh_thach:1", pick: true);
        Assert.Equal(0, p.Count("gem_hoa_lv10"));
        Assert.Equal(1, p.Count("thuong_pham_linh_thach"));
        Assert.Equal(3 * price + GameData.ItemById["thuong_pham_linh_thach"].Sell, p.Count("linh_thach"));
        var balance = p.Count("linh_thach");
        service.Sell(p, "linh_thach", balance);
        Assert.Equal(balance, p.Count("linh_thach"));
        Assert.Throws<GameException>(() => service.Sell(p, "gem_hoa_lv10", 1));
    }

    [Fact]
    public void AdminResetClearsVaultSocketsAndTheirCombatBonuses()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var player = new Player { Tainted = true };
        db.Players.Add(player);
        player.Add("gem_thien_phu", 10);
        GemData.SetSlots(player, new() { ["weapon"] = new() { Extra = 2, Sockets = new() { "gem_ruby_xin" } } });
        db.SaveChanges();
        var service = new GameService(db);
        service.AdminAction(player, "resetall", null, 0);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        var saved = db.Players.Single();
        Assert.Empty(GemData.GetVault(saved));
        Assert.Empty(GemData.GetSlots(saved));
        Assert.Equal(0, GemData.MaxSockets(saved, "weapon"));
        Assert.Equal(new GemBonuses(), GemData.GetSocketedBonuses(saved));
        Assert.False(saved.Tainted);
    }
}
