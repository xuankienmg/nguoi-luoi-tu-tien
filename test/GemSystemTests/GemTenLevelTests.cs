using System.Text.Json;
using VanDao.Data;
using VanDao.Game;
using Xunit;

namespace GemSystemTests;

public class GemTenLevelTests
{
    private sealed class FixedRandom(double value) : Random { public override double NextDouble() => value; }

    private static Player Equipped(string gem)
    {
        var player = new Player { Realm = 9, Stage = 0 };
        player.Equipment.Add(new() { Slot = "weapon", ItemId = GameData.AllItems.First(i => i.Slot == "weapon" && i.Rarity == "supreme").Id });
        GemData.SetSlots(player, new() { ["weapon"] = new() { Sockets = new() { gem } } });
        return player;
    }

    [Fact]
    public void AllTenLevelsExistAndLegacyExpansionIdsKeepTheirPreviousGrade()
    {
        foreach (var theme in GemProgression.Themes)
        for (var level = 0; level < 10; level++)
        {
            var item = GemData.ById[GemProgression.Id(theme, level)];
            Assert.Equal(level + 1, item.GemLevel);
            Assert.Equal(GameData.RarityOrder[level], item.Rarity);
            Assert.Contains($"Cấp {level + 1}", item.Name);
            Assert.Equal(0, item.Tier); // Gem levels must not use the pill-price tier.
        }
        foreach (var (suffix, level) in new[] { ("t0", 2), ("t1", 3), ("t2", 6), ("t3", 7) })
            Assert.Equal(level, GemData.ById[$"gem_hoa_{suffix}"].GemLevel);
        Assert.Equal(10, GemData.UpgradeChances.Length);
        Assert.True(GemData.UpgradeChances.Take(9).Zip(GemData.UpgradeChances.Skip(1)).All(pair => pair.First > pair.Second));
    }

    [Fact]
    public void GuaranteedExtraAttackExistsOnlyOnLevelTenAndNeverStacks()
    {
        var granting = GemData.Items.Where(i => i.Effects?.Any(e => e.Id == "gem_extra_attack") == true).ToList();
        Assert.NotEmpty(granting);
        Assert.All(granting, item => { Assert.Equal(10, item.GemLevel); Assert.Equal("supreme", item.Rarity); });
        var player = Equipped("gem_am_lv10");
        GemData.SetSlots(player, new() { ["weapon"] = new() { Sockets = new() { "gem_am_lv10", "gem_am_lv10", "gem_am_lv10" } } });
        Assert.Equal(1, GemData.GetSocketedBonuses(player).ExtraAttacks);
        Assert.Equal(0, GemData.GetSocketedBonuses(Equipped("gem_am_t3")).ExtraAttacks);
    }

    [Fact]
    public void EveryNewCombatEffectIsAccumulatedFromActiveSockets()
    {
        Assert.True(GemData.GetSocketedBonuses(Equipped("gem_hoa_lv10")).CritDamage > 0);
        Assert.True(GemData.GetSocketedBonuses(Equipped("gem_loi_lv10")).ExtraHit > 0);
        Assert.True(GemData.GetSocketedBonuses(Equipped("gem_bang_lv10")).Block > 0);
        Assert.True(GemData.GetSocketedBonuses(Equipped("gem_tho_lv10")).AbsorbHealChance > 0);
        var wind = GemData.GetSocketedBonuses(Equipped("gem_phong_lv10"));
        Assert.True(wind.MoveSpeed > 0 && wind.AttackSpeed > 0); Assert.Equal(0, wind.Speed);
        Assert.True(GemData.GetSocketedBonuses(Equipped("gem_quang_lv10")).Accuracy > 0);
        Assert.True(GemData.GetSocketedBonuses(Equipped("gem_am_lv10")).LifeSteal > 0);
        Assert.True(GemData.GetSocketedBonuses(Equipped("gem_linh_lv10")).RagePerHit > 0);
        Assert.True(GemData.GetSocketedBonuses(Equipped("gem_phuc_nguyen_lv10")).Regen > 0);
        Assert.True(GemData.GetSocketedBonuses(Equipped("gem_vu_tru_lv10")).Dodge > 0);
    }

    [Fact]
    public void ThirtySocketedGemsRespectCapsAndConversionChanceDoesNotStack()
    {
        GemBonuses Stack(string theme)
        {
            var p = new Player { Realm = 9 };
            var slots = new Dictionary<string, GemSlotData>();
            foreach (var slot in GemData.EquipSlots)
            {
                p.Equipment.Add(new() { Slot = slot, ItemId = GameData.AllItems.First(i => i.Slot == slot && i.Rarity == "supreme").Id });
                slots[slot] = new() { Sockets = Enumerable.Repeat(GemProgression.Id(theme, 9), 3).ToList() };
            }
            GemData.SetSlots(p, slots);
            return GemData.GetSocketedBonuses(p);
        }
        Assert.Equal(600, Stack("hoa").CritDamage);
        Assert.Equal(232.5, Stack("loi").Crit); Assert.Equal(30, Stack("loi").ExtraHit);
        Assert.Equal(50, Stack("bang").Block);
        Assert.Equal(40, Stack("tho").Absorb); Assert.Equal(23, Stack("tho").AbsorbHealChance);
        Assert.Equal(60, Stack("phong").MoveSpeed); Assert.Equal(100, Stack("phong").AttackSpeed);
        Assert.Equal(465, Stack("quang").Accuracy);
        Assert.Equal(10, Stack("am").LifeSteal); Assert.Equal(1, Stack("am").ExtraAttacks);
        Assert.Equal(7, Stack("linh").RagePerHit); Assert.Equal(10, Stack("phuc_nguyen").Regen);
        Assert.Equal(150, Stack("vu_tru").Dodge);
    }

