using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>
/// Lôi đài online ở chế độ Server: bậc thang trong game."ArenaRanks", trận trong game."ArenaFights", ảnh chụp kỹ năng
/// trong LeaderboardEntries.Loadout. Begin và Finish chạy trong cùng request và transaction của OnlineBoundary,
/// backend tự đánh trận nên client không báo được kết quả. Luật giống arena_rules() (docs/arena/supabase.sql).
/// Mọi thay đổi ArenaRanks chạy sau khóa Lôi đài; thứ tự khóa luôn là tài khoản rồi Lôi đài, nên không deadlock.
/// </summary>
public sealed partial class ServerArena(GameDbContext db) : IArenaStore
{
    public bool Enabled => true;
    public bool Mock => false;

    /// <summary>Khóa giao dịch chung của Lôi đài (Postgres); SQLite trong test chạy một luồng nên bỏ qua.</summary>
    private async Task LockAsync()
    {
        if (db.Database.ProviderName?.Contains("Npgsql") == true)
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtextextended('vandao-arena', 0))");
    }

    /// <summary>Tầm khiêu chiến: max(5, 20% hạng hiện tại) hạng phía trên.</summary>
    public static long Window(long rank) => Math.Max(ArenaService.MinWindow, (long)Math.Ceiling(rank * ArenaService.WindowPct / 100.0));

    /// <summary>0h hôm nay theo giờ Việt Nam (UTC+7), tính theo UTC.</summary>
    public static DateTime DayStart(DateTime utcNow) => utcNow.AddHours(7).Date.AddHours(-7);

    private async Task<LeaderboardEntry> EntryAsync(Player p)
    {
        var e = await db.LeaderboardEntries.AsNoTracking().FirstOrDefaultAsync(x => x.PlayerId == p.Id)
            ?? throw new GameException("Nhân vật chưa có trên bảng xếp hạng. Chờ vài giây rồi thử lại.");
        if (e.Flagged) throw new GameException("Dữ liệu nhân vật từng được mở bằng bản game cũ nên không vào được Lôi đài.");
        return e;
    }

    /// <summary>Vào Lôi đài lần đầu: đứng cuối bậc thang (dưới khóa, hai người cùng vào không trùng Pos).</summary>
    private async Task JoinAsync(Player p)
    {
        if (await db.ArenaRanks.AnyAsync(a => a.PlayerId == p.Id)) return;
        await LockAsync();
        if (await db.ArenaRanks.AnyAsync(a => a.PlayerId == p.Id)) return;
        var pos = (await db.ArenaRanks.MaxAsync(a => (long?)a.Pos) ?? 0) + 1;
        db.ArenaRanks.Add(new ArenaRank { PlayerId = p.Id, Pos = pos });
        await db.SaveChangesAsync();
    }

    /// <summary>Hạng hiển thị: toàn bộ người chơi xếp theo ĐIỂM (nhiều điểm đứng trên), bằng điểm thì vào trước (Pos nhỏ); bỏ người bị gắn cờ.
    /// Khiêu chiến không đổi chỗ: chỗ đứng chỉ đổi khi điểm đổi.</summary>
    private async Task<List<ArenaService.Row>> LadderAsync() => (await LadderPosAsync()).Select(x => x.Row).ToList();

    /// <summary>Bậc thang kèm Pos (thứ tự vào trước) để tự xếp lại khi điểm đổi mà không phải đọc lại cả bảng.</summary>
    private async Task<List<(ArenaService.Row Row, long Pos)>> LadderPosAsync()
    {
        var rows = await (from a in db.ArenaRanks.AsNoTracking()
                          join e in db.LeaderboardEntries.AsNoTracking() on a.PlayerId equals e.PlayerId
                          where !e.Flagged
                          orderby a.Points descending, a.Pos
                          select new { a.PlayerId, a.Pos, a.Wins, a.Losses, a.Points, e.Name, e.Realm, e.Stage, e.RealmName, e.StageName, e.Power }).ToListAsync();
        return rows.Select((r, i) => (new ArenaService.Row(r.PlayerId, r.Name, r.Realm, r.Stage, r.RealmName, r.StageName, r.Power, r.Wins, r.Losses,
            i + 1L, r.Points), r.Pos)).ToList();
    }

    /// <summary>Lượt đã dùng hôm nay và trận gần nhất trong một truy vấn: chỉ lấy trận từ 0h (hoặc từ đầu thời gian điều tức nếu sớm hơn),
    /// trận cũ hơn không ảnh hưởng lượt lẫn điều tức.</summary>
    private async Task<(int used, DateTime? last)> UsageAsync(Player p, DateTime now)
    {
        var day = DayStart(now);
        var rest = now.AddSeconds(-ArenaService.CooldownNow);
        var since = rest < day ? rest : day;
        var times = await db.ArenaFights.Where(f => f.AttackerId == p.Id && f.CreatedAt >= since).Select(f => f.CreatedAt).ToListAsync();
        return (times.Count(t => t >= day), times.Count > 0 ? times.Max() : null);
    }

    public async Task<ArenaService.StateRow> StateAsync(Player p)
    {
        await EntryAsync(p);
        await JoinAsync(p);
        var now = DateTime.UtcNow;
        var all = await LadderAsync();
        var me = all.FirstOrDefault(r => r.PlayerId == p.Id);
        var ladder = all;   // một bảng chung cho mọi người chơi
        var myRank = me?.Rank ?? ladder.Count + 1;
        var w = Window(myRank);
        var (used, last) = await UsageAsync(p, now);
        var targets = ladder;
        var ready = last?.AddSeconds(ArenaService.CooldownNow);
        var fights = await db.ArenaFights.AsNoTracking().Where(f => (f.AttackerId == p.Id || f.DefenderId == p.Id) && f.Status == "done")
            .OrderByDescending(f => f.Id).Take(20).ToListAsync();
        var history = fights.Select(f =>
        {
            var attacker = f.AttackerId == p.Id;
            var from = attacker ? f.AttackerRank : f.DefenderRank;
            return new ArenaService.HistoryRow(f.Id, attacker, attacker ? f.DefenderName : f.AttackerName, attacker ? f.Win : !f.Win,
                from, !f.Swapped ? from : attacker ? f.DefenderRank : f.AttackerRank, f.CreatedAt, f.Practice);
        }).ToList();
        return new ArenaService.StateRow(me, ladder.Where(r => r.Rank <= 3).ToList(), targets, w, used, ArenaService.DailyLimit,
            ready > now ? ready : null, ladder.Count, history, await TierRowAsync(p, now));
    }

    public async Task<ArenaService.BeginRow> BeginAsync(Player p, Guid target, PvpFighter me)
    {
        // Mỗi lệnh SQL là một lượt đi về Supabase (~80 ms): một trận dùng 1 truy vấn hai nhân vật, khóa, lượt đánh, bậc thang,
        // rồi FinishAsync 1 UPDATE (thắng / thua / điểm) + 1 INSERT trận (trước đây ~17 lượt).
        var entries = await db.LeaderboardEntries.AsNoTracking().Where(e => e.PlayerId == p.Id || e.PlayerId == target).ToListAsync();
        var mine = entries.FirstOrDefault(e => e.PlayerId == p.Id)
            ?? throw new GameException("Nhân vật chưa có trên bảng xếp hạng. Chờ vài giây rồi thử lại.");
        if (mine.Flagged) throw new GameException("Dữ liệu nhân vật từng được mở bằng bản game cũ nên không vào được Lôi đài.");
        if (target == p.Id) throw new GameException("Không thể tự khiêu chiến chính mình.");
        await LockAsync();    // giữ tới hết transaction: lượt, hạng và đổi chỗ không bị trận khác chen vào
        var now = DateTime.UtcNow;
        var (used, last) = await UsageAsync(p, now);
        if (used >= ArenaService.DailyLimit) throw new GameException($"Đã dùng hết {ArenaService.DailyLimit} lượt khiêu chiến hôm nay. Làm mới lúc 0h.");
        if (last?.AddSeconds(ArenaService.CooldownNow) is { } ready && ready > now)
            throw new GameException($"Đang điều tức, chờ {Math.Ceiling((ready - now).TotalSeconds)} giây nữa.");
        var ladder = await LadderPosAsync();
        if (ladder.All(r => r.Row.PlayerId != p.Id)) { await JoinAsync(p); ladder = await LadderPosAsync(); }   // lần đầu vào Lôi đài
        var mineRow = ladder.First(r => r.Row.PlayerId == p.Id).Row;
        var myRank = mineRow.Rank;
        var foe = ladder.FirstOrDefault(r => r.Row.PlayerId == target).Row ?? throw new GameException("Đối thủ không còn ở Lôi đài.");
        // Mọi trận đều tính điểm; người xếp trên phải trong tầm
        var practice = ArenaTierData.IsPractice(TierOf(mineRow), myRank, foe.Rank);
        if (!practice && myRank - foe.Rank > Window(myRank))
            throw new GameException($"Đối thủ ngoài tầm: chỉ khiêu chiến được trong {Window(myRank)} hạng phía trên.");
        var foeEntry = entries.First(e => e.PlayerId == target);
        var fight = new ArenaFight { AttackerId = p.Id, DefenderId = target, AttackerName = mine.Name, DefenderName = foeEntry.Name,
            AttackerRank = myRank, DefenderRank = foe.Rank, Seed = Random.Shared.NextInt64(0, int.MaxValue), Practice = practice, CreatedAt = now };
        var id = --nextPending;   // mã tạm (âm) trong request: FinishAsync mới ghi trận vào DB
        pending[id] = new Pending(fight, ladder);
        JsonElement? loadout = foeEntry.Loadout is { } json ? JsonDocument.Parse(json).RootElement.Clone() : null;
        return new ArenaService.BeginRow(id, fight.Seed, foeEntry.Name, foeEntry.Realm, loadout,
            JsonSerializer.Deserialize<Dictionary<string, double>>(foeEntry.Stats), myRank, foe.Rank, practice);
    }

    /// <summary>Trận đã cấp seed trong request này, chưa ghi DB; kèm bậc thang đã đọc dưới khóa Lôi đài.</summary>
    private sealed record Pending(ArenaFight Fight, List<(ArenaService.Row Row, long Pos)> Ladder);
    private readonly Dictionary<long, Pending> pending = new();
    private long nextPending;

    public async Task<ArenaService.FinishRow> FinishAsync(Player p, long fightId, bool win)
    {
        if (!pending.TryGetValue(fightId, out var pend) || pend.Fight.AttackerId != p.Id)
            throw new GameException("Trận này đã kết thúc hoặc hết hạn.");
        pending.Remove(fightId);
        // khóa Lôi đài giữ từ BeginAsync tới hết transaction: bậc thang đã đọc vẫn đúng, không cần khóa / đọc lại
        var (f, ladder) = (pend.Fight, pend.Ladder);
        var meRow = ladder.First(r => r.Row.PlayerId == p.Id).Row;
        var foeRow = ladder.First(r => r.Row.PlayerId == f.DefenderId).Row;
        long myRank = meRow.Rank, foeRank = foeRow.Rank;
        // khiêu chiến KHÔNG đổi chỗ: chỗ đứng chỉ phụ thuộc điểm (xem LadderAsync)
        int delta = 0, points = meRow.Points, oldTier = TierOf(meRow), newTier = oldTier;
        var ranked = !f.Practice;
        if (ranked)
        {
            delta = win ? ArenaTierData.WinPoints(myRank, foeRank) : -Math.Min(ArenaTierData.LosePoints, meRow.Points);
            points = meRow.Points + delta;
            newTier = ArenaTierData.IndexOf(points);
            Guid me = p.Id, winner = win ? p.Id : f.DefenderId, loser = win ? f.DefenderId : p.Id;
            await db.ArenaRanks.Where(a => a.PlayerId == winner || a.PlayerId == loser).ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Wins, a => a.PlayerId == winner ? a.Wins + 1 : a.Wins)
                .SetProperty(a => a.Losses, a => a.PlayerId == loser ? a.Losses + 1 : a.Losses)
                .SetProperty(a => a.Points, a => a.PlayerId == me ? points : a.Points)
                .SetProperty(a => a.Protect, a => a.PlayerId == me ? 0 : a.Protect));
        }
        f.Status = "done"; f.Win = win; f.Swapped = false;
        f.AttackerRank = myRank; f.DefenderRank = foeRank;
        db.ArenaFights.Add(f);
        await db.SaveChangesAsync();
        // chỗ đứng mới theo điểm vừa đổi: xếp lại bậc thang đã đọc (điểm giảm dần, vào trước đứng trên)
        var newRank = ladder.Select(r => (r.Row.PlayerId, Points: r.Row.PlayerId == p.Id ? points : r.Row.Points, r.Pos))
            .OrderByDescending(r => r.Points).ThenBy(r => r.Pos).Select((r, i) => (r.PlayerId, Rank: i + 1L)).First(r => r.PlayerId == p.Id).Rank;
        return new ArenaService.FinishRow(myRank, newRank, delta, points, oldTier, newTier, ranked);
    }

    /// <summary>Ảnh chụp kỹ năng đi cùng ảnh chụp bảng xếp hạng (ServerLeaderboard.StageAsync), không cần gửi riêng.</summary>
    public void TrackLoadout(Player p, bool excluded) { }
}
