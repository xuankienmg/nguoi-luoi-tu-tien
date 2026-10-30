using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class GemRealmBalanceTests
{
    private sealed class FixedRandom(double value) : Random
    {
        public override double NextDouble() => value;
    }

    private static Player Socketed(int realm, int stage = 0)
    {
        var p = new Player { Realm = realm, Stage = stage };
        var gear = GameData.AllItems.First(i => i.Slot == "weapon" && i.MinRealm == realm && i.Rarity == "supreme");
        p.Equipment.Add(new() { Slot = "weapon", ItemId = gear.Id });
        GemData.SetSlots(p, new() { ["weapon"] = new() { Sockets = new() { "gem_linh_lv10" } } });
        return p;
    }

    [Fact]
    public void FlatHealthRemainsAndRealmAndStagePercentUsesOnlyFoundation()
    {
        foreach (var (realm, stage) in new[] { (0, 0), (4, 0), (7, 2), (9, 0) })
        {
            var p = Socketed(realm, stage);
            var flat = GemData.ById["gem_linh_lv10"].Hp;
            var expected = flat + CombatNumber.Round(GameData.BaseStats(GameData.Realms[realm], stage).Hp * .1);
            Assert.Equal(expected, GemData.GetSocketedBonuses(p).Hp);
            p.BodyHp = 1_000_000;
            Assert.Equal(expected, GemData.GetSocketedBonuses(p).Hp);
        }
    }

    [Fact]
    public void EnemyRatingsStartAtNormalAndGrowWithDifficulty()
    {
        foreach (var tier in new[] { "elite", "boss" })
        {
            var normal = MonsterCombatRatings.For("unknown", tier, 0);
            var hell = MonsterCombatRatings.For("unknown", tier, 3);
            Assert.True(normal.Crit > 0);
            Assert.True(normal.Dodge > 0);
            Assert.True(hell.Crit > normal.Crit);
            Assert.True(hell.Dodge > normal.Dodge);
            var monster = MonsterCombatRatings.Apply(new("Enemy", "enemy.png", 1, 1, 1, tier), 3);
            Assert.False(MonsterCombatRatings.RollCritical(new FixedRandom(0), monster, 1000));
        }
        Assert.Equal(160, MonsterCombatRatings.For("unknown", "", 3).Crit);
    }

    [Fact]
    public void UncappedDodgeIsOpposedByFullAccuracyAndExplicitCapsRemain()
    {
        Assert.Equal(150, CombatDodge.Chance(60, 80, 10));
        Assert.False(GemCombat.Hits(new FixedRandom(.6), 100, 150));
        Assert.True(GemCombat.Hits(new FixedRandom(.99), 150, 150));
        Assert.Equal(32, GemCombat.RagePerHit(new(RagePerHit: 200)));
        Assert.Equal(25, GemCombat.RagePerHit(new()));
        Assert.Equal(25, GemCombat.RagePerHit(new(RagePerHit: -10)));
        Assert.Equal(29, GemCombat.RagePerHit(new(RagePerHit: 4)));
        Assert.Equal(200, GemCombat.LifeSteal(1000, 100, new(LifeSteal: 200)));
    }

    [Fact]
    public void PvpSubtractsResistanceBeforeLimitingCriticalChance()
    {
        var a = PvpFighter.FromStats("a", 0, new() { ["atk"] = 10, ["hp"] = 100000, ["def"] = 0 })
            with { Crit = 150, BasicCrit = 0, Dodge = 0 };
        var target = a with { Name = "target", CritResist = 50 };
        Assert.All(PvpCombat.Run(a, target, 42).Actions.Where(x => x.ByA), x => Assert.True(x.BasicCrit));
        target = target with { CritResist = 150 };
        Assert.All(PvpCombat.Run(a, target, 42).Actions.Where(x => x.ByA), x => Assert.False(x.BasicCrit));
    }
}
