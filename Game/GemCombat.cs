namespace VanDao.Game;

public record GemMitigation(long Damage, long Healing, long Absorbed, bool Blocked);

/// <summary>Shared rules for PvE and PvP; all healing uses actual damage and cannot revive a defeated fighter.</summary>
public static class GemCombat
{
    // Applies after general defense, before shields; PvP never calls this rule.
    public static long ReduceMonsterDamage(long incoming, GemBonuses bonuses, string tier)
    {
        var reduction = tier switch { "elite" => bonuses.EliteReduction, "boss" => bonuses.BossReduction, _ => 0 };
        return CombatNumber.Round(Math.Max(0, incoming) * (1 - Math.Clamp(reduction, 0, 30) / 100));
    }

    public static bool Hits(Random random, double accuracy, double dodge)
    {
        var chance = Math.Max(0, 100 - CombatRatingRules.Opposed(dodge, accuracy));
        return CombatRatingRules.Roll(random, chance);
    }

    public static GemMitigation Mitigate(long incoming, GemBonuses bonuses, Random random)
    {
        if (incoming <= 0) return new(0, 0, 0, false);
        var blocked = bonuses.Block > 0 && random.NextDouble() * 100 < Math.Min(50, bonuses.Block);
        var damage = blocked ? CombatNumber.Round(incoming * .5) : incoming;
        var absorbed = CombatNumber.Round(damage * Math.Clamp(bonuses.Absorb, 0, 40) / 100);
        var healing = absorbed > 0 && bonuses.AbsorbHealChance > 0 && random.NextDouble() * 100 < Math.Min(50, bonuses.AbsorbHealChance) ? absorbed : 0;
        return new(Math.Max(0, damage - absorbed), healing, absorbed, blocked);
    }

    public static long LifeSteal(long damage, long targetHpBefore, GemBonuses bonuses) =>
        CombatNumber.Round(Math.Max(0, Math.Min(damage, targetHpBefore)) * Math.Max(0, bonuses.LifeSteal) / 100);

    public static long Heal(long hp, long maxHp, long amount) => hp <= 0 ? 0 : Math.Max(0, Math.Min(amount, maxHp - hp));
    public const int MaxBonusRagePerHit = 7;
    public static int RagePerHit(GemBonuses bonuses) => SkillData.RagePerHit + (int)Math.Clamp(bonuses.RagePerHit, 0, MaxBonusRagePerHit);
}
