using VanDao.Game;

namespace GemSystemTests;

public class AlchemyMaterialSourceTests
{
    [Fact]
    public void LowNormalMapCannotSupplyNascentSoulPillIngredients()
    {
        var chapter = GameData.Chapters[1]; // 2-15 Thường
        var drops = PetLoot.StageOdds(chapter, 0, 15).Select(d => d.Item).ToHashSet();
        var recipe = GameData.Brews.Single(b => b.Out == "ket_anh_dan");
        var materials = recipe.In.Keys.Where(id => GameData.ItemById[id].Type == "herb").ToArray();
        Assert.NotEmpty(materials);
        Assert.All(materials, id => Assert.DoesNotContain(id, drops));
        var correctMap = PetLoot.StageOdds(GameData.Chapters[2], 0, 15).Select(d => d.Item).ToHashSet();
        Assert.All(materials, id => Assert.Contains(id, correctMap));
    }

    [Fact]
    public void NormalMapHerbsAlwaysRespectChapterAndMinimumArea()
    {
        foreach (var chapter in GameData.Chapters.Where(c => c.Enabled))
        foreach (var area in Enumerable.Range(1, GameData.Areas))
        {
            var actual = PetLoot.StageOdds(chapter, 0, area)
                .Where(d => GameData.ItemById.TryGetValue(d.Item, out var item) && item.Type == "herb").Select(d => d.Item).ToHashSet();
            var expected = GameData.HerbDrops.Where(h => h.Chapters.Contains(chapter.Index) && h.MinArea <= area)
                .Select(h => h.Item).ToHashSet();
            Assert.True(expected.SetEquals(actual), $"Sai nguồn linh thảo: {chapter.Index + 1}-{area}");
        }
    }

    [Fact]
    public void NoChestCanBypassMapFarmingRegardlessOfPlayerRealmOrMap()
    {
        for (var realm = 0; realm < GameData.Realms.Count; realm++)
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        for (var chapter = 0; chapter < GameData.Chapters.Count; chapter++)
        foreach (var chest in GameData.Chests.Keys)
        {
            var table = ChestRewards.Table(realm, chest, diff, chapter);
            Assert.NotEmpty(table);
            Assert.Equal(1, table.Sum(r => r.Probability), 8);
            Assert.All(table, reward => {
                Assert.True(reward.Probability >= 0);
                Assert.NotEqual("herb", GameData.ItemById[reward.Item].Type);
                Assert.NotEqual("pill", reward.Group);
            });
        }
        Assert.All(GameData.HerbDrops, h => Assert.False(h.InChest));
    }

    [Fact]
    public void ChestOddsUiReportsNoHerbReward()
    {
        var player = new VanDao.Data.Player { Realm = 3, Chapter = 1 };
        foreach (var chest in GameService.ChestOdds(player))
        {
            Assert.Equal(0, chest.Herb);
            Assert.Empty(chest.Herbs!);
        }
    }
}
