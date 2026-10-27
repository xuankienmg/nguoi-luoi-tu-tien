using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;
using Xunit;
namespace GemSystemTests;
public class GemSystemVerificationTests
{
    private class FixedRandom(double chance, int pick = 0) : Random
    {
        public override double NextDouble() => chance;
        public override int Next(int maxValue) => Math.Min(pick, maxValue - 1);
    }

    [Fact]
    public void MaterialsRetainLegacyIdsAndGradesAfterRebalancing()
    {
        Assert.Equal(124, GemData.Items.Count);
        Assert.Equal(124, GemData.Items.Count(i => i.Type == "gem"));
        Assert.Equal(2, GemData.ById["stone_viem_so"].GemLevel);
        Assert.Equal(6, GemData.ById["gem_ruby_xin"].GemLevel);
        Assert.False(GemData.ById.ContainsKey("stone_socket"));
        Assert.Equal(-1, GemData.GemLevel("stone_socket"));
    }
    [Fact]
    public void NineSameGradeDifferentTypesUpgradeOrReturnOneCurrentGradeGem()
    {
        foreach (var success in new[] { true, false })
        {
            var player = new Player();
            player.Add("stone_viem_so", 4); player.Add("stone_bang_so", 5);
            var result = new GameService(null!).CraftGemFromItems(player, "stone_viem_so:4,stone_bang_so:5", random: new FixedRandom(success ? 0 : .99, 2));
            Assert.Equal(success ? "good" : "bad", result.kind);
            Assert.NotNull(result.craft);
            Assert.Equal(1, result.craft.Output.Qty);
            Assert.Equal(success ? 2 : 1, GemData.GemLevel(result.craft.Output.Item));
            Assert.Equal(1, GemData.GetVault(player).Values.Sum());
            Assert.Equal(9, result.craft.Inputs.Sum(i => i.Qty));
        }
    }
    [Fact]
    public void UpgradeChanceDecreasesAndRejectsInvalidCombinations()
    {
        var chances = Enumerable.Range(0, 9).Select(level => GemData.EvaluateFusion(new() { [GemData.GemsAtLevel(level)[0]] = 9 }).Chance).ToList();
        Assert.True(chances.Zip(chances.Skip(1)).All(pair => pair.First > pair.Second));
        Assert.False(GemData.EvaluateFusion(new() { ["stone_viem_so"] = 8 }).IsValid);
        Assert.False(GemData.EvaluateFusion(new() { ["stone_viem_so"] = 10 }).IsValid);
        Assert.False(GemData.EvaluateFusion(new() { ["stone_viem_so"] = 4, ["gem_ruby_cui"] = 5 }).IsValid);
        Assert.False(GemData.EvaluateFusion(new() { [GemProgression.Id("vu_tru", 9)] = 9 }).IsValid);
        Assert.False(GemData.EvaluateFusion(new() { ["stone_socket"] = 9 }).IsValid);
    }
    [Theory]
    [InlineData("stone_viem_so:2147483647")]
    [InlineData("stone_viem_so:-9")]
    [InlineData("stone_viem_so:no")]
    [InlineData("stone_viem_so:9:9")]
    public void InvalidQuantitiesDoNotConsumeAnything(string input)
    {
        var player = new Player(); player.Add("stone_viem_so", 9);
        Assert.Throws<GameException>(() => new GameService(null!).CraftGemFromItems(player, input));
        Assert.Equal(9, player.Count("stone_viem_so"));
    }
    [Fact]
    public void QuickFusionUsesNineAndDoesNotCascadeNewOutputs()
    {
        var player = new Player(); player.Add("stone_viem_so", 18); player.Add("gem_ruby_cui", 8);
        var result = new GameService(null!).QuickFuseGems(player);
        Assert.NotNull(result.craft);
        Assert.Equal(18, result.craft.Inputs.Sum(i => i.Qty));
        Assert.Equal(2, result.craft.Outputs!.Sum(i => i.Qty));
        Assert.Equal(10, GemData.GetVault(player).Values.Sum());
        Assert.Equal(8, player.Count("gem_ruby_cui") - (result.craft.Outputs!.Where(i => i.Item == "gem_ruby_cui").Sum(i => i.Qty)));
    }
    [Fact]
    public void SocketedGemCanBeRemovedExactlyOnceAndItsBonusesDisappear()
    {
        var player = new Player();
        player.Equipment.Add(new EquippedItem { Slot = "weapon", ItemId = GameData.AllItems.First(i => i.Slot == "weapon" && i.Rarity == "common").Id });
        player.Add("stone_viem_so", 1); player.Add("gem_ruby_cui", 1);
        var service = new GameService(null!);
        service.SocketGem(player, "weapon", 0, "stone_viem_so");
        Assert.Throws<GameException>(() => service.SocketGem(player, "weapon", 0, "gem_ruby_cui"));
        Assert.Equal(1, player.Count("gem_ruby_cui"));
        Assert.Equal("stone_viem_so", GemData.GetSocketedItems(player, "weapon")[0]);
        var cost = GemData.UnsocketCost("stone_viem_so");
        player.Add("linh_thach", cost - 1);
        Assert.Throws<GameException>(() => service.UnsocketGem(player, "weapon", 0));
        Assert.Equal(cost - 1, player.Count("linh_thach"));
        Assert.Equal("stone_viem_so", GemData.GetSocketedItems(player, "weapon")[0]);
        Assert.Equal(0, player.Count("stone_viem_so"));
        player.Add("linh_thach", 1);
        service.UnsocketGem(player, "weapon", 0);
        Assert.Equal(0, player.Count("linh_thach"));
        Assert.Equal(1, player.Count("stone_viem_so"));
        Assert.True(string.IsNullOrEmpty(GemData.GetSocketedItems(player, "weapon")[0]));
        Assert.Equal(0, GemData.GetSocketedBonuses(player).Atk);
        Assert.Throws<GameException>(() => service.UnsocketGem(player, "weapon", 0));
        Assert.Throws<GameException>(() => service.UnsocketGem(player, "weapon", -1));
        Assert.Throws<GameException>(() => service.UnsocketGem(player, "invalid", 0));
        Assert.Equal(1, player.Count("stone_viem_so"));
    }
    [Fact]
    public void LegacyVaultMigrationIsIdempotent()
    {
        var player = new Player();
        player.Inventory.Add(new InventoryItem { ItemId = "stone_viem_so", Count = 9 });
        GameService.ConvertLegacy(player); GameService.ConvertLegacy(player);
        Assert.Equal(9, player.Count("stone_viem_so"));
        Assert.Equal(0, player.Inventory.Single(i => i.ItemId == "stone_viem_so").Count);
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

            Assert.All(GemData.BossBonusDrops(0), d => {
                Assert.InRange(GemData.GemLevel(d.Item), 0, 2);
                Assert.Equal(1, d.Max);
            });
        }
    }
}
