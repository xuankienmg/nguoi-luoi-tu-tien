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
    public void TestGameService_SocketGem_DirectSocketIndex2_WhenLowerEmpty()
    {
        var service = new GameService(null!);
        var player = new Player
        {
            Realm = 4, // Hóa Thần: 3 sockets available (0, 1, 2)
            GemSlots = "{}",
            GemVault = "{}"
        };

        // Player has stones
        GemData.Add(player, "stone_viem_so", 2);
        GemData.Add(player, "gem_ruby_cui", 1);

        // 1. Socket directly into socketIndex = 2 (Lỗ 3) when 0 and 1 are empty
        var (msg, kind) = service.SocketGem(player, "weapon", 2, "stone_viem_so");
        Assert.Equal("good", kind);
        Assert.Equal(1, GemData.GetCount(player, "stone_viem_so"));

        var slotData = GemData.GetSlots(player)["weapon"];
        Assert.True(slotData.Sockets.Count >= 3);
        Assert.Equal("", slotData.Sockets[0]);
        Assert.Equal("", slotData.Sockets[1]);
        Assert.Equal("stone_viem_so", slotData.Sockets[2]);

        // Verify stats bonuses include Lỗ 3
        var bonus = GemData.GetSocketedBonuses(player);
        Assert.Equal(20, bonus.Atk);
        Assert.Equal(3.0, bonus.SkillPct);

        // Verify BuildState
        var state = GemData.BuildState(player);
        Assert.Equal(3, state.Slots["weapon"].Count);
        Assert.Equal("", state.Slots["weapon"][0]);
        Assert.Equal("", state.Slots["weapon"][1]);
        Assert.Equal("stone_viem_so", state.Slots["weapon"][2]);

        // 2. Replace gem in socketIndex = 2
        var (repMsg, repKind) = service.SocketGem(player, "weapon", 2, "gem_ruby_cui");
        Assert.Equal("good", repKind);
        Assert.Equal(2, GemData.GetCount(player, "stone_viem_so")); // returned to vault
        Assert.Equal(0, GemData.GetCount(player, "gem_ruby_cui"));
        Assert.Equal("gem_ruby_cui", GemData.GetSlots(player)["weapon"].Sockets[2]);

        // 3. Unsocket from socketIndex = 2
        var (unMsg, unKind) = service.UnsocketGem(player, "weapon", 2);
        Assert.Equal("good", unKind);
        Assert.Equal(1, GemData.GetCount(player, "gem_ruby_cui"));
        Assert.Equal("", GemData.GetSlots(player)["weapon"].Sockets[2]);
    }

    [Fact]
    public void TestGameService_SocketGem_ExactHinh1Scenario_Hole1Filled_Hole2Empty_SocketDirectlyHole3()
    {
        var service = new GameService(null!);
        var player = new Player
        {
            Realm = 4, // Hóa Thần: 3 sockets available (0, 1, 2)
            GemSlots = "{}",
            GemVault = "{}"
        };

        // Hình 1 state:
        // Lỗ 1 (index 0): Lôi Cổ Phù - Sơ ("stone_loi_so")
        // Lỗ 2 (index 1): Trống ("")
        // Lỗ 3 (index 2): Trống -> người dùng chọn Lỗ 3 và khảm vật phẩm
        GemData.Add(player, "stone_loi_so", 1);
        GemData.Add(player, "stone_viem_so", 1);

        // 1. Khảm Lỗ 1
        service.SocketGem(player, "weapon", 0, "stone_loi_so");
        Assert.Equal("stone_loi_so", GemData.GetSlots(player)["weapon"].Sockets[0]);

        // 2. Người dùng nhấp chọn Lỗ 3 (socketIndex = 2) trong khi Lỗ 2 đang trống
        var (msg, kind) = service.SocketGem(player, "weapon", 2, "stone_viem_so");
        Assert.Equal("good", kind);
        Assert.Equal(0, GemData.GetCount(player, "stone_viem_so"));

        var slotData = GemData.GetSlots(player)["weapon"];
        Assert.Equal(3, slotData.Sockets.Count);
        Assert.Equal("stone_loi_so", slotData.Sockets[0]);
        Assert.Equal("", slotData.Sockets[1]); // Lỗ 2 vẫn trống
        Assert.Equal("stone_viem_so", slotData.Sockets[2]); // Lỗ 3 được khảm chính xác

        // Kiểm tra tổng thuộc tính: cộng dồn cả Lỗ 1 (+3% crit, +5% lightning) và Lỗ 3 (+20 atk, +3% skill)
        var bonus = GemData.GetSocketedBonuses(player);
        Assert.Equal(20, bonus.Atk);
        Assert.Equal(3.0, bonus.Crit);
        Assert.Equal(5.0, bonus.Lightning);
        Assert.Equal(3.0, bonus.SkillPct);

        // Kiểm tra BuildState cho UI client
        var state = GemData.BuildState(player);
        Assert.Equal("stone_loi_so", state.Slots["weapon"][0]);
        Assert.Equal("", state.Slots["weapon"][1]);
        Assert.Equal("stone_viem_so", state.Slots["weapon"][2]);
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
        var succRng = new DeterministicRandom(0.10);
        var (msg, kind, craft) = service.CraftGemRecipe(player, "craft_gem_ruby", times: 2, random: succRng);
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

    private class DeterministicRandom : Random
    {
        private readonly double _val;
        public DeterministicRandom(double val) => _val = val;
        public override double NextDouble() => _val;
    }

    [Fact]
    public void TestGameService_CraftGemRecipe_SuccessAndFailureRates()
    {
        var service = new GameService(null!);
        var player = new Player
        {
            Inventory = new List<InventoryItem>(),
            GemSlots = "{}",
            GemVault = "{}"
        };

        // 1. Verify recipes have defined chances < 1.0
        var viemRecipe = GemData.RecipeById["fuse_stone_viem"];
        Assert.Equal(0.85, viemRecipe.Chance);
        var mutantRecipe = GemData.RecipeById["fuse_stone_liet_loi"];
        Assert.Equal(0.75, mutantRecipe.Chance);
        var divineRecipe = GemData.RecipeById["ascend_thien_phu"];
        Assert.Equal(0.65, divineRecipe.Chance);

        // 2. Test Failure with partial refund:
        // fuse_stone_viem needs 3 stone_viem_so + 200 gold
        GemData.Add(player, "stone_viem_so", 3);
        player.Add("linh_thach", 500);

        // DeterministicRandom with 0.99 => will fail (0.99 > 0.85)
        var failRng = new DeterministicRandom(0.99);
        var (msgFail, kindFail, craftFail) = service.CraftGemRecipe(player, "fuse_stone_viem", times: 1, random: failRng);

        Assert.Equal("bad", kindFail);
        Assert.NotNull(craftFail);
        Assert.Equal("gem:fail", craftFail.Recipe);
        Assert.Equal(0, GemData.GetCount(player, "stone_viem_cao")); // 0 created
        // Player should NOT lose everything: 1 stone preserved and refunded!
        Assert.Equal(1, GemData.GetCount(player, "stone_viem_so"));
        Assert.Equal(300, player.Count("linh_thach")); // 500 - 200

        // 3. Test Success:
        // Add 2 more stone_viem_so so player has 3 again
        GemData.Add(player, "stone_viem_so", 2);
        Assert.Equal(3, GemData.GetCount(player, "stone_viem_so"));

        // DeterministicRandom with 0.01 => will succeed (0.01 < 0.85)
        var succRng = new DeterministicRandom(0.01);
        var (msgSucc, kindSucc, craftSucc) = service.CraftGemRecipe(player, "fuse_stone_viem", times: 1, random: succRng);

        Assert.Equal("gold", kindSucc);
        Assert.NotNull(craftSucc);
        Assert.Equal("gem:ok", craftSucc.Recipe);
        Assert.Equal(1, GemData.GetCount(player, "stone_viem_cao")); // 1 created!
        Assert.Equal(0, GemData.GetCount(player, "stone_viem_so")); // 3 consumed
        Assert.Equal(100, player.Count("linh_thach")); // 300 - 200
    }

    [Fact]
    public void TestDesktop_PanelWindow_TabNames_ContainsVault()
    {
        var panelType = typeof(VanDao.Desktop.PanelWindow);
        var tabNamesField = panelType.GetField("TabNames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(tabNamesField);
        var tabNames = tabNamesField.GetValue(null) as Dictionary<string, string>;
        Assert.NotNull(tabNames);
        Assert.True(tabNames.ContainsKey("vault"));
        Assert.Equal("Luyện chế · Chú linh", tabNames["vault"]);

        var tabNamesEnField = panelType.GetField("TabNamesEn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(tabNamesEnField);
        var tabNamesEn = tabNamesEnField.GetValue(null) as Dictionary<string, string>;
        Assert.NotNull(tabNamesEn);
        Assert.True(tabNamesEn.ContainsKey("vault"));
        Assert.Equal("Crafting · Socketing", tabNamesEn["vault"]);
    }

    [Fact]
    public void TestGemService_UnsocketEdgeCases()
    {
        var service = new GameService(null!);
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Name = "TuSiUnsocket",
            Realm = 4, // 3 sockets
            GemSlots = "{}",
            GemVault = "{}"
        };

        // 1. Invalid slot
        Assert.Throws<GameException>(() => service.UnsocketGem(player, "invalid_slot", 0));

        // 2. Empty slot (nothing socketed yet)
        Assert.Throws<GameException>(() => service.UnsocketGem(player, "weapon", 0));

        // 3. Out of bounds index
        Assert.Throws<GameException>(() => service.UnsocketGem(player, "weapon", -1));
        Assert.Throws<GameException>(() => service.UnsocketGem(player, "weapon", 99));

        // 4. Socket 2 gems: slot 0 = stone_viem_so, slot 1 = stone_bang_so
        GemData.Add(player, "stone_viem_so", 1);
        GemData.Add(player, "stone_bang_so", 1);
        service.SocketGem(player, "weapon", 0, "stone_viem_so");
        service.SocketGem(player, "weapon", 1, "stone_bang_so");

        Assert.Equal(0, GemData.GetCount(player, "stone_viem_so"));
        Assert.Equal(0, GemData.GetCount(player, "stone_bang_so"));

        // Unsocket slot 0 (stone_viem_so)
        var (msg1, kind1) = service.UnsocketGem(player, "weapon", 0);
        Assert.Equal("good", kind1);
        Assert.Contains("Đã tháo", msg1);
        Assert.Equal(1, GemData.GetCount(player, "stone_viem_so"));
        Assert.Equal(0, GemData.GetCount(player, "stone_bang_so")); // stone_bang_so still socketed

        // Check slot state: slot 0 is empty, slot 1 still has stone_bang_so
        var slots = GemData.GetSlots(player);
        Assert.Equal("", slots["weapon"].Sockets[0]);
        Assert.Equal("stone_bang_so", slots["weapon"].Sockets[1]);

        // Unsocketing slot 0 again throws GameException because it's now empty
        Assert.Throws<GameException>(() => service.UnsocketGem(player, "weapon", 0));

        // Unsocket slot 1
        var (msg2, kind2) = service.UnsocketGem(player, "weapon", 1);
        Assert.Equal("good", kind2);
        Assert.Equal(1, GemData.GetCount(player, "stone_bang_so"));
        slots = GemData.GetSlots(player);
        Assert.Equal("", slots["weapon"].Sockets[1]);
    }

    [Fact]
    public void TestGemData_EvaluateFusion_ExactRecipes()
    {
        // 1. 3 identical stone_*_so -> stone_*_cao
        var evalViem = GemData.EvaluateFusion(new() { ["stone_viem_so"] = 3 });
        Assert.True(evalViem.IsValid);
        Assert.Equal("fuse_stone_viem", evalViem.RecipeId);
        Assert.Equal("stone_viem_cao", evalViem.ResultItem);
        Assert.Equal(0.85, evalViem.Chance);
        Assert.Equal(200, evalViem.Stones);

        // 2. 5 different stone_*_so -> stone_socket
        var evalSocket = GemData.EvaluateFusion(new()
        {
            ["stone_viem_so"] = 1,
            ["stone_bang_so"] = 1,
            ["stone_loi_so"] = 1,
            ["stone_kim_so"] = 1,
            ["stone_tho_so"] = 1
        });
        Assert.True(evalSocket.IsValid);
        Assert.Equal("fuse_stone_socket", evalSocket.RecipeId);
        Assert.Equal("stone_socket", evalSocket.ResultItem);
        Assert.Equal(1.0, evalSocket.Chance);
        Assert.Equal(1000, evalSocket.Stones);

        // 3. 1 viem_cao + 1 loi_cao -> stone_liet_loi
        var evalLietLoi = GemData.EvaluateFusion(new() { ["stone_viem_cao"] = 1, ["stone_loi_cao"] = 1 });
        Assert.True(evalLietLoi.IsValid);
        Assert.Equal("fuse_stone_liet_loi", evalLietLoi.RecipeId);
        Assert.Equal("stone_liet_loi", evalLietLoi.ResultItem);
        Assert.Equal(0.75, evalLietLoi.Chance);
        Assert.Equal(500, evalLietLoi.Stones);

        // 4. 3 identical gem_*_cui -> gem_*_xin
        var evalRuby = GemData.EvaluateFusion(new() { ["gem_ruby_cui"] = 3 });
        Assert.True(evalRuby.IsValid);
        Assert.Equal("fuse_gem_ruby", evalRuby.RecipeId);
        Assert.Equal("gem_ruby_xin", evalRuby.ResultItem);
        Assert.Equal(0.75, evalRuby.Chance);
        Assert.Equal(400, evalRuby.Stones);

        // 5. 1 gem_ruby_cui + 3 stone_viem_so -> gem_ruby_xin
        var evalAscendRuby = GemData.EvaluateFusion(new() { ["gem_ruby_cui"] = 1, ["stone_viem_so"] = 3 });
        Assert.True(evalAscendRuby.IsValid);
        Assert.Equal("ascend_ruby", evalAscendRuby.RecipeId);
        Assert.Equal("gem_ruby_xin", evalAscendRuby.ResultItem);
        Assert.Equal(0.75, evalAscendRuby.Chance);
        Assert.Equal(1000, evalAscendRuby.Stones);

        // 6. 10 herbs -> gem_ruby_cui
        var evalHerb = GemData.EvaluateFusion(new() { ["huyet_can_thao"] = 10 });
        Assert.True(evalHerb.IsValid);
        Assert.Equal("craft_gem_ruby", evalHerb.RecipeId);
        Assert.Equal("gem_ruby_cui", evalHerb.ResultItem);
        Assert.Equal(0.85, evalHerb.Chance);
        Assert.Equal(500, evalHerb.Stones);
    }

    [Fact]
    public void TestGemData_EvaluateFusion_FreeCombinations()
    {
        // 1. Any 3 stone_*_so (mixed) -> pool of corresponding cao stones
        var evalMixedSo = GemData.EvaluateFusion(new() { ["stone_viem_so"] = 2, ["stone_loi_so"] = 1 });
        Assert.True(evalMixedSo.IsValid);
        Assert.Equal("free_so_stones", evalMixedSo.RecipeId);
        Assert.Equal(0.75, evalMixedSo.Chance);
        Assert.Equal(250, evalMixedSo.Stones);
        Assert.Contains("stone_viem_cao", evalMixedSo.PossibleResults);
        Assert.Contains("stone_loi_cao", evalMixedSo.PossibleResults);

        // 2. Any 2 stone_*_cao -> pool of divine stones
        var evalCao = GemData.EvaluateFusion(new() { ["stone_viem_cao"] = 1, ["stone_tho_cao"] = 1 });
        Assert.True(evalCao.IsValid);
        Assert.Equal("free_cao_stones", evalCao.RecipeId);
        Assert.Equal(0.70, evalCao.Chance);
        Assert.Equal(600, evalCao.Stones);
        Assert.Contains("stone_liet_loi", evalCao.PossibleResults);
        Assert.Contains("stone_huyen_bang", evalCao.PossibleResults);
        Assert.Contains("stone_am_duong", evalCao.PossibleResults);

        // 3. Any 3 gem_*_cui (mixed colors) -> pool of corresponding xin gems
        var evalMixedGems = GemData.EvaluateFusion(new()
        {
            ["gem_ruby_cui"] = 1,
            ["gem_sapphire_cui"] = 1,
            ["gem_topaz_cui"] = 1
        });
        Assert.True(evalMixedGems.IsValid);
        Assert.Equal("free_cui_gems", evalMixedGems.RecipeId);
        Assert.Equal(0.68, evalMixedGems.Chance);
        Assert.Equal(450, evalMixedGems.Stones);
        Assert.Contains("gem_ruby_xin", evalMixedGems.PossibleResults);
        Assert.Contains("gem_sapphire_xin", evalMixedGems.PossibleResults);
        Assert.Contains("gem_topaz_xin", evalMixedGems.PossibleResults);

        // 4. Any 1 gem_*_cui + Any 2 stone_*_so -> gem_*_xin (Step 7b mixed high)
        var evalCuiSo = GemData.EvaluateFusion(new() { ["gem_ruby_cui"] = 1, ["stone_bang_so"] = 2 });
        Assert.True(evalCuiSo.IsValid);
        Assert.Equal("mixed_high", evalCuiSo.RecipeId);
        Assert.Equal("gem_ruby_xin", evalCuiSo.ResultItem);
        Assert.Equal(0.78, evalCuiSo.Chance);
        Assert.Equal(900, evalCuiSo.Stones);

        // 5. Any 1 gem_*_xin + Any 1 Divine Stone -> gem_thien_phu (Step 6 pure high tier)
        var evalXinDivine = GemData.EvaluateFusion(new() { ["gem_sapphire_xin"] = 1, ["stone_am_duong"] = 1 });
        Assert.True(evalXinDivine.IsValid);
        Assert.Equal("free_high_tier", evalXinDivine.RecipeId);
        Assert.Equal("gem_thien_phu", evalXinDivine.ResultItem);
        Assert.Equal(0.68, evalXinDivine.Chance);
        Assert.Equal(3000, evalXinDivine.Stones);

        // 6. Any 1 gem_*_xin + Any 2 stone_*_cao -> gem_thien_phu (Step 7a mixed to thien phu)
        var evalXinCao = GemData.EvaluateFusion(new() { ["gem_emerald_xin"] = 1, ["stone_bang_cao"] = 1, ["stone_tho_cao"] = 1 });
        Assert.True(evalXinCao.IsValid);
        Assert.Equal("mixed_to_thien_phu", evalXinCao.RecipeId);
        Assert.Equal("gem_thien_phu", evalXinCao.ResultItem);
        Assert.Equal(0.73, evalXinCao.Chance);
        Assert.Equal(2500, evalXinCao.Stones);

        // 7. Any 2 Divine Stones -> gem_thien_phu (Step 6 pure high tier)
        var evalTwoDivine = GemData.EvaluateFusion(new() { ["stone_liet_loi"] = 1, ["stone_huyen_bang"] = 1 });
        Assert.True(evalTwoDivine.IsValid);
        Assert.Equal("free_high_tier", evalTwoDivine.RecipeId);
        Assert.Equal("gem_thien_phu", evalTwoDivine.ResultItem);
        Assert.Equal(0.68, evalTwoDivine.Chance);
        Assert.Equal(3000, evalTwoDivine.Stones);
    }

    [Fact]
    public void TestGemData_EvaluateFusion_HintsAndEdgeCases()
    {
        // 1. Empty input
        var evalEmpty = GemData.EvaluateFusion(new());
        Assert.False(evalEmpty.IsValid);
        Assert.Contains("Chọn nguyên liệu", evalEmpty.Desc);

        // 2. 1 stone_*_so -> needs more
        var eval1So = GemData.EvaluateFusion(new() { ["stone_viem_so"] = 1 });
        Assert.False(eval1So.IsValid);
        Assert.Contains("Cần thêm ít nhất 1 Cổ Phù Sơ", eval1So.Desc);

        // 3. 2 stone_*_so -> valid! (Step 3: 2 slots -> 1 CaoStone, 60%, 150 LT)
        var eval2So = GemData.EvaluateFusion(new() { ["stone_viem_so"] = 2 });
        Assert.True(eval2So.IsValid);
        Assert.Equal("stone_viem_cao", eval2So.ResultItem);
        Assert.Equal(0.60, eval2So.Chance);
        Assert.Equal(150, eval2So.Stones);

        // 4. 4 different stone_*_so -> valid! (Step 3: 4 slots -> 1 CaoStone, 90%, 300 LT)
        var eval4So = GemData.EvaluateFusion(new()
        {
            ["stone_viem_so"] = 1,
            ["stone_bang_so"] = 1,
            ["stone_loi_so"] = 1,
            ["stone_kim_so"] = 1
        });
        Assert.True(eval4So.IsValid);
        Assert.Equal(0.90, eval4So.Chance);
        Assert.Equal(300, eval4So.Stones);

        // 5. 1 stone_*_cao -> needs more
        var eval1Cao = GemData.EvaluateFusion(new() { ["stone_viem_cao"] = 1 });
        Assert.False(eval1Cao.IsValid);
        Assert.Contains("Cần thêm ít nhất 1 Cổ Phù Cao", eval1Cao.Desc);

        // 6. 1 gem_*_cui -> needs more
        var eval1Cui = GemData.EvaluateFusion(new() { ["gem_ruby_cui"] = 1 });
        Assert.False(eval1Cui.IsValid);
        Assert.Contains("Cần thêm ít nhất 1 Tiên Châu Thô", eval1Cui.Desc);

        // 7. 1 gem_*_xin -> needs more
        var eval1Xin = GemData.EvaluateFusion(new() { ["gem_ruby_xin"] = 1 });
        Assert.False(eval1Xin.IsValid);
        Assert.Contains("Cần thêm ít nhất 1 nguyên liệu", eval1Xin.Desc);

        // 8. 1 divine stone -> needs more
        var eval1Divine = GemData.EvaluateFusion(new() { ["stone_liet_loi"] = 1 });
        Assert.False(eval1Divine.IsValid);
        Assert.Contains("Cần thêm ít nhất 1 nguyên liệu", eval1Divine.Desc);

        // 9. Over capacity (>5 slots)
        var evalOver = GemData.EvaluateFusion(new() { ["stone_viem_so"] = 6 });
        Assert.False(evalOver.IsValid);
        Assert.Contains("tối đa 5 ô", evalOver.Desc);

        // 10. Incomplete herb slot (<10 herbs)
        var evalHerbIncomplete = GemData.EvaluateFusion(new() { ["tu_van_sam"] = 5 });
        Assert.False(evalHerbIncomplete.IsValid);
        Assert.Contains("tối thiểu 10", evalHerbIncomplete.Desc);
    }

    [Fact]
    public void TestGemData_UniversalFusion_FiveIdenticalItems()
    {
        // 1. 5 slots of identical herbs (Tử Vân Sâm ×50 = 5 slots): User's screenshot scenario!
        var eval5TuVanSam = GemData.EvaluateFusion(new() { ["tu_van_sam"] = 50 });
        Assert.True(eval5TuVanSam.IsValid);
        Assert.Equal("herb_xin_5", eval5TuVanSam.RecipeId);
        Assert.Equal("gem_amethyst_xin", eval5TuVanSam.ResultItem);
        Assert.Equal(1, eval5TuVanSam.ResultCount);
        Assert.Equal(0.85, eval5TuVanSam.Chance);
        Assert.Equal(1000, eval5TuVanSam.Stones);

        // 2. 5 identical SoStones -> 2 CaoStones!
        var eval5ViemSo = GemData.EvaluateFusion(new() { ["stone_viem_so"] = 5 });
        Assert.True(eval5ViemSo.IsValid);
        Assert.Equal("stone_viem_cao", eval5ViemSo.ResultItem);
        Assert.Equal(2, eval5ViemSo.ResultCount);
        Assert.Equal(0.90, eval5ViemSo.Chance);
        Assert.Equal(400, eval5ViemSo.Stones);

        // 3. 5 identical CuiGems -> 2 XinGems!
        var eval5RubyCui = GemData.EvaluateFusion(new() { ["gem_ruby_cui"] = 5 });
        Assert.True(eval5RubyCui.IsValid);
        Assert.Equal("gem_ruby_xin", eval5RubyCui.ResultItem);
        Assert.Equal(2, eval5RubyCui.ResultCount);
        Assert.Equal(0.90, eval5RubyCui.Chance);
        Assert.Equal(650, eval5RubyCui.Stones);

        // 4. 5 identical CaoStones -> 1 gem_thien_phu!
        var eval5BangCao = GemData.EvaluateFusion(new() { ["stone_bang_cao"] = 5 });
        Assert.True(eval5BangCao.IsValid);
        Assert.Equal("gem_thien_phu", eval5BangCao.ResultItem);
        Assert.Equal(1, eval5BangCao.ResultCount);
        Assert.Equal(0.80, eval5BangCao.Chance);
        Assert.Equal(2000, eval5BangCao.Stones);

        // 5. 5 identical XinGems -> 2 gem_thien_phu!
        var eval5TopazXin = GemData.EvaluateFusion(new() { ["gem_topaz_xin"] = 5 });
        Assert.True(eval5TopazXin.IsValid);
        Assert.Equal("gem_thien_phu", eval5TopazXin.ResultItem);
        Assert.Equal(2, eval5TopazXin.ResultCount);
        Assert.Equal(0.92, eval5TopazXin.Chance);
        Assert.Equal(5000, eval5TopazXin.Stones);

        // 6. 3 slots of herbs (30 herbs) -> 2 CuiGems!
        var eval3Herbs = GemData.EvaluateFusion(new() { ["tu_van_sam"] = 30 });
        Assert.True(eval3Herbs.IsValid);
        Assert.Equal("gem_amethyst_cui", eval3Herbs.ResultItem);
        Assert.Equal(2, eval3Herbs.ResultCount);
        Assert.Equal(0.85, eval3Herbs.Chance);
        Assert.Equal(750, eval3Herbs.Stones);

        // 7. 4 slots of herbs (40 herbs) -> 1 XinGem!
        var eval4Herbs = GemData.EvaluateFusion(new() { ["tu_van_sam"] = 40 });
        Assert.True(eval4Herbs.IsValid);
        Assert.Equal("gem_amethyst_xin", eval4Herbs.ResultItem);
        Assert.Equal(1, eval4Herbs.ResultCount);
        Assert.Equal(0.75, eval4Herbs.Chance);
        Assert.Equal(900, eval4Herbs.Stones);
    }

    [Fact]
    public void TestGemData_UniversalFusion_MixedCategoriesAndTiers()
    {
        // 1. 1 stone_viem_so + 1 gem_ruby_cui -> Step 7c (Low-tier mix)
        var evalLowMix = GemData.EvaluateFusion(new() { ["stone_viem_so"] = 1, ["gem_ruby_cui"] = 1 });
        Assert.True(evalLowMix.IsValid);
        Assert.Equal("mixed_low", evalLowMix.RecipeId);
        Assert.Equal(0.75, evalLowMix.Chance);
        Assert.Equal(350, evalLowMix.Stones);

        // 2. 2 herbs + 2 stones + 1 gem -> Step 7b (Mid/High mix, 5 slots)
        var eval5Mix = GemData.EvaluateFusion(new()
        {
            ["tu_van_sam"] = 20, // 2 slots
            ["stone_viem_so"] = 2, // 2 slots
            ["gem_ruby_cui"] = 1 // 1 slot
        });
        Assert.True(eval5Mix.IsValid);
        Assert.Equal("mixed_high", eval5Mix.RecipeId);
        Assert.Equal(0.90, eval5Mix.Chance);
        Assert.Equal(1100, eval5Mix.Stones);

        // 3. 1 CaoStone + 1 SoStone -> Step 7b (score = 6 + 2 = 8 >= 6)
        var evalCaoSo = GemData.EvaluateFusion(new() { ["stone_viem_cao"] = 1, ["stone_bang_so"] = 1 });
        Assert.True(evalCaoSo.IsValid);
        Assert.Equal("mixed_high", evalCaoSo.RecipeId);
        Assert.Equal(0.72, evalCaoSo.Chance);
        Assert.Equal(800, evalCaoSo.Stones);
    }

    [Fact]
    public void TestGameService_CraftGemFromItems_SuccessAndFailure()
    {
        var service = new GameService(null!);
        var player = new Player
        {
            Inventory = new List<InventoryItem>(),
            GemSlots = "{}",
            GemVault = "{}"
        };

        // 1. Successful free combination: 2 stone_viem_so + 1 stone_loi_so -> Cao stone (75% chance, 250 LT)
        GemData.Add(player, "stone_viem_so", 2);
        GemData.Add(player, "stone_loi_so", 1);
        player.Add("linh_thach", 1000);

        var succRng = new DeterministicRandom(0.10); // < 0.75 -> succeeds
        var (msgSucc, kindSucc, craftSucc) = service.CraftGemFromItems(player, "stone_viem_so,stone_viem_so,stone_loi_so", times: 1, random: succRng);

        Assert.Equal("gold", kindSucc);
        Assert.NotNull(craftSucc);
        Assert.Equal("gem:ok", craftSucc.Recipe);
        Assert.Equal(0, GemData.GetCount(player, "stone_viem_so"));
        Assert.Equal(0, GemData.GetCount(player, "stone_loi_so"));
        Assert.Equal(750, player.Count("linh_thach")); // 1000 - 250
        var gotCao = GemData.GetCount(player, "stone_viem_cao") + GemData.GetCount(player, "stone_loi_cao");
        Assert.Equal(1, gotCao);

        // 2. Failed free combination with partial refund: 1 gem_sapphire_xin + 1 stone_am_duong (68% chance, 3000 LT)
        GemData.Add(player, "gem_sapphire_xin", 1);
        GemData.Add(player, "stone_am_duong", 1);
        player.Add("linh_thach", 10000);

        var failRng = new DeterministicRandom(0.99); // > 0.68 -> fails
        var (msgFail, kindFail, craftFail) = service.CraftGemFromItems(player, "gem_sapphire_xin,stone_am_duong", times: 1, random: failRng);

        Assert.Equal("bad", kindFail);
        Assert.NotNull(craftFail);
        Assert.Equal("gem:fail", craftFail.Recipe);
        Assert.Equal(7750, player.Count("linh_thach")); // 10750 - 3000
        Assert.Equal(0, GemData.GetCount(player, "gem_thien_phu"));
        // Preserves 1 item
        var totalReturned = GemData.GetCount(player, "gem_sapphire_xin") + GemData.GetCount(player, "stone_am_duong");
        Assert.Equal(1, totalReturned);

        // 3. Batch Crafting (times = 2): 2 divine stones -> gem_thien_phu (68% chance, 3000 LT per craft)
        GemData.Add(player, "stone_liet_loi", 2);
        GemData.Add(player, "stone_huyen_bang", 2);
        player.Add("linh_thach", 6000);

        var (msgBatch, kindBatch, craftBatch) = service.CraftGemFromItems(player, "stone_liet_loi,stone_huyen_bang", times: 2, random: succRng);
        Assert.Equal("gold", kindBatch);
        Assert.NotNull(craftBatch);
        Assert.Equal(2, GemData.GetCount(player, "gem_thien_phu"));
        Assert.Equal(0, GemData.GetCount(player, "stone_liet_loi"));
        Assert.Equal(0, GemData.GetCount(player, "stone_huyen_bang"));
        Assert.Equal(7750, player.Count("linh_thach")); // 7750 + 6000 - 6000 = 7750

        // 4. Insufficient linh thạch throws GameException
        GemData.Add(player, "stone_liet_loi", 2);
        player.Inventory.First(i => i.ItemId == "linh_thach").Count = 100; // < 3000
        Assert.Throws<GameException>(() => service.CraftGemFromItems(player, "stone_liet_loi,stone_liet_loi", times: 1));

        // 5. User's exact scenario: 5 slots of Tử Vân Sâm in furnace (50 tu_van_sam -> 1 gem_amethyst_xin)
        player.Add("tu_van_sam", 50);
        player.Add("linh_thach", 2000);
        var (msg5Herbs, kind5Herbs, craft5Herbs) = service.CraftGemFromItems(
            player,
            "tu_van_sam,tu_van_sam,tu_van_sam,tu_van_sam,tu_van_sam",
            times: 1,
            random: succRng
        );
        Assert.Equal("gold", kind5Herbs);
        Assert.NotNull(craft5Herbs);
        Assert.Equal(1, GemData.GetCount(player, "gem_amethyst_xin"));
        Assert.Equal(0, player.Count("tu_van_sam"));
        Assert.Equal(1100, player.Count("linh_thach")); // 100 + 2000 - 1000 = 1100

        // 6. User's requirement: 5 slots of identical SoStones -> 2 CaoStones!
        var prevViemCao = GemData.GetCount(player, "stone_viem_cao");
        GemData.Add(player, "stone_viem_so", 5);
        player.Add("linh_thach", 1000);
        var (msg5So, kind5So, craft5So) = service.CraftGemFromItems(
            player,
            "stone_viem_so,stone_viem_so,stone_viem_so,stone_viem_so,stone_viem_so",
            times: 1,
            random: succRng
        );
        Assert.Equal("gold", kind5So);
        Assert.NotNull(craft5So);
        Assert.Equal(2, GemData.GetCount(player, "stone_viem_cao") - prevViemCao); // got 2 new ones
        Assert.Equal(0, GemData.GetCount(player, "stone_viem_so"));
        Assert.Equal(1700, player.Count("linh_thach")); // 1100 + 1000 - 400 = 1700
        Assert.Contains("Dung hợp thành công 2 Viêm Cổ Phù · Cao", msg5So);

        // 7. 3 slots of herbs -> 2 CuiGems!
        player.Add("tu_van_sam", 30);
        player.Add("linh_thach", 1000);
        var (msg3Herbs, kind3Herbs, craft3Herbs) = service.CraftGemFromItems(
            player,
            "tu_van_sam,tu_van_sam,tu_van_sam",
            times: 1,
            random: succRng
        );
        Assert.Equal("gold", kind3Herbs);
        Assert.NotNull(craft3Herbs);
        Assert.Equal(2, GemData.GetCount(player, "gem_amethyst_cui"));
        Assert.Contains("Dung hợp thành công 2 Tử Lôi Tiên Châu · Thô", msg3Herbs);

        // 8. Reject invalid incomplete herb slot (<10 herbs via custom string)
        Assert.Throws<GameException>(() => service.CraftGemFromItems(player, "tu_van_sam:5", times: 1));
    }

    [Fact]
    public void TestVaultUi_TierClassification_AccumulatedStats_And_CssAlignment()
    {
        // 1. Verify all 25 GemData.Items partition cleanly into Sơ Cấp (10), Cao Cấp (11), Đỉnh Cấp (4)
        static string GetTier(string id)
        {
            if (id == "gem_thien_phu" || id == "stone_liet_loi" || id == "stone_huyen_bang" || id == "stone_am_duong")
                return "dinh";
            if (id == "stone_socket" || id.EndsWith("_cao") || id.EndsWith("_xin"))
                return "cao";
            return "so";
        }

        var tiers = GemData.Items.GroupBy(i => GetTier(i.Id)).ToDictionary(g => g.Key, g => g.ToList());
        Assert.Equal(10, tiers["so"].Count);   // 5 stone_*_so + 5 gem_*_cui
        Assert.Equal(11, tiers["cao"].Count);  // 5 stone_*_cao + 1 stone_socket + 5 gem_*_xin
        Assert.Equal(4, tiers["dinh"].Count);  // 3 divine stones + 1 gem_thien_phu

        // 2. Verify JS/CSS files in wwwroot
        var rootDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var vaultJs = System.IO.File.ReadAllText(System.IO.Path.Combine(rootDir, "wwwroot", "js", "vault.js"));
        var invJs = System.IO.File.ReadAllText(System.IO.Path.Combine(rootDir, "wwwroot", "js", "inventory.js"));
        var vaultCss = System.IO.File.ReadAllText(System.IO.Path.Combine(rootDir, "wwwroot", "css", "vault.css"));

        // Tab Vị Trí Chú Linh & Inventory tooltips: accumulated stats + special effects in top card & removal of bottom total row
        Assert.Contains("getCombinedItemStatsHtml", vaultJs);
        Assert.Contains("formatGemEffectText", vaultJs);
        Assert.Contains("mergeItemGemEffects", invJs);
        Assert.Contains("formatItemGemEffectText", invJs);
        Assert.DoesNotContain("socket-summary-totals", vaultJs);
        Assert.DoesNotContain("Tổng thuộc tính:", vaultJs);
        Assert.DoesNotContain("iv-gem-total", invJs);
        Assert.DoesNotContain("Tổng tăng thêm", invJs);

        // Tab Kho: 3 tier sub-tabs and quick fuse button synced with Hợp Thành style
        Assert.Contains("data-vt-tier=\"so\"", vaultJs);
        Assert.Contains("data-vt-tier=\"cao\"", vaultJs);
        Assert.Contains("data-vt-tier=\"dinh\"", vaultJs);
        Assert.Contains("btn-quick-fuse dd-synth-btn fz-go", vaultJs);

        // Typography & Lò Hợp alignment in vault.css
        Assert.DoesNotContain("--f-display", vaultCss);
        Assert.Contains(".vault-tier-tab", vaultCss);
        Assert.Contains("max-width: 500px;", vaultCss);
        Assert.Contains("height: 38px;", vaultCss);
    }
}