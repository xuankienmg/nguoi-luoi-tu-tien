using VanDao.Data;
namespace VanDao.Game;

/// <summary>Bí cảnh Yêu Triều: mỗi ngày giờ VN vào tối đa 2 lần; mỗi lần vào nhận Linh Thú Đan một lần khi kết thúc ván.</summary>
public partial class GameService
{
    /// <summary>Đưa lượt trong ngày về 0 khi sang ngày mới (giờ VN).</summary>
    private static void SwarmRollDay(Player p, string day)
    {
        if (p.SwarmDay == day) return;
        p.SwarmDay = day; p.SwarmEntries = 0; p.SwarmClaims = 0;
    }

    /// <summary>Vào bí cảnh: tốn 1 lượt trong ngày. Lượt chưa nhận thưởng của ván trước (thoát giữa chừng) bị bỏ.</summary>
    public (string, string) EnterSwarm(Player p)
    {
        if (EventNow is not { } now) throw new GameException("Cần kết nối mạng để vào Bí Cảnh.");
        SwarmRollDay(p, LoginDayOf(now));
        if (p.SwarmEntries >= SwarmData.DailyEntries)
            throw new GameException($"Hôm nay đã vào Yêu Triều {SwarmData.DailyEntries}/{SwarmData.DailyEntries} lần; quay lại sau 00:00 giờ Việt Nam.");
        p.SwarmClaims = p.SwarmEntries;   // ván trước chưa nhận thưởng: bỏ, không nhận bù
        p.SwarmEntries++;
        return ($"Tiến vào Yêu Triều (lượt {p.SwarmEntries}/{SwarmData.DailyEntries} hôm nay).", "good");
    }

    /// <summary>Kết thúc ván: nhận Linh Thú Đan theo quái đã hạ, chỉ một lần cho mỗi lượt vào.</summary>
    public (string, string) ClaimSwarm(Player p, Dictionary<string, int>? kills, int seconds)
    {
        if (EventNow is not { } now) throw new GameException("Cần kết nối mạng để nhận thưởng Yêu Triều.");
        SwarmRollDay(p, LoginDayOf(now));
        if (p.SwarmClaims >= p.SwarmEntries) throw new GameException("Ván này đã nhận thưởng hoặc chưa vào Bí Cảnh.");
        var pills = SwarmData.Pills(kills ?? [], seconds);
        p.SwarmClaims = p.SwarmEntries;
        if (pills < 1) return ("Ván này chưa hạ đủ yêu để nhận Linh Thú Đan.", "bad");
        p.Add(SwarmData.Reward, pills);
        return Log(p, $"Yêu Triều: nhận {pills} {GameData.ItemById[SwarmData.Reward].Name} (lượt {p.SwarmEntries}/{SwarmData.DailyEntries} hôm nay).", "gold");
    }

    /// <summary>Số lượt đã vào Yêu Triều hôm nay (offline: tính theo giờ máy, chỉ để hiển thị).</summary>
    public SwarmDto SwarmStateFor(Player p)
    {
        var day = LoginDayOf(EventNow ?? DateTimeOffset.UtcNow);
        return new(p.SwarmDay == day ? Math.Min(p.SwarmEntries, SwarmData.DailyEntries) : 0, SwarmData.DailyEntries);
    }
}