    [Fact]
    public void BlockAbsorptionConversionAndLifeStealUseActualDamageAndCannotRevive()
    {
        var bonuses = new GemBonuses(Block: 50, Absorb: 40, AbsorbHealChance: 50, LifeSteal: 20);
        var blocked = GemCombat.Mitigate(1000, bonuses, new FixedRandom(0));
        Assert.True(blocked.Blocked); Assert.Equal(300, blocked.Damage); Assert.Equal(200, blocked.Absorbed); Assert.Equal(200, blocked.Healing);
        var missedProc = GemCombat.Mitigate(1000, bonuses, new FixedRandom(.99));
        Assert.False(missedProc.Blocked); Assert.Equal(600, missedProc.Damage); Assert.Equal(0, missedProc.Healing);
        Assert.Equal(0, GemCombat.Mitigate(0, bonuses, new FixedRandom(0)).Healing);
        Assert.Equal(10, GemCombat.LifeSteal(10000, 100, bonuses));
        Assert.Equal(0, GemCombat.Heal(0, 1000, 100)); Assert.Equal(10, GemCombat.Heal(990, 1000, 100));
    }

    [Fact]
    public void AccuracyOffsetsDodgeAndAttackSpeedAccumulatesSeparatelyFromGuaranteedAttacks()
    {
        Assert.False(GemCombat.Hits(new FixedRandom(.99), 0, 10));
        Assert.True(GemCombat.Hits(new FixedRandom(.99), 10, 10));
        var speed = new GemCombatState(new(AttackSpeed: 25));
        Assert.Equal(5, Enumerable.Range(0, 20).Sum(_ => speed.BonusAttacks()));
        var both = new GemCombatState(new(AttackSpeed: 100, ExtraAttacks: 1));
        Assert.All(Enumerable.Range(0, 20), _ => Assert.Equal(2, both.BonusAttacks()));
        Assert.Equal(SkillData.RagePerHit + 4, GemCombat.RagePerHit(new(RagePerHit: 4)));
    }

    [Fact]
    public void ArenaSnapshotCarriesAllGemEffectsAndOldSnapshotsStillWork()
    {
        var player = Equipped("gem_am_lv10");
        var fighter = GameService.PvpFighterOf(player);
        Assert.Equal(1, fighter.Gems!.ExtraAttacks);
        Assert.True(fighter.Gems.LifeSteal > 0);
        var restored = JsonSerializer.Deserialize<PvpFighter>(JsonSerializer.Serialize(fighter))!;
        Assert.Equal(fighter.Gems, restored.Gems);
        var old = PvpFighter.FromStats("old", 0, new() { ["atk"] = 10, ["hp"] = 1000, ["def"] = 1 });
        Assert.Null(old.Gems);
        var result = PvpCombat.Run(restored, old, 1234);
        Assert.NotEmpty(result.Actions);
        Assert.Contains(result.Actions, action => action.ByA && action.Extra);
    }

    [Fact]
    public void PveExtraAttackIsRealAndBattleRemainsDeterministic()
    {
        var player = Equipped("gem_am_lv10");
        player.Realm = 0;
        player.Equipment[0].ItemId = GameData.AllItems.First(i => i.Slot == "weapon" && i.Rarity == "supreme" && i.MinRealm == 0).Id;
        var trip = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);
        var first = GameService.BattleAt(player, 1, trip);
        var second = GameService.BattleAt(player, 1, trip);
        Assert.Contains(first.ExtraDamage!, damage => damage > 0);
        Assert.Equal(first.HeroHpLeft, second.HeroHpLeft);
        Assert.Equal(first.MonHpLeft, second.MonHpLeft);
        Assert.Equal(first.ExtraDamage, second.ExtraDamage);
    }

    [Fact]
    public void PvpAbsorptionReducesActualHpLossAndLifeStealActuallyHeals()
    {
        var a = PvpFighter.FromStats("a", 0, new() { ["atk"] = 100, ["hp"] = 10000, ["def"] = 0 });
        var d = a with { Name = "d" };
        var normal = PvpCombat.Run(a, d, 77);
        var absorbed = PvpCombat.Run(a, d with { Gems = new(Absorb: 40) }, 77);
        var normalLoss = normal.D.Hp - normal.Actions[0].HpD;
        var absorbedLoss = absorbed.D.Hp - absorbed.Actions[0].HpD;
        Assert.True(normalLoss > 0);
        Assert.Equal(normalLoss - CombatNumber.Round(normalLoss * .4), absorbedLoss);
        var leech = PvpCombat.Run(a, d with { Gems = new(LifeSteal: 20) }, 77);
        Assert.Contains(leech.Actions, action => !action.ByA && action.Heal > 0);
        Assert.Equal(.7, GameService.PvpFighterOf(Equipped("gem_hoa_lv10")).CritMult, 8);
    }
}
