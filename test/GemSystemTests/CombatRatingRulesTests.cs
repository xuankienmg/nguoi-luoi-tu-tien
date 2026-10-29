using System.Text.Json;
using VanDao.Game;
using VanDao.Data;

namespace GemSystemTests;

public class CombatRatingRulesTests
{
    private static void EquipAccuracy(Player p)
    {
        foreach (var slot in GameData.Realms[p.Realm].EquipSlots)
            p.Equipment.Add(new() { Slot = slot, ItemId = GameData.AllItems.First(i => i.Slot == slot && i.MinRealm == p.Realm && i.Rarity == "common").Id });
        GemData.SetSlots(p, p.Equipment.ToDictionary(e => e.Slot, e => new GemSlotData { Sockets = new() { "gem_quang_lv10" } }));
    }
    private sealed class FixedRandom(double value) : Random { public override double NextDouble() => value; }

    [Theory]
    [InlineData(350, 280, 70)]
    [InlineData(350, 100, 250)]
    [InlineData(100, 100, 0)]
    [InlineData(0, 50, 0)]
    public void OpposedRatingsKeepFullDifference(double own, double resistance, double expected)
        => Assert.Equal(expected, CombatRatingRules.Opposed(own, resistance));

    [Fact]
    public void ProcIsSingleAndSubtractsResistanceBeforeRolling()
    {
        Assert.True(CombatRatingRules.Roll(new FixedRandom(.69), 350, 280));
        Assert.False(CombatRatingRules.Roll(new FixedRandom(.70), 350, 280));
        Assert.True(CombatRatingRules.Roll(new FixedRandom(.999), 350, 100));
        Assert.False(CombatRatingRules.Roll(new FixedRandom(0), 50, 100));
        Assert.Equal(250, ComboCombat.Chance(350, 100));
        Assert.Equal(370, new DivineCombat(8).ExtraChance(350));
    }

    [Fact]
    public void ArenaCarriesFullEightStatsWithoutReaddingGemsOrDivineBonus()
    {
        var p = new Player { Realm = 9 };
        foreach (var slot in GameData.Realms[9].EquipSlots)
            p.Equipment.Add(new() { Slot = slot, ItemId = GameData.AllItems.First(i => i.Slot == slot && i.MinRealm == 9 && i.Rarity == GameData.RarityOrder[8]).Id });
        GemData.SetSlots(p, new() { ["weapon"] = new() { Sockets = new() { "gem_phong_lv10" } } });
        var stats = GameService.Stats(p);
        var fighter = GameService.PvpFighterOf(p);
        Assert.Equal(CombatRatings.From(stats), fighter.Ratings);
        var restored = JsonSerializer.Deserialize<PvpFighter>(JsonSerializer.Serialize(fighter))!;
        Assert.Equal(fighter.Ratings, restored.Ratings);
        Assert.Equal(fighter.Ratings, ArenaService.Sanitize(fighter, "A", 9, null).Ratings);
    }

    [Fact]
    public void LegacyArenaDefaultsNewResistancesToZeroAndRetainsLegacyCombo()
    {
        var old = PvpFighter.FromStats("A", 0, new() { ["atk"] = 100, ["hp"] = 10000 }) with { ExtraHit = 350, Divine = 8, Gems = new(AttackSpeed: 25, Accuracy: 180) };
        Assert.Equal(395, old.Combo);
        Assert.Equal(180, old.Ratings.Accuracy);
        Assert.Equal(0, old.Ratings.ComboResist);
        Assert.Equal(0, old.Ratings.ReflectResist);
    }

    [Fact]
    public void PvpComboOpposesComboResistanceInsteadOfTargetsCombo()
    {
        var a = PvpFighter.FromStats("A", 0, new() { ["atk"] = 100, ["hp"] = 100000 }) with { ExtraHit = 350 };
        var d = a with { Name = "D", ExtraHit = 1000, ComboResist = 250 };
        Assert.True(PvpCombat.Run(a, d, 42).Actions[0].Extra);
        Assert.False(PvpCombat.Run(a, d with { ComboResist = 350 }, 42).Actions[0].Extra);
    }

