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

    /// <summary>Tổng 8 chỉ số đối kháng (điểm %), dùng cho lực chiến.</summary>
    public double Total => new[] { Crit, CritResist, Accuracy, Dodge, Combo, ComboResist, Reflect, ReflectResist }
        .Sum(v => double.IsFinite(v) ? Math.Max(0, v) : 0);

    /// <summary>Lực chiến: Công×2 + Thủ×3 + Máu×0,4 + tổng 8 chỉ số đối kháng×4. Cùng công thức với inventory.js → power.</summary>
    public static long Power(long atk, long def, long hp, CombatRatings ratings) =>
        (long)Math.Round(atk * 2.0 + def * 3.0 + hp * .4 + ratings.Total * 4);

    public static CombatRatings From(ItemDef item) => new CombatRatings(item.CritPct, item.CritResistPct,
        item.AccuracyPct, item.SpeedPct, item.ComboPct, item.ComboResistPct, item.ReflectPct, item.ReflectResistPct).Validate();

    public static CombatRatings From(CombatStats stats) => new CombatRatings(stats.Crit, stats.CritResist,
        stats.Accuracy, stats.Dodge, stats.Combo, stats.ComboResist, stats.Reflect, stats.ReflectResist).Validate();

    public static CombatRatings From(MonsterDef monster) => new CombatRatings(monster.Crit, monster.CritResist,
        monster.Accuracy, monster.Dodge, monster.Combo, monster.ComboResist, monster.Reflect, monster.ReflectResist).Validate();
}
