using System.Collections.Concurrent;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>
/// Cây nhân vật (Player + Inventory + Equipment + Pets) giữ trong RAM ở chế độ Server (singleton, cờ Online:PlayerCache).
/// Mỗi lần đồng bộ chỉ gắn lại cây vào DbContext của request rồi ghi phần thay đổi, không đọc lại ~25–40 KB từ Supabase (Egress).
/// Cây chỉ đúng khi mọi lần ghi nhân vật đi qua process này: lỗi / rollback bất kỳ thì Evict, ghi từ nơi khác thì
/// xmin của dòng Player (concurrency token) làm lần lưu sau báo xung đột → GameService.ActAsync nạp lại và chạy lại.
/// Mỗi tài khoản chỉ có một request chạy cùng lúc (khóa advisory trong OnlineBoundary), nên một cây không bị hai request dùng chung.
/// </summary>
public sealed class PlayerCache(TimeProvider? clock = null)
{
    /// <summary>Không dùng quá lâu thì bỏ khỏi RAM; lần sau đọc lại database.</summary>
    public static readonly TimeSpan Idle = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SweepEvery = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly ConcurrentDictionary<Guid, Entry> _items = new();
    private long _nextSweep;   // UtcTicks; đọc / ghi qua Volatile: nhiều request của các tài khoản khác nhau chạy song song

    private sealed class Entry(Player player, DateTimeOffset now)
    {
        public readonly Player Player = player;
        public long LastUsed = now.UtcTicks;   // Volatile: Sweep của request khác đọc cùng lúc
        // nhật ký: Id dòng mới nhất đã biết, số dòng trong database, các dòng hiển thị (mới nhất trước)
        public bool LogsKnown;
        public long LogTop;
        public int LogCount;
        public LogDto[] Logs = [];
    }

    public int Count => _items.Count;

    public bool Contains(Guid id) => _items.ContainsKey(id);

    /// <summary>Lấy cây đã nạp (chưa hết hạn). Danh sách Logs được làm rỗng: các dòng cũ đã nằm trong database.</summary>
    public Player? TryTake(Guid id)
    {
        var now = _clock.GetUtcNow();
        Sweep(now);
        if (!_items.TryGetValue(id, out var e)) return null;
        if (Expired(e, now)) { _items.TryRemove(KeyValuePair.Create(id, e)); return null; }
        Volatile.Write(ref e.LastUsed, now.UtcTicks);
        e.Player.Logs.Clear();
        return e.Player;
    }

    /// <summary>Cây vừa đọc từ database (đã chuyển đổi dữ liệu bản cũ).</summary>
    public void Put(Player p)
    {
        var now = _clock.GetUtcNow();
        Sweep(now);
        _items[p.Id] = new Entry(p, now);
    }

    public void Evict(Guid id) => _items.TryRemove(id, out _);

    private void Sweep(DateTimeOffset now)
    {
        if (now.UtcTicks < Volatile.Read(ref _nextSweep)) return;
        Volatile.Write(ref _nextSweep, (now + SweepEvery).UtcTicks);
        foreach (var (id, e) in _items)
            if (Expired(e, now)) _items.TryRemove(KeyValuePair.Create(id, e));
    }

    private static bool Expired(Entry e, DateTimeOffset now) => now.UtcTicks - Volatile.Read(ref e.LastUsed) > Idle.Ticks;

    private Entry? Current(Player p) => _items.TryGetValue(p.Id, out var e) && ReferenceEquals(e.Player, p) ? e : null;

    // ================= Nhật ký =================

    public bool TryGetLogs(Player p, out LogDto[] logs)
    {
        if (Current(p) is { LogsKnown: true } e) { logs = e.Logs; return true; }
        logs = [];
        return false;
    }

    /// <summary>Vừa đọc nhật ký từ database: top = Id lớn nhất, count = số dòng của nhân vật.</summary>
    public void SetLogs(Player p, long top, int count, IEnumerable<LogDto> newestFirst)
    {
        if (Current(p) is not { } e) return;
        e.LogTop = top; e.LogCount = count; e.Logs = newestFirst.ToArray(); e.LogsKnown = true;
    }

    /// <summary>Sau khi ghi thành công: đưa các dòng vừa có Id vào danh sách. Trả về true khi số dòng vượt keep (cần cắt),
    /// null khi chưa biết số dòng (cần hỏi database như trước).</summary>
    public bool? NoteSavedLogs(Player p, int show, int keep)
    {
        if (Current(p) is not { LogsKnown: true } e) return null;
        var fresh = p.Logs.Where(l => l.Id > e.LogTop).OrderByDescending(l => l.Id).ToList();
        if (fresh.Count > 0)
        {
            e.Logs = fresh.Select(l => new LogDto(DateTime.SpecifyKind(l.At, DateTimeKind.Utc), l.Text, l.Kind)).Concat(e.Logs).Take(show).ToArray();
            e.LogTop = fresh[0].Id;
            e.LogCount += fresh.Count;
        }
        return e.LogCount > keep;
    }

    public void LogsTrimmed(Player p, int deleted)
    {
        if (Current(p) is { LogsKnown: true } e) e.LogCount = Math.Max(0, e.LogCount - deleted);
    }

    /// <summary>Nhật ký bị xóa / sửa ngoài luồng thường (reset nhân vật): lần sau đọc lại.</summary>
    public void ForgetLogs(Guid id)
    {
        if (_items.TryGetValue(id, out var e)) e.LogsKnown = false;
    }

    // ================= Ranh giới transaction (OnlineBoundary) =================

    /// <summary>Transaction của request đã commit: ghi nhận các dòng nhật ký được lưu ở lần SaveChanges cuối của OnlineBoundary.</summary>
    public void Committed(GameDbContext db)
    {
        foreach (var id in db.TouchedPlayers)
            if (_items.TryGetValue(id, out var e)) NoteSavedLogs(e.Player, GameService.LogShow, int.MaxValue);
    }

    /// <summary>Transaction không commit (lỗi, mã ≥ 400, request bị hủy): cây trong RAM có thể đã bị sửa, bỏ đi.</summary>
    public void Abandoned(GameDbContext db)
    {
        foreach (var id in db.TouchedPlayers) Evict(id);
    }
}
