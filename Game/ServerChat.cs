using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>
/// Kênh Thế giới ở chế độ online Server: tin nằm trong game."ChatMessages".
/// Chỉ đọc vài cột nhân vật bằng AsNoTracking — poll 4 giây/lần không nạp cây nhân vật, không đụng PlayerCache.
/// Query filter theo OwnerId của Players giữ nguyên: người khác không gửi được dưới tên mình.
/// </summary>
public sealed class ServerChat(GameDbContext db)
{
    private static (DateTime At, int N) onlineCache = (DateTime.MinValue, 0);
    public static void ResetOnlineCacheForTests() => onlineCache = (DateTime.MinValue, 0);

    private sealed record Author(string Name, int Realm, bool Blocked);

    private async Task<Author> AuthorAsync(Guid id) =>
        await db.Players.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new Author(x.Name, x.Realm, x.Flagged || x.Tainted)).FirstOrDefaultAsync()
        ?? throw new KeyNotFoundException();

    private Task<DateTime?> LastPostAsync(Guid id) =>
        db.ChatMessages.Where(m => m.PlayerId == id).MaxAsync(m => (DateTime?)m.At);

    public async Task<ChatPageDto> PageAsync(Guid id, long after, DateTime now)
    {
        var me = await AuthorAsync(id);
        var q = db.ChatMessages.AsNoTracking().Where(m => m.SectId == null)   // tin kênh Tông Môn nằm ở ServerSect
            .Where(m => m.PlayerId == null ||!db.LeaderboardEntries.Any(e => e.PlayerId == m.PlayerId && e.Flagged));
        var rows = after > 0
            ? await q.Where(m => m.Id > after).OrderBy(m => m.Id).Take(ChatRules.PageSize).ToListAsync()
            : (await q.OrderByDescending(m => m.Id).Take(ChatRules.PageSize).ToListAsync()).AsEnumerable().Reverse().ToList();
        var why = ChatRules.LockReason(me.Realm, me.Blocked);
        var wait = why is null ? ChatRules.CooldownLeft(await LastPostAsync(id), now) : 0;
        return new ChatPageDto(true, rows.Select(m => Dto(m, id)).ToList(), await OnlineAsync(now),
            why is null && wait == 0, wait, why, ChatRules.MaxLen);
    }

    public async Task<ChatPageDto> PostAsync(Guid id, string? text, long after, DateTime now)
    {
        var me = await AuthorAsync(id);
        if (ChatRules.LockReason(me.Realm, me.Blocked) is { } why) throw new GameException(why);
        var clean = ChatRules.Clean(text);
        if (clean.Length == 0) throw new GameException("Chưa nhập nội dung.");
        var wait = ChatRules.CooldownLeft(await LastPostAsync(id), now);
        if (wait > 0) throw new GameException($"Chờ {wait} giây nữa mới truyền âm tiếp được.");
        db.ChatMessages.Add(new ChatMessage { At = now, Kind = ChatRules.KindPlayer, PlayerId = id, Name = me.Name, Realm = me.Realm, Text = clean });
        await db.SaveChangesAsync();
        return await PageAsync(id, after, now);
    }

    /// <summary>Tin hệ thống: chỉ Add, lưu cùng lần SaveChanges của người gọi (GameService lưu nhân vật).</summary>
    public static void AddSystem(GameDbContext db, string text, DateTime now) =>
        db.ChatMessages.Add(new ChatMessage { At = now, Kind = ChatRules.KindSystem,
            Text = text.Length <= ChatRules.SystemMaxLen ? text : text[..ChatRules.SystemMaxLen] });

    public static Task<int> DeleteOldAsync(GameDbContext db, DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddDays(-ChatRules.RetainDays);
        return db.ChatMessages.Where(m => m.At < cutoff).ExecuteDeleteAsync(ct);
    }

    /// <summary>Số nhân vật có tick trong 5 phút gần nhất, đệm 30 giây. Bỏ query filter OwnerId để đếm toàn máy chủ.</summary>
    private async Task<int> OnlineAsync(DateTime now)
    {
        var c = onlineCache;
        if (now >= c.At && now - c.At < TimeSpan.FromSeconds(30)) return c.N;
        var since = now.AddMinutes(-ChatRules.OnlineMinutes);
        var n = await db.Players.IgnoreQueryFilters().CountAsync(x => x.LastTickAt > since);
        onlineCache = (now, n);
        return n;
    }

    private static ChatMessageDto Dto(ChatMessage m, Guid me) => new(m.Id, m.At, m.Kind, m.Name, m.Realm,
        m.Kind == ChatRules.KindSystem ? "" : GameData.Realms[Math.Clamp(m.Realm, 0, GameData.Realms.Count - 1)].Name,
        m.Text, m.PlayerId == me);
}
