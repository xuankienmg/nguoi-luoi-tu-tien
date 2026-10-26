namespace VanDao.Game;

public record HellSkillDef(string Id, string Name, string Description);
public record MonsterSkillEventDto(int Turn, int Wave, string SkillId, long Value = 0, int Duration = 0);

public static class HellMonsterSkills
{
    public static readonly HellSkillDef[] Definitions = [
        new("thieu_hon", "Thiêu Hồn", "Mỗi đòn thứ 3 trúng đích gây bỏng 5% công quái mỗi lượt trong 2 lượt; không cộng dồn."),
        new("dung_giap", "Dung Giáp", "Đầu mỗi đợt có khiên bằng 10% máu quái, hết hạn sau 2 lượt."),
        new("xieng_nguc", "Xiềng Ngục", "Đầu mỗi đợt giảm 20% sát thương một đòn đánh của tu sĩ; không khóa hành động."),
        new("nham_boc", "Nham Bộc", "Mỗi 4 lượt tích lực 1 lượt rồi phản công bằng 150% công; bị hủy khi quái chết hoặc lỡ lượt tung.")
    ];
    public static HellSkillDef[] For(MonsterDef monster, int chapter)
    {
        string[] ids = monster.Tier == "boss" ? chapter % 2 == 0
            ? ["dung_giap", "thieu_hon", "nham_boc"] : ["xieng_nguc", "thieu_hon", "nham_boc"]
            : monster.Tier == "elite" ? monster.Image.Contains("cot_tuong") ? ["dung_giap", "xieng_nguc"]
                : monster.Image.Contains("cot_long") ? ["thieu_hon", "nham_boc"]
                : monster.Image.Contains("bach_cot_tinh") ? ["thieu_hon", "xieng_nguc"] : ["xieng_nguc", "nham_boc"]
            : monster.Image.Contains("thach_nhan") || monster.Image.Contains("nguu_dau") ? ["dung_giap"]
            : monster.Image.Contains("thach_tich") || monster.Image.Contains("hoa_diem_tieu_yeu") ? ["nham_boc"]
            : monster.Image.Contains("giap_binh") || monster.Image.Contains("ma_dien") || monster.Image.Contains("hac_vo_thuong") ? ["xieng_nguc"]
            : ["thieu_hon"];
        return ids.Select(id => Definitions.Single(d => d.Id == id)).ToArray();
    }
}

/// <summary>State is owned by one wave of one battle; never shared between previews or offline trips.</summary>
public sealed class HellMonsterCombat(MonsterDef monster, int chapter, int wave)
{
    private readonly HashSet<string> skills = HellMonsterSkills.For(monster, chapter).Select(s => s.Id).ToHashSet();
    private long armor;
    private int burnLeft, landedHits, chargedAt = -2;
    private bool chained;
    public List<MonsterSkillEventDto> Events { get; } = [];
    public void BeginTurn(int turn, int waveTurn)
    {
        if (waveTurn == 0) {
            armor = skills.Contains("dung_giap") ? CombatNumber.Round(monster.Hp * .10) : 0;
            if (armor > 0) Events.Add(new(turn, wave, "dung_giap", armor, 2));
            chained = skills.Contains("xieng_nguc");
            if (chained) Events.Add(new(turn, wave, "xieng_nguc", 20, 1));
        }
        if (waveTurn >= 2) armor = 0;
        if (waveTurn > chargedAt + 1) chargedAt = -2;
    }
    public long OwnerDamage(long damage)
    {
        if (!chained || damage <= 0) return damage;
        chained = false;
        return Math.Max(1, CombatNumber.Round(damage * .80));
    }
    public long Absorb(long damage)
    {
        var absorbed = Math.Min(armor, damage); armor -= absorbed;
        return damage - absorbed;
    }
    public double CounterMultiplier(int turn, int waveTurn)
    {
        if (!skills.Contains("nham_boc")) return 1;
        if (chargedAt == waveTurn - 1) {
            chargedAt = -2; Events.Add(new(turn, wave, "nham_boc", 150)); return 1.5;
        }
        if (waveTurn % 4 == 2) {
            chargedAt = waveTurn; Events.Add(new(turn, wave, "nham_boc_charge", 0, 1));
        }
        return 1;
    }
    public void CounterLanded()
    {
        if (skills.Contains("thieu_hon") && ++landedHits % 3 == 0) burnLeft = 2;
    }
    public long TickBurn(int turn)
    {
        if (burnLeft <= 0) return 0;
        var damage = Math.Max(1, CombatNumber.Round(monster.Atk * .05));
        Events.Add(new(turn, wave, "thieu_hon", damage, burnLeft--));
        return damage;
    }
}
