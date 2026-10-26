using VanDao.Data;

namespace VanDao.Game;

public record CultivationManual(string Item, string Element, string Name, string Color);

/// <summary>Permanent tree access is separate from refundable skill levels.</summary>
public static class CultivationManuals
{
    public const double BossChance = .01;
    public const string Starter = "bi_kip_hoa";
    public static readonly CultivationManual[] All =
    [
        new(Starter, "hoa", "Xích Diễm Phần Thiên Kinh", "#e0521e"),
        new("bi_kip_thuy", "thuy", "Thương Hải Huyền Thủy Kinh", "#3f8fd8"),
        new("bi_kip_moc", "moc", "Thanh Đế Trường Sinh Kinh", "#4f9a5a"),
        new("bi_kip_tho", "tho", "Hậu Thổ Trấn Sơn Kinh", "#b08a4a"),
        new("bi_kip_loi", "loi", "Cửu Tiêu Ngự Lôi Kinh", "#a46ae6"),
        new("bi_kip_bang", "bang", "Huyền Băng Ngưng Thiên Kinh", "#a8dcf0"),
        new("bi_kip_phong", "gio", "Thái Hư Ngự Phong Kinh", "#8fd0b0"),
    ];
    public static CultivationManual[] Available => All.Where(m => SkillData.Elements.Any(e => e.Id == m.Element && e.Open)).ToArray();
    public static readonly ItemDef[] Items = All.Select(m => new ItemDef(m.Item, m.Name, "manual", m.Color,
        $"Bí kíp đạo pháp hệ {SkillData.Elements.First(e => e.Id == m.Element).Name}. Lĩnh ngộ đủ {Required} cuốn để mở vĩnh viễn cây kỹ năng của hệ này.",
        Icon: "img/bi_kip_cong_phap.png", Tint: m.Element switch
        {
            "hoa" => "hue-rotate(-28deg) saturate(2.6) brightness(1.12)",
            "thuy" => "hue-rotate(180deg) saturate(2.4) brightness(1.25)",
            "moc" => "hue-rotate(82deg) saturate(2.2) brightness(1.15)",
            "tho" => "saturate(.7)",
            "loi" => "hue-rotate(245deg) saturate(2) brightness(1.2)",
            "bang" => "hue-rotate(155deg) saturate(.8) brightness(1.65)",
            "gio" => "hue-rotate(110deg) saturate(1.2) brightness(1.4)",
            _ => "none"
        }, Rarity: "legendary", Kind: "Công pháp",
        Use: Available.Contains(m)
            ? $"Dùng: +{100 / Required}% lĩnh ngộ hệ tương ứng; đủ {Required} cuốn (100%) mở cây kỹ năng. Nâng kỹ năng vẫn cần điểm và cảnh giới. Rương Boss: tổng 1% nhận một bí kíp trong 7 hệ, xác suất mỗi hệ bằng nhau."
            : "Hệ này chưa mở. Bí kíp được giữ lại, hiện không thể học và không rơi từ rương." )).ToArray();

    public static string[] Unlocked(Player p) => p.LearnedManuals.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Where(id => Available.Any(m => m.Element == id)).Distinct().ToArray();
    public static bool IsUnlocked(Player p, string element) => Unlocked(p).Contains(element);
    public static void Unlock(Player p, string element) => p.LearnedManuals = string.Join(',', p.LearnedManuals.Split(',', StringSplitOptions.RemoveEmptyEntries).Append(element).Distinct());

    /// <summary>Số bí kíp cần lĩnh ngộ để mở một hệ; mỗi cuốn +1 (10%). Hệ đã mở theo luật cũ (1 cuốn) giữ nguyên.</summary>
    public const int Required = 10;
    public static Dictionary<string, int> Progress(Player p) => p.ManualProgress.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Split(':')).Where(x => x.Length == 2 && int.TryParse(x[1], out _))
        .GroupBy(x => x[0]).ToDictionary(g => g.Key, g => Math.Clamp(int.Parse(g.Last()[1]), 0, Required));
    /// <summary>Tiến độ từng hệ đang mở (hệ đã mở = Required), gửi cho client.</summary>
    public static Dictionary<string, int> ProgressAll(Player p)
    {
        var progress = Progress(p);
        return Available.ToDictionary(m => m.Element, m => IsUnlocked(p, m.Element) ? Required : progress.GetValueOrDefault(m.Element));
    }
    /// <summary>Lĩnh ngộ một bí kíp: +1 tiến độ; đủ Required thì mở hệ. Trả về tiến độ mới.</summary>
    public static int Comprehend(Player p, string element)
    {
        var progress = Progress(p);
        var count = Math.Min(Required, progress.GetValueOrDefault(element) + 1);
        if (count >= Required) { progress.Remove(element); Unlock(p, element); }
        else progress[element] = count;
        p.ManualProgress = string.Join(',', progress.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}:{kv.Value}"));
        return count;
    }
    /// <summary>Bị động (chỉ số cộng thêm) của các hệ không kích hoạt chỉ còn tỉ lệ này.</summary>
    public const double InactiveRate = .5;

    /// <summary>Đòn đánh thường của một hệ (null nếu hệ chưa có).</summary>
    public static SkillDef? BasicOf(string element) => SkillData.Skills.FirstOrDefault(s => s.Element == element && s.Tree == "basic");

    /// <summary>Công pháp đang kích hoạt: hệ đã chọn nếu còn hợp lệ, không thì hệ đã học có tổng cấp kỹ năng cao nhất (hòa: theo thứ tự All).</summary>
    public static string Active(Player p)
    {
        var unlocked = Unlocked(p);
        if (unlocked.Contains(p.ActiveManual) && BasicOf(p.ActiveManual) is not null) return p.ActiveManual;
        var lv = p.SkillLevels();
        return All.Select(m => m.Element).Where(e => unlocked.Contains(e) && BasicOf(e) is not null)
            .OrderByDescending(e => lv.Where(kv => SkillData.ById.TryGetValue(kv.Key, out var s) && s.Element == e).Sum(kv => kv.Value))
            .FirstOrDefault() ?? "hoa";
    }

    /// <summary>Hệ số bị động của một hệ: 100% nếu đang kích hoạt, InactiveRate nếu không.</summary>
    public static double Rate(string active, string element) => element == active ? 1 : InactiveRate;

    /// <summary>Chốt công pháp kích hoạt và đặt đòn thường của hệ đó vào ô đòn thường.</summary>
    public static void SyncActive(Player p)
    {
        p.ActiveManual = Active(p);
        if (BasicOf(p.ActiveManual) is not { } basic) return;
        var slots = p.Slots();
        slots[SkillData.BasicSlot] = basic.Id;
        p.SkillSlots = string.Join(',', slots);
    }

    public static void Migrate(Player p)
    {
        if (p.ManualVersion > 0) return;
        // Legacy characters keep the innate fire tree and every learned/equipped tree.
        Unlock(p, "hoa");
        foreach (var id in p.SkillLevels().Where(kv => kv.Value > 0).Select(kv => kv.Key).Concat(p.Slots()))
            if (SkillData.ById.TryGetValue(id, out var skill)) Unlock(p, skill.Element);
        p.ManualVersion = 1;
    }
}
