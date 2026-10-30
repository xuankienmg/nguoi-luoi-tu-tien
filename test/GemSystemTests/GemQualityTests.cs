using VanDao.Data;
using VanDao.Game;
using Xunit;
namespace GemSystemTests;
public class GemQualityTests
{
    [Fact]
    public void SocketCountFollowsAllTenEquipmentQualitiesAndIgnoresRealmAndLegacyUnlocks()
    {
        var player = new Player { Realm = 9 };
        GemData.SetSlots(player, new() { ["weapon"] = new() { Extra = 2 } });
        Assert.Equal(0, GemData.MaxSockets(player, "weapon"));
        for (var q = 0; q < 10; q++)
        {
            player.Equipment.Clear();
            player.Equipment.Add(new() { Slot = "weapon", ItemId = GameData.AllItems.First(i => i.Slot == "weapon" && i.Rarity == GameData.RarityOrder[q]).Id });
            player.Realm = q % 2 == 0 ? 0 : 9;
            Assert.Equal(q < 4 ? 1 : q < 7 ? 2 : 3, GemData.MaxSockets(player, "weapon"));
        }
    }
    [Fact]
    public void DowngradingGearDisablesExtraBonusesButAllowsRecoveringExactGem()
    {
        var player = new Player();
        var equipped = new EquippedItem { Slot = "weapon", ItemId = GameData.AllItems.First(i => i.Slot == "weapon" && i.Rarity == "supreme").Id };
        player.Equipment.Add(equipped);
        player.Add("gem_hoa_lv10", 3);
        var service = new GameService(null!);
        for (var i = 0; i < 3; i++) service.SocketGem(player, "weapon", i, "gem_hoa_lv10");
        var perGem = GemData.ById["gem_hoa_lv10"].Atk + CombatNumber.Round(GameData.BaseStats(GameData.Realms[player.Realm], player.Stage).Atk * .1);
        Assert.Equal(perGem * 3, GemData.GetSocketedBonuses(player).Atk);
        equipped.ItemId = GameData.AllItems.First(i => i.Slot == "weapon" && i.Rarity == "common").Id;
        Assert.Equal(perGem, GemData.GetSocketedBonuses(player).Atk);
        Assert.Equal(3, GemData.BuildState(player).Slots["weapon"].Count);
        Assert.Throws<GameException>(() => service.SocketGem(player, "weapon", 1, "gem_hoa_lv10"));
        Assert.Equal(10000, GemData.UnsocketCost("gem_hoa_lv10"));
        player.Add("linh_thach", 20000);
        service.UnsocketGem(player, "weapon", 2);
        Assert.Equal(10000, player.Count("linh_thach"));
        Assert.Equal(1, player.Count("gem_hoa_lv10"));
        Assert.Equal(perGem, GemData.GetSocketedBonuses(player).Atk);
        player.Equipment.Clear();
        Assert.Equal(0, GemData.GetSocketedBonuses(player).Atk);
        service.UnsocketGem(player, "weapon", 0);
        Assert.Equal(2, player.Count("gem_hoa_lv10"));
        Assert.Equal(0, player.Count("linh_thach"));
    }
    [Fact]
    public void RemovedToolCannotBeCraftedOrUnlockSocketsAndIsHiddenFromLegacyVault()
    {
        var player = new Player { GemVault = "{\"stone_socket\":99,\"stone_viem_so\":9}" };
        Assert.False(GemData.GetVault(player).ContainsKey("stone_socket"));
        Assert.Equal(9, player.Count("stone_viem_so"));
        Assert.Throws<GameException>(() => new GameService(null!).UnlockSocket(player, "weapon"));
        Assert.Throws<GameException>(() => new GameService(null!).CraftGemRecipe(player, "fuse_stone_socket"));
    }
}
