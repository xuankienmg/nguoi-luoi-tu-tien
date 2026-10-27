using VanDao.Data;
using VanDao.Game;
using Xunit;

namespace GemSystemTests;

public class GemExpansionTests
{
    private sealed class FixedRandom(double value) : Random
    {
        public override double NextDouble() => value;
        public override int Next(int minValue, int maxValue) => minValue;
        public override int Next(int maxValue) => 0;
    }

    [Fact]
    public void EveryNewThemeHasTenGradesAndCanBeFusedAndSocketed()
    {
        var themes = new[] { "hoa", "loi", "bang", "tho", "phong", "quang", "am", "linh", "phuc_nguyen", "vu_tru" };
        foreach (var theme in themes)
        for (var tier = 0; tier < 10; tier++)
        {
            var id = GemProgression.Id(theme, tier);
            var item = GameData.ItemById[id];
            Assert.Equal(tier, GemData.GemLevel(id));
            Assert.Equal($"img/gems/{theme}.png", item.Icon);
            Assert.Equal("gem", item.Type);
            Assert.NotEmpty(item.Special!);
            var evaluation = GemData.EvaluateFusion(new() { [id] = 9 });
            Assert.Equal(tier < 9, evaluation.IsValid);
            if (tier < 9) Assert.Contains(GemProgression.Id(theme, tier + 1), evaluation.PossibleResults);
        }
        var player = new Player();
        player.Equipment.Add(new() { Slot = "weapon", ItemId = GameData.AllItems.First(i => i.Slot == "weapon" && i.Rarity == "common").Id });
        player.Add("gem_hoa_t0", 1);
        new GameService(null!).SocketGem(player, "weapon", 0, "gem_hoa_t0");
        Assert.Equal(0, player.Count("gem_hoa_t0"));
        Assert.Equal(GemData.ById["gem_hoa_t0"].Atk, GemData.GetSocketedBonuses(player).Atk);
        Assert.Equal(4, GemData.GetSocketedBonuses(player).CritDamage);
    }

    [Fact]
    public void ChestTablesRemainNormalizedAndEveryEligibleGemCanActuallyBeRolled()
    {
        foreach (var chest in GameData.Chests.Keys)
        for (var realm = 0; realm < GameData.Realms.Count; realm++)
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        {
            var table = ChestRewards.Table(realm, chest, diff, 0);
            Assert.InRange(table.Sum(x => x.Probability), 1 - 1e-8, 1 + 1e-8);
            Assert.All(table, reward => Assert.True(reward.Probability >= 0));
            Assert.Equal(GemData.ChestGemChance(chest), table.Where(x => x.Group == "gem").Sum(x => x.Probability), 8);
            Assert.All(table.Where(x => x.Group == "gem"), reward => {
                Assert.True(GemData.IsGem(reward.Item));
                Assert.InRange(GemData.GemLevel(reward.Item), 0, 6);
                Assert.Equal(1, reward.Qty);
            });
            Assert.Equal(GemData.ChestDivineChance(chest), table.Where(x => x.Group == "gem" && GemData.GemLevel(x.Item) == 6).Sum(x => x.Probability), 8);
        }
        var rewards = ChestRewards.Table(0, GameData.BossChest, 3, 0);
        var position = 0d;
        var reached = new HashSet<string>();
        foreach (var reward in rewards)
        {
            if (reward.Group == "gem")
            {
                var rolled = ChestRewards.Roll(0, GameData.BossChest, new FixedRandom(position + reward.Probability / 2), 3, 0);
                Assert.Equal(reward.Item, rolled.Item);
                Assert.Equal(1, rolled.Qty);
                reached.Add(rolled.Item);
            }
            position += reward.Probability;
        }
        Assert.Equal(GemData.Items.Count(i => GemData.GemLevel(i.Id) <= 6), reached.Count);
    }

    [Fact]
    public void BossLootIncludesNewGemsAndItsAdvertisedExtraOddsMatchTheRoll()
    {
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        {
            Assert.Equal(1, GemData.RewardPool(diff).Sum(g => g.Probability), 8);
            Assert.Equal(GemData.BossBonusChance(diff), GemData.BossBonusDrops(diff).Sum(g => g.Chance), 8);
            Assert.Null(GemData.RollBossBonus(diff, new FixedRandom(.99)));
        }
        var loot = PetLoot.RollBossMaterials(0, 1, new FixedRandom(.24));
        Assert.Contains(loot, g => GemData.IsGem(g.Item) && GemData.GemLevel(g.Item) <= 2);
        Assert.DoesNotContain(PetLoot.BossMaterials(0, 3), d => GemData.IsGem(d.Item) && GemData.GemLevel(d.Item) > 2);
        Assert.DoesNotContain(GemData.RewardPool(0), g => GemData.GemLevel(g.Item) > 3);
        Assert.Contains(GemData.RewardPool(3), g => GemData.GemLevel(g.Item) == 9);
    }

    [Fact]
    public void AllMapsAndBossesDropAtMostOneStoneOfHuyenOrLower()
    {
        var rng = new Random(2026);
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        for (var chapter = 0; chapter < GameData.OpenChapters; chapter++)
        {
            foreach (var area in new[] { 1, GameData.AreasFor(diff) })
            {
                var groups = PetLoot.StageGroups(GameData.ChapterFor(diff, chapter), diff, area)
                    .Where(g => g.Choices.Any(c => GemData.IsGem(c.Item))).ToArray();
                Assert.Single(groups);
                Assert.All(groups[0].Choices, c => {
                    Assert.InRange(GemData.GemLevel(c.Item), 0, 2);
                    Assert.Equal(1, c.Min); Assert.Equal(1, c.Max);
                    Assert.Equal(1, PetLoot.DropQuantity(c.Item, 1, diff, chapter));
                });
            }
            Assert.All(PetLoot.BossMaterials(chapter, diff).Where(d => GemData.IsGem(d.Item)), d => {
                Assert.InRange(GemData.GemLevel(d.Item), 0, 2);
                Assert.Equal(1, d.Min); Assert.Equal(1, d.Max);
            });
            for (var roll = 0; roll < 100; roll++)
            {
                var gems = PetLoot.RollBossMaterials(chapter, diff, rng).Where(g => GemData.IsGem(g.Item)).ToArray();
                Assert.True(gems.Sum(g => g.Quantity) <= 1);
                Assert.All(gems, g => Assert.InRange(GemData.GemLevel(g.Item), 0, 2));
            }
        }
    }

    [Fact]
    public void ChestOddsExposeGemRewardsAndBatchOpeningReturnsRealGemItems()
    {
        var player = new Player();
        var odds = GameService.ChestOdds(player).First(o => o.Chest == GameData.BossChest);
        Assert.NotNull(odds.Gems);
        Assert.Equal(25, odds.Gems!.Sum(x => x.Pct), 7);
        var table = ChestRewards.Table(player.Realm, GameData.BossChest, 0, player.Chapter);
        var chosen = table.First(x => x.Item == "gem_hoa_t0");
        var position = table.TakeWhile(x => x != chosen).Sum(x => x.Probability) + chosen.Probability / 2;
        player.Add(GameData.BossChest, 20);
        var result = new GameService(null!).UseItem(player, GameData.BossChest, 20, new FixedRandom(position));
        Assert.NotNull(result.craft);
        Assert.Equal(20, result.craft.Outputs!.Sum(x => x.Qty));
        Assert.Contains(result.craft.Outputs, x => GemData.IsGem(x.Item));
        Assert.Equal(0, player.Count(GameData.BossChest));
        Assert.Equal(20, player.Count("gem_hoa_t0"));
    }
}
