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
    [InlineData(3)]
    [InlineData(4)]
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
