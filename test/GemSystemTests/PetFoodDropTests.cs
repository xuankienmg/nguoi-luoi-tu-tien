using VanDao.Game;

namespace GemSystemTests;

public class PetFoodDropTests
{
    private sealed class EndpointRandom(bool upper) : Random
    {
        public override int Next(int minValue, int maxValue) => upper ? maxValue - 1 : minValue;
        public override double NextDouble() => 0;
    }

    [Fact]
    public void HerbsStartAtOneAndKeepTheirScaledMaximum()
    {
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        for (var ch = 0; ch < GameData.OpenChapters; ch++)
        foreach (var area in new[] { 1, 10, GameData.AreasFor(diff) })
        {
            var chapter = GameData.ChapterFor(diff, ch);
            var odds = PetLoot.StageOdds(chapter, diff, area);
            foreach (var herb in GameData.HerbDropsAt(chapter, diff, area))
            {
                var shown = odds.Single(d => d.Item == herb.Item);
                var max = (int)Math.Round(herb.Max * GameData.DiffRewardAt(diff, ch));
                Assert.Equal(1, shown.Min);
                Assert.Equal(max, shown.Max);
                Assert.Equal(1, PetLoot.RollQuantity(herb.Item, herb.Min, herb.Max, diff, ch, new EndpointRandom(false)));
                Assert.Equal(max, PetLoot.RollQuantity(herb.Item, herb.Min, herb.Max, diff, ch, new EndpointRandom(true)));
            }
        }
    }

    [Fact]
    public void TransformationPillStartsAtOneForBossesAndChests()
    {
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        for (var ch = 0; ch < GameData.OpenChapters; ch++)
        {
            var boss = PetLoot.BossMaterials(ch, diff).Single(d => d.Item == "hoa_hinh_dan");
            Assert.Equal(1, boss.Min);
            Assert.Equal(2 + ch / 3 + diff, boss.Max);
            Assert.Contains(PetLoot.RollBossMaterials(ch, diff, new EndpointRandom(false)), r => r.Item == "hoa_hinh_dan" && r.Quantity == 1);
            Assert.Contains(PetLoot.RollBossMaterials(ch, diff, new EndpointRandom(true)), r => r.Item == "hoa_hinh_dan" && r.Quantity == boss.Max);
            var chest = ChestRewards.Table(0, GameData.BossChest, diff, ch)
                .Where(r => r.Item == "hoa_hinh_dan").OrderBy(r => r.Qty).ToArray();
            Assert.Equal(Enumerable.Range(1, 1 + ch / 3 + diff), chest.Select(r => r.Qty));
            Assert.All(chest, r => Assert.Equal(chest[0].Probability, r.Probability, 10));
        }
    }

    [Fact]
    public void StageFoodAlwaysRollsOneThroughFourWithoutDifficultyScaling()
    {
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        for (var ch = 0; ch < GameData.OpenChapters; ch++)
        for (var area = 1; area <= GameData.AreasFor(diff); area++)
        {
            var chapter = GameData.ChapterFor(diff, ch);
            var choice = PetLoot.StageGroups(chapter, diff, area).SelectMany(g => g.Choices)
                .Single(c => c.Item == "linh_thu_dan");
            Assert.Equal(1, choice.Min);
            Assert.Equal(4, choice.Max);
            var shown = PetLoot.StageOdds(chapter, diff, area).Single(d => d.Item == "linh_thu_dan");
            Assert.Equal(1, shown.Min);
            Assert.Equal(4, shown.Max);
            for (var n = 1; n <= 4; n++)
                Assert.Equal(n, PetLoot.DropQuantity("linh_thu_dan", n, diff, ch));
        }
    }

    [Fact]
    public void ChestFoodHasFourEquallyLikelyAmounts()
    {
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        for (var ch = 0; ch < GameData.OpenChapters; ch++)
        foreach (var chest in GameData.Chests.Keys.Where(c => c != GameData.BossChest))
        {
            var table = ChestRewards.Table(0, chest, diff, ch);
            var food = table.Where(r => r.Item == "linh_thu_dan").OrderBy(r => r.Qty).ToArray();
            Assert.Equal(new[] { 1, 2, 3, 4 }, food.Select(r => r.Qty));
            Assert.All(food, r => Assert.Equal(food[0].Probability, r.Probability, 10));
            Assert.Equal(1, table.Sum(r => r.Probability), 8);
        }
    }
}
