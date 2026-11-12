using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>
/// Cấp tông của từng nhân vật cho buff tu luyện trong GameService.Rate (hàm tĩnh, không đọc DB).
/// Cộng thêm SectBossData.BuffQiPct khi tông vừa hạ Boss hôm trước (Sect.BuffDay = hôm nay).
/// LoadAsync gọi EnsureAsync: đọc lại DB tối đa mỗi 60 giây một lần cho mỗi nhân vật. Hành động tông làm mới ngay.
/// </summary>
public static class SectBuff
{
    private static readonly ConcurrentDictionary<Guid, (int Level, string BuffDay, DateTime At)> cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public static int LevelOf(Guid id) => cache.TryGetValue(id, out var c) ? c.Level : 0;
    public static double QiPct(Guid id) => QiPct(id, DateTime.UtcNow);
    public static double QiPct(Guid id, DateTime now)
    {
        if (!cache.TryGetValue(id, out var c) || c.Level <= 0) return 0;
        return SectData.QiPct(c.Level) + (c.BuffDay == SectData.DayOf(now) ? SectBossData.BuffQiPct : 0);
    }

    public static async Task EnsureAsync(GameDbContext db, Guid id, DateTime now)
    {
        if (cache.TryGetValue(id, out var c) && now >= c.At && now - c.At < Ttl) return;
        int level = 0; string buffDay = "";
        try
        {
            var row = await db.SectMembers.AsNoTracking().Where(m => m.PlayerId == id && m.SectId != null)
                .Join(db.Sects, m => m.SectId, s => (long?)s.Id, (m, s) => new { s.Level, s.BuffDay }).FirstOrDefaultAsync();
            if (row is not null) { level = row.Level; buffDay = row.BuffDay; }
        }
        catch (Exception) when (db.Database.IsSqlite()) { level = 0; }   // file SQLite chưa nâng cấp bảng: không buff
        cache[id] = (level, buffDay, now);
    }

    public static void Set(Guid id, int level, DateTime now, string buffDay = "") => cache[id] = (level, buffDay, now);
    public static void Forget(Guid id) => cache.TryRemove(id, out _);
    /// <summary>Tông lên cấp / giải tán / hạ Boss: mọi thành viên đọc lại ở lần nạp kế.</summary>
    public static void Clear() => cache.Clear();
}
