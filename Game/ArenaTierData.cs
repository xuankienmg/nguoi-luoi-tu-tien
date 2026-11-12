using System.Globalization;

namespace VanDao.Game;

/// <summary>Hạng Lôi đài online theo điểm hạng: mỗi hạng có bậc thang vị trí riêng, chỉ thách đấu người cùng hạng.</summary>
public static class ArenaTierData
{
    public record Tier(string Id, string Name, int Min);

    // cân bằng 11/2026: mốc điểm các hạng, hạ thấp (trước 300/700/1.200/1.800/2.500) vì server ít người (~10)
    public static readonly Tier[] Tiers =
    [
        new("dong", "Đồng", 0), new("bac", "Bạc", 100), new("vang", "Vàng", 250),
        new("bach_kim", "Bạch Kim", 450), new("kim_cuong", "Kim Cương", 700), new("tong_su", "Cao Thủ", 1000),
    ];

    /// <summary>Thắng khiêu chiến: +WinBase, cộng thêm 1 điểm mỗi hạng đối thủ xếp trên mình (tối đa WinGapMax).</summary>
    public const int WinBase = 25, WinGapMax = 15, LosePoints = 15, ProtectMatches = 3;

    public static int IndexOf(int points)
    {
        for (var i = Tiers.Length - 1; i > 0; i--) if (points >= Tiers[i].Min) return i;
        return 0;
    }

    /// <summary>Từ 11/2026 mọi trận cùng hạng đều tính điểm (không còn tỷ thí): đánh người xếp dưới vẫn +25 / −15 nhưng không đổi chỗ. Giữ hàm để các nơi gọi không đổi.</summary>
    public static bool IsPractice(int tier, long myRank, long foeRank) => false;

    public static int WinPoints(long myRank, long foeRank) => WinBase + (int)Math.Clamp(myRank - foeRank, 0, WinGapMax);

    // ---- Thưởng theo hạng (cần đã đánh ít nhất 1 trận thật trong ngày / tuần) ----
    public record Reward(string Item, int Qty);
    private static Reward[] R(params (string, int)[] r) => r.Select(x => new Reward(x.Item1, x.Item2)).ToArray();

    // cân bằng 11/2026: thưởng ngày nhỏ hơn rương Hoạt Lực; thưởng tuần lấy ở Chủ nhật
    private static readonly Reward[][] DailyItems =
    [
        R((DailyQuestData.BoNguyen, 1)), R(("linh_thu_dan", 10)), R(("ve_tai_xiu", 1)),
        R(("ve_tai_xiu", 2)), R(("ve_linh_thu", 1)), R(("ve_linh_thu", 1), ("thuong_pham_linh_thach", 2)),
    ];
    private static readonly Reward[][] WeeklyItems =
    [
        R(("ve_tai_xiu", 2)), R(("ve_tai_xiu", 3)), R(("linh_thu_dan", 30), ("ve_tai_xiu", 3)),
        R(("ve_linh_thu", 2)), R(("hoa_hinh_dan", 1), ("ve_linh_thu", 2)), R(("thuong_pham_linh_thach", 20), ("ve_tai_xiu", 5)),
    ];

    public static List<Reward> RewardsFor(int tier, int realm, bool weekly)
    {
        tier = Math.Clamp(tier, 0, Tiers.Length - 1);
        var pct = weekly ? .10 + .05 * tier : .02 + .01 * tier;
        var list = new List<Reward> { new("linh_thach", (int)Math.Max(DailyQuestData.MinStones, Math.Round(DailyQuestData.StoneBase(realm) * pct))) };
        list.AddRange((weekly ? WeeklyItems : DailyItems)[tier].Select(i => i.Item == DailyQuestData.BoNguyen ? new Reward(DailyQuestData.BoNguyenFor(realm), i.Qty) : i));
        return list;
    }

    /// <summary>Ngày theo giờ Việt Nam (UTC+7): khóa ngày của lần nhận thưởng ngày.</summary>
    public static string DayKey(DateTime utcNow) => utcNow.AddHours(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Thứ Hai đầu tuần (giờ Việt Nam): khóa tuần của lần nhận thưởng tuần.</summary>
    public static string WeekKey(DateTime utcNow)
    {
        var d = utcNow.AddHours(7).Date;
        return d.AddDays(-(((int)d.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static bool IsSunday(DateTime utcNow) => utcNow.AddHours(7).DayOfWeek == DayOfWeek.Sunday;
}
