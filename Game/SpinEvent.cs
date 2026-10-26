namespace VanDao.Game;

/// <summary>
/// Sự kiện quay tích lũy "Thiên Cơ": mỗi tuần (thứ Hai 00:00 → Chủ nhật 23:59 giờ Việt Nam) luân phiên
/// tuần Trang bị (đếm lượt Tài Xỉu) và tuần Linh Thú (đếm lượt Quay Linh Thú). Đạt mốc thì nhận quà.
/// Chỉ tính điểm và nhận quà khi có giờ máy chủ (đang online), để không chỉnh đồng hồ máy được.
/// </summary>
public static class SpinEvent
{
    public const string ChestItem = "ruong_thien_co";
    /// <summary>Thứ Hai mở đợt đầu (tuần Trang bị); các tuần sau xen kẽ Linh Thú → Trang bị → …</summary>
    public static readonly DateTimeOffset Anchor = new(2026, 10, 19, 0, 0, 0, TimeSpan.FromHours(7));
    public static readonly TimeSpan Length = TimeSpan.FromDays(7);
    /// <summary>Rương Thiên Cơ: 40% Tiên · 30% Đạo · 30% Huyền Tiên.</summary>
    public static readonly (string Rarity, double Chance)[] ChestOdds = { ("immortal", .40), ("dao", .30), ("supreme", .30) };

    public record Reward(string Item, int Qty);
    public record Milestone(int Points, Reward[] Rewards);
    public record Round(string Key, string Track, string Name, DateTimeOffset Start, DateTimeOffset End, Milestone[] Milestones);

    private static Milestone M(int points, params (string, int)[] rewards) => new(points, rewards.Select(r => new Reward(r.Item1, r.Item2)).ToArray());

    /// <summary>Tuần Trang bị: 1 lượt Tài Xỉu = 1 điểm (giới hạn 1.000 lượt/ngày).</summary>
    public static readonly Milestone[] GearMilestones =
    {
        M(10, ("ve_tai_xiu", 2), ("linh_thu_dan", 10)),
        M(30, ("hoa_hinh_dan", 3), ("linh_thu_dan", 20)),
        M(60, ("ve_tai_xiu", 5), ("hoan_cot_dan", 2)),
        M(100, ("thu_dan_huyen", 1), ("than_thu_huyet", 3)),
        M(160, ("ve_tai_xiu", 10), ("ve_linh_thu", 1)),
        M(240, ("thu_dan_dia", 1), ("hoan_cot_dan", 3)),
        M(340, ("ve_tai_xiu", 20), ("hoan_cot_dan", 5)),
        M(460, ("thu_dan_thien", 1), ("than_thu_huyet", 10)),
        M(600, (ChestItem, 1)),
    };
    /// <summary>Tuần Linh Thú: 1 lượt Quay Linh Thú = 1 điểm (giới hạn 300 lượt/ngày). Quà cuối: Thượng Cổ Thú Đản.</summary>
    public static readonly Milestone[] PetMilestones =
    {
        M(5, ("linh_thu_dan", 20)),
        M(15, ("ve_linh_thu", 1), ("hoa_hinh_dan", 3)),
        M(30, ("thu_dan_huyen", 1), ("linh_thu_dan", 30)),
        M(50, ("ve_linh_thu", 3), ("hoan_cot_dan", 2)),
        M(80, ("thu_dan_dia", 1), ("than_thu_huyet", 5)),
        M(120, ("ve_linh_thu", 5), ("hoa_hinh_dan", 5)),
        M(170, ("thu_dan_thien", 1), ("hoan_cot_dan", 5)),
        M(230, ("ve_linh_thu", 8), ("than_thu_huyet", 10)),
        M(300, ("thu_dan_thuong_co", 1)),
    };

    /// <summary>Đợt đang chạy tại thời điểm now (null = trước đợt đầu).</summary>
    public static Round? At(DateTimeOffset now)
    {
        if (now < Anchor) return null;
        var index = (int)((now - Anchor).Ticks / Length.Ticks);
        return Of(index);
    }
    public static Round Of(int index)
    {
        var start = Anchor + Length * index;
        var gear = index % 2 == 0;
        return new($"{start:yyyy-MM-dd}:{(gear ? "gear" : "pet")}", gear ? "gear" : "pet",
            gear ? "Thiên Cơ · Tuần Trang Bị" : "Thiên Cơ · Tuần Linh Thú", start, start + Length, gear ? GearMilestones : PetMilestones);
    }
    /// <summary>Khôi phục đợt từ khóa đã lưu trong save (để gửi quà còn sót khi sang đợt mới).</summary>
    public static Round? FromKey(string key)
    {
        var parts = key.Split(':');
        if (parts.Length != 2 || !DateTimeOffset.TryParseExact(parts[0] + " +07:00", "yyyy-MM-dd zzz", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var start)) return null;
        if (start < Anchor) return null;
        var index = (int)Math.Round((start - Anchor).TotalDays / Length.TotalDays);
        var round = Of(index);
        return round.Key == key ? round : null;
    }

    public static readonly ItemDef[] Items =
    {
        new ItemDef(ChestItem, "Rương Thiên Cơ", "eventchest", "#ffd36a",
            "Quà mốc cuối sự kiện Thiên Cơ tuần Trang bị. Mở ra 1 trang bị bộ cảnh giới hiện tại: 40% phẩm Tiên, 30% phẩm Đạo, 30% phẩm Huyền Tiên. Không giao dịch ở Chợ.",
            Icon: "img/ruong_boss.png", Tint: "hue-rotate(-25deg) saturate(1.8) brightness(1.25)", Rarity: "dao", Kind: "Rương sự kiện"),
    };
}
