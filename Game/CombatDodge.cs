namespace VanDao.Game;

/// <summary>Né là sự kiện thật, không phải hệ số giảm sát thương kỳ vọng.</summary>
public static class CombatDodge
{
    public static double Chance(double speed, double gear, double skill) =>
        Math.Max(0, speed + gear + skill);
    public static bool Roll(Random rng, double chance) => chance > 0 && rng.NextDouble() * 100 < Math.Min(100, chance);
}
