using System.Globalization;
using VanDao.Data;

namespace VanDao.Game;

public partial class GameService
{
    public const int SpinDailyLimit = 1000;
    public const int PetSpinDailyLimit = 300;
    private static readonly TimeSpan SpinDayOffset = TimeSpan.FromHours(7);
    public static SpinDailyDto SpinDailyFor(Player p, DateTimeOffset? now = null, bool pet = false)
    {
        var local = (now ?? DateTimeOffset.UtcNow).ToOffset(SpinDayOffset);
        var reset = new DateTimeOffset(local.Date.AddDays(1), SpinDayOffset).ToUnixTimeMilliseconds();
        var day = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var limit = pet ? PetSpinDailyLimit : SpinDailyLimit;
        var savedDay = pet ? p.PetSpinDay : p.SpinDay;
        var savedUsed = pet ? p.PetSpinDailyUsed : p.SpinDailyUsed;
        var used = savedDay == day ? Math.Clamp(savedUsed, 0, limit) : 0;
        // Save cũ: tính lượt hôm nay còn trong lịch sử, rồi lưu bộ đếm riêng từ lần quay kế tiếp.
        if (string.IsNullOrEmpty(savedDay))
        {
            foreach (var entry in p.SpinHistory.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split('|');
                if (parts.Length != 2 || !long.TryParse(parts[0], out var seconds) || seconds < 0 || seconds > 253402300799) continue;
                if (seconds >= reset / 1000 - 86400 && seconds < reset / 1000)
                    used += parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Count(id => (PetData.EggQuality(id) >= 0) == pet);
            }
            used = Math.Min(limit, used);
        }
        return new(limit, used, limit - used, reset, Math.Clamp(pet ? p.PetSpinPityUsed : p.SpinPityUsed, 0, SpinPity.Threshold));
    }
    private static SpinDailyDto CheckSpinDaily(Player p, int n, DateTimeOffset? now, bool pet = false)
    {
        var quota = SpinDailyFor(p, now, pet);
        if (n > quota.Remaining)
            throw new GameException($"Mỗi ngày chỉ được quay {quota.Limit} lượt {(pet ? "linh thú" : "trang bị")}. Còn {quota.Remaining} lượt; đặt lại lúc 00:00 giờ Việt Nam.");
        return quota;
    }
    private static void RecordSpinDaily(Player p, int n, SpinDailyDto quota, bool pet = false)
    {
        var day = DateTimeOffset.FromUnixTimeMilliseconds(quota.ResetAt).ToOffset(SpinDayOffset).AddDays(-1)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (pet) { p.PetSpinDay = day; p.PetSpinDailyUsed = quota.Used + n; }
        else { p.SpinDay = day; p.SpinDailyUsed = quota.Used + n; }
    }
}
