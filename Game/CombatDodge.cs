namespace VanDao.Game;

/// <summary>Né là sự kiện thật, không phải hệ số giảm sát thương kỳ vọng.</summary>
public static class CombatDodge
{
    public static double Chance(double speed, double gear, double skill) =>
        Math.Max(0, speed + gear + skill);
    public static bool Roll(Random rng, double chance) => CombatRatingRules.Roll(rng, chance);
}
