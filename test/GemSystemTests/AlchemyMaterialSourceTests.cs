using VanDao.Game;

namespace GemSystemTests;

public class AlchemyMaterialSourceTests
{
    [Fact]
    public void EveryDifficultyUsesCurrentChapterHerbsAtEveryStage()
    {
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        for (var ch = 0; ch < GameData.OpenChapters; ch++)
        for (var area = 1; area <= GameData.AreasFor(diff); area++)
        {
            var chapter = GameData.ChapterFor(diff, ch);
            var actual = PetLoot.StageOdds(chapter, diff, area)
                .Where(d => GameData.ItemById.TryGetValue(d.Item, out var item) && item.Type == "herb").Select(d => d.Item).ToHashSet();
            var expected = GameData.HerbDrops.Where(h => h.Chapters.Contains(ch) && h.MinArea <= area)
                .Select(h => h.Item).ToHashSet();
            Assert.True(expected.SetEquals(actual), $"Herb sources: difficulty {diff}, chapter {ch + 1}, stage {area}");
            Assert.Contains("tay_tuy_thao", actual);
        }
    }

    [Theory]
    [InlineData("bo_nguyen_dan_trung", 1, 4, 20000)]
    [InlineData("bo_nguyen_dan_thuong", 4, 4, 80000)]
    public void ReplenishingRecipesCanBeGatheredInTheirOwnProgressionRegion(string id, int chapter, int herbTypes, int stones)
    {
        var recipe = GameData.Brews.Single(b => b.Out == id);
        var herbs = recipe.In.Where(x => GameData.ItemById[x.Key].Type == "herb").ToDictionary();
        Assert.Equal(herbTypes, herbs.Count);
        Assert.Equal(stones, recipe.In["linh_thach"]);
        var expected = id == "bo_nguyen_dan_trung"
            ? new Dictionary<string, int> { ["bich_ngoc_linh_thao"] = 6, ["hoa_tam_hoa"] = 2, ["nguyet_lo_hoa"] = 1, ["thuy_lo_diep"] = 2 }
            : new Dictionary<string, int> { ["tu_van_sam"] = 12, ["thien_nien_sam"] = 3, ["cuu_diep_hon_lien"] = 2, ["linh_cot_chi"] = 1 };
        Assert.Equal(expected.OrderBy(x => x.Key), herbs.OrderBy(x => x.Key));
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        {
            var sources = PetLoot.StageOdds(GameData.ChapterFor(diff, chapter), diff, 10).Select(d => d.Item).ToHashSet();
            Assert.All(herbs.Keys, herb => Assert.Contains(herb, sources));
        }
    }

    [Theory]
    [InlineData("linh_cot_chi", 3, 5)]
    [InlineData("dia_mach_tien_dang", 5, 6)]
    [InlineData("thien_truc_duan", 6, 8)]
    [InlineData("phuong_vu_chi", 7, 9)]
    [InlineData("hon_nguyen_linh_chung", 8, 9)]
    public void RareHerbsStayInTheirProgressionWindows(string herb, int first, int last)
    {
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        for (var ch = 0; ch < GameData.OpenChapters; ch++)
        {
            var chapter = GameData.ChapterFor(diff, ch);
            Assert.DoesNotContain(GameData.HerbDropsAt(chapter, diff, 9), d => d.Item == herb);
            Assert.Equal(ch >= first && ch <= last, GameData.HerbDropsAt(chapter, diff, 10).Any(d => d.Item == herb));
        }
    }

    [Fact]
    public void FinalChapterKeepsPreviousTierMainHerb()
    {
        Assert.Contains(GameData.HerbDropsAt(GameData.Chapters[9], 0, 1), d => d.Item == "kien_moc_mieu");
    }

    [Fact]
    public void EveryRecipeCanBeGatheredBeforeItsProgressionMilestone()
    {
        // Last chapter needed before using each pill; earlier chapters remain farmable.
        var milestones = new Dictionary<string, int>
        {
            ["bo_nguyen_dan_ha"] = 0, ["bo_nguyen_dan_trung"] = 1, ["bo_nguyen_dan_thuong"] = 4,
            ["tu_khi_dan"] = 0, ["truc_co_dan"] = 0, ["ngung_kim_dan"] = 1,
            ["ket_anh_dan"] = 2, ["hoa_than_dan"] = 4, ["luyen_hu_dan"] = 6,
            ["hop_the_dan"] = 8, ["dai_thua_dan"] = 8, ["do_kiep_dan"] = 8, ["chan_tien_dan"] = 8,
            ["linh_thu_dan"] = 0, ["hoa_hinh_dan"] = 4, ["than_thu_huyet"] = 4,
            ["tay_tuy_dan"] = 0, ["ngo_dao_dan"] = 4,
            // đan hỗ trợ: cùng linh thảo với đan tiến giai cùng phẩm cấp
            ["tu_linh_dan"] = 0, ["dan_tam_dan"] = 1, ["tat_hanh_dan"] = 2, ["thu_linh_dan"] = 4, ["be_quan_dan"] = 6
        };
        var breakthroughChapters = new[] { 0, 1, 2, 4, 6, 8, 8, 8, 8, 8 };
        for (var realm = 0; realm < breakthroughChapters.Length; realm++)
            milestones[GameData.BreakPillId(realm)] = breakthroughChapters[realm];
        foreach (var recipe in GameData.Brews)
        {
            Assert.True(milestones.TryGetValue(recipe.Out, out var chapter), $"Missing progression milestone for {recipe.Out}");
            var sources = Enumerable.Range(0, chapter + 1)
                .SelectMany(ch => GameData.HerbDropsAt(ch, GameData.Areas)).Select(d => d.Item).ToHashSet();
            foreach (var ingredient in recipe.In.Keys.Where(id => GameData.ItemById[id].Type == "herb"))
                Assert.True(sources.Contains(ingredient), $"{recipe.Out} needs {ingredient} before chapter {chapter + 1}");
        }
    }

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
