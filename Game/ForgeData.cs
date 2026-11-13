using System.Globalization;
using System.Text.Json;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>Lò Rèn: cường hóa theo ô trang bị (+0…+200). Cấp gắn với ô, không với món: thay đồ không mất cấp.
/// Player.ForgeLevels = "slot:cấp;…" (đọc trong Stats), Player.ForgeState = JSON ForgeSave (Hỏa Khí, nhát có thể hoàn, nhật ký).</summary>
public static class ForgeData
{
    public const string Iron = "tinh_thiet", Rewind = "hoi_thien_phu";
    public const int MaxLevel = 200, FrenzyCharge = 10, FrenzyStrikes = 3, HistoryKeep = 50;
    // cân bằng 11/2026: theo bản thiết kế Lò Rèn
    public const double Great = 0.08, Hold = 0.15, Damage = 0.12;
    public const double BreakBase = 0.02, BreakPerLevel = 0.005, BreakMax = 0.45;

    public enum Outcome { Great, Success, Hold, Damage, Break }
    public static readonly Outcome[] Order = [Outcome.Great, Outcome.Success, Outcome.Hold, Outcome.Damage, Outcome.Break];
    public static string Key(Outcome o) => o switch
    {
        Outcome.Great => "great", Outcome.Success => "success", Outcome.Hold => "hold", Outcome.Damage => "damage", _ => "break",
    };

    public record Odds(double Great, double Success, double Hold, double Damage, double Break)
    {
        public double Of(Outcome o) => o switch
        {
            Outcome.Great => Great, Outcome.Success => Success, Outcome.Hold => Hold, Outcome.Damage => Damage, _ => Break,
        };
        public double[] ToArray() => Order.Select(Of).ToArray();
    }

    /// <summary>Tỉ lệ nhát kế tiếp. shielded = Thần Hỏa đang cháy (không thể vỡ). mods = bảo vật trên kệ.</summary>
    public static Odds OddsAt(int level, bool shielded, ForgeRelicData.Mods? mods = null)
    {
        var m = mods ?? ForgeRelicData.Mods.None;
        var brk = shielded || level <= 0 ? 0 : Math.Min(BreakMax, BreakBase + BreakPerLevel * level) * m.BreakMult;
        var dmg = level <= 0 ? 0 : Math.Max(0, Damage - m.DamageSub);
        var moved = level <= 0 ? 0 : Damage - dmg;   // Bàn Thạch Đe: phần Tổn hại bớt đi chuyển sang Bất biến
        var great = Great + m.GreatAdd;
        var hold = Math.Max(0, Hold - m.GreatAdd - m.SuccessAdd + moved);
        return new(great, Math.Max(0, 1 - brk - great - dmg - hold), hold, dmg, brk);
    }

    /// <summary>r ∈ [0,1): đi lần lượt Order; sai số làm tròn rơi vào kết quả cuối còn tỉ lệ > 0.</summary>
    public static Outcome Roll(Odds o, double r)
    {
        var acc = 0.0;
        foreach (var k in Order) { acc += o.Of(k); if (r < acc) return k; }
        return Order.Last(k => o.Of(k) > 0);
    }

    public sealed class SlotState
    {
        public int L { get; set; }   // cấp
        public int C { get; set; }   // Hỏa Khí 0..9
        public int F { get; set; }   // số nhát Thần Hỏa còn lại
    }

    public static SlotState Apply(SlotState cur, Outcome o, ForgeRelicData.Mods? mods = null)
    {
        var m = mods ?? ForgeRelicData.Mods.None;
        var n = new SlotState
        {
            L = o switch
            {
                Outcome.Great => Math.Min(MaxLevel, cur.L + 2),
                Outcome.Success => Math.Min(MaxLevel, cur.L + 1),
                Outcome.Damage => Math.Max(0, cur.L - 1),
                Outcome.Break => m.HalfOnBreak ? cur.L / 2 : 0,   // Bất Diệt Hồn: chỉ mất nửa cấp
                _ => cur.L,
            },
            C = cur.C, F = cur.F,
        };
        if (cur.F > 0) n.F = cur.F - 1;
        else if ((n.C += m.ChargePerStrike) >= FrenzyCharge) { n.C -= FrenzyCharge; n.F = m.FrenzyStrikes; }   // dư Hỏa Khí được giữ lại
        if (o == Outcome.Break) { n.C = 0; n.F = 0; }
        return n;
    }

    /// <summary>Hệ số % chỉ số rèn từ dấu Linh Văn Khắc trong ForgeLevels (đọc trong Stats, không parse JSON).</summary>
    public static double BonusMult(IReadOnlyDictionary<string, int> levels) =>
        levels.ContainsKey(ForgeRelicData.LinhVanKey) ? ForgeRelicData.ModsOf(["linh_van_khac"]).BonusMult : 1;

