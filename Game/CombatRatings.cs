namespace VanDao.Game;

/// <summary>Uncapped, nonnegative percentage ratings. Reflect is counterattack chance, not a damage multiplier.</summary>
public record CombatRatings(double Crit = 0, double CritResist = 0, double Accuracy = 0, double Dodge = 0,
    double Combo = 0, double ComboResist = 0, double Reflect = 0, double ReflectResist = 0)
{
    public CombatRatings Validate()
    {
        foreach (var (name, value) in new[] {
            (nameof(Crit), Crit), (nameof(CritResist), CritResist), (nameof(Accuracy), Accuracy),
            (nameof(Dodge), Dodge), (nameof(Combo), Combo), (nameof(ComboResist), ComboResist),
            (nameof(Reflect), Reflect), (nameof(ReflectResist), ReflectResist) })
            if (!double.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(name, value, "Rating must be finite and nonnegative.");
        return this;
    }

    public CombatRatings Add(CombatRatings other)
    {
        Validate();
        other.Validate();
        return new CombatRatings(Crit + other.Crit, CritResist + other.CritResist,
            Accuracy + other.Accuracy, Dodge + other.Dodge, Combo + other.Combo,
            ComboResist + other.ComboResist, Reflect + other.Reflect, ReflectResist + other.ReflectResist).Validate();
    }

    public static CombatRatings From(ItemDef item) => new CombatRatings(item.CritPct, item.CritResistPct,
        item.AccuracyPct, item.SpeedPct, item.ComboPct, item.ComboResistPct, item.ReflectPct, item.ReflectResistPct).Validate();

    public static CombatRatings From(CombatStats stats) => new CombatRatings(stats.Crit, stats.CritResist,
        stats.Accuracy, stats.Dodge, stats.Combo, stats.ComboResist, stats.Reflect, stats.ReflectResist).Validate();

    public static CombatRatings From(MonsterDef monster) => new CombatRatings(monster.Crit, monster.CritResist,
        monster.Accuracy, monster.Dodge, monster.Combo, monster.ComboResist, monster.Reflect, monster.ReflectResist).Validate();
}
