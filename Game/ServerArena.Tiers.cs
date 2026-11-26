using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Game;

public sealed partial class ServerArena
{
    private static int TierOf(ArenaService.Row r) => ArenaTierData.IndexOf(r.Points);

    /// <summary>Thứ Hai 0h giờ Việt Nam của tuần hiện tại, tính theo UTC.</summary>
    public static DateTime WeekStart(DateTime utcNow) =>
        DateTime.SpecifyKind(DateTime.ParseExact(ArenaTierData.WeekKey(utcNow), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture).AddHours(-7), DateTimeKind.Utc);

    private Task<int> RealFightsSinceAsync(Player p, DateTime since) =>
        db.ArenaFights.CountAsync(f => f.AttackerId == p.Id && f.Status == "done" && !f.Practice && f.CreatedAt >= since);

    private async Task<ArenaService.TierRow> TierRowAsync(Player p, DateTime now)
    {
        var ar = await db.ArenaRanks.AsNoTracking().FirstAsync(a => a.PlayerId == p.Id);
        return new ArenaService.TierRow(ar.Points, 0, await RealFightsSinceAsync(p, DayStart(now)) > 0, await RealFightsSinceAsync(p, WeekStart(now)) > 0,
            ar.DailyClaimDay == ArenaTierData.DayKey(now), ar.WeeklyClaimWeek == ArenaTierData.WeekKey(now));
    }

    public async Task<List<ArenaService.Row>> TierBoardAsync(Player p, int tier, int limit)
    {
        await EntryAsync(p);
        await JoinAsync(p);
        return (await LadderAsync()).Take(limit).ToList();
    }

    public async Task<int> ClaimCheckAsync(Player p, bool weekly)
    {
        await EntryAsync(p);
        await JoinAsync(p);
        var now = DateTime.UtcNow;
        var a = await db.ArenaRanks.AsNoTracking().FirstAsync(x => x.PlayerId == p.Id);
        if (weekly)
        {
            if (!ArenaTierData.IsSunday(now)) throw new GameException("Thưởng tuần Lôi đài nhận vào Chủ nhật.");
            if (a.WeeklyClaimWeek == ArenaTierData.WeekKey(now)) throw new GameException("Đã nhận thưởng tuần này.");
            if (await RealFightsSinceAsync(p, WeekStart(now)) == 0) throw new GameException("Cần đánh ít nhất 1 trận Lôi đài (không tính tỷ thí) trong tuần.");
        }
        else
        {
            if (a.DailyClaimDay == ArenaTierData.DayKey(now)) throw new GameException("Hôm nay đã nhận thưởng hạng.");
            if (await RealFightsSinceAsync(p, DayStart(now)) == 0) throw new GameException("Cần đánh ít nhất 1 trận Lôi đài (không tính tỷ thí) hôm nay.");
        }
        return ArenaTierData.IndexOf(a.Points);
    }

    public async Task ClaimMarkAsync(Player p, bool weekly)
    {
        var now = DateTime.UtcNow;
        if (weekly) { var k = ArenaTierData.WeekKey(now); await db.ArenaRanks.Where(a => a.PlayerId == p.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.WeeklyClaimWeek, k)); }
        else { var k = ArenaTierData.DayKey(now); await db.ArenaRanks.Where(a => a.PlayerId == p.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.DailyClaimDay, k)); }
    }
    // Điểm hạng sau trận (thắng +25..+40, thua −15): tính trong ServerArena.FinishAsync, ghi chung một UPDATE với thắng / thua.
}
