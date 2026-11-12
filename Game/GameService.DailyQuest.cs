using System.Globalization;
using VanDao.Data;
namespace VanDao.Game;

/// <summary>Nhiệm vụ ngày/tuần (DailyQuestData): đếm việc, cộng Hoạt Lực, nhận rương mốc ngày và tuần.</summary>
public partial class GameService
{
    public static bool DailyQuestUnlocked(Player p) => p.NewbieQuestStep >= NewbieQuestData.Total;

    private static string DqDayOf(DateTimeOffset now) => LoginDayOf(now);
    /// <summary>Khóa tuần: ngày thứ Hai (giờ VN) của tuần chứa now.</summary>
    private static string DqWeekOf(DateTimeOffset now)
    {
        var d = now.ToOffset(SpinDayOffset).Date;
        return d.AddDays(-(((int)d.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
    private static long DqWeekResetAt(DateTimeOffset now)
    {
        var monday = DateTime.ParseExact(DqWeekOf(now), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new DateTimeOffset(monday.AddDays(7), SpinDayOffset).ToUnixTimeMilliseconds();
    }

    private static Dictionary<string, int> DqCountsOf(Player p)
    {
        var d = new Dictionary<string, int>();
        foreach (var part in (p.DqCounts ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split(':');
            if (kv.Length == 2 && DailyQuestData.TaskById.ContainsKey(kv[0]) && int.TryParse(kv[1], out var n) && n > 0) d[kv[0]] = n;
        }
        return d;
    }
    private static void DqSave(Player p, Dictionary<string, int> c) =>
        p.DqCounts = string.Join(",", c.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}:{kv.Value}"));

    private static int DqDailyPoints(Dictionary<string, int> c) =>
        DailyQuestData.Tasks.Where(t => c.GetValueOrDefault(t.Id) >= t.Need).Sum(t => t.Points);

    /// <summary>Sang ngày / tuần mới thì xóa bộ đếm và rương đã nhận của chu kỳ cũ.</summary>
    private static void SyncDailyQuest(Player p, DateTimeOffset now)
    {
        var day = DqDayOf(now);
        if (p.DqDay != day) { p.DqDay = day; p.DqCounts = ""; p.DqClaimed = 0; }
        var week = DqWeekOf(now);
        if (p.DqWeek != week) { p.DqWeek = week; p.DqWeekPoints = 0; p.DqWeekClaimed = 0; }
    }

    /// <summary>Gọi sau khi hành động thành công. Chỉ đếm khi đã mở khóa, có giờ máy chủ và không bị chặn quà sự kiện.
    /// Việc vừa đủ số lần thì cộng điểm việc đó vào điểm tuần (đúng một lần).</summary>
    public void RecordDaily(Player p, string ev, int n = 1) => RecordDailyAt(p, ev, n, EventNow);

    public static void RecordDailyAt(Player p, string ev, int n, DateTimeOffset? at)
    {
        if (n <= 0 || at is not { } now || !DailyQuestUnlocked(p) || EventBlocked(p) is not null) return;
        if (!DailyQuestData.TaskById.TryGetValue(ev, out var t)) return;
        SyncDailyQuest(p, now);
        var c = DqCountsOf(p);
        var before = c.GetValueOrDefault(ev);
        if (before >= t.Need) return;
        var after = Math.Min(t.Need, before + n);
        c[ev] = after;
        DqSave(p, c);
        if (after >= t.Need) p.DqWeekPoints = Math.Min(DailyQuestData.MaxWeekly, p.DqWeekPoints + t.Points);
    }

    public (string, string) ClaimDailyChest(Player p, int index) => ClaimDqChestAt(p, index, false, EventNow);
    public (string, string) ClaimWeeklyChest(Player p, int index) => ClaimDqChestAt(p, index, true, EventNow);

    public (string, string) ClaimDqChestAt(Player p, int index, bool weekly, DateTimeOffset? at)
    {
        if (at is not { } now) throw new GameException("Cần kết nối mạng để nhận rương nhiệm vụ.");
        if (EventBlocked(p) is { } why) throw new GameException(why);
        if (!DailyQuestUnlocked(p)) throw new GameException("Hoàn thành nhiệm vụ tân thủ để mở nhiệm vụ ngày.");
        SyncDailyQuest(p, now);
        var chests = weekly ? DailyQuestData.WeeklyChests : DailyQuestData.DailyChests;
        if (index < 0 || index >= chests.Length) throw new GameException("Rương không hợp lệ.");
        var points = weekly ? p.DqWeekPoints : DqDailyPoints(DqCountsOf(p));
        var claimed = weekly ? p.DqWeekClaimed : p.DqClaimed;
        var chest = chests[index];
        if ((claimed & (1 << index)) != 0) throw new GameException("Đã nhận rương này.");
        if (points < chest.Points) throw new GameException($"Cần {chest.Points} Hoạt Lực{(weekly ? " trong tuần" : "")}.");
        var rewards = DailyQuestData.RewardsFor(chest, p.Realm);
        var need = rewards.Select(r => r.Item).Distinct().Count(i => !IsCurrency(i) && p.Count(i) == 0);
        if (BagCap(p) - BagUsed(p) < need) throw new GameException($"Hành trang cần {need} ô trống để nhận quà.");
        foreach (var r in rewards) p.Add(r.Item, r.Qty);
        if (weekly) p.DqWeekClaimed |= 1 << index; else p.DqClaimed |= 1 << index;
        return Log(p, $"Rương Hoạt Lực {(weekly ? "tuần" : "ngày")} mốc {chest.Points}: nhận {string.Join(", ", rewards.Select(r => $"{r.Qty:N0} {GameData.ItemById[r.Item].Name}"))}.", "gold");
    }

    /// <summary>Trạng thái nhiệm vụ ngày/tuần; null khi chưa mở khóa. Chỉ đọc (chu kỳ cũ hiện như đã xóa).</summary>
    public DailyQuestDto? DailyQuestStateFor(Player p) => DailyQuestStateAt(p, EventNow);

    public static DailyQuestDto? DailyQuestStateAt(Player p, DateTimeOffset? now)
    {
        if (!DailyQuestUnlocked(p)) return null;
        var at = now ?? DateTimeOffset.UtcNow;
        var sameDay = p.DqDay == DqDayOf(at);
        var sameWeek = p.DqWeek == DqWeekOf(at);
        var c = sameDay ? DqCountsOf(p) : new Dictionary<string, int>();
        var tasks = DailyQuestData.Tasks.Select(t => new DailyQuestTaskDto(t.Id, t.Title, t.Tab, Math.Min(c.GetValueOrDefault(t.Id), t.Need), t.Need, t.Points)).ToList();
        List<DailyQuestChestDto> Chests(DailyQuestData.Chest[] cs, int claimed) => cs.Select((ch, i) => new DailyQuestChestDto(ch.Points,
            (claimed & (1 << i)) != 0, DailyQuestData.RewardsFor(ch, p.Realm).Select(r => new ItemQtyDto(r.Item, r.Qty)).ToList())).ToList();
        return new(now is not null, DqDailyPoints(c), DailyQuestData.MaxDaily, sameWeek ? p.DqWeekPoints : 0,
            tasks, Chests(DailyQuestData.DailyChests, sameDay ? p.DqClaimed : 0), Chests(DailyQuestData.WeeklyChests, sameWeek ? p.DqWeekClaimed : 0),
            LoginResetAt(at), DqWeekResetAt(at), EventBlocked(p));
    }
}
