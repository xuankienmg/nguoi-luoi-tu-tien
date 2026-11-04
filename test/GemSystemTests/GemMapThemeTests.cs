using VanDao.Game;

namespace GemSystemTests;

public class GemMapThemeTests
{
    private static string ThemeOf(string gem) =>
        GemProgression.Themes.Single(t => Enumerable.Range(0, 10).Any(l => GemProgression.Id(t, l) == gem));

    [Fact]
    public void EveryThemeDropsInExactlyTwoChapters()
    {
        Assert.Equal(10, GemData.ChapterThemes.Length);
        foreach (var theme in GemProgression.Themes)
            Assert.Equal(2, GemData.ChapterThemes.Count(c => c.First == theme || c.Second == theme));
        Assert.All(GemData.ChapterThemes, c => Assert.NotEqual(c.First, c.Second));
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(0, 8, 0)]
    [InlineData(0, 9, 1)]
    [InlineData(0, 15, 1)]
    [InlineData(3, 8, 0)]
    [InlineData(3, 9, 1)]
    public void FirstHalfOfAreasDropsFirstThemeAndSecondHalfDropsSecond(int diff, int area, int which)
    {
        for (var chapter = 0; chapter < GameData.Chapters.Count; chapter++)
        {
            var (first, second) = GemData.ThemesOf(chapter);
            var group = GemData.GetStageStoneGroup(chapter, diff, area);
            Assert.All(group.Choices, c => Assert.Equal(which == 0 ? first : second, ThemeOf(c.Item)));
            Assert.Equal(1, group.Choices.Sum(c => c.Weight), 8);
            Assert.All(group.Choices, c => Assert.True(GemData.ById.ContainsKey(c.Item), c.Item));
        }
    }

    [Fact]
    public void StageChanceAndLevelOddsStayTheSame()
    {
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        {
            var group = GemData.GetStageStoneGroup(2, diff, 1);
            Assert.Equal(1 - (1 - .38) * (1 - .12), group.Chance, 8);
            var oldByLevel = GemData.MapRewardPool(diff).GroupBy(g => GemData.GemLevel(g.Item)).ToDictionary(g => g.Key, g => g.Sum(x => x.Probability));
            var newByLevel = group.Choices.GroupBy(c => GemData.GemLevel(c.Item)).ToDictionary(g => g.Key, g => g.Sum(x => x.Weight));
            Assert.Equal(oldByLevel.Keys.OrderBy(k => k), newByLevel.Keys.OrderBy(k => k));
            foreach (var (level, p) in oldByLevel) Assert.Equal(p, newByLevel[level], 8);
        }
    }

    [Fact]
    public void EveryDifficultyMapUsesItsChapterThemes()
    {
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        for (var chapter = 0; chapter < GameData.Chapters.Count; chapter++)
        {
            var map = GameData.ChapterFor(diff, chapter);
            var (first, second) = GemData.ThemesOf(chapter);
            var gems = Enumerable.Range(1, GameData.AreasFor(diff))
                .SelectMany(a => PetLoot.StageGroups(map, diff, a)).SelectMany(g => g.Choices).Where(c => GemData.IsGem(c.Item))
                .Select(c => ThemeOf(c.Item)).Distinct().OrderBy(x => x);
            Assert.Equal(new[] { first, second }.OrderBy(x => x), gems);
            Assert.Equal(new[] { first, second }.OrderBy(x => x),
                PetLoot.BossMaterials(map.Index, diff).Where(d => GemData.IsGem(d.Item)).Select(d => ThemeOf(d.Item)).Distinct().OrderBy(x => x));
        }
    }

    [Fact]
    public void BossDropsOnlyItsChapterThemes()
    {
        for (var chapter = 0; chapter < GameData.Chapters.Count; chapter++)
        {
            var (first, second) = GemData.ThemesOf(chapter);
            var drops = GemData.BossBonusDrops(chapter, 1);
            Assert.Equal(new[] { first, second }.OrderBy(x => x), drops.Select(d => ThemeOf(d.Item)).Distinct().OrderBy(x => x));
            Assert.Equal(GemData.BossBonusChance(1), drops.Sum(d => d.Chance), 8);
            var rolled = GemData.RollBossBonus(chapter, 1, new Random(chapter));
            if (rolled is not null) Assert.Contains(ThemeOf(rolled), new[] { first, second });
        }
    }
}
