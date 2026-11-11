using System.Globalization;

namespace VanDao.Game;

/// <summary>
/// Tông Môn: luật và con số. Cống hiến cá nhân = tiền tiêu ở cửa hàng tông; mỗi điểm kiếm được cũng cộng 1 vào quỹ tông.
/// Quỹ tông dùng để nâng cấp (tăng buff tu luyện, mở thêm hàng). Không giới hạn số thành viên (server ít người).
/// Ngày theo 0h giờ Việt Nam, tuần bắt đầu 0h thứ Hai giờ Việt Nam.
/// </summary>
public static class SectData
{
    public const string Leader = "leader", Elder = "elder", Disciple = "disciple";

    public const int MinRealm = 1;                 // Trúc Cơ mới tạo / vào tông
    public const long CreateCostMin = 10_000;
    public const int NameMin = 2, NameMax = 16, NoticeMax = 120;
    public const int MaxPendingApps = 3;
    public const int RejoinHours = 24;
    public const int LeaderAwayDays = 7;
    public const int MaxElders = 2;
    public const int ListSize = 50;

    // cân bằng 11/2026: điểm danh 10/ngày; quyên 3 lần/ngày theo % phí đột phá cảnh giới hiện tại
    public const int CheckinPoints = 10;
    public const int DonatesPerDay = 3;
    public record DonateTier(double Pct, long Min, int Points);
    public static readonly DonateTier[] Donates =
    {
        new(.02, 1_000, 10),
        new(.05, 2_500, 30),
        new(.10, 5_000, 70),
    };

    public record LevelDef(int Level, long Cost, double QiPct);
    // cân bằng 11/2026: quỹ giảm một nửa cho server ít người (tông 5 người lên cấp 5 trong ~2–3 tuần)
    public static readonly LevelDef[] Levels =
    {
        new(1, 0, 2),
        new(2, 500, 4),
        new(3, 1_500, 6),
        new(4, 4_000, 8),
        new(5, 10_000, 10),
    };
    public static int MaxLevel => Levels.Length;
    public static LevelDef LevelOf(int level) => Levels[Math.Clamp(level, 1, MaxLevel) - 1];
    public static double QiPct(int level) => level <= 0 ? 0 : LevelOf(level).QiPct;
    /// <summary>Quỹ cần để lên cấp kế; null khi đã tối đa.</summary>
    public static long? NextCost(int level) => level >= MaxLevel ? null : Levels[level].Cost;

    public record ShopItem(string ItemId, int Price, int WeeklyLimit, int MinLevel);
    // cân bằng 11/2026: giá theo cống hiến; 1 tuần chăm (~70/ngày) ≈ 500 điểm
    public static readonly ShopItem[] Shop =
    {
        new("linh_thu_dan", 5, 30, 1),
        new("bo_nguyen_dan_ha", 10, 10, 1),
        new(GameData.SpinTicket, 40, 3, 2),
        new("bo_nguyen_dan_trung", 30, 5, 2),
        new(GameData.PetSpinTicket, 80, 2, 3),
        new("bo_nguyen_dan_thuong", 60, 3, 4),
        new("hoa_hinh_dan", 200, 1, 4),
        new("thu_dan_huyen", 300, 1, 5),
    };
    public static readonly Dictionary<string, ShopItem> ShopById = Shop.ToDictionary(s => s.ItemId);

    /// <summary>Phí linh thạch đột phá của cảnh giới (cảnh giới không có thì lấy cảnh giới gần nhất phía trước).</summary>
    public static long StoneBase(int realm)
    {
        for (var r = Math.Clamp(realm, 0, GameData.Realms.Count - 1); r >= 0; r--)
            if (GameData.Realms[r].BreakCost is { } c && c.TryGetValue("linh_thach", out var v) && v > 0) return v;
        return 200;
    }

    public static long CreateCost(int realm) => Math.Max(CreateCostMin, StoneBase(realm));
    public static long DonateCost(int tier, int realm) =>
        Math.Max(Donates[tier].Min, (long)Math.Round(StoneBase(realm) * Donates[tier].Pct));

    public static bool IsOfficer(string role) => role is Leader or Elder;

    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);
    public static string DayOf(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(Vn).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    /// <summary>Khóa tuần: ngày thứ Hai (giờ VN) của tuần chứa utc.</summary>
    public static string WeekOf(DateTime utc)
    {
        var d = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(Vn).Date;
        return d.AddDays(-(((int)d.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>Tên tông: lọc như chat, 2–12 ký tự. Trả null nếu không hợp lệ.</summary>
    public static string? CleanName(string? name)
    {
        var s = ChatRules.Clean(name);
        return s.Length is >= NameMin and <= NameMax ? s : null;
    }
    public static string NameKey(string name) => name.ToLowerInvariant();

    public static Dictionary<string, int> ParseBought(string s) =>
        s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(':')).Where(x => x.Length == 2 && int.TryParse(x[1], out _))
            .ToDictionary(x => x[0], x => int.Parse(x[1]));
    public static string FormatBought(Dictionary<string, int> d) => string.Join(',', d.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}:{kv.Value}"));
}
