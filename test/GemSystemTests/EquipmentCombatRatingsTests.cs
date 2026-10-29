using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class EquipmentCombatRatingsTests
{
    [Theory]
    [InlineData("weapon", "CritPct")]
    [InlineData("offhand", "ReflectResistPct")]
    [InlineData("head", "CritResistPct")]
    [InlineData("body", "ReflectPct")]
    [InlineData("hands", "AccuracyPct")]
    [InlineData("legs", "ComboResistPct")]
    [InlineData("feet", "SpeedPct")]
    [InlineData("necklace", "AccuracyPct")]
    [InlineData("charm", "CritResistPct")]
    [InlineData("ring", "ComboPct")]
    public void SlotBudgetScalesByRealmAndQualityExactlyOnce(string slot, string field)
    {
        var seed = new ItemDef("seed", "Seed", "equip", "", "", Slot: slot, MinRealm: 9);
        var item = EquipmentBudget.Apply(seed, 9);
        Assert.Equal(230d, typeof(ItemDef).GetProperty(field)!.GetValue(item));
        Assert.Equal(item, EquipmentBudget.Apply(item, 9));
        var low = EquipmentBudget.Apply(seed with { MinRealm = 0 }, 0);
        Assert.Equal(10d, typeof(ItemDef).GetProperty(field)!.GetValue(low));
    }

    private static Player Equipped()
    {
        var p = new Player { Realm = 9 };
        foreach (var slot in GameData.Realms[9].EquipSlots)
            p.Equipment.Add(new() { Slot = slot, ItemId = GameData.AllItems.First(i => i.Slot == slot && i.MinRealm == 9 && i.Rarity == "supreme").Id });
        return p;
    }

    [Fact]
    public void FullSetAggregatesEightRatingsWithoutCapsOrDoubleCounting()
    {
        var p = Equipped();
        var st = GameService.Stats(p);
        var fx = EquipmentEffects.Calculate(p);
        var gems = GemData.GetSocketedBonuses(p);
        double Effect(string id) => fx.Effects.SingleOrDefault(e => e.Id == id)?.Effective ?? 0;
        // Six armor/offhand slots now carry their former 25-point resistance effect as a basic stat.
        Assert.Equal(610 + Effect("crit_resist") + gems.CritResist, st.CritResist);
        Assert.Equal(460 + gems.Accuracy, st.Accuracy);
        Assert.Equal(230 + Effect("lien_kich") + gems.ExtraHit, st.Combo);
        Assert.Equal(230, st.ComboResist);
        Assert.Equal(230 + Effect("dia_mach") + gems.Reflect, st.Reflect);
        Assert.Equal(230, st.ReflectResist);
        Assert.True(st.Crit > 100);
        Assert.True(st.Dodge > 100);
        var comboEffect = Assert.Single(fx.Effects, e => e.Id == "lien_kich");
        Assert.Null(comboEffect.Cap);
        Assert.Equal(comboEffect.Raw, comboEffect.Effective);
        Assert.Equal(st, GameService.Stats(p));
        var restored = JsonSerializer.Deserialize<Player>(JsonSerializer.Serialize(p))!;
        Assert.Equal(st, GameService.Stats(restored));
    }

    [Fact]
    public void RemovingAndReplacingEquipmentRecalculatesRatings()
    {
        var p = Equipped();
        var before = GameService.Stats(p);
        p.Equipment.RemoveAll(e => e.Slot == "ring");
        Assert.Equal(before.Combo - 230, GameService.Stats(p).Combo);
        p.Equipment.Add(new() { Slot = "ring", ItemId = GameData.AllItems.First(i => i.Slot == "ring" && i.MinRealm == 9 && i.Rarity == "common").Id });
        Assert.Equal(before.Combo - 130, GameService.Stats(p).Combo);
        p.Equipment.Clear();
        var naked = GameService.Stats(p);
        Assert.Equal(0, naked.Accuracy);
        Assert.Equal(0, naked.Combo);
        Assert.Equal(0, naked.ComboResist);
        Assert.Equal(0, naked.Reflect);
        Assert.Equal(0, naked.ReflectResist);
    }

    [Fact]
    public void StarterBootsContributeDodgeEvenBeforeRealmBaseDodgeUnlocks()
    {
        var p = new Player { Realm = 0 };
        p.Equipment.Add(new() { Slot = "feet", ItemId = GameData.AllItems.First(i => i.Slot == "feet" && i.MinRealm == 0 && i.Rarity == "common").Id });
        Assert.Equal(10, GameService.Stats(p).Dodge);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    public async Task StateShowsUncappedTotalsAndSourcesReconcileWithStats(int quality)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var p = Equipped();
        foreach (var equipped in p.Equipment)
            equipped.ItemId = GameData.AllItems.First(i => i.Slot == equipped.Slot && i.MinRealm == 9 && i.Rarity == GameData.RarityOrder[quality]).Id;
        Assert.Equal(quality, DivineAbility.For(p).Tier);
        GemData.SetSlots(p, new() { ["weapon"] = new() { Sockets = new() { "gem_phong_lv10" } } });
        db.Players.Add(p);
        await db.SaveChangesAsync();
        var state = await new GameService(db).BuildStateAsync(p);
        var totals = new (string Id, double Value, Func<StatSourceDto, double> Source)[] {
            ("crit", state.Stats.Crit, s => s.Crit),
            ("crit_resist", state.Stats.CritResist, s => s.CritResist),
            ("accuracy", state.Stats.Accuracy, s => s.Accuracy),
            ("dodge", state.Stats.Dodge, s => s.Dodge),
            ("lien_kich", state.Stats.Combo, s => s.Combo),
            ("combo_resist", state.Stats.ComboResist, s => s.ComboResist),
            ("reflect", state.Stats.Reflect, s => s.Reflect),
            ("reflect_resist", state.Stats.ReflectResist, s => s.ReflectResist)
        };
        foreach (var (id, value, source) in totals)
        {
            var shown = Assert.Single(state.CombatLimits!, e => e.Id == id);
            Assert.Null(shown.Cap);
            Assert.Equal(Math.Round(value, 4), shown.Effective);
            Assert.Equal(value, state.StatSources!.Sum(source), 6);
        }
    }
}
