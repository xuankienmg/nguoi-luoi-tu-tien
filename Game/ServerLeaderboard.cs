using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>Bảng xếp hạng đã tính của cả máy chủ, dùng chung giữa các request (đăng ký singleton).</summary>
public sealed class ServerLeaderboardCache
{
    internal volatile ServerLeaderboard.Ranked[]? Rows;
    internal DateTime At;
    public void Forget() => Rows = null;

    /// <summary>Ảnh chụp đã ghi gần nhất của từng nhân vật: lần lưu không đổi gì thì khỏi đọc lại dòng bảng xếp hạng
    /// (kèm JSON Loadout / Equipment / Stats) ở mỗi lần đồng bộ. Mỗi lần đọc database đều tính vào Egress của Supabase.</summary>
    internal readonly ConcurrentDictionary<Guid, (string Key, string Loadout, DateTime At)> Staged = new();
}

/// <summary>
/// Bảng xếp hạng online ở chế độ Server: ảnh chụp mỗi nhân vật nằm trong game."LeaderboardEntries",
/// ghi cùng transaction lúc lưu nhân vật (OnlineBoundary). Hạng tính bằng C# theo đúng luật view leaderboard_public
/// (docs/leaderboard/supabase.sql): 10 hạng đầu chỉ dành cho nhân vật không gắn cờ.
/// </summary>
public sealed class ServerLeaderboard(GameDbContext db, ServerLeaderboardCache? cache = null)
{
    public const int Top = LeaderboardService.Top;
    private static readonly TimeSpan Refresh = TimeSpan.FromMinutes(3);    // không đổi gì: vẫn cập nhật tu vi mỗi 3 phút
    // hạng tính lại theo chu kỳ, không xóa cache mỗi khi có người lưu (trước đây gần như lần nào cũng đọc lại cả bảng)
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);

    /// <summary>Ghi ảnh chụp của nhân vật (kèm ảnh chụp kỹ năng cho Lôi đài) vào DbContext; gọi trước SaveChangesAsync để cùng transaction.
    /// force: ghi cả khi không đổi gì (Lôi đài cần tu vi mới nhất).</summary>
    public async Task StageAsync(Player p, bool force = false)
    {
        if (p.Tainted)
        {
            var t = await db.LeaderboardEntries.FindAsync(p.Id);
            if (t is not null) { db.LeaderboardEntries.Remove(t); cache?.Forget(); }
            cache?.Staged.TryRemove(p.Id, out _);
            return;
        }
        var s = LeaderboardSnapshot.Of(p, false);
        var loadout = JsonSerializer.Serialize(GameService.PvpFighterOf(p));
        var now = DateTime.UtcNow;
        if (!force && cache is not null && cache.Staged.TryGetValue(p.Id, out var last)
            && last.Key == s.Key && last.Loadout == loadout && now - last.At < Refresh) return;
        var e = await db.LeaderboardEntries.FindAsync(p.Id);
        if (e is null) { db.LeaderboardEntries.Add(e = new LeaderboardEntry { PlayerId = p.Id }); cache?.Forget(); }   // người mới: hiện ngay trên bảng
        else if (!force && e.Key == s.Key && e.Loadout == loadout && now - e.UpdatedAt < Refresh)
        {
            if (cache is not null) cache.Staged[p.Id] = (e.Key, loadout, e.UpdatedAt);
            return;
        }
        e.Loadout = loadout;
        e.Name = s.Name; e.Realm = s.Realm; e.Stage = s.Stage; e.Level = s.Level; e.Qi = s.Qi;
        e.RealmName = s.RealmName; e.StageName = s.StageName;
        e.Power = s.Power; e.Progress = s.Progress; e.PetPower = s.PetPower;
        e.Equipment = JsonSerializer.Serialize(s.Equipment); e.Stats = JsonSerializer.Serialize(s.Stats);
        e.Flagged |= s.Flagged;      // đã gắn cờ thì giữ
        e.Key = s.Key; e.UpdatedAt = now;
        if (cache is not null) cache.Staged[p.Id] = (s.Key, loadout, now);
    }

    internal sealed record Ranked(LeaderboardEntry Row, long RealmRank, long PowerRank, long ProgressRank, long PetRank, long AlchemyRank = 0, int AlchemyExp = 0)
    {
        public long RankBy(string by) => by switch { "power" => PowerRank, "progress" => ProgressRank, "pet" => PetRank, "alchemy" => AlchemyRank, _ => RealmRank };
    }

    /// <summary>EXP luyện đan gửi kèm ảnh chụp (stats["alchemy_exp"]); ảnh chụp cũ chưa có thì 0.</summary>
    private static int AlchemyExpOf(LeaderboardEntry e)
    {
        try { var v = StatsOf(e).GetValueOrDefault("alchemy_exp"); return double.IsFinite(v) ? (int)Math.Clamp(v, 0, int.MaxValue) : 0; }
        catch (JsonException) { return 0; }
    }

    /// <summary>Hạng = thứ tự theo (không gắn cờ và nằm trong 10 người sạch đầu tiên trước), rồi theo tiêu chí của bảng.</summary>
    private static Dictionary<Guid, long> Rank(LeaderboardEntry[] rows, Func<IEnumerable<LeaderboardEntry>, IOrderedEnumerable<LeaderboardEntry>> order)
    {
        var cleanTop = order(rows.Where(r => !r.Flagged)).Take(10).Select(r => r.PlayerId).ToHashSet();
        return order(rows).OrderBy(r => cleanTop.Contains(r.PlayerId) ? 0 : 1)   // OrderBy ổn định: giữ thứ tự tiêu chí trong mỗi nhóm
            .Select((r, i) => (r.PlayerId, Rank: (long)i + 1)).ToDictionary(x => x.PlayerId, x => x.Rank);
    }

    private async Task<Ranked[]> RankedAsync()
    {
        if (cache?.Rows is { } hit && DateTime.UtcNow - cache.At < CacheFor) return hit;
        // chỉ các cột dùng để xếp hạng / hiện bảng; Loadout và Equipment (JSON lớn) chỉ đọc khi xem hồ sơ hoặc đánh Lôi đài
        var rows = await db.LeaderboardEntries.AsNoTracking().Select(r => new LeaderboardEntry
        {
            PlayerId = r.PlayerId, Name = r.Name, Realm = r.Realm, Stage = r.Stage, Level = r.Level, Qi = r.Qi,
            RealmName = r.RealmName, StageName = r.StageName, Power = r.Power, Progress = r.Progress, PetPower = r.PetPower,
            Stats = r.Stats, Flagged = r.Flagged, UpdatedAt = r.UpdatedAt,
        }).ToArrayAsync();
        var realm = Rank(rows, q => q.OrderByDescending(r => r.Realm).ThenByDescending(r => r.Stage).ThenByDescending(r => r.Qi).ThenBy(r => r.PlayerId));
        var power = Rank(rows, q => q.OrderByDescending(r => r.Power).ThenBy(r => r.PlayerId));
        var progress = Rank(rows, q => q.OrderByDescending(r => r.Progress).ThenByDescending(r => r.Power).ThenBy(r => r.PlayerId));
        var pet = Rank(rows, q => q.OrderByDescending(r => r.PetPower).ThenBy(r => r.PlayerId));
        var exp = rows.ToDictionary(r => r.PlayerId, AlchemyExpOf);
        var alchemy = Rank(rows, q => q.OrderByDescending(r => exp[r.PlayerId]).ThenByDescending(r => r.Realm).ThenBy(r => r.PlayerId));
        var ranked = rows.Select(r => new Ranked(r, realm[r.PlayerId], power[r.PlayerId], progress[r.PlayerId], pet[r.PlayerId], alchemy[r.PlayerId], exp[r.PlayerId])).ToArray();
        if (cache is not null) { cache.At = DateTime.UtcNow; cache.Rows = ranked; }
        return ranked;
    }

    private static Dictionary<string, double> StatsOf(LeaderboardEntry e) =>
        JsonSerializer.Deserialize<Dictionary<string, double>>(e.Stats) ?? new();

    /// <param name="by">"realm", "power", "progress", "pet" hoặc "alchemy".</param>
    public async Task<LeaderboardDto> GetAsync(Player p, string? by)
    {
        by = by is "power" or "progress" or "pet" or "alchemy" ? by : "realm";
        var ranked = await RankedAsync();
        var titles = Titles(ranked, await ArenaChampionAsync());
        LbRowDto Dto(Ranked x)
        {
            var r = x.Row;
            var stats = StatsOf(r);
            return new(x.RankBy(by), r.Name, r.Realm, r.Stage, r.RealmName, r.StageName, r.Power, r.PlayerId == p.Id, r.PlayerId, r.Flagged,
                r.Progress, r.PetPower, LeaderboardService.RankedPet(stats), titles.GetValueOrDefault(r.PlayerId), LeaderboardService.RankedHero(stats),
                x.AlchemyExp, Alchemy.RankName(Alchemy.RankOf(x.AlchemyExp)));
        }
        var top = ranked.OrderBy(x => x.RankBy(by)).Take(Top).Select(Dto).ToList();
        var mine = p.Tainted ? null : ranked.FirstOrDefault(x => x.Row.PlayerId == p.Id);
        return new LeaderboardDto(true, by, top, mine is null ? null : Dto(mine), p.Tainted, p.Name, Flagged: SaveGuard.DataFlagged(p, false));
    }

    /// <summary>Ảnh trang phục đang hiện của từng người (Lôi đài); không có trên bảng hoặc chưa mặc thì bỏ qua.</summary>
    public async Task<Dictionary<Guid, string>> HeroesAsync(IEnumerable<Guid> ids)
    {
        var want = ids.ToHashSet();
        return (await RankedAsync()).Where(x => want.Contains(x.Row.PlayerId))
            .Select(x => (x.Row.PlayerId, Hero: LeaderboardService.RankedHero(StatsOf(x.Row))))
            .Where(x => x.Hero is not null).ToDictionary(x => x.PlayerId, x => x.Hero!);
    }

    /// <summary>Trang bị + chỉ số đã chụp của một người trên bảng. Null = không có trên bảng.</summary>
    public async Task<LbProfileDto?> ProfileAsync(Player me, Guid playerId)
    {
        var r = await db.LeaderboardEntries.AsNoTracking().FirstOrDefaultAsync(x => x.PlayerId == playerId);
        if (r is null) return null;
        var equipment = LeaderboardService.CurrentGear(JsonSerializer.Deserialize<Dictionary<string, string>>(r.Equipment));
        var stats = StatsOf(r);
        return new LbProfileDto(r.Name, r.Realm, r.Stage, r.RealmName, r.StageName, r.Power, equipment, stats,
            DateTime.SpecifyKind(r.UpdatedAt, DateTimeKind.Utc), r.PlayerId == me.Id, (await TitlesAsync()).GetValueOrDefault(r.PlayerId),
            LeaderboardService.RankedHero(stats), LeaderboardService.RankedPet(stats), r.PetPower, LeaderboardService.RankedPetGear(stats));
    }

    /// <summary>Danh hiệu Đệ nhất: hạng 1 không gắn cờ của mỗi bảng và của Lôi đài (như view titles_public).</summary>
    public async Task<Dictionary<Guid, List<TitleDto>>> TitlesAsync() => Titles(await RankedAsync(), await ArenaChampionAsync());

    /// <summary>Hạng 1 Lôi đài: Pos nhỏ nhất trong số người không bị gắn cờ (như view arena_public). Không cache: đổi sau mỗi trận.</summary>
    private Task<Guid?> ArenaChampionAsync() =>
        db.ArenaRanks.Where(a => !db.LeaderboardEntries.Any(e => e.PlayerId == a.PlayerId && e.Flagged)
                && db.LeaderboardEntries.Any(e => e.PlayerId == a.PlayerId))
            .OrderBy(a => a.Pos).Select(a => (Guid?)a.PlayerId).FirstOrDefaultAsync();

    private static Dictionary<Guid, List<TitleDto>> Titles(Ranked[] ranked, Guid? arenaChampion)
    {
        var holders = new List<(string id, Guid player)>();
        if (arenaChampion is { } champion) holders.Add(("arena", champion));
        foreach (var by in new[] { "realm", "power", "progress", "pet", "alchemy" })
        {
            var first = ranked.FirstOrDefault(x => x.RankBy(by) == 1);
            if (first is null || first.Row.Flagged) continue;
            if (by == "progress" && first.Row.Progress <= 0 || by == "pet" && first.Row.PetPower <= 0 || by == "alchemy" && first.AlchemyExp <= 0) continue;
            holders.Add((by, first.Row.PlayerId));
        }
        return holders.GroupBy(h => h.player).ToDictionary(g => g.Key,
            g => g.Select(h => Game.Titles.ById[h.id]).OrderBy(t => Array.IndexOf(Game.Titles.All, t)).ToList());
    }
}
