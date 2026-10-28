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
        var chance = Math.Clamp(100 + Math.Max(0, accuracy) - dodge, 0, 100);
        return chance >= 100 || random.NextDouble() * 100 < chance;
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
        CombatNumber.Round(Math.Max(0, Math.Min(damage, targetHpBefore)) * Math.Clamp(bonuses.LifeSteal, 0, 10) / 100);

    public static long Heal(long hp, long maxHp, long amount) => hp <= 0 ? 0 : Math.Max(0, Math.Min(amount, maxHp - hp));
    public static int RagePerHit(GemBonuses bonuses) => Math.Min(10, SkillData.RagePerHit + (int)Math.Max(0, bonuses.RagePerHit));
}

public sealed class GemCombatState(GemBonuses bonuses)
{
    private double attackCharge;
    public int BonusAttacks()
    {
        attackCharge += Math.Clamp(bonuses.AttackSpeed, 0, 100);
        var fromSpeed = (int)(attackCharge / 100);
        attackCharge -= fromSpeed * 100;
        return Math.Clamp(bonuses.ExtraAttacks, 0, 1) + fromSpeed;
    }
}
