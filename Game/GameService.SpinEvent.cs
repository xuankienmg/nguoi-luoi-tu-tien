using System.Globalization;
using VanDao.Data;
namespace VanDao.Game;

/// <summary>Sự kiện quay tích lũy Thiên Cơ: cộng điểm khi quay, nhận quà theo mốc, mở Rương Thiên Cơ.</summary>
public partial class GameService
{
    /// <summary>Giờ dùng cho sự kiện: giờ máy chủ Supabase; môi trường dev không cấu hình Supabase thì dùng giờ máy.</summary>
    private DateTimeOffset? EventNow => _version?.ServerNow ?? (Player.AdminEnabled ? DateTimeOffset.UtcNow : null);
    private static string? EventBlocked(Player p) =>
        Player.AdminEnabled ? null
        : p.Flagged ? "Dữ liệu nhân vật từng được mở bằng bản game cũ nên không nhận được quà sự kiện."
        : p.Tainted ? "Nhân vật đã dùng admin mode nên không nhận được quà sự kiện."
        : null;
    private static HashSet<int> Claimed(Player p) => p.EventClaimed.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : -1).Where(i => i >= 0).ToHashSet();
    private static string Rewards(SpinEvent.Milestone m) => string.Join(", ", m.Rewards.Select(r => $"{r.Qty} {GameData.ItemById[r.Item].Name}"));

    // Reset only after every earned milestone is delivered; keep spins beyond the cap.
    private static bool ResetCompletedEventCycle(Player p, SpinEvent.Round round)
    {
        var cap = round.Milestones[^1].Points;
        var claimed = Claimed(p);
        if (p.EventPoints < cap || !Enumerable.Range(0, round.Milestones.Length).All(claimed.Contains)) return false;
        p.EventPoints -= cap;
        p.EventClaimed = "";
        return true;
    }

    /// <summary>Sang đợt mới: gửi quà đã đạt mà chưa nhận của đợt cũ vào túi (bỏ qua giới hạn túi để không mất), rồi đặt lại tiến độ. Offline thì giữ nguyên.</summary>
    private void SyncEvent(Player p)
    {
        if (EventNow is not { } now) return;
        var key = SpinEvent.At(now)?.Key ?? "";
        if (p.EventKey == key)
        {
            if (SpinEvent.At(now) is { } current && EventBlocked(p) is null) ResetCompletedEventCycle(p, current);
            return;
        }
        if (SpinEvent.FromKey(p.EventKey) is { } old && EventBlocked(p) is null)
        {
            var claimed = Claimed(p);
            var cap = old.Milestones[^1].Points;
            var cycles = Math.Max(0, p.EventPoints) / cap;
            var remainder = Math.Max(0, p.EventPoints) % cap;
            var left = old.Milestones.Select((m, i) => (m, count: Math.Max(0,
                cycles + (remainder >= m.Points ? 1 : 0) - (claimed.Contains(i) ? 1 : 0))))
                .Where(x => x.count > 0).ToList();
            foreach (var (m, count) in left) foreach (var r in m.Rewards) p.Add(r.Item, r.Qty * count);
            if (left.Count > 0) Log(p, $"{old.Name} đã kết thúc: gửi {left.Sum(x => x.count)} mốc quà chưa nhận vào hành trang ({string.Join("; ", left.Select(x => $"{x.count} × ({Rewards(x.m)})"))}).", "gold");
        }
        p.EventKey = key; p.EventPoints = 0; p.EventClaimed = "";
    }

    /// <summary>Cộng điểm khi quay đúng loại vòng của tuần (gear · pet). Offline / ngoài đợt không cộng. Trả về số điểm đã cộng.</summary>
    private int AddEventPoints(Player p, string track, int n)
    {
        SyncEvent(p);
        if (EventNow is not { } now || SpinEvent.At(now) is not { } round || round.Track != track || p.EventKey != round.Key) return 0;
        p.EventPoints += n;
        return n;
    }

    /// <summary>Nhận quà một mốc (index) hoặc mọi mốc đã đạt (index &lt; 0). Phải online.</summary>
    public (string, string) ClaimEvent(Player p, int index)
    {
        if (EventNow is not { } now) throw new GameException("Cần kết nối mạng để nhận quà sự kiện.");
        if (EventBlocked(p) is { } why) throw new GameException(why);
        SyncEvent(p);
        if (SpinEvent.At(now) is not { } round) throw new GameException("Hiện chưa có sự kiện.");
        var claimed = Claimed(p);
        var pick = round.Milestones.Select((m, i) => (m, i))
            .Where(x => (index < 0 || x.i == index) && p.EventPoints >= x.m.Points && !claimed.Contains(x.i)).ToList();
        if (pick.Count == 0)
            throw new GameException(index >= 0 && index < round.Milestones.Length
                ? claimed.Contains(index) ? "Đã nhận quà mốc này." : $"Cần {round.Milestones[index].Points} điểm (đang có {p.EventPoints})."
                : "Chưa có mốc nào nhận được.");
        var items = pick.SelectMany(x => x.m.Rewards).Select(r => r.Item).Distinct().ToList();
        var need = items.Count(i => p.Count(i) == 0);
        if (BagCap(p) - BagUsed(p) < need) throw new GameException($"Hành trang cần {need} ô trống để nhận quà.");
        foreach (var (m, i) in pick) { foreach (var r in m.Rewards) p.Add(r.Item, r.Qty); claimed.Add(i); }
        p.EventClaimed = string.Join(',', claimed.OrderBy(i => i));
        var reset = ResetCompletedEventCycle(p, round);
        return Log(p, $"{round.Name}: nhận {pick.Count} mốc quà ({string.Join("; ", pick.Select(x => Rewards(x.m)))})."
            + (reset ? $" Bắt đầu chu kỳ mới, giữ {p.EventPoints} điểm dư." : ""), "gold");
    }

    public SpinEventDto EventStateFor(Player p)
    {
        var now = EventNow;
        var online = now is not null;
        var at = now ?? DateTimeOffset.UtcNow;
        var round = SpinEvent.At(at);
        var next = round is null ? (at < SpinEvent.Anchor ? SpinEvent.Of(0) : null) : SpinEvent.At(round.End);
        if (round is null)
            return new(false, online, "", "", "", 0, 0, 0, [], EventBlocked(p), next?.Track ?? "", next?.Start.ToUnixTimeMilliseconds() ?? 0);
        // offline: hiện tiến độ đã lưu nếu cùng đợt, không thì 0
        var points = p.EventKey == round.Key ? p.EventPoints : 0;
        var claimed = p.EventKey == round.Key ? Claimed(p) : [];
        var ms = round.Milestones.Select((m, i) => new SpinEventMilestoneDto(m.Points, m.Rewards.Select(r => new ItemQtyDto(r.Item, r.Qty)).ToList(), claimed.Contains(i), points >= m.Points && !claimed.Contains(i))).ToList();
        return new(true, online, round.Key, round.Track, round.Name, round.Start.ToUnixTimeMilliseconds(), round.End.ToUnixTimeMilliseconds(), points, ms,
            EventBlocked(p), next?.Track ?? "", next?.Start.ToUnixTimeMilliseconds() ?? 0);
    }

    /// <summary>Mở Rương Thiên Cơ (n = số rương, 0 = hết): mỗi rương 1 trang bị bộ cảnh giới hiện tại, 40% Tiên · 30% Đạo · 30% Huyền Tiên.</summary>
    private (string msg, string kind, CraftResultDto craft) OpenEventChest(Player p, ItemDef chest, int n)
    {
        var have = p.Count(chest.Id);
        if (have < 1) throw new GameException("Không có rương này trong hành trang.");
        var want = n <= 0 ? have : Math.Min(n, have);
        var free = BagCap(p) - BagUsed(p) + (want == have ? 1 : 0);
        want = Math.Min(want, free);
        if (want < 1) throw new GameException("Hành trang đầy, dọn bớt rồi mở rương.");
        var pool = GameData.SpinPool(p.Realm);
        var outs = new List<string>();
        for (var i = 0; i < want; i++)
        {
            var roll = Random.Shared.NextDouble(); var rarity = SpinEvent.ChestOdds[^1].Rarity;
            foreach (var (r, c) in SpinEvent.ChestOdds) { if ((roll -= c) < 0) { rarity = r; break; } }
            outs.Add(GameData.GearId(pool[Random.Shared.Next(pool.Length)], Array.IndexOf(GameData.RarityOrder, rarity)));
        }
        p.Add(chest.Id, -want);
        foreach (var id in outs) p.Add(id, 1);
        var got = outs.GroupBy(x => x).Select(g => new ItemQtyDto(g.Key, g.Count())).OrderByDescending(x => Array.IndexOf(GameData.RarityOrder, GameData.ItemById[x.Item].Rarity)).ToList();
        var (msg, kind) = Log(p, $"Mở {(want > 1 ? $"{want} " : "")}{chest.Name}: nhận {string.Join(", ", got.Select(g => $"{g.Qty} {GameData.Label(GameData.ItemById[g.Item])}"))}.", "gold");
        return (msg, kind, new CraftResultDto("chest:thien_co", got[0], new() { new(chest.Id, want) }, got));
    }
}
