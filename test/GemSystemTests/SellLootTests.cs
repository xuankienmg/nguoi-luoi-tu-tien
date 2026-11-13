using VanDao.Game;
using VanDao.Data;

namespace GemSystemTests;

public class SellLootTests
{
    [Fact]
    public void EveryWinDropsExactlyOneItemBesidesSpiritStonesKeepingCategoryRatios()
    {
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        for (var ch = 0; ch < GameData.OpenChapters; ch++)
        for (var area = 1; area <= GameData.AreasFor(diff); area++)
        {
            var chapter = GameData.ChapterFor(diff, ch);
            var groups = PetLoot.StageGroups(chapter, diff, area);
            // Tinh Thiết (Lò Rèn, 11/2026) là lượt gieo riêng ngoài "1 món mỗi trận"
            var group = Assert.Single(groups.Where(g => g.Choices.All(c => c.Item is not ("linh_thach" or ForgeData.Iron))));
            Assert.All(groups.Where(g => g != group), g => Assert.Contains(Assert.Single(g.Choices).Item, new[] { "linh_thach", ForgeData.Iron }));
            Assert.Equal(1, group.Chance);
            Assert.Equal(1, group.Choices.Sum(c => c.Weight), 10);
            double W(Func<StageLootChoice, bool> f) => group.Choices.Where(f).Sum(c => c.Weight);
            var low = W(c => c.Item == "sell_loot_low");
            // Hỏa Tâm Hoa (linh thảo) được nâng lên đúng 5% bằng phần lấy từ Quặng vụn: tổng linh thảo + Quặng vụn giữ tỉ lệ gốc với Quặng thường
            var medium = W(c => c.Item == "sell_loot_medium");
            var herbs = W(c => GameData.ItemById.TryGetValue(c.Item, out var it) && it.Type == "herb");
            Assert.Equal((.30 + .40) / .18, (herbs + low) / medium, 10);
            if (group.Choices.Any(c => c.Item == "hoa_tam_hoa")) Assert.Equal(.05, W(c => c.Item == "hoa_tam_hoa"), 10);
            // Bảo vật vàng ngọc ≤ 0,1%, Khiêu Chiến Lệnh ≤ 10%; phần bớt ra dồn cho Tinh Thạch nên Tinh Thạch ≥ tỉ lệ gốc (10/40 quặng vụn)
            Assert.InRange(W(c => c.Item == "sell_loot_high"), 0, PetLoot.MaxHighSellLoot + 1e-12);
            Assert.InRange(W(c => c.Item == GameData.MapKey(diff)), 0, PetLoot.MaxKeyDrop + 1e-12);
            Assert.True(W(c => GemData.IsGem(c.Item)) / low >= .10 / .40 - 1e-10);
            var odds = PetLoot.StageOdds(chapter, diff, area);
            Assert.Equal(1, odds.Where(d => d.Item is not ("linh_thach" or ForgeData.Iron)).Sum(d => d.Chance), 10);
            Assert.Equal(ForgeData.StageIronChance, odds.Single(d => d.Item == ForgeData.Iron).Chance, 10);
            Assert.All(group.Choices.Where(c => c.Item.StartsWith("sell_loot_")), c => {
                Assert.Equal(1, PetLoot.DropQuantity(c.Item, c.Min, diff, ch));
                Assert.Equal(1, PetLoot.DropQuantity(c.Item, c.Max, diff, ch));
            });
        }
    }

    [Theory]
    [InlineData("sell_loot_low", 100, "common")]
    [InlineData("sell_loot_medium", 1000, "mythic")]
    [InlineData("sell_loot_high", 100000, "supreme")]
    public void SellOnlyItemsHaveFixedPricesAndNoUsableOrEquipmentEffects(string id, int price, string rarity)
    {
        Assert.True(GameData.ItemById.TryGetValue(id, out var item));
        Assert.Equal("salvage", item!.Type);
        Assert.Equal(price, item.Sell);
        Assert.Equal(rarity, item.Rarity);
        Assert.Null(item.Slot);
        Assert.Equal(0, item.Tier);
        Assert.Equal(0, item.UseQiSeconds);
        Assert.Equal($"img/items/{id}.png", item.Icon);
        var player = new Player();
        player.Add(id, 5);
        var game = new GameService(null!);
        game.Sell(player, id, 3);
        Assert.Equal(2m, player.Count(id));
        Assert.Equal(price * 3m, player.Count("linh_thach"));
        game.SellBulk(player, 9, items: $"{id}:2", pick: true);
        Assert.Equal(0m, player.Count(id));
        Assert.Equal(price * 5m, player.Count("linh_thach"));
    }

    [Fact]
    public void BulkSellByRarityNeverCountsSpiritStones()
    {
        var player = new Player();
        player.Add("linh_thach", 1000);
        player.Add("thuong_pham_linh_thach", 3);
        player.Add("sell_loot_low", 2);
        var game = new GameService(null!);
        var (msg, _) = game.SellBulk(player, 9);
        Assert.Contains("Bán 2 món", msg);
        Assert.Equal(1000m + 200m, player.Count("linh_thach"));
        Assert.Equal(3m, player.Count("thuong_pham_linh_thach"));
        Assert.Throws<GameException>(() => game.SellBulk(player, 9));
    }

    [Fact]
    public void SpiritStonesDoNotUseABagSlot()
    {
        var player = new Player();
        player.Add("linh_thach", 1000);
        player.Add("thuong_pham_linh_thach", 3);
        Assert.Equal(0, GameService.BagUsed(player));
        player.Add("sell_loot_low", 2);
        Assert.Equal(1, GameService.BagUsed(player));
    }

    private sealed class FixedRandom(double value) : Random
    {
        public override double NextDouble() => value;
    }

    [Fact]
    public void ActualChoiceFollowsTheExclusiveIntervals()
    {
        var group = PetLoot.StageGroups(GameData.Chapters[0], 0, 1).Single(g => g.Choices.Any(c => c.Item == "sell_loot_low"));
        var start = 0d;
        foreach (var choice in group.Choices)
        {
            Assert.Equal(choice, PetLoot.Choose(group, new FixedRandom(start + choice.Weight / 2)));
            start += choice.Weight;
        }
    }
}
