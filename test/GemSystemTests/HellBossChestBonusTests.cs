using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class HellBossChestBonusTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NonHellBossChestHasNoBonusReward(int diff)
    {
        var chest = GameData.ChestId(GameData.BossChest, diff);
        Assert.Null(ChestRewards.RollBonus(5, chest, new Random(1), diff, 5));
    }

    [Theory]
    [InlineData("ruong_thuong")]
    [InlineData("ruong_hiem")]
    public void OtherHellChestsHaveNoBonusReward(string baseId)
    {
        Assert.Null(ChestRewards.RollBonus(5, GameData.ChestId(baseId, 4), new Random(1), 4, 5));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    public void BossChestBonusCountByDifficulty(int diff, int expected)
    {
        Assert.Equal(expected, GameData.BossChestBonusCount(diff));
    }

    [Fact]
    public void EndlessBossChestOpensThreeItems()
    {
        var player = new Player { Realm = 5 };
        var chest = GameData.ChestId(GameData.BossChest, EndlessData.Diff);
        player.Add(chest, 1);
        var result = new GameService(null!).UseItem(player, chest, 1, new Random(3));
        var rng = new Random(3);   // cùng hạt: 1 lượt chính + 2 lượt phụ
        var main = ChestRewards.Roll(5, chest, rng, EndlessData.Diff, player.Chapter);
        var extra = new[] { ChestRewards.RollBonus(5, chest, rng, EndlessData.Diff, player.Chapter)!, ChestRewards.RollBonus(5, chest, rng, EndlessData.Diff, player.Chapter)! };
        Assert.Equal(main.Qty + extra.Sum(x => x.Qty), result.craft!.Outputs!.Sum(g => g.Qty));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void HellBossChestBonusOnlyDropsGearGemOrIron(int diff)
    {
        var chest = GameData.ChestId(GameData.BossChest, diff);
        var allowed = ChestRewards.Table(5, chest, diff, 5).Where(r => r.Group is "gear" or "gem" or "iron").Select(r => r.Item).ToHashSet();
        var rng = new Random(7);
        for (var i = 0; i < 500; i++)
        {
            var bonus = ChestRewards.RollBonus(5, chest, rng, diff, 5);
            Assert.NotNull(bonus);
            Assert.Contains(bonus!.Item, allowed);
        }
    }
}
