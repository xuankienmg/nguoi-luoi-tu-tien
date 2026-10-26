namespace VanDao.Game;

/// <summary>One source of truth for combat rolls and client skill descriptions.</summary>
public record SkillSecondaryDef(string Kind, string Name, int ChancePerLv, int Duration, double Value, string Target, string Description);

public static class SkillSecondaryEffects
{
    private static readonly IReadOnlyDictionary<string, SkillSecondaryDef> Definitions = new Dictionary<string, SkillSecondaryDef>
    {
        ["hoa"] = new("burn", "Thiêu đốt", 10, 2, 5, "monster", "Thiêu đốt 5% máu tối đa của quái mỗi lượt trong 2 lượt, gồm lượt kích hoạt. Không cộng dồn; kích hoạt lại làm mới thời gian."),
        ["thuy"] = new("weaken", "Suy yếu", 10, 2, 20, "monster", "Giảm 20% công quái trong 2 lượt, gồm lượt thi triển. Không cộng dồn; kích hoạt lại làm mới thời gian."),
        ["moc"] = new("heal", "Hồi phục", 10, 0, 5, "hero", "Hồi 5% máu tối đa vào cuối lượt thi triển nếu còn sống. Không vượt máu tối đa, không hồi sinh."),
        ["tho"] = new("stun", "Choáng", 10, 1, 0, "monster", "Gây choáng, khiến quái mất lượt phản công hiện tại."),
        ["loi"] = new("paralyze", "Tê liệt", 10, 1, 0, "monster", "Gây tê liệt, khiến quái mất lượt phản công hiện tại."),
        ["bang"] = new("freeze", "Đóng băng", 10, 1, 10, "monster", "Đóng băng, khiến quái mất lượt phản công hiện tại. Lượt sau quái nhận thêm 10% sát thương nếu băng chưa bị hiệu ứng khác thay thế."),
        ["gio"] = new("wind", "Phong hộ", 10, 2, 20, "hero", "Tăng 20 điểm phần trăm né tránh trong 2 lượt, gồm lượt thi triển; tuân giới hạn né tránh. Không cộng dồn, kích hoạt lại làm mới thời gian.")
    };

    public static SkillSecondaryDef? For(SkillDef skill) => skill.Tree == "active" && skill.DmgBase > 0
        ? Definitions.GetValueOrDefault(skill.Element) : null;
    public static bool DebuffEnabled(string activeManual, string element) => activeManual == element;
    public static int Chance(SkillDef skill, int level, string? activeManual = null) => skill.SecondaryEffect is { } effect
        && (activeManual is null || effect.Target != "monster" || DebuffEnabled(activeManual, skill.Element))
        ? effect.ChancePerLv * Math.Clamp(level, 0, skill.MaxLv) : 0;
    public static bool TargetSurvives(int hp, int equipmentDamage, double reduction, int otherDamage) =>
        hp > (reduction < 1 ? Math.Round(equipmentDamage * reduction) : (double)equipmentDamage) + otherDamage;
}
