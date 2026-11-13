using System.Globalization;
using VanDao.Data;
namespace VanDao.Game;

/// <summary>Quà đăng nhập hằng ngày (DailyLoginData): mỗi ngày giờ Việt Nam nhận 1 ô trong vòng 7 ngày.</summary>
public partial class GameService
{
    internal static string LoginDayOf(DateTimeOffset now) => now.ToOffset(SpinDayOffset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static long LoginResetAt(DateTimeOffset now)
    {
        var local = now.ToOffset(SpinDayOffset);
        return new DateTimeOffset(local.Date.AddDays(1), SpinDayOffset).ToUnixTimeMilliseconds();
    }
    private static int LoginStep(Player p) => Math.Clamp(p.LoginStep, 0, DailyLoginData.Days.Length - 1);

    /// <summary>Nhận quà đăng nhập hôm nay. Phải online (giờ máy chủ), mỗi ngày một lần.</summary>
    public (string, string) ClaimDailyLogin(Player p)
    {
        if (EventNow is not { } now) throw new GameException("Cần kết nối mạng để nhận quà đăng nhập.");
        if (EventBlocked(p) is { } why) throw new GameException(why);
        var today = LoginDayOf(now);
        if (p.LoginDay == today) throw new GameException("Hôm nay đã nhận quà đăng nhập; quay lại sau 00:00 giờ Việt Nam.");
        var step = LoginStep(p);
        var rewards = DailyLoginData.Days[step];
        var need = rewards.Select(r => r.Item).Distinct().Count(i => !IsCurrency(i) && p.Count(i) == 0);
        if (BagCap(p) - BagUsed(p) < need) throw new GameException($"Hành trang cần {need} ô trống để nhận quà.");
        foreach (var r in rewards) p.Add(r.Item, r.Qty);
        p.LoginDay = today;
        p.LoginStep = (step + 1) % DailyLoginData.Days.Length;
        RecordDaily(p, DailyQuestData.EvLogin);
        return Log(p, $"Quà đăng nhập ngày {step + 1}/{DailyLoginData.Days.Length}: nhận {string.Join(", ", rewards.Select(r => $"{r.Qty} {GameData.ItemById[r.Item].Name}"))}.", "gold");
    }

    public DailyLoginDto DailyLoginStateFor(Player p)
    {
        var now = EventNow;
        var at = now ?? DateTimeOffset.UtcNow;
        var claimedToday = p.LoginDay == LoginDayOf(at);
        // Đã nhận hôm nay: ô vừa nhận là ô trước LoginStep (ngày 7 vừa nhận thì LoginStep đã về 0).
        var step = LoginStep(p);
        var today = claimedToday ? (step + DailyLoginData.Days.Length - 1) % DailyLoginData.Days.Length : step;
        var days = DailyLoginData.Days.Select(d => d.Select(r => new ItemQtyDto(r.Item, r.Qty)).ToList()).ToList();
        return new(now is not null, today, claimedToday, LoginResetAt(at), days, EventBlocked(p));
    }
}