    // cân bằng 11/2026: Tinh Thiết rơi ở ải (gieo riêng mỗi trận thắng, 1 viên; Khó / Ác mộng nhân hệ số thưởng)
    // và trong rương (lấy phần tỉ lệ của trang bị; Tinh Thạch, linh thú, công pháp, Ngộ Đạo Đan giữ nguyên)
    public const double StageIronChance = .03;
    public static (double Chance, int Min, int Max) ChestIron(string chest) => chest switch
    {
        GameData.BossChest => (.10, 1, 4),   // cân bằng 11/2026: số lượng 1–4 (trước 3–6)
        "ruong_hiem" => (.08, 1, 3),         // cân bằng 11/2026: 1–3 (trước 2–4)
        "ruong_thuong" => (.05, 1, 2),
        _ => (0, 0, 0),
    };

    /// <summary>% cộng thêm vào Công / Sinh lực / Phòng của món đang mặc ở ô.</summary>
    public static double BonusPct(int level) => 2.0 * level + 0.01 * level * level;
    // cân bằng 11/2026: cố định 10.000 linh thạch mỗi nhát, không theo món hay cấp (trước: 10% giá bán × (1 + cấp/10))
    public const long StonePerStrike = 10_000;
    public static long StoneCost(ItemDef item, int level) => StonePerStrike;
    public static int IronCost(int level) => 1 + level / 10;

    // ---- lưu trữ ----
    public sealed record UndoState(string Slot, SlotState Before);
    public sealed record LogLine(int N, string Slot, string Item, int From, int To, string Kind, long At);
    public sealed class ForgeSave
    {
        public Dictionary<string, SlotState> Slots { get; set; } = new();
        public int N { get; set; }
        public UndoState? Undo { get; set; }
        public List<LogLine> Log { get; set; } = new();
        /// <summary>Bảo vật trên kệ (tối đa ForgeRelicData.Slots), Điểm Luyện Khí, kệ hôm nay: ngày VN, số lần làm mới, món đã mua.</summary>
        public List<string> Relics { get; set; } = new();
        public long Points { get; set; }
        public string ShopDay { get; set; } = "";
        public int Rerolls { get; set; }
        public List<string> Bought { get; set; } = new();
    }

    public static Dictionary<string, int> Levels(Player p)
    {
        var d = new Dictionary<string, int>();
        foreach (var x in (p.ForgeLevels ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(':')))
            if (x.Length == 2 && int.TryParse(x[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                d[x[0]] = Math.Clamp(v, 0, MaxLevel);   // trùng ô: lấy giá trị cuối
        return d;
    }

    public static ForgeSave Load(Player p)
    {
        if (string.IsNullOrWhiteSpace(p.ForgeState)) return Seeded(p);
        try
        {
            var s = JsonSerializer.Deserialize<ForgeSave>(p.ForgeState);
            if (s is null) return Seeded(p);
            s.Slots ??= new(); s.Log ??= new(); s.Relics ??= new(); s.Bought ??= new(); s.ShopDay ??= "";
            return s;
        }
        catch (JsonException) { return Seeded(p); }
    }

    /// <summary>ForgeState trống/hỏng: dựng lại từ ForgeLevels (chỉ cấp) để Store sau đó không xóa cấp.</summary>
    private static ForgeSave Seeded(Player p)
    {
        var s = new ForgeSave();
        foreach (var (slot, l) in Levels(p)) if (l > 0 && GemData.EquipSlots.Contains(slot)) s.Slots[slot] = new SlotState { L = l };
        return s;
    }

    public static void Store(Player p, ForgeSave s)
    {
        if (s.Log.Count > HistoryKeep) s.Log.RemoveRange(HistoryKeep, s.Log.Count - HistoryKeep);
        p.ForgeState = JsonSerializer.Serialize(s);
        var parts = s.Slots.Where(kv => kv.Value.L > 0).Select(kv => $"{kv.Key}:{kv.Value.L.ToString(CultureInfo.InvariantCulture)}");
        if (s.Relics.Contains("linh_van_khac")) parts = parts.Append($"{ForgeRelicData.LinhVanKey}:1");
        p.ForgeLevels = string.Join(';', parts);
    }

    // ---- vật phẩm ----
    public static readonly ItemDef[] Items =
    [
        new ItemDef(Iron, "Tinh Thiết", "ticket", "#b8c2c4", "Thỏi sắt trăm lần tôi luyện, vân kim loại ánh lên khi gặp lửa.",
            Icon: "img/items/tinh_thiet.png", Rarity: "uncommon", Kind: "Nguyên liệu rèn",
            Use: "Dùng ở Luyện chế → Lò Rèn: mỗi nhát rèn tốn 1–10 Tinh Thiết tùy cấp (thêm 1 mỗi 10 cấp). Mua ở Chợ → Tạp Hóa bà Dĩm; rơi ít khi thắng ở ải (3%) và trong rương."),
        new ItemDef(Rewind, "Hồi Thiên Phù", "ticket", "#e6c46a", "Lá phù vẽ ngược dòng thời gian, đốt lên là nhát búa vừa rồi như chưa từng giáng xuống.",
            Icon: "img/items/hoi_thien_phu.png", Rarity: "epic", Kind: "Phù",
            Use: "Ở Lò Rèn: hoàn nhát rèn gần nhất, kể cả khi vỡ nát. Không hoàn linh thạch, Tinh Thiết. Mua ở Chợ → Tạp Hóa VIP."),
    ];
}
