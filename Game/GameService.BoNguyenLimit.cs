using System.Globalization;
using VanDao.Data;

namespace VanDao.Game;

public partial class GameService
{
    /// <summary>Giới hạn số Bổ Nguyên Đan được dùng mỗi ngày (reset 0h UTC+7), riêng từng phẩm.</summary>
    public static readonly IReadOnlyDictionary<string, int> BoNguyenDailyLimits = new Dictionary<string, int>
    {
        ["bo_nguyen_dan_ha"] = 50, ["bo_nguyen_dan_trung"] = 20, ["bo_nguyen_dan_thuong"] = 10,   // cân bằng 11/2026
    };
    private const string BoNguyenFlag = "bnlimit=";   // lưu trong Player.Admin (không cần migration): bnlimit=yyyy-MM-dd|id*số|id*số

    private static string BoNguyenDay(DateTimeOffset now) =>
        now.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Dictionary<string, int> BoNguyenUsedToday(Player p, DateTimeOffset now)
    {
        var flag = (p.Admin ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(f => f.StartsWith(BoNguyenFlag));
        var used = new Dictionary<string, int>();
        if (flag is null) return used;
        var parts = flag[BoNguyenFlag.Length..].Split('|');
        if (parts[0] != BoNguyenDay(now)) return used;
        foreach (var part in parts.Skip(1))
        {
            var kv = part.Split('*');
            if (kv.Length == 2 && int.TryParse(kv[1], out var n)) used[kv[0]] = Math.Max(0, n);
        }
        return used;
    }

    /// <summary>Số viên còn dùng được hôm nay; null nếu loại đan này không bị giới hạn.</summary>
    public static int? BoNguyenRemaining(Player p, string itemId, DateTimeOffset? now = null)
    {
        if (!BoNguyenDailyLimits.TryGetValue(itemId, out var limit)) return null;
        return Math.Max(0, limit - BoNguyenUsedToday(p, now ?? DateTimeOffset.UtcNow).GetValueOrDefault(itemId));
    }

    private static void BoNguyenRecord(Player p, string itemId, int n, DateTimeOffset now)
    {
        var used = BoNguyenUsedToday(p, now);
        used[itemId] = used.GetValueOrDefault(itemId) + n;
        var flag = BoNguyenFlag + BoNguyenDay(now) + string.Concat(used.Select(kv => $"|{kv.Key}*{kv.Value}"));
        p.Admin = string.Join(',', (p.Admin ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Where(f => !f.StartsWith(BoNguyenFlag)).Append(flag));
    }
}
