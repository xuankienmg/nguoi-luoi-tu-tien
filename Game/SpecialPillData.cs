using System.Globalization;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>Đan tiêu hao linh thảo cao giai (11/2026): Bổ Nguyên Đan · Cực phẩm, Hộ Đạo Đan, Bàn Đào Trường Sinh Đan.
/// Hỗn Nguyên Chiến Ý Đan là đan hỗ trợ có thời hạn, khai báo trong SupportPills.
/// Trạng thái lưu trong cờ Player.Admin (không cần migration): hodao=1 · bandao=số viên đã dùng.</summary>
public static class SpecialPillData
{
    public const string BoNguyenCuc = "bo_nguyen_dan_cuc", HoDao = "ho_dao_dan", BanDao = "ban_dao_truong_sinh_dan";
    /// <summary>Bàn Đào Trường Sinh Đan: % máu và công gốc vĩnh viễn mỗi viên, tối đa MaxBanDao viên.</summary>
    public const double BanDaoPct = 1;   // cân bằng 11/2026: +1% / viên
    public const int MaxBanDao = 10;

    public static IEnumerable<ItemDef> Items => new[]
    {
        new ItemDef(BoNguyenCuc, "Bổ Nguyên Đan · Cực phẩm", "pill", "#ffc65a", "Đan tăng tu vi bậc 4, luyện từ mầm Kiến Mộc và hoa lửa Bất Tử Phượng Hoa.", "img/dan/bo_nguyen_cuc.png",
            Rarity: "legendary", Kind: "Đan tăng tu vi", UseQiSeconds: 10800, Tier: 4,
            Use: "Dùng: nhận ngay tu vi tương đương 3 giờ tu luyện theo tốc độ hiện tại. Không vượt giới hạn tu vi của tầng hiện tại; tính chung hiệu quả trong ngày với các phẩm Bổ Nguyên Đan khác."),
        new ItemDef(HoDao, "Hộ Đạo Đan", "elixir", "#8fe3ff", "Đan hộ đạo kết từ tro lửa phượng hoàng, giữ vững kinh mạch trước phản phệ.", "img/dan/ho_dao_dan.png",
            MinRealm: 4, Rarity: "mythic", Kind: "Đan dược", Tier: 6,
            Use: "Dùng từ Hóa Thần. Hiệu lực giữ đến khi đỡ được 1 lần đột phá thất bại: lần đó không mất tu vi và không bị tẩu hỏa nhập ma (vẫn mất nguyên liệu, vẫn phải chờ điều tức). Chỉ có 1 hiệu lực cùng lúc."),
        new ItemDef(BanDao, "Bàn Đào Trường Sinh Đan", "elixir", "#ffa0b8", "Đan luyện từ Âm Dương Bàn Đào, bồi bổ căn cơ trường sinh.", "img/dan/ban_dao_truong_sinh_dan.png",
            MinRealm: 6, Rarity: "divine", Kind: "Đan dược", Tier: 7,
            Use: $"Dùng từ Hợp Thể: +{BanDaoPct:0.#}% máu và công vĩnh viễn mỗi viên, tối đa {MaxBanDao} viên (+{BanDaoPct * MaxBanDao:0}%)."),
    };

    /// <summary>Đan phương (phụ liệu đã ghi thẳng, không qua HerbExpansion.RecipeHerbs).</summary>
    public static IEnumerable<BrewDef> Brews => new[]
    {
        new BrewDef(BoNguyenCuc, new() { ["kien_moc_mieu"] = 6, ["bat_tu_phuong_hoa"] = 1, ["dia_mach_tien_dang"] = 1, ["linh_thach"] = 250000 }),
        new BrewDef(HoDao, new() { ["bat_tu_phuong_hoa"] = 3, ["phuong_vu_chi"] = 1, ["linh_thach"] = 500000 }),
        new BrewDef(SupportPills.ChienY, new() { ["hon_nguyen_dao_lien"] = 2, ["kien_moc_mieu"] = 2, ["thien_truc_duan"] = 1, ["linh_thach"] = 200000 }),
        new BrewDef(BanDao, new() { ["am_duong_ban_dao"] = 3, ["hon_nguyen_dao_lien"] = 2, ["hon_nguyen_linh_chung"] = 1, ["linh_thach"] = 1000000 }),
    };

    // ---- cờ trong Player.Admin ----
    private const string HoDaoFlag = "hodao=1", BanDaoFlag = "bandao=";
    private static IEnumerable<string> Flags(Player p) => (p.Admin ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
    private static void SetFlag(Player p, string prefix, string? flag) =>
        p.Admin = string.Join(',', Flags(p).Where(f => !f.StartsWith(prefix)).Concat(flag is null ? [] : [flag]));

    public static bool HoDaoActive(Player p) => Flags(p).Contains(HoDaoFlag);
    public static void SetHoDao(Player p, bool on) => SetFlag(p, HoDaoFlag, on ? HoDaoFlag : null);

    public static int BanDaoUsed(Player p) => Flags(p).Where(f => f.StartsWith(BanDaoFlag))
        .Select(f => int.TryParse(f[BanDaoFlag.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? Math.Clamp(n, 0, MaxBanDao) : 0)
        .FirstOrDefault();
    public static void SetBanDao(Player p, int n) => SetFlag(p, BanDaoFlag, n > 0 ? BanDaoFlag + n.ToString(CultureInfo.InvariantCulture) : null);
    /// <summary>% máu và công cộng thêm từ Bàn Đào Trường Sinh Đan.</summary>
    public static double BanDaoBonus(Player p) => BanDaoUsed(p) * BanDaoPct;
}