    [Fact]
    public void ReflectionIsOneCounterAttackRatherThanPercentageOfIncomingDamage()
    {
        var a = PvpFighter.FromStats("A", 0, new() { ["atk"] = 100, ["hp"] = 10000 }) with { Crit = 0, BasicPct = 100, BasicCrit = 0, Reflect = 350, ReflectResist = 250 };
        var d = a with { Name = "D", Atk = 200, ReflectResist = 0 };
        var result = PvpCombat.Run(a, d, 42);
        var first = result.Actions[0];
        Assert.Equal(100, first.Direct);
        Assert.Equal(200, first.Reflect);
        Assert.Equal(29800, first.HpA);
        Assert.Equal(29900, first.HpD);
        Assert.Equal(25, first.RageA);
        Assert.Equal(0, first.RageD);
        var replay = PvpCombat.ToBattle(result, 100).battle;
        Assert.Equal(200, replay.MonsterReflectDamage![0]);
        Assert.Equal(100, replay.Reflect![0]);
        var blocked = PvpCombat.Run(a, d with { Reflect = 250 }, 42).Actions[0];
        Assert.Equal(0, blocked.Reflect);
    }

    [Fact]
    public void CounterRequiresActualHealthDamageAndSurvivalAndConsumesShields()
    {
        var a = PvpFighter.FromStats("A", 0, new() { ["atk"] = 100, ["hp"] = 10000 }) with { Crit = 0, BasicPct = 100, BasicCrit = 0, GuardPct = 10 };
        var d = a with { Name = "D", Atk = 200, Reflect = 100, GuardPct = 0 };
        var first = PvpCombat.Run(a, d, 42).Actions[0];
        Assert.Equal(200, first.Reflect);
        Assert.Equal(30000, first.HpA);
        Assert.Equal(2800, first.GuardA);
        var guardedReplay = PvpCombat.ToBattle(PvpCombat.Run(a, d, 42), 100).battle;
        Assert.Equal(0, guardedReplay.MonsterReflectDamage![0]);
        Assert.Equal(0, PvpCombat.Run(a, d with { GuardPct = 10 }, 42).Actions[0].Reflect);
        Assert.Equal(0, PvpCombat.Run(a, d with { Hp = 1 }, 42).Actions[0].Reflect);
        Assert.Equal(50, PvpCombat.Run(a with { Def = 150 }, d, 42).Actions[0].Reflect);
    }

