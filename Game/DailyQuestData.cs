namespace VanDao.Game;

/// <summary>
/// Nhiệm vụ ngày/tuần (Hoạt Lực): danh sách việc cố định mỗi ngày, tự hoàn thành khi đủ số lần.
/// Hoạt Lực ngày mở rương mốc ngày; Hoạt Lực ngày cộng dồn vào điểm tuần để mở rương tuần.
/// Ngày theo 0h giờ Việt Nam, tuần bắt đầu 0h thứ Hai giờ Việt Nam. Mở sau khi xong chuỗi tân thủ.
/// </summary>
public static class DailyQuestData
{
    public record TaskDef(string Id, string Title, int Need, int Points, string? Tab);
    public record Reward(string Item, int Qty);
    /// <summary>Mốc rương: Pct = % phí linh thạch đột phá cảnh giới hiện tại; "bo_nguyen" = Bổ Nguyên Đan theo phẩm cảnh giới.</summary>
    public record Chest(int Points, double StonePct, Reward[] Items);

    public const string EvLogin = "login", EvKill = "kill", EvBoss = "boss", EvBrew = "brew", EvFuse = "fuse",
                        EvSpin = "spin", EvArena = "arena", EvKyDuyen = "kyduyen", EvFeed = "feed";
    public const string BoNguyen = "bo_nguyen";

    // cân bằng 11/2026: tổng 115 điểm, mốc cuối 100 nên không cần làm hết
    public static readonly TaskDef[] Tasks =
    {
        new(EvLogin, "Nhận quà đăng nhập", 1, 10, "event"),
        new(EvKill, "Hạ quái khi thám hiểm", 50, 15, "map"),
        new(EvBoss, "Hạ Boss chương", 1, 15, "map"),
        new(EvBrew, "Khai lò luyện đan", 3, 10, "craft"),
        new(EvFuse, "Luyện khí (ghép trang bị)", 2, 10, "fuse"),
        new(EvSpin, "Quay Tài Xỉu hoặc Linh Thú", 10, 15, "spin"),
        new(EvArena, "Đấu Lôi đài", 2, 15, "arena"),   // cân bằng 11/2026: 5 → 2 trận
        new(EvKyDuyen, "Giải Kỳ duyên", 1, 15, "kyduyen"),
        new(EvFeed, "Cho linh thú ăn", 1, 10, "pet"),
    };

    public static readonly Dictionary<string, TaskDef> TaskById = Tasks.ToDictionary(t => t.Id);
    public static int MaxDaily => Tasks.Sum(t => t.Points);
    public static int MaxWeekly => MaxDaily * 7;

    private static Reward[] R(params (string, int)[] r) => r.Select(x => new Reward(x.Item1, x.Item2)).ToArray();

    // cân bằng 11/2026: 1 tuần đủ mốc = 22 vé Tài Xỉu, 9 vé Linh Thú — nhỏ hơn nhiều so với 1 vòng Thiên Cơ
    public static readonly Chest[] DailyChests =
    {
        new(20, .02, R((BoNguyen, 1))),
        new(40, .03, R(("linh_thu_dan", 10))),
        new(60, .04, R((BoNguyen, 2))),
        new(80, .05, R(("ve_tai_xiu", 2))),
        new(100, .06, R(("ve_linh_thu", 1), (BoNguyen, 2), ("thuong_pham_linh_thach", 5))),   // cân bằng 11/2026: +5 linh thạch thượng phẩm (tiền VIP)
    };

    public static readonly Chest[] WeeklyChests =
    {
        new(150, .10, R(("ve_tai_xiu", 3))),
        new(300, .15, R((BoNguyen, 5), ("linh_thu_dan", 30))),
        new(450, .20, R(("ve_linh_thu", 2), ("hoa_hinh_dan", 1))),
        new(600, .25, R(("ve_tai_xiu", 5), ("thu_dan_huyen", 1), ("thuong_pham_linh_thach", 20))),   // cân bằng 11/2026: +20 linh thạch thượng phẩm
    };

    public const int MinStones = 10;

    /// <summary>Phí linh thạch đột phá của cảnh giới (cảnh giới không có thì lấy cảnh giới gần nhất phía trước).</summary>
    public static long StoneBase(int realm)
    {
        for (var r = Math.Clamp(realm, 0, GameData.Realms.Count - 1); r >= 0; r--)
            if (GameData.Realms[r].BreakCost is { } c && c.TryGetValue("linh_thach", out var v) && v > 0) return v;
        return 200;
    }

    /// <summary>Phẩm Bổ Nguyên Đan theo cảnh giới: Luyện Khí–Trúc Cơ Hạ, Kim Đan–Nguyên Anh Trung, Hóa Thần trở lên Thượng.</summary>
    public static string BoNguyenFor(int realm) => realm <= 1 ? "bo_nguyen_dan_ha" : realm <= 3 ? "bo_nguyen_dan_trung" : "bo_nguyen_dan_thuong";

    /// <summary>Quà thật của một rương ở cảnh giới realm (linh thạch đứng đầu).</summary>
    public static List<Reward> RewardsFor(Chest c, int realm)
    {
        var list = new List<Reward> { new("linh_thach", (int)Math.Max(MinStones, Math.Round(StoneBase(realm) * c.StonePct))) };
        list.AddRange(c.Items.Select(i => i.Item == BoNguyen ? new Reward(BoNguyenFor(realm), i.Qty) : i));
        return list;
    }
}
