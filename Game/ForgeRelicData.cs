namespace VanDao.Game;

/// <summary>Bảo vật Lò Rèn: kệ 4 ô, bảo vật vỡ lúc sang ngày mới theo giờ VN; mua bằng Điểm Luyện Khí.
/// Trạng thái nằm trong ForgeData.ForgeSave (JSON Player.ForgeState).</summary>
public static class ForgeRelicData
{
    public const int Slots = 4, ShelfSize = 3;
    public const long RerollStone = 20_000;   // cân bằng 11/2026
    /// <summary>Dấu trong Player.ForgeLevels khi có Linh Văn Khắc: Stats nhân % chỉ số rèn mà không đọc JSON.</summary>
    public const string LinhVanKey = "linhvan";

    public sealed record RelicDef(string Id, string Name, string Desc, int Price);
    // cân bằng 11/2026: giá theo spec Bảo Vật Lò Rèn
    public static readonly RelicDef[] All =
    [
        new("an_ho_binh", "Ấn Hộ Binh", "Tỉ lệ Vỡ nát ×0,8.", 120),
        new("kim_cuong_chuy", "Kim Cương Chùy", "Đại thành +4% (bớt từ Bất biến).", 100),
        new("thien_co_do", "Thiên Cơ Đồ", "Thành công +8% (bớt từ Bất biến).", 110),
        new("ban_thach_de", "Bàn Thạch Đe", "Tổn hại −6% (chuyển sang Bất biến).", 60),
        new("hoa_tinh_thach", "Hỏa Tinh Thạch", "Mỗi nhát thường tích +2 Hỏa Khí.", 90),
        new("phuong_vu", "Phượng Vũ", "Thần Hỏa che chở 5 nhát (thay vì 3).", 90),
        new("tiet_thiet_lo", "Tiết Thiết Lô", "25% nhát rèn không tốn Tinh Thiết.", 70),
        new("hoi_quang_kinh", "Hồi Quang Kính", "Hồi Thiên Phù: 20% không bị tiêu.", 60),
        new("linh_van_khac", "Linh Văn Khắc", "Chỉ số rèn ×1,1.", 150),
        new("bat_diet_hon", "Bất Diệt Hồn", "Vỡ nát chỉ mất nửa cấp.", 200),
        new("huyen_vu_an", "Huyền Vũ Ấn", "Tỉ lệ Vỡ nát giảm 25%.", 2_000),
        new("tu_van_thach", "Tụ Vận Thạch", "Dưới +30, chuyển 3 điểm phần trăm từ Bất biến sang Thành công.", 60),
        new("huyet_hoa_chuy", "Huyết Hỏa Chùy", "Chuyển 2 điểm phần trăm từ Thành công sang Đại thành.", 80),
        new("hoan_thiet_an", "Hoán Thiết Ấn", "Mỗi nhát rèn tốn ít hơn 500 linh thạch.", 70),
        new("ho_mach_de", "Hộ Mạch Đe", "Từ +100, chuyển 2 điểm phần trăm từ Vỡ nát sang Bất biến.", 100),
    ];
    public static readonly IReadOnlyDictionary<string, RelicDef> ById = All.ToDictionary(r => r.Id);

    public sealed record Mods(double BreakMult = 1, double GreatAdd = 0, double SuccessAdd = 0, double DamageSub = 0,
        int ChargePerStrike = 1, int FrenzyStrikes = ForgeData.FrenzyStrikes, double IronFreeChance = 0,
        double RewindKeepChance = 0, double BonusMult = 1, bool HalfOnBreak = false,
        double EarlySuccessAdd = 0, double GreatFromSuccess = 0, long StoneDiscount = 0, double LateBreakToHold = 0)
    {
        public static readonly Mods None = new();
    }

    public static Mods ModsOf(IEnumerable<string> owned)
    {
        var s = owned.ToHashSet();
        return new(
            BreakMult: (s.Contains("an_ho_binh") ? 0.8 : 1) * (s.Contains("huyen_vu_an") ? 0.75 : 1),
            GreatAdd: s.Contains("kim_cuong_chuy") ? 0.04 : 0,
            SuccessAdd: s.Contains("thien_co_do") ? 0.08 : 0,
            DamageSub: s.Contains("ban_thach_de") ? 0.06 : 0,
            ChargePerStrike: s.Contains("hoa_tinh_thach") ? 2 : 1,
            FrenzyStrikes: s.Contains("phuong_vu") ? 5 : ForgeData.FrenzyStrikes,
            IronFreeChance: s.Contains("tiet_thiet_lo") ? 0.25 : 0,
            RewindKeepChance: s.Contains("hoi_quang_kinh") ? 0.2 : 0,
            BonusMult: s.Contains("linh_van_khac") ? 1.1 : 1,
            HalfOnBreak: s.Contains("bat_diet_hon"),
            EarlySuccessAdd: s.Contains("tu_van_thach") ? 0.03 : 0,
            GreatFromSuccess: s.Contains("huyet_hoa_chuy") ? 0.02 : 0,
            StoneDiscount: s.Contains("hoan_thiet_an") ? 500 : 0,
            LateBreakToHold: s.Contains("ho_mach_de") ? 0.02 : 0);
    }

    /// <summary>Điểm Luyện Khí khi đổi 1 trang bị: 2^phẩm (Phàm 1 … Kim Tiên 1024). 0 = không phải trang bị.</summary>
    public static long SalvagePoints(ItemDef d)
    {
        if (d.Slot is null || d.Base is null) return 0;
        var q = Array.IndexOf(GameData.RarityOrder, d.Rarity);
        return q < 0 ? 0 : 1L << q;
    }

    /// <summary>Điểm theo chỉ số phẩm (GameData.RarityOrder), gửi client qua /data.</summary>
    public static long[] SalvagePointsByRarity => GameData.RarityOrder.Select((_, q) => 1L << q).ToArray();

    /// <summary>Kệ hôm nay: xáo bảo vật theo (nhân vật, ngày VN, số lần làm mới) — cố định, tải lại vẫn ra cùng kệ.
    /// Lấy 3 món đầu chưa có trên kệ bảo vật; món mua hôm nay vẫn giữ chỗ (client hiện "Đã mua").</summary>
    public static List<string> Shelf(Guid playerId, string day, int rerolls, ICollection<string> owned, ICollection<string> boughtToday)
    {
        var rng = new Random(StableSeed($"{playerId:N}|{day}|{rerolls}"));
        var includeRare = rng.Next(4) == 0 || boughtToday.Contains("huyen_vu_an");
        return All.Select(r => (r.Id, Key: rng.Next())).OrderBy(x => x.Key).Select(x => x.Id)
            .Where(id => id != "huyen_vu_an" || includeRare)
            .Where(id => !owned.Contains(id) || boughtToday.Contains(id)).Take(ShelfSize).ToList();
    }

    /// <summary>FNV-1a: string.GetHashCode đổi theo tiến trình nên không dùng làm hạt giống.</summary>
    private static int StableSeed(string s)
    {
        unchecked
        {
            var h = (int)2166136261;
            foreach (var c in s) h = (h ^ c) * 16777619;
            return h;
        }
    }
}
