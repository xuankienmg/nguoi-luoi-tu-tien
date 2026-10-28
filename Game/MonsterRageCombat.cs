namespace VanDao.Game;

/// <summary>Authoritative counter state for one monster wave. -1 means no rage bar.</summary>
public record MonsterRageTurnDto(int Before, int After, bool Ultimate, string SkillId, long Damage = 0);

public sealed class MonsterRageCombat(string tier, string image)
{
    public const int Max = 100, BossGain = 25, EliteGain = 20;
    public const double BossMultiplier = 1.7, EliteMultiplier = 1.4;
    public int Rage { get; private set; } = tier is "boss" or "elite" ? 0 : -1;
    public bool Ultimate { get; private set; }
    public string SkillId { get; } = Family(image);

    // Scale the base counter after defense; scaling raw attack would bypass high defense.
    public static long ScaleCounter(long damage, double multiplier) => CombatNumber.Round(damage * multiplier);

    // Called only for a living, uncontrolled monster actually attempting a counter.
    public double CounterMultiplier()
    {
        Ultimate = Rage == Max;
        if (Rage < 0) return 1;
        if (Ultimate) { Rage = 0; return tier == "boss" ? BossMultiplier : EliteMultiplier; }
        Rage = Math.Min(Max, Rage + (tier == "boss" ? BossGain : EliteGain));
        return 1;
    }

    public static string Family(string image)
    {
        var key = image.ToLowerInvariant();
        if (new[] { "kiem", "giap_binh", "cot_tuong", "dao_ngot" }.Any(key.Contains)) return "weapon";
        if (new[] { "xa", "mang", "giao", "phuong", "am_ma", "ma_dang", "chu_nu", "cuu_vi", "cot_long", "thao_thiet" }.Any(key.Contains)) return "magic";
        return "beast";
    }
}
