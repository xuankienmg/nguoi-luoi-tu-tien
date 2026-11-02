using VanDao.Game;

namespace GemSystemTests;

public class BossHerbDropTests
{
    [Fact]
    public void BossDropsEachRareHerbOfItsChapter()
    {
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        for (var ch = 0; ch < GameData.OpenChapters; ch++)
        {
            var herbs = PetLoot.BossHerbs(ch);
            Assert.NotEmpty(herbs);
            var rare = GameData.HerbDrops.Where(h => h.Chapters.Contains(ch) && h.MinArea >= GameData.EliteMinArea).Select(h => h.Item).Distinct();
            Assert.Equal(rare.OrderBy(x => x), herbs.OrderBy(x => x));
            var drops = PetLoot.BossMaterials(ch, diff);
            Assert.All(herbs, h =>
            {
                var d = Assert.Single(drops, x => x.Item == h);
                Assert.Equal(PetLoot.BossHerbChance(diff), d.Chance, 10);
                Assert.Equal(1, d.Min);
                Assert.Equal(1 + diff, d.Max);
            });
        }
        Assert.Contains("huyet_can_thao", PetLoot.BossHerbs(0));
        Assert.DoesNotContain("hoa_tam_hoa", PetLoot.BossHerbs(0));
    }

    [Fact]
    public void RolledBossHerbsComeFromTheBossChapter()
    {
        var rng = new Random(7);
        var seen = new HashSet<string>();
        for (var i = 0; i < 400; i++)
            foreach (var (item, qty) in PetLoot.RollBossMaterials(3, 1, rng))
                if (GameData.ItemById[item].Type == "herb") { Assert.Contains(item, PetLoot.BossHerbs(3)); Assert.InRange(qty, 1, 2); seen.Add(item); }
        Assert.Equal(PetLoot.BossHerbs(3).OrderBy(x => x), seen.OrderBy(x => x));
    }
}
