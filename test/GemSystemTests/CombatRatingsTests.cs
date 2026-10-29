using System.Text.Json;
using VanDao.Game;

namespace GemSystemTests;

public class CombatRatingsTests
{
    [Fact]
    public void EquipmentRatingsAggregateWithoutLosingLegacyDodgeOrCappingTotals()
    {
        var first = new ItemDef("a", "A", "equip", "", "", CritPct: 350, SpeedPct: 120,
            CritResistPct: 250, AccuracyPct: 150, ComboPct: 100, ComboResistPct: 80, ReflectPct: 60, ReflectResistPct: 40);
        var total = CombatRatings.From(first).Add(CombatRatings.From(first));
        Assert.Equal(new CombatRatings(700, 500, 300, 240, 200, 160, 120, 80), total);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidRatingsAreRejectedAtTheSharedBoundary(double value)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new CombatRatings(Crit: value).Validate());

    [Fact]
    public void CombatAndMonsterAdaptersKeepEveryRating()
    {
        var stats = new CombatStats(1, 1, 1, Crit: 10, CritResist: 20, Accuracy: 30, Dodge: 40,
            Combo: 50, ComboResist: 60, Reflect: 70, ReflectResist: 80);
        var monster = new MonsterDef("M", "", 1, 1, 1, Crit: 10, CritResist: 20, Accuracy: 30, Dodge: 40,
            Combo: 50, ComboResist: 60, Reflect: 70, ReflectResist: 80);
        Assert.Equal(new CombatRatings(10, 20, 30, 40, 50, 60, 70, 80), CombatRatings.From(stats));
        Assert.Equal(CombatRatings.From(stats), CombatRatings.From(monster));
    }

    [Theory]
    [InlineData(typeof(ItemDef), "CritResistPct")]
    [InlineData(typeof(ItemDef), "AccuracyPct")]
    [InlineData(typeof(ItemDef), "ComboPct")]
    [InlineData(typeof(ItemDef), "ComboResistPct")]
    [InlineData(typeof(ItemDef), "ReflectPct")]
    [InlineData(typeof(ItemDef), "ReflectResistPct")]
    [InlineData(typeof(CombatStats), "CritResist")]
    [InlineData(typeof(CombatStats), "ComboResist")]
    [InlineData(typeof(CombatStats), "Reflect")]
    [InlineData(typeof(CombatStats), "ReflectResist")]
    [InlineData(typeof(MonsterDef), "Crit")]
    [InlineData(typeof(MonsterDef), "CritResist")]
    [InlineData(typeof(MonsterDef), "Accuracy")]
    [InlineData(typeof(MonsterDef), "Dodge")]
    [InlineData(typeof(MonsterDef), "ComboResist")]
    [InlineData(typeof(MonsterDef), "Reflect")]
    [InlineData(typeof(MonsterDef), "ReflectResist")]
    [InlineData(typeof(WaveInfoDto), "CritResist")]
    [InlineData(typeof(WaveInfoDto), "Accuracy")]
    [InlineData(typeof(WaveInfoDto), "Combo")]
    [InlineData(typeof(WaveInfoDto), "ComboResist")]
    [InlineData(typeof(WaveInfoDto), "Reflect")]
    [InlineData(typeof(WaveInfoDto), "ReflectResist")]
    [InlineData(typeof(StatSourceDto), "CritResist")]
    [InlineData(typeof(StatSourceDto), "Combo")]
    [InlineData(typeof(StatSourceDto), "ComboResist")]
    [InlineData(typeof(StatSourceDto), "Reflect")]
    [InlineData(typeof(StatSourceDto), "ReflectResist")]
    public void OldPayloadDefaultsNewRatingsToZeroAndRoundTripsUncappedValues(Type model, string field)
    {
        var property = model.GetProperty(field);
        Assert.NotNull(property);
        var old = JsonSerializer.Deserialize("{}", model)!;
        Assert.Equal(0d, property!.GetValue(old));
        var upgraded = JsonSerializer.Deserialize("{\"" + field + "\":1000.5}", model)!;
        Assert.Equal(1000.5, property.GetValue(upgraded));
        var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(upgraded, model), model)!;
        Assert.Equal(1000.5, property.GetValue(restored));
    }
}
