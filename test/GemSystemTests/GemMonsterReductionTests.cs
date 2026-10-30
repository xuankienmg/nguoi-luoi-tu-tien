using VanDao.Game;
using VanDao.Data;

namespace GemSystemTests;

public class GemMonsterReductionTests
{
    [Theory]
    [InlineData("bang")]
    [InlineData("tho")]
    public void SocketBonusesStackToCapAndDisappearWhenRemoved(string theme)
    {
        var player = new Player();
        var service = new GameService(null!);
        var gem = GemProgression.Id(theme, 9);
        foreach (var slot in new[] { "head", "body", "hands", "legs" })
        {
            player.Equipment.Add(new() { Slot = slot, ItemId = GameData.AllItems.First(i => i.Slot == slot && i.Rarity == "common").Id });
            player.Add(gem, 1);
            service.SocketGem(player, slot, 0, gem);
        }
        var bonuses = GemData.GetSocketedBonuses(player);
        Assert.Equal(theme == "bang" ? 30 : 0, bonuses.EliteReduction);
        Assert.Equal(theme == "tho" ? 30 : 0, bonuses.BossReduction);
        GemData.SetSlots(player, new());
        Assert.Equal(0, GemData.GetSocketedBonuses(player).EliteReduction);
        Assert.Equal(0, GemData.GetSocketedBonuses(player).BossReduction);
    }

    [Fact]
    public void ExistingDefenseGemsGainReductionAtEveryLevel()
    {
        foreach (var tier in Enumerable.Range(0, 10))
        foreach (var (theme, effect) in new[] { ("bang", "gem_elite_reduction"), ("tho", "gem_boss_reduction") })
        {
            var gem = GemData.ById[GemProgression.Id(theme, tier)];
            Assert.Equal(tier + 1, Assert.Single(gem.Effects!, e => e.Id == effect).Value);
            Assert.Equal("defense", gem.GemCategory);
        }
    }

    [Theory]
    [InlineData("", 1000)]
    [InlineData("player", 1000)]
    [InlineData("elite", 800)]
    [InlineData("boss", 900)]
    public void ReductionMatchesOnlyTheAttackingMonsterTier(string tier, long expected)
    {
        var bonuses = new GemBonuses(EliteReduction: 20, BossReduction: 10);
        Assert.Equal(expected, GemCombat.ReduceMonsterDamage(1000, bonuses, tier));
    }

    [Fact]
    public void ReductionIsCappedAndCannotCreateDamage()
    {
        Assert.Equal(700, GemCombat.ReduceMonsterDamage(1000, new(EliteReduction: 200), "elite"));
        Assert.Equal(1000, GemCombat.ReduceMonsterDamage(1000, new(BossReduction: -10), "boss"));
        Assert.Equal(0, GemCombat.ReduceMonsterDamage(0, new(BossReduction: 20), "boss"));
    }
}
