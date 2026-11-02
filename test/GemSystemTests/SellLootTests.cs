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
            var group = Assert.Single(groups.Where(g => g.Choices.All(c => c.Item != "linh_thach")));
            Assert.All(groups.Where(g => g != group), g => Assert.Equal("linh_thach", Assert.Single(g.Choices).Item));
            Assert.Equal(1, group.Chance);
            Assert.Equal(1, group.Choices.Sum(c => c.Weight), 10);
            double W(Func<StageLootChoice, bool> f) => group.Choices.Where(f).Sum(c => c.Weight);
            var low = W(c => c.Item == "sell_loot_low");
            Assert.Equal(.18 / .40, W(c => c.Item == "sell_loot_medium") / low, 10);
            Assert.Equal(.30 / .40, W(c => GameData.ItemById.TryGetValue(c.Item, out var it) && it.Type == "herb") / low, 10);
            // Bảo vật vàng ngọc ≤ 0,1%, Khiêu Chiến Lệnh ≤ 10%; phần bớt ra dồn cho Tinh Thạch nên Tinh Thạch ≥ tỉ lệ gốc (10/40 quặng vụn)
            Assert.InRange(W(c => c.Item == "sell_loot_high"), 0, PetLoot.MaxHighSellLoot + 1e-12);
            Assert.InRange(W(c => c.Item == GameData.MapKey(diff)), 0, PetLoot.MaxKeyDrop + 1e-12);
            Assert.True(W(c => GemData.IsGem(c.Item)) / low >= .10 / .40 - 1e-10);
            var odds = PetLoot.StageOdds(chapter, diff, area);
            Assert.Equal(1, odds.Where(d => d.Item != "linh_thach").Sum(d => d.Chance), 10);
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
