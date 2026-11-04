using System.Globalization;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>Hiệu quả Bổ Nguyên Đan trong ngày: % tu vi nhận được, giờ tu luyện gốc đã nhận, mốc giảm kế tiếp (null = đã ở mức sàn).</summary>
public record BoNguyenDto(int EfficiencyPct, double HoursToday, double? NextDropHours, int? NextPct);

public partial class GameService
{
    /// <summary>Bổ Nguyên Đan không giới hạn lượt; hiệu quả giảm dần theo tổng giờ tu luyện gốc đã nhận từ đan trong ngày
    /// (cả ba phẩm tính chung, reset 0h UTC+7). Mỗi mốc: (từ giờ thứ, hiệu quả).</summary>
    public static readonly (double FromHours, double Efficiency)[] BoNguyenTiers =
        [(0, 1.0), (12, 0.5), (24, 0.25)];   // cân bằng 11/2026: bỏ giới hạn lượt; 12h 100%, 12–24h 50%, sau 24h 25% (sàn)

    public static bool IsBoNguyen(string itemId) => itemId.StartsWith("bo_nguyen_dan", StringComparison.Ordinal);

    private const string BoNguyenFlag = "bnqi=";        // lưu trong Player.Admin (không cần migration): bnqi=yyyy-MM-dd|giây gốc
    private const string BoNguyenOldFlag = "bnlimit=";  // định dạng cũ (giới hạn lượt), bỏ khi ghi

    private static string BoNguyenDay(DateTimeOffset now) =>
        now.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Số giây tu luyện gốc (chưa giảm) đã nhận từ Bổ Nguyên Đan hôm nay.</summary>
    public static double BoNguyenSecondsToday(Player p, DateTimeOffset? now = null)
    {
        var flag = (p.Admin ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(f => f.StartsWith(BoNguyenFlag));
        if (flag is null) return 0;
        var parts = flag[BoNguyenFlag.Length..].Split('|');
        if (parts.Length != 2 || parts[0] != BoNguyenDay(now ?? DateTimeOffset.UtcNow)) return 0;
        return double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? Math.Max(0, s) : 0;
    }

    /// <summary>Hiệu quả tại thời điểm đã nhận <paramref name="seconds"/> giây gốc.</summary>
    public static double BoNguyenEfficiencyAt(double seconds)
    {
        var eff = BoNguyenTiers[0].Efficiency;
        foreach (var (from, e) in BoNguyenTiers) if (seconds >= from * 3600) eff = e;
        return eff;
    }

    /// <summary>Số giây tu luyện thực nhận khi dùng <paramref name="secs"/> giây gốc, bắt đầu từ mức đã nhận <paramref name="from"/> (tách qua từng mốc).</summary>
    public static double BoNguyenEffectiveSeconds(double from, double secs)
    {
        double total = 0, at = from, end = from + secs;
        for (var i = 0; i < BoNguyenTiers.Length && at < end; i++)
        {
            var tierEnd = i + 1 < BoNguyenTiers.Length ? BoNguyenTiers[i + 1].FromHours * 3600 : double.PositiveInfinity;
            if (at >= tierEnd) continue;
            var upto = Math.Min(end, tierEnd);
            total += (upto - at) * BoNguyenTiers[i].Efficiency;
            at = upto;
        }
        return total;
    }

    public static BoNguyenDto BoNguyenState(Player p, DateTimeOffset? now = null)
    {
        var s = BoNguyenSecondsToday(p, now);
        var next = BoNguyenTiers.Where(t => t.FromHours * 3600 > s).Select(t => ((double, double)?)t).FirstOrDefault();
        return new BoNguyenDto((int)Math.Round(BoNguyenEfficiencyAt(s) * 100), Math.Round(s / 3600, 2),
            next?.Item1, next is { } n ? (int)Math.Round(n.Item2 * 100) : null);
    }

    private static void BoNguyenRecord(Player p, double seconds, DateTimeOffset now)
    {
        var total = BoNguyenSecondsToday(p, now) + seconds;
        var flag = BoNguyenFlag + BoNguyenDay(now) + "|" + total.ToString("0.###", CultureInfo.InvariantCulture);
        p.Admin = string.Join(',', (p.Admin ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(f => !f.StartsWith(BoNguyenFlag) && !f.StartsWith(BoNguyenOldFlag)).Append(flag));
    }
}
