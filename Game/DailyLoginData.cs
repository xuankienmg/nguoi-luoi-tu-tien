namespace VanDao.Game;

/// <summary>
/// Quà đăng nhập hằng ngày: vòng 7 ngày lặp lại. Mỗi ngày (00:00 giờ Việt Nam) nhận 1 ô quà kế tiếp;
/// bỏ lỡ ngày không mất tiến độ, chỉ nhận chậm hơn. Nhận đủ ngày 7 thì quay lại ngày 1.
/// Chỉ nhận khi có giờ máy chủ (đang online), giống sự kiện Thiên Cơ.
/// </summary>
public static class DailyLoginData
{
    public record Reward(string Item, int Qty);

    private static Reward[] R(params (string, int)[] rewards) => rewards.Select(r => new Reward(r.Item1, r.Item2)).ToArray();

    /// <summary>Quà từng ngày (chỉ số 0..6). Cân bằng 11/2026: tổng 1 vòng nhỏ hơn nhiều so với quà mốc sự kiện tuần.</summary>
    public static readonly Reward[][] Days =
    {
        R(("ve_tai_xiu", 2), ("linh_thu_dan", 5)),
        R(("hoa_hinh_dan", 1), ("linh_thu_dan", 10)),
        R(("ve_tai_xiu", 3), ("hoan_cot_dan", 1)),
        R(("ve_linh_thu", 1), ("than_thu_huyet", 1)),
        R(("ve_tai_xiu", 5), ("hoa_hinh_dan", 2)),
        R(("ve_linh_thu", 1), ("hoan_cot_dan", 2)),
        R(("thu_dan_huyen", 1), ("ve_tai_xiu", 5), ("than_thu_huyet", 2)),
    };
}
