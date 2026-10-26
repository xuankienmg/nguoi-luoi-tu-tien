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
        Assert.Equal(1.0, evalHerb.Chance);
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
        Assert.Equal(0.65, evalMixedGems.Chance);
        Assert.Equal(450, evalMixedGems.Stones);
        Assert.Contains("gem_ruby_xin", evalMixedGems.PossibleResults);
        Assert.Contains("gem_sapphire_xin", evalMixedGems.PossibleResults);
        Assert.Contains("gem_topaz_xin", evalMixedGems.PossibleResults);

        // 4. Any 1 gem_*_cui + Any 2 stone_*_so -> gem_*_xin
        var evalCuiSo = GemData.EvaluateFusion(new() { ["gem_ruby_cui"] = 1, ["stone_bang_so"] = 2 });
        Assert.True(evalCuiSo.IsValid);
        Assert.Equal("free_ascend_gem", evalCuiSo.RecipeId);
        Assert.Equal("gem_ruby_xin", evalCuiSo.ResultItem);
        Assert.Equal(0.75, evalCuiSo.Chance);
        Assert.Equal(800, evalCuiSo.Stones);

        // 5. Any 1 gem_*_xin + Any 1 Divine Stone -> gem_thien_phu (Phẩm Đỏ)
        var evalXinDivine = GemData.EvaluateFusion(new() { ["gem_sapphire_xin"] = 1, ["stone_am_duong"] = 1 });
        Assert.True(evalXinDivine.IsValid);
        Assert.Equal("free_ascend_thien_phu", evalXinDivine.RecipeId);
        Assert.Equal("gem_thien_phu", evalXinDivine.ResultItem);
        Assert.Equal(0.65, evalXinDivine.Chance);
        Assert.Equal(5000, evalXinDivine.Stones);

        // 6. Any 1 gem_*_xin + Any 2 stone_*_cao -> gem_thien_phu
        var evalXinCao = GemData.EvaluateFusion(new() { ["gem_emerald_xin"] = 1, ["stone_bang_cao"] = 1, ["stone_tho_cao"] = 1 });
        Assert.True(evalXinCao.IsValid);
        Assert.Equal("free_ascend_thien_phu", evalXinCao.RecipeId);
        Assert.Equal("gem_thien_phu", evalXinCao.ResultItem);
        Assert.Equal(0.65, evalXinCao.Chance);
        Assert.Equal(5000, evalXinCao.Stones);

        // 7. Any 2 Divine Stones -> gem_thien_phu
        var evalTwoDivine = GemData.EvaluateFusion(new() { ["stone_liet_loi"] = 1, ["stone_huyen_bang"] = 1 });
        Assert.True(evalTwoDivine.IsValid);
        Assert.Equal("free_divine_fusion", evalTwoDivine.RecipeId);
        Assert.Equal("gem_thien_phu", evalTwoDivine.ResultItem);
        Assert.Equal(0.70, evalTwoDivine.Chance);
        Assert.Equal(3000, evalTwoDivine.Stones);
    }

    [Fact]
    public void TestGemData_EvaluateFusion_HintsAndEdgeCases()
    {
        // 1. Empty input
        var evalEmpty = GemData.EvaluateFusion(new());
        Assert.False(evalEmpty.IsValid);
        Assert.Contains("Chọn nguyên liệu", evalEmpty.Desc);

        // 2. 1 stone_*_so -> needs 2 more
        var eval1So = GemData.EvaluateFusion(new() { ["stone_viem_so"] = 1 });
        Assert.False(eval1So.IsValid);
        Assert.Contains("Cần thêm 2 Cổ Phù Sơ", eval1So.Desc);

        // 3. 2 stone_*_so -> needs 1 more
        var eval2So = GemData.EvaluateFusion(new() { ["stone_viem_so"] = 2 });
        Assert.False(eval2So.IsValid);
        Assert.Contains("Cần thêm 1 Cổ Phù Sơ", eval2So.Desc);

        // 4. 4 different stone_*_so -> needs 1 more for socket
        var eval4So = GemData.EvaluateFusion(new()
        {
            ["stone_viem_so"] = 1,
            ["stone_bang_so"] = 1,
            ["stone_loi_so"] = 1,
            ["stone_kim_so"] = 1
        });
        Assert.False(eval4So.IsValid);
        Assert.Contains("Khai Khiếu", eval4So.Desc);

        // 5. 1 stone_*_cao -> needs 1 more
        var eval1Cao = GemData.EvaluateFusion(new() { ["stone_viem_cao"] = 1 });
        Assert.False(eval1Cao.IsValid);
        Assert.Contains("Cần thêm 1 Cổ Phù Cao", eval1Cao.Desc);

        // 6. 1 gem_*_cui -> needs 2 more
        var eval1Cui = GemData.EvaluateFusion(new() { ["gem_ruby_cui"] = 1 });
        Assert.False(eval1Cui.IsValid);
        Assert.Contains("Cần thêm 2 Tiên Châu Thô", eval1Cui.Desc);

        // 7. 1 gem_*_xin -> needs divine stone or 2 cao
        var eval1Xin = GemData.EvaluateFusion(new() { ["gem_ruby_xin"] = 1 });
        Assert.False(eval1Xin.IsValid);
        Assert.Contains("Biến Dị hoặc 2 Cổ Phù Cao", eval1Xin.Desc);

        // 8. 1 divine stone -> needs 1 more divine or 1 xin
        var eval1Divine = GemData.EvaluateFusion(new() { ["stone_liet_loi"] = 1 });
        Assert.False(eval1Divine.IsValid);
        Assert.Contains("luyện Cổ Phù Tiên Châu", eval1Divine.Desc);

        // 9. Invalid combinations
        var evalInvalid = GemData.EvaluateFusion(new() { ["stone_liet_loi"] = 1, ["stone_viem_so"] = 1 });
        Assert.False(evalInvalid.IsValid);
        Assert.Contains("chưa phù hợp", evalInvalid.Desc);
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

        // 2. Failed free combination with partial refund: 1 gem_sapphire_xin + 1 stone_am_duong (65% chance, 5000 LT)
        GemData.Add(player, "gem_sapphire_xin", 1);
        GemData.Add(player, "stone_am_duong", 1);
        player.Add("linh_thach", 10000);

        var failRng = new DeterministicRandom(0.99); // > 0.65 -> fails
        var (msgFail, kindFail, craftFail) = service.CraftGemFromItems(player, "gem_sapphire_xin,stone_am_duong", times: 1, random: failRng);

        Assert.Equal("bad", kindFail);
        Assert.NotNull(craftFail);
        Assert.Equal("gem:fail", craftFail.Recipe);
        Assert.Equal(5750, player.Count("linh_thach")); // 10750 - 5000
        Assert.Equal(0, GemData.GetCount(player, "gem_thien_phu"));
        // Preserves 1 item
        var totalReturned = GemData.GetCount(player, "gem_sapphire_xin") + GemData.GetCount(player, "stone_am_duong");
        Assert.Equal(1, totalReturned);

        // 3. Batch Crafting (times = 2): 2 divine stones -> gem_thien_phu (70% chance, 3000 LT per craft)
        GemData.Add(player, "stone_liet_loi", 2);
        GemData.Add(player, "stone_huyen_bang", 2);
        player.Add("linh_thach", 6000);

        var (msgBatch, kindBatch, craftBatch) = service.CraftGemFromItems(player, "stone_liet_loi,stone_huyen_bang", times: 2, random: succRng);
        Assert.Equal("gold", kindBatch);
        Assert.NotNull(craftBatch);
        Assert.Equal(2, GemData.GetCount(player, "gem_thien_phu"));
        Assert.Equal(0, GemData.GetCount(player, "stone_liet_loi"));
        Assert.Equal(0, GemData.GetCount(player, "stone_huyen_bang"));
        Assert.Equal(5750, player.Count("linh_thach")); // 5750 + 6000 - 6000 = 5750

        // 4. Insufficient linh thạch throws GameException
        GemData.Add(player, "stone_liet_loi", 2);
        player.Inventory.First(i => i.ItemId == "linh_thach").Count = 100; // < 3000
        Assert.Throws<GameException>(() => service.CraftGemFromItems(player, "stone_liet_loi,stone_liet_loi", times: 1));
    }
}