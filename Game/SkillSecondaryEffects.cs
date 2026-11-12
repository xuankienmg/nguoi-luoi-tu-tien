namespace VanDao.Game;

/// <summary>One source of truth for combat rolls and client skill descriptions.</summary>
public record SkillSecondaryDef(string Kind, string Name, int ChancePerLv, int Duration, double Value, string Target, string Description)
{
    // cân bằng 11/2026: mỗi cấp bị động cùng hệ +0.5%, tổng (chủ động + bị động) tối đa 50%
    public double PassivePerLv => SkillSecondaryEffects.PassivePerLv;
    // cân bằng 11/2026: khống (choáng · tê liệt · đóng băng) tối đa 25%, hiệu ứng khác 50%
    public double MaxChance => Kind is "stun" or "paralyze" or "freeze" ? SkillSecondaryEffects.ControlMaxChance : SkillSecondaryEffects.MaxChance;
}

public static class SkillSecondaryEffects
{
    private static readonly IReadOnlyDictionary<string, SkillSecondaryDef> Definitions = new Dictionary<string, SkillSecondaryDef>
    {
        // cân bằng 11/2026: chủ động 10 → 1%/cấp (Lv5 = 5%); phần còn lại đến từ bị động cùng hệ
        ["hoa"] = new("burn", "Thiêu đốt", 1, 2, 5, "monster", "Thiêu đốt 5% máu tối đa của quái mỗi lượt trong 2 lượt, gồm lượt kích hoạt. Không cộng dồn; kích hoạt lại làm mới thời gian."),
        ["thuy"] = new("weaken", "Suy yếu", 1, 2, 20, "monster", "Giảm 20% công quái trong 2 lượt, gồm lượt thi triển. Không cộng dồn; kích hoạt lại làm mới thời gian."),
        // 11/2026: Mộc đổi Hồi phục → Nhiễm độc (dùng chung nhịp sát thương theo lượt với Thiêu đốt)
        ["moc"] = new("poison", "Nhiễm độc", 1, CombatBurn.PoisonDuration, CombatBurn.PoisonPercent, "monster", "Nhiễm độc, mất 3% máu tối đa mỗi lượt trong 4 lượt, gồm lượt kích hoạt. Không cộng dồn; kích hoạt lại làm mới thời gian."),
        ["tho"] = new("stun", "Choáng", 1, 1, 0, "monster", "Gây choáng, khiến quái mất lượt phản công hiện tại."),
        ["loi"] = new("paralyze", "Tê liệt", 1, 1, 0, "monster", "Gây tê liệt, khiến quái mất lượt phản công hiện tại."),
        ["bang"] = new("freeze", "Đóng băng", 1, 1, 10, "monster", "Đóng băng, khiến quái mất lượt phản công hiện tại. Lượt sau quái nhận thêm 10% sát thương nếu băng chưa bị hiệu ứng khác thay thế."),
        // 11/2026: Phong hộ tăng cả né tránh lẫn chính xác
        ["gio"] = new("wind", "Phong hộ", 1, 2, 20, "hero", "Tăng 20 điểm né tránh và 20 điểm chính xác trong 2 lượt, gồm lượt thi triển. Không cộng dồn, kích hoạt lại làm mới thời gian.")
    };
    public const double PassivePerLv = 0.5, MaxChance = 50, ControlMaxChance = 25;

    // cân bằng 11/2026: chiêu chủ động (có sát thương) luôn kèm hồi máu theo hệ, ngoài hiệu ứng phụ
    public const double WoodCastHealPct = 5;                          // Mộc: hồi 5% máu tối đa cuối lượt tung chiêu nếu còn sống
    public const double WaterLeechPct = 10, WaterLeechCapPct = 8;     // Thủy: hút 10% sát thương chiêu gây ra, tối đa 8% máu tối đa mỗi lần
    /// <summary>Hồi máu kèm chiêu chủ động: (hồi cuối lượt, hút máu ngay). Chiêu không trúng thì Thủy không hút.</summary>
    public static (decimal endHeal, decimal leech) CastHeal(SkillDef skill, decimal skillDamage, decimal maxHp) =>
        skill.Tree != "active" || skill.DmgBase <= 0 ? (0, 0)
        : skill.Element == "moc" ? (CombatNumber.Scale(maxHp, WoodCastHealPct / 100), 0)
        : skill.Element == "thuy" ? (0, Math.Min(CombatNumber.Scale(skillDamage, WaterLeechPct / 100), CombatNumber.Scale(maxHp, WaterLeechCapPct / 100)))
        : (0, 0);

    /// <summary>Tỉ lệ cộng thêm từ bị động theo hệ: mỗi cấp bị động +0.5%.</summary>
    public static Dictionary<string, double> PassiveBonuses(IReadOnlyDictionary<string, int> levels)
    {
        var bonus = new Dictionary<string, double>();
        foreach (var (id, lv) in levels)
            if (SkillData.ById.TryGetValue(id, out var s) && s.Tree == "passive" && lv > 0)
                bonus[s.Element] = bonus.GetValueOrDefault(s.Element) + PassivePerLv * Math.Clamp(lv, 0, s.MaxLv);
        return bonus;
    }

    public static SkillSecondaryDef? For(SkillDef skill) => skill.Tree == "active" && skill.DmgBase > 0
        ? Definitions.GetValueOrDefault(skill.Element) : null;
    public static bool DebuffEnabled(string activeManual, string element) => activeManual == element;

    // cân bằng 11/2026: đòn thường chính gây hiệu ứng bất lợi cùng hệ với ½ tỉ lệ chiêu chủ động tốt nhất cùng hệ (tối đa 25%); liên kích không gây
    public const double BasicChanceRate = 0.5;
    /// <summary>Hiệu ứng bất lợi (đích là quái) của đòn thường theo hệ; hiệu ứng lên tu sĩ (Phong hộ) không tính.</summary>
    public static SkillSecondaryDef? ForBasic(SkillDef basic) => basic.Tree == "basic" && basic.DmgBase > 0
        && Definitions.GetValueOrDefault(basic.Element) is { Target: "monster" } effect ? effect : null;
    /// <summary>Tỉ lệ (%) đòn thường gây hiệu ứng: ½ tỉ lệ chiêu chủ động cùng hệ cao nhất theo cấp đã học + bị động; chưa học chiêu thì chỉ phần bị động.</summary>
    public static double BasicChance(SkillDef basic, IReadOnlyDictionary<string, int> levels, string activeManual, IReadOnlyDictionary<string, double>? passive = null)
    {
        if (ForBasic(basic) is null || !DebuffEnabled(activeManual, basic.Element)) return 0;
        var best = SkillData.Skills.Where(s => s.Element == basic.Element && s.SecondaryEffect is not null)
            .Select(s => Chance(s, levels.GetValueOrDefault(s.Id), activeManual, passive)).DefaultIfEmpty(0).Max();
        return best * BasicChanceRate;
    }
    public static double Chance(SkillDef skill, int level, string? activeManual = null, IReadOnlyDictionary<string, double>? passive = null) => skill.SecondaryEffect is { } effect
        && (activeManual is null || effect.Target != "monster" || DebuffEnabled(activeManual, skill.Element))
        ? Math.Min(effect.MaxChance, effect.ChancePerLv * Math.Clamp(level, 0, skill.MaxLv) + (passive?.GetValueOrDefault(skill.Element) ?? 0)) : 0;
    public static bool TargetSurvives(decimal hp, decimal equipmentDamage, double reduction, decimal otherDamage) =>
        hp > (reduction < 1 ? Math.Round(equipmentDamage * CombatNumber.Factor(reduction)) : equipmentDamage) + otherDamage;
}
