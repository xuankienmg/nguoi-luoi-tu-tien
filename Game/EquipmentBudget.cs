namespace VanDao.Game;

/// <summary>Ngân sách trang bị theo kế hoạch 19/10/2026. Baseline là tổng bộ Huyền cũ,
/// đã giảm chỉ số trang bị; không áp GearAtkHpMult/GearDefMult lần nữa.</summary>
public static class EquipmentBudget
{
    public static readonly int[] Basic = { 100, 125, 155, 190, 240, 300, 380, 480, 600, 750 };
    public static readonly int[] Utility = { 100, 110, 120, 130, 145, 160, 175, 190, 210, 230 };
    private static readonly (int Atk, int Def, int Hp)[] Baseline =
    {
        (13, 12, 115), (53, 54, 499), (267, 266, 2499),
        (1334, 1334, 12495), (6668, 6669, 62475), (33338, 33343, 312375), (166690, 166715, 1561875), (833450, 833575, 7809375), (4167250, 4167875, 39046875), (20836250, 20839375, 195234375)
    };

    public static string Profile(ItemDef item) => item.Slot switch
    {
        "weapon" => "Vũ khí chuẩn",
        "offhand" => item.Atk == 0 && (item.Hp > 0 || item.Def > 0)
            ? "Pháp bảo hộ thân" : "Pháp bảo công kích",
        "head" => "Nón", "body" => "Áo", "hands" => "Găng tay",
        "legs" => "Quần", "feet" => "Giày", "necklace" => "Dây chuyền",
        "charm" => "Hộ phù", "ring" => "Nhẫn",
        _ => throw new ArgumentException($"Ô trang bị không hỗ trợ: {item.Slot}")
    };

    public static ItemDef Apply(ItemDef item, int quality)
    {
        if (quality < 0 || quality >= Basic.Length) throw new ArgumentOutOfRangeException(nameof(quality));
        if (item.MinRealm < 0 || item.MinRealm >= Baseline.Length) throw new ArgumentOutOfRangeException(nameof(item));
        // Công, thủ, máu; tiện ích bạo kích, thân pháp, tu luyện; hệ số ngân sách.
        (double A, double D, double H, double C, double S, double Q, double B) p = Profile(item) switch
        {
            "Vũ khí chuẩn" => (1, 0, 0, 2, 0, 0, 1),
            "Pháp bảo công kích" => (.8, 0, .2, 0, 0, 2, 1),
            "Pháp bảo hộ thân" => (0, .55, .45, 0, 0, 0, 1),
            "Nón" => (0, .35, .65, 0, 0, 1, 1),
            "Áo" => (0, .45, .55, 0, 0, 0, 1),
            "Găng tay" => (.6, .4, 0, 0, 0, 0, 1),
            "Quần" => (0, .4, .6, 0, 0, 0, 1),
            "Giày" => (0, .25, .75, 0, 2, 0, 1),
            "Dây chuyền" => (.2, 0, .8, 0, 0, 2, 1),
            "Hộ phù" => (0, .6, .4, 0, 0, 0, 1),
            "Nhẫn" => (.7, 0, .3, 0, 0, 3, 1),
            _ => throw new InvalidOperationException("Thiếu hồ sơ trang bị")
        };
        var baseline = Baseline[item.MinRealm];
        int Stat(double share, double coefficient) => share == 0 ? 0
            : Math.Max(1, (int)Math.Round(Basic[quality] * p.B * share * coefficient, MidpointRounding.AwayFromZero));
        double Percent(double value) => Math.Round(value * Utility[quality] / 100, 1, MidpointRounding.AwayFromZero);
        // One specialty per slot; realm and quality scale ratings exactly once.
        var rating = Percent(10 * (item.MinRealm + 1));
        // Bạo kích hiện trong nhóm "Thuộc tính cơ bản" của thẻ trang bị, không còn dòng riêng (Special).
        return item with
        {
            Atk = Stat(p.A, baseline.Atk / (155 * 3.3)),
            Def = Stat(p.D, baseline.Def / (155 * 2.45)),
            Hp = Stat(p.H, baseline.Hp / (155 * 4.25)),
            CritPct = item.Slot == "weapon" ? rating : 0,
            SpeedPct = item.Slot == "feet" ? rating : 0,
            CritResistPct = item.Slot is "head" or "charm" ? rating : 0,
            AccuracyPct = item.Slot is "hands" or "necklace" ? rating : 0,
            ComboPct = item.Slot == "ring" ? rating : 0,
            ComboResistPct = item.Slot == "legs" ? rating : 0,
            ReflectPct = item.Slot == "body" ? rating : 0,
            ReflectResistPct = item.Slot == "offhand" ? rating : 0,
            QiPct = quality >= 4 ? Percent(p.Q) : 0, Special = null
        };
    }
}