    [Fact]
    public void MonsterComboAndCounterHaveAlignedReplayDataAndDoNotChargeExtraRage()
    {
        var p = new Player { Id = Guid.Parse("40000000-0000-0000-0000-000000000002"), Realm = 0, Chapter = 9, Diff = 0, BodyHp = 1_000_000_000_000, MapVer = 1 };
        EquipAccuracy(p);
        var boss = GameData.AreaWaves(GameData.ChapterFor(0, 9), 16, 0).Single();
        p.BodyDef = CombatNumber.Round((boss.Atk - GameService.Stats(p).Def - 10d) / (1 + GameService.GearEffects(p).SetPct / 100));
        var at = new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);
        var battle = GameService.BattleAt(p, 16, at);
        Assert.Equal(battle.Hits, battle.MonsterExtraDamage!.Length);
        Assert.Equal(battle.Hits, battle.MonsterReflectDamage!.Length);
        Assert.All(battle.MonsterExtraDamage, hit => Assert.Contains(hit, new long[] { 10, 15 }));
        Assert.Contains(battle.MonsterReflectDamage, hit => hit > 0);
        for (var i = 0; i < battle.Hits; i++)
        {
            Assert.Equal(i % 5 * 25, battle.MonsterRage![i].Before);
            Assert.True(battle.CounterDamage![i] >= battle.MonsterExtraDamage[i]);
            Assert.Equal(battle.HeroHpBefore![i] - battle.HeroHpLeft[i],
                battle.CounterDamage[i] + battle.MonsterReflectDamage[i]);
        }
        Assert.Equal(battle.MonsterExtraDamage, GameService.BattleAt(p, 16, at).MonsterExtraDamage);
        Assert.Equal(battle.MonsterReflectDamage, GameService.BattleAt(p, 16, at).MonsterReflectDamage);
    }

    [Fact]
    public void PlayerResistanceCancelsMonsterComboAndCounter()
    {
        var p = new Player { Realm = 9, Chapter = 0, Diff = 3, BodyHp = 1_000_000_000_000_000, MapVer = 1 };
        foreach (var slot in new[] { "legs", "offhand" })
            p.Equipment.Add(new() { Slot = slot, ItemId = GameData.AllItems.First(i => i.Slot == slot && i.MinRealm == 9 && i.Rarity == "supreme").Id });
        var battle = GameService.BattleAt(p, 1, new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc));
        Assert.NotEmpty(battle.MonsterExtraDamage!);
        Assert.All(battle.MonsterExtraDamage!, damage => Assert.Equal(0, damage));
        Assert.All(battle.MonsterReflectDamage!, damage => Assert.Equal(0, damage));
    }

    [Fact]
    public void ArenaSanitizesInvalidRatingsWithoutCappingValidRatings()
    {
        var f = PvpFighter.FromStats("A", 0, null) with {
            Crit = 1000, CritResist = 1000, Accuracy = 1000, Dodge = 1000,
            ExtraHit = 1000, ComboRating = 1000, ComboResist = 1000, Reflect = 1000, ReflectResist = 1000 };
        Assert.Equal(new CombatRatings(1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000), ArenaService.Sanitize(f, "A", 0, null).Ratings);
        f = f with { Crit = double.NaN, CritResist = double.PositiveInfinity, Accuracy = -1, Dodge = double.NaN,
            ExtraHit = 0, ComboRating = double.PositiveInfinity, ComboResist = -1, Reflect = double.NaN, ReflectResist = -1 };
        Assert.Equal(new CombatRatings(), ArenaService.Sanitize(f, "A", 0, null).Ratings);
    }

    [Fact]
    public void PartiallyNullifiedMonsterComboIsNotReportedAsFullyNullified()
    {
        var found = false;
        for (var seed = 0; seed < 32; seed++)
        {
            var p = new Player { Id = new Guid(seed, 0, 0, new byte[8]), Realm = 0, Chapter = 9, BodyHp = 10000, MapVer = 1 };
            foreach (var slot in GameData.Realms[0].EquipSlots)
                p.Equipment.Add(new() { Slot = slot, ItemId = GameData.AllItems.First(i => i.Slot == slot && i.MinRealm == 0 && i.Rarity == GameData.RarityOrder[7]).Id });
            var boss = GameData.AreaWaves(GameData.ChapterFor(0, 9), 16, 0).Single();
            var fx = GameService.GearEffects(p);
            var weakenedAttack = boss.Atk * (1 - Math.Min(40, fx.MonAtkDown) / 100);
            p.BodyDef = CombatNumber.Round((weakenedAttack - GameService.Stats(p).Def - 1000d) / (1 + fx.SetPct / 100));
            var b = GameService.BattleAt(p, 16, new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc));
            for (var i = 0; i < b.Hits; i++)
                if (b.CounterDamage![i] > 0)
                {
                    found = true;
                    Assert.False(b.Divine!.Turns[i].Nullified);
                }
        }
        Assert.True(found);
    }

    [Fact]
    public void LethalMonsterCounterStillAllowsButterflyResurrection()
    {
        var found = false;
        for (var seed = 0; seed < 64 && !found; seed++)
        {
            var p = new Player { Id = new Guid(seed, 0, 0, new byte[8]), Realm = 0, Chapter = 9, MapVer = 1 };
            EquipAccuracy(p);
            p.Pets.Add(new() { DefId = "that_thai_ho_diep", Slot = 1, Level = 1 });
            var b = GameService.BattleAt(p, 16, new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc));
            if (b.MonsterReflectDamage![0] <= 0) continue;
            found = true;
            Assert.Contains(b.Pet!.Casts, c => c.Kind == "butterfly_rebirth" && c.Turn == 0);
        }
        Assert.True(found);
    }
}
