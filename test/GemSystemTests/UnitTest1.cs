using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class GemSystemVerificationTests
{
    [Fact]
    public void TestGemData_ItemDefinitions_CountAndProperties()
    {
        Assert.Equal(25, GemData.Items.Count);
        foreach (var item in GemData.Items)
        {
            Assert.False(string.IsNullOrEmpty(item.Id));
            Assert.False(string.IsNullOrEmpty(item.Name));
            Assert.True(item.Type == "stone" || item.Type == "gem");
            if (item.Type == "stone") Assert.Equal("Cổ Phù", item.Kind);
            if (item.Type == "gem") Assert.Equal("Tiên Châu", item.Kind);
            Assert.True(GemData.IsGemOrStone(item.Id));
            Assert.True(GameData.ItemById.ContainsKey(item.Id));
        }

        Assert.False(GemData.IsGemOrStone("linh_thach"));
        Assert.False(GemData.IsGemOrStone("ruong_boss"));
        Assert.False(GemData.IsGemOrStone("kiem_go"));
        Assert.False(GemData.IsGemOrStone(null));
        Assert.False(GemData.IsGemOrStone(""));
    }

    [Fact]
    public void TestPlayer_GemVault_BypassesInventory()
    {
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Name = "TuSiTest",
            Inventory = new List<InventoryItem>(),
            GemVault = "{}"
        };

        Assert.Empty(player.Inventory);
        Assert.Equal(0, player.Count("gem_ruby_cui"));
        Assert.Equal(0, player.Count("stone_viem_so"));

        // Add stones and gems
        player.Add("stone_viem_so", 50);
        player.Add("gem_ruby_cui", 10);

        // Inventory list should still be empty!
        Assert.Empty(player.Inventory);

        // Player.Count should return amounts in GemVault
        Assert.Equal(50, player.Count("stone_viem_so"));
        Assert.Equal(10, player.Count("gem_ruby_cui"));

        // Deduct
        player.Add("stone_viem_so", -20);
        Assert.Equal(30, player.Count("stone_viem_so"));
    }

    [Fact]
    public void TestGemData_MaxSockets_And_ExtraUnlocks()
    {
        var player = new Player
        {
            Realm = 0,
            GemSlots = "{}"
        };

        // Realm 0: 1 socket
        Assert.Equal(1, GemData.MaxSockets(player, "weapon"));

        // Realm 2: 2 sockets
        player.Realm = 2;
        Assert.Equal(2, GemData.MaxSockets(player, "weapon"));

        // Realm 4: 3 sockets
        player.Realm = 4;
        Assert.Equal(3, GemData.MaxSockets(player, "weapon"));

        // Realm 0 with extra unlock
        player.Realm = 0;
        var slots = GemData.GetSlots(player);
        slots["weapon"] = new GemSlotData { Extra = 1 };
        GemData.SetSlots(player, slots);

        Assert.Equal(2, GemData.MaxSockets(player, "weapon"));

        // Cap at 3
        slots["weapon"].Extra = 5;
        GemData.SetSlots(player, slots);
        Assert.Equal(3, GemData.MaxSockets(player, "weapon"));
    }

    [Fact]
    public void TestGemData_SocketAndUnsocket()
    {
        var player = new Player
        {
            Realm = 4,
            GemVault = "{}",
            GemSlots = "{}"
        };

        // Add stone to vault
        GemData.Add(player, "stone_viem_so", 2);
        Assert.Equal(2, GemData.GetCount(player, "stone_viem_so"));

        // Socket into weapon slot 0
        var slots = GemData.GetSlots(player);
        var slotData = slots.GetValueOrDefault("weapon") ?? new GemSlotData();
        GemData.Add(player, "stone_viem_so", -1);
        while (slotData.Sockets.Count <= 0) slotData.Sockets.Add("");
        slotData.Sockets[0] = "stone_viem_so";
        slots["weapon"] = slotData;
        GemData.SetSlots(player, slots);

        Assert.Equal(1, GemData.GetCount(player, "stone_viem_so"));
        var reloadedSlots = GemData.GetSlots(player);
        Assert.Equal("stone_viem_so", reloadedSlots["weapon"].Sockets[0]);

        // Unsocket
        var unGId = reloadedSlots["weapon"].Sockets[0];
        reloadedSlots["weapon"].Sockets[0] = "";
        GemData.Add(player, unGId, 1);
        GemData.SetSlots(player, reloadedSlots);

        Assert.Equal(2, GemData.GetCount(player, "stone_viem_so"));
        Assert.Equal("", GemData.GetSlots(player)["weapon"].Sockets[0]);
    }

    [Fact]
    public void TestGemData_SocketBonusesCalculation()
    {
        var player = new Player
        {
            Realm = 4,
            GemSlots = "{}"
        };

        var slots = GemData.GetSlots(player);
        // Weapon: gem_ruby_xin (Atk +320) and stone_viem_cao (Atk +60, linh_ap +8%)
        slots["weapon"] = new GemSlotData
        {
            Sockets = new List<string> { "gem_ruby_xin", "stone_viem_cao" }
        };
        // Body: gem_sapphire_xin (Def +260) and stone_bang_cao (Def +60, ho_the +8%)
        slots["body"] = new GemSlotData
        {
            Sockets = new List<string> { "gem_sapphire_xin", "stone_bang_cao" }
        };
        GemData.SetSlots(player, slots);

        var bonuses = GemData.GetSocketedBonuses(player);
        Assert.Equal(380, bonuses.Atk);
        Assert.Equal(320, bonuses.Def);
        Assert.Equal(8.0, bonuses.SkillPct);
        Assert.Equal(8.0, bonuses.Guard);
    }

    [Fact]
    public void TestGemData_RecipesDefinitions_AllItemsExistInGame()
    {
        Assert.Equal(25, GemData.Recipes.Count);

        // Verify each recipe has valid output and inputs that actually exist in the game!
        foreach (var r in GemData.Recipes)
        {
            Assert.False(string.IsNullOrEmpty(r.Id));
            Assert.False(string.IsNullOrEmpty(r.Name));
            Assert.NotEmpty(r.Inputs);
            Assert.False(string.IsNullOrEmpty(r.ResultItem));
            Assert.True(r.ResultCount > 0);
            Assert.True(GameData.ItemById.ContainsKey(r.ResultItem), $"ResultItem '{r.ResultItem}' of recipe '{r.Id}' must exist in GameData.ItemById");

            foreach (var (itemId, count) in r.Inputs)
            {
                Assert.False(string.IsNullOrEmpty(itemId));
                Assert.True(count > 0);
                Assert.True(GameData.ItemById.ContainsKey(itemId), $"Input item '{itemId}' of recipe '{r.Id}' must exist in GameData.ItemById");
            }
        }
    }

    [Fact]
    public void TestGemData_CorruptedJson_Handling()
    {
        var player = new Player
        {
            Realm = 2,
            GemSlots = "{\"weapon\":{\"Sockets\":null,\"Extra\":1},\"body\":null,\"invalid_slot\":{\"Sockets\":[\"gem_ruby_xin\"],\"Extra\":0}}",
            GemVault = "invalid json"
        };

        // Should not throw exception
        var slots = GemData.GetSlots(player);
        Assert.NotNull(slots);
        Assert.NotNull(slots["weapon"].Sockets);

        var bonuses = GemData.GetSocketedBonuses(player);
        Assert.NotNull(bonuses);
        // Invalid slot key should be ignored
        Assert.Equal(0, bonuses.Atk);

        var state = GemData.BuildState(player);
        Assert.NotNull(state);
        Assert.Equal(10, state.Slots.Count);
    }

    [Fact]
    public void TestGameService_SocketGem_Validation()
    {
        var service = new GameService(null!);
        var player = new Player
        {
            Realm = 0, // only 1 socket available
            GemSlots = "{}",
            GemVault = "{}"
        };

        // 1. Cannot socket chisel
        GemData.Add(player, "stone_socket", 5);
        var exChisel = Assert.Throws<GameException>(() => service.SocketGem(player, "weapon", 0, "stone_socket"));
        Assert.Contains("không thể khảm nạm", exChisel.Message);

        // 2. Cannot socket into unopened socket index
        GemData.Add(player, "gem_ruby_cui", 1);
        var exLocked = Assert.Throws<GameException>(() => service.SocketGem(player, "weapon", 1, "gem_ruby_cui"));
        Assert.Contains("chưa được mở", exLocked.Message);

        // 3. Cannot socket item not in vault
        var exEmpty = Assert.Throws<GameException>(() => service.SocketGem(player, "weapon", 0, "gem_ruby_xin"));
        Assert.Contains("Kho không còn", exEmpty.Message);

        // 4. Successful socket and gem exchange
        var (msg, kind) = service.SocketGem(player, "weapon", 0, "gem_ruby_cui");
        Assert.Equal("good", kind);
        Assert.Equal(0, GemData.GetCount(player, "gem_ruby_cui"));
        Assert.Equal("gem_ruby_cui", GemData.GetSlots(player)["weapon"].Sockets[0]);

        // 5. Replace gem: old gem returns to vault
        GemData.Add(player, "stone_viem_so", 1);
        service.SocketGem(player, "weapon", 0, "stone_viem_so");
        Assert.Equal("stone_viem_so", GemData.GetSlots(player)["weapon"].Sockets[0]);
        Assert.Equal(1, GemData.GetCount(player, "gem_ruby_cui")); // returned to vault!

        // 6. Unsocket
        var (unMsg, unKind) = service.UnsocketGem(player, "weapon", 0);
        Assert.Equal("good", unKind);
        Assert.Equal(1, GemData.GetCount(player, "stone_viem_so"));
        Assert.Equal("", GemData.GetSlots(player)["weapon"].Sockets[0]);
    }

    [Fact]
    public void TestGameService_QuickFuseGems_GoldCapping()
    {
        var service = new GameService(null!);
        var player = new Player
        {
            Inventory = new List<InventoryItem>(),
            GemVault = "{}"
        };

        // Player has 9 stone_viem_so (needs 600 gold for all 3 fusions)
        // But only has 450 gold -> can only fuse 2 (400 gold)
        GemData.Add(player, "stone_viem_so", 9);
        player.Add("linh_thach", 450);

        var (msg, kind, craft) = service.QuickFuseGems(player);
        Assert.Equal("gold", kind);
        Assert.NotNull(craft);
        Assert.Equal(2, GemData.GetCount(player, "stone_viem_cao"));
        Assert.Equal(3, GemData.GetCount(player, "stone_viem_so")); // 3 left
        Assert.Equal(50, player.Count("linh_thach")); // 450 - 400 = 50

        // If player has stones but 0 gold
        var (msgFail, _, _) = service.QuickFuseGems(player);
        Assert.Contains("Không đủ linh thạch", msgFail);
    }

    [Fact]
    public void TestGameService_UnlockSocket()
    {
        var service = new GameService(null!);
        var player = new Player
        {
            Realm = 0, // max sockets 1
            GemSlots = "{}",
            GemVault = "{}"
        };

        // Fail when player has no chisel
        var exNoChisel = Assert.Throws<GameException>(() => service.UnlockSocket(player, "weapon"));
        Assert.Contains("Cần 1 Khai Khiếu Cổ Phù", exNoChisel.Message);

        // Add 2 chisels
        GemData.Add(player, "stone_socket", 2);
        Assert.Equal(1, GemData.MaxSockets(player, "weapon"));

        // Unlock socket 2
        var (msg1, kind1) = service.UnlockSocket(player, "weapon");
        Assert.Equal("gold", kind1);
        Assert.Equal(2, GemData.MaxSockets(player, "weapon"));
        Assert.Equal(1, GemData.GetCount(player, "stone_socket"));

        // Unlock socket 3
        var (msg2, kind2) = service.UnlockSocket(player, "weapon");
        Assert.Equal("gold", kind2);
        Assert.Equal(3, GemData.MaxSockets(player, "weapon"));
        Assert.Equal(0, GemData.GetCount(player, "stone_socket"));

        // Try unlocking when already max (3)
        GemData.Add(player, "stone_socket", 1);
        var exMax = Assert.Throws<GameException>(() => service.UnlockSocket(player, "weapon"));
        Assert.Contains("đã mở tối đa 3 lỗ khảm", exMax.Message);
    }

    [Fact]
    public void TestGameService_CraftGemRecipe()
    {
        var service = new GameService(null!);
        var player = new Player
        {
            Inventory = new List<InventoryItem>(),
            GemSlots = "{}",
            GemVault = "{}"
        };

        // Craft Ruby from herb (huyet_can_thao)
        player.Add("huyet_can_thao", 25);
        player.Add("linh_thach", 2000);

        // Batch craft times = 2
        var (msg, kind, craft) = service.CraftGemRecipe(player, "craft_gem_ruby", times: 2);
        Assert.Equal("gold", kind);
        Assert.NotNull(craft);
        Assert.Equal(2, GemData.GetCount(player, "gem_ruby_cui"));
        Assert.Equal(5, player.Count("huyet_can_thao")); // 25 - 20 = 5
        Assert.Equal(1000, player.Count("linh_thach")); // 2000 - 1000 = 1000

        // Craft Khai Khiếu Cổ Phù (Khai Khiếu Cổ Phù needs 1 of each 5 sơ cổ phù + 1000 gold)
        GemData.Add(player, "stone_viem_so", 1);
        GemData.Add(player, "stone_bang_so", 1);
        GemData.Add(player, "stone_loi_so", 1);
        GemData.Add(player, "stone_kim_so", 1);
        GemData.Add(player, "stone_tho_so", 1);
        var (msgSocket, kindSocket, craftSocket) = service.CraftGemRecipe(player, "fuse_stone_socket", times: 1);
        Assert.Equal("gold", kindSocket);
        Assert.Equal(1, GemData.GetCount(player, "stone_socket"));
        Assert.Equal(0, player.Count("linh_thach"));
    }

    [Fact]
    public void TestGameService_ConvertLegacy()
    {
        var player = new Player
        {
            Inventory = new List<InventoryItem>
            {
                new() { ItemId = "stone_viem_so", Count = 15 },
                new() { ItemId = "gem_ruby_xin", Count = 3 },
                new() { ItemId = "linh_thach", Count = 1000 }
            },
            GemVault = "{}"
        };

        GameService.ConvertLegacy(player);

        // Gem and stone items in inventory must have Count = 0
        Assert.Equal(0, player.Inventory.First(i => i.ItemId == "stone_viem_so").Count);
        Assert.Equal(0, player.Inventory.First(i => i.ItemId == "gem_ruby_xin").Count);

        // GemVault must have received them
        Assert.Equal(15, GemData.GetCount(player, "stone_viem_so"));
        Assert.Equal(3, GemData.GetCount(player, "gem_ruby_xin"));

        // Normal items untouched
        Assert.Equal(1000, player.Count("linh_thach"));
    }

    [Fact]
    public void TestGemData_StageGroups_And_BossDrops()
    {
        for (int ch = 0; ch < 10; ch++)
        {
            var stoneGrp = GemData.GetStageStoneGroup(ch, 0, 1);
            var gemGrp = GemData.GetStageGemGroup(ch, 0, 1);
            Assert.NotNull(stoneGrp);
            Assert.NotEmpty(stoneGrp.Choices);
            Assert.NotNull(gemGrp);
            Assert.NotEmpty(gemGrp.Choices);

            var bossDrop = GemData.BossDrops(ch, 0);
            Assert.False(string.IsNullOrEmpty(bossDrop.Stone));
            Assert.False(string.IsNullOrEmpty(bossDrop.Gem));
            Assert.True(GemData.IsStone(bossDrop.Stone));
            Assert.True(GemData.IsGem(bossDrop.Gem));
        }
    }
}