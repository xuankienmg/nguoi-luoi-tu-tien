using System.Text.Json;
using System.Text.Json.Nodes;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class GemCombatRatingIntegrationTests
{
    private static Player Socketed(string theme, int quality = 9)
    {
        var p = new Player { Id = Guid.Parse("50000000-0000-0000-0000-000000000003"), Realm = 9 };
        var id = $"gem_{theme}_lv10";
        var slots = new Dictionary<string, GemSlotData>();
        foreach (var slot in GemData.EquipSlots)
        {
            p.Equipment.Add(new() { Slot = slot, ItemId = GameData.AllItems.First(i => i.Slot == slot && i.MinRealm == 9 && i.Rarity == GameData.RarityOrder[quality]).Id });
            if (GemData.CanSocket(id, slot))
                slots[slot] = new() { Sockets = Enumerable.Repeat(id, 3).ToList() };
        }
        Assert.Equal(15, slots.Values.Sum(s => s.Sockets.Count));
        GemData.SetSlots(p, slots);
        return p;
    }

    [Theory]
    [InlineData("tho")]
    [InlineData("vu_tru")]
    [InlineData("loi")]
    [InlineData("quang")]
    public void ArenaSnapshotKeepsCanonicalTotalsThroughJsonAndSanitize(string theme)
    {
        var p = Socketed(theme, 8);
        Assert.Equal(8, DivineAbility.For(p).Tier);
        var stats = GameService.Stats(p);
        var fighter = GameService.PvpFighterOf(p);
        var expected = CombatRatings.From(stats);
        Assert.Equal(expected, fighter.Ratings);
        var restored = JsonSerializer.Deserialize<PvpFighter>(JsonSerializer.Serialize(fighter))!;
        Assert.Equal(expected, restored.Ratings);
        Assert.Equal(expected, ArenaService.Sanitize(restored, p.Name, p.Realm, null).Ratings);
        var authoritative = new Dictionary<string, double> {
            ["crit"] = stats.Crit, ["crit_resist"] = stats.CritResist,
            ["accuracy"] = stats.Accuracy, ["dodge"] = stats.Dodge,
            ["combo"] = stats.Combo, ["combo_resist"] = stats.ComboResist,
            ["reflect"] = stats.Reflect, ["reflect_resist"] = stats.ReflectResist
        };
        Assert.Equal(expected, ArenaService.Sanitize(restored with { ComboResist = 1, ReflectResist = 1 }, p.Name, p.Realm, authoritative).Ratings);
        Assert.Equal(450, theme switch {
            "tho" => fighter.Gems!.ComboResist,
            "vu_tru" => fighter.Gems!.ReflectResist,
            "loi" => fighter.Gems!.ExtraHit,
            _ => fighter.Gems!.Accuracy
        });
    }

    [Fact]
    public void OldArenaJsonWithoutNewResistancesRemainsReadable()
    {
        var old = PvpFighter.FromStats("old", 0, new() { ["atk"] = 100, ["hp"] = 10000 })
            with { ExtraHit = 350, Divine = 8, Gems = new(AttackSpeed: 25, Accuracy: 180) };
        var json = JsonNode.Parse(JsonSerializer.Serialize(old))!.AsObject();
        foreach (var field in new[] { "ComboResist", "ReflectResist", "ComboRating", "Accuracy" }) json.Remove(field);
        var gems = json["Gems"]!.AsObject();
        gems.Remove("ComboResist"); gems.Remove("ReflectResist");
        var restored = JsonSerializer.Deserialize<PvpFighter>(json.ToJsonString())!;
        Assert.Equal(0, restored.ComboResist); Assert.Equal(0, restored.ReflectResist);
        Assert.Equal(0, restored.Gems!.ComboResist); Assert.Equal(0, restored.Gems.ReflectResist);
        Assert.Equal(395, restored.Combo);
        var sanitized = ArenaService.Sanitize(restored, "old", 0, null);
        Assert.Equal(395, sanitized.Combo); Assert.Equal(180, sanitized.HitAccuracy);
        Assert.Equal(0, sanitized.ComboResist); Assert.Equal(0, sanitized.ReflectResist);
    }

    private static PvpFighter Plain(string name, long attack) => PvpFighter.FromStats(name, 0,
        new() { ["atk"] = attack, ["hp"] = 10000 }) with { BasicPct = 100, BasicCrit = 0 };

    [Fact]
    public void DarkGemCounterIsSingleAndDoesNotCritLeechChargeRageOrChain()
    {
        var am = GemData.GetSocketedBonuses(Socketed("am"));
        Assert.Equal(450, am.Reflect); Assert.Equal(1, am.ExtraAttacks); Assert.True(am.LifeSteal > 0);
        var a = Plain("A", 100) with { Reflect = 1000 };
        var d = Plain("D", 200) with { Reflect = am.Reflect, Gems = am, Crit = 1000, CritMult = 5 };
        var result = PvpCombat.Run(a, d, 42);
        var first = result.Actions[0];
        Assert.Equal(100, first.Direct);
        Assert.Equal(200, first.Reflect);
        Assert.Equal(29800, first.HpA);
        Assert.Equal(29900, first.HpD); // No leech or recursive counter by either side.
        Assert.Equal(25, first.RageA); Assert.Equal(0, first.RageD);
        Assert.False(first.Extra);
        var next = result.Actions[1];
        Assert.False(next.ByA); Assert.True(next.Extra); // Am level-10 bonus belongs to its normal turn.
        Assert.Equal(50, next.RageD);
        var resistance = GemData.GetSocketedBonuses(Socketed("vu_tru"));
        Assert.Equal(450, resistance.ReflectResist);
        var blocked = PvpCombat.Run(a with { ReflectResist = resistance.ReflectResist, Gems = resistance }, d, 42).Actions[0];
        Assert.Equal(0, blocked.Reflect);
        Assert.Equal(30000, blocked.HpA);
    }

    [Fact]
    public void GemResistanceCancelsEnemyComboAndGemComboAbove100AddsOnlyOneHit()
    {
        var loi = GemData.GetSocketedBonuses(Socketed("loi"));
        var tho = GemData.GetSocketedBonuses(Socketed("tho"));
        var a = Plain("A", 100) with { ComboRating = loi.ExtraHit, Gems = loi };
        var d = Plain("D", 200) with { ComboResist = tho.ComboResist, Gems = tho };
        Assert.False(PvpCombat.Run(a, d, 42).Actions[0].Extra);
        var proc = PvpCombat.Run(a, d with { ComboResist = 200, Gems = null }, 42).Actions[0];
        Assert.True(proc.Extra);
        Assert.Equal(proc.Basic, proc.ExtraDmg);
        Assert.Equal(25, proc.RageA);
    }
}
