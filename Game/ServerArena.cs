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
public sealed class ServerArena(GameDbContext db) : IArenaStore
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

    /// <summary>Hạng hiển thị: thứ tự Pos, bỏ người bị gắn cờ (như view arena_public).</summary>
    private async Task<List<ArenaService.Row>> LadderAsync()
    {
        var rows = await (from a in db.ArenaRanks.AsNoTracking()
                          join e in db.LeaderboardEntries.AsNoTracking() on a.PlayerId equals e.PlayerId
                          where !e.Flagged
                          orderby a.Pos
                          select new { a.PlayerId, a.Wins, a.Losses, e.Name, e.Realm, e.Stage, e.RealmName, e.StageName, e.Power }).ToListAsync();
        return rows.Select((r, i) => new ArenaService.Row(r.PlayerId, r.Name, r.Realm, r.Stage, r.RealmName, r.StageName, r.Power, r.Wins, r.Losses, i + 1L)).ToList();
    }

    private async Task<(int used, DateTime? last)> UsageAsync(Player p, DateTime now)
    {
        var day = DayStart(now);
        var mine = db.ArenaFights.Where(f => f.AttackerId == p.Id);
        return (await mine.CountAsync(f => f.CreatedAt >= day), await mine.MaxAsync(f => (DateTime?)f.CreatedAt));
    }

    public async Task<ArenaService.StateRow> StateAsync(Player p)
    {
        await EntryAsync(p);
        await JoinAsync(p);
        var now = DateTime.UtcNow;
        var ladder = await LadderAsync();
        var me = ladder.FirstOrDefault(r => r.PlayerId == p.Id);
        var myRank = me?.Rank ?? ladder.Count + 1;
        var w = Window(myRank);
        var (used, last) = await UsageAsync(p, now);
        // danh sách chọn đánh (trừ top 3 đã hiện riêng): bản thân, mọi người trong tầm phía trên, và 7 người có hạng gần mình nhất
        var near = ladder.Where(r => r.Rank > 3 && r.PlayerId != p.Id).OrderBy(r => Math.Abs(r.Rank - myRank)).ThenBy(r => r.Rank).Take(7)
            .Select(r => r.PlayerId).ToHashSet();
        var targets = ladder.Where(r => r.Rank > 3 && (r.PlayerId == p.Id || r.Rank >= myRank - w && r.Rank < myRank || near.Contains(r.PlayerId))).ToList();
        var ready = last?.AddSeconds(ArenaService.CooldownSec);
        var fights = await db.ArenaFights.AsNoTracking().Where(f => (f.AttackerId == p.Id || f.DefenderId == p.Id) && f.Status == "done")
            .OrderByDescending(f => f.Id).Take(20).ToListAsync();
        var history = fights.Select(f =>
        {
            var attacker = f.AttackerId == p.Id;
            var from = attacker ? f.AttackerRank : f.DefenderRank;
            return new ArenaService.HistoryRow(f.Id, attacker, attacker ? f.DefenderName : f.AttackerName, attacker ? f.Win : !f.Win,
                from, !f.Swapped ? from : attacker ? f.DefenderRank : f.AttackerRank, f.CreatedAt, f.Practice);
        }).ToList();
        return new ArenaService.StateRow(me, ladder.Where(r => r.Rank <= 3).ToList(), targets, w, used, ArenaService.Daily,
            ready > now ? ready : null, ladder.Count, history);
    }

    public async Task<ArenaService.BeginRow> BeginAsync(Player p, Guid target, PvpFighter me)
    {
        var mine = await EntryAsync(p);
        if (target == p.Id) throw new GameException("Không thể tự khiêu chiến chính mình.");
        await JoinAsync(p);
        await LockAsync();    // giữ tới hết transaction: lượt, hạng và đổi chỗ không bị trận khác chen vào
        var now = DateTime.UtcNow;
        var (used, last) = await UsageAsync(p, now);
        if (used >= ArenaService.Daily) throw new GameException($"Đã dùng hết {ArenaService.Daily} lượt khiêu chiến hôm nay. Làm mới lúc 0h.");
        if (last?.AddSeconds(ArenaService.CooldownSec) is { } ready && ready > now)
            throw new GameException($"Đang điều tức, chờ {Math.Ceiling((ready - now).TotalSeconds)} giây nữa.");
        var ladder = await LadderAsync();
        var myRank = ladder.First(r => r.PlayerId == p.Id).Rank;
        var foe = ladder.FirstOrDefault(r => r.PlayerId == target) ?? throw new GameException("Đối thủ không còn ở Lôi đài.");
        // người xếp dưới: tỷ thí (không đổi hạng); người xếp trên: phải trong tầm
        var practice = foe.Rank > myRank;
        if (!practice && myRank - foe.Rank > Window(myRank))
            throw new GameException($"Đối thủ ngoài tầm: chỉ khiêu chiến được trong {Window(myRank)} hạng phía trên.");
        var foeEntry = await db.LeaderboardEntries.AsNoTracking().FirstAsync(e => e.PlayerId == target);
        var fight = new ArenaFight { AttackerId = p.Id, DefenderId = target, AttackerName = mine.Name, DefenderName = foeEntry.Name,
            AttackerRank = myRank, DefenderRank = foe.Rank, Seed = Random.Shared.NextInt64(0, int.MaxValue), Practice = practice, CreatedAt = now };
        db.ArenaFights.Add(fight);
        await db.SaveChangesAsync();
        JsonElement? loadout = foeEntry.Loadout is { } json ? JsonDocument.Parse(json).RootElement.Clone() : null;
        return new ArenaService.BeginRow(fight.Id, fight.Seed, foeEntry.Name, foeEntry.Realm, loadout,
            JsonSerializer.Deserialize<Dictionary<string, double>>(foeEntry.Stats), myRank, foe.Rank, practice);
    }

    public async Task<ArenaService.FinishRow> FinishAsync(Player p, long fightId, bool win)
    {
        await LockAsync();
        var f = await db.ArenaFights.FirstOrDefaultAsync(x => x.Id == fightId && x.AttackerId == p.Id && x.Status == "pending")
            ?? throw new GameException("Trận này đã kết thúc hoặc hết hạn.");
        var ladder = await LadderAsync();
        long? myRank = ladder.FirstOrDefault(r => r.PlayerId == p.Id)?.Rank, foeRank = ladder.FirstOrDefault(r => r.PlayerId == f.DefenderId)?.Rank;
        // đối thủ đã tụt xuống dưới mình (hoặc rời Lôi đài): thắng vẫn ghi nhưng không đổi hạng; tỷ thí chỉ ghi lịch sử
        var swap = !f.Practice && win && foeRank is not null && myRank is not null && foeRank < myRank;
        if (swap)
        {
            var pa = await db.ArenaRanks.Where(a => a.PlayerId == p.Id).Select(a => a.Pos).FirstAsync();
            var pd = await db.ArenaRanks.Where(a => a.PlayerId == f.DefenderId).Select(a => a.Pos).FirstAsync();
            // đi qua vị trí tạm 0 để ràng buộc unique trên Pos không vỡ giữa hai lệnh
            await db.ArenaRanks.Where(a => a.PlayerId == p.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Pos, 0L));
            await db.ArenaRanks.Where(a => a.PlayerId == f.DefenderId).ExecuteUpdateAsync(s => s.SetProperty(a => a.Pos, pa));
            await db.ArenaRanks.Where(a => a.PlayerId == p.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Pos, pd));
        }
        if (!f.Practice)
        {
            var (winner, loser) = win ? (p.Id, f.DefenderId) : (f.DefenderId, p.Id);
            await db.ArenaRanks.Where(a => a.PlayerId == winner).ExecuteUpdateAsync(s => s.SetProperty(a => a.Wins, a => a.Wins + 1));
            await db.ArenaRanks.Where(a => a.PlayerId == loser).ExecuteUpdateAsync(s => s.SetProperty(a => a.Losses, a => a.Losses + 1));
        }
        f.Status = "done"; f.Win = win; f.Swapped = swap;
        f.AttackerRank = myRank ?? f.AttackerRank; f.DefenderRank = foeRank ?? f.DefenderRank;
        await db.SaveChangesAsync();
        return new ArenaService.FinishRow(myRank, swap ? foeRank : myRank);
    }

    /// <summary>Ảnh chụp kỹ năng đi cùng ảnh chụp bảng xếp hạng (ServerLeaderboard.StageAsync), không cần gửi riêng.</summary>
    public void TrackLoadout(Player p, bool excluded) { }
}
