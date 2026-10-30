namespace VanDao.Game;

/// <summary>Shared opposed percentage rules. Ratings remain uncapped; a roll produces at most one proc.</summary>
public static class CombatRatingRules
{
    public static double Opposed(double own, double resistance)
    {
        if (!double.IsFinite(own) || !double.IsFinite(resistance))
            throw new ArgumentOutOfRangeException(nameof(own), "Ratings must be finite.");
        return Math.Max(0, Math.Max(0, own) - Math.Max(0, resistance));
    }

    public static bool Roll(Random random, double own, double resistance = 0)
    {
        var chance = Opposed(own, resistance);
        return chance > 0 && (chance >= 100 || random.NextDouble() * 100 < chance);
    }

    public static bool Counter(Random random, double rating, double resistance, long actualDamage, long remainingHp)
        => actualDamage > 0 && remainingHp > 0 && Roll(random, rating, resistance);

    public static long CounterDamage(long attack, long defense, double reduction = 0)
        => attack <= 0 ? 0 : CombatNumber.Round(Math.Max(1d, (double)attack - Math.Max(0, defense))
            * (1 - Math.Clamp(reduction, 0, 100) / 100));
}
