using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>
/// Boss Tông Môn hằng ngày: đọc/ghi SectBosses, SectBossHits. Như ServerSect, mọi số dùng chung chỉ đổi bằng UPDATE có điều kiện
/// (ExecuteUpdate): hai người đánh cùng lúc không ghi đè nhau, và chỉ một người kích hoạt được "hạ Boss".
/// Không chạy trận và không đụng vật phẩm: phần đó ở GameService.SectBoss.cs.
/// </summary>
public sealed class ServerSectBoss(GameDbContext db)
{
    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    private async Task<(SectMember M, Sect S)> MineAsync(Guid me)
    {
        var m = await db.SectMembers.AsNoTracking().FirstOrDefaultAsync(x => x.PlayerId == me);
        var s = m?.SectId is { } sid ? await db.Sects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sid) : null;
        return s is null ? throw new GameException("Chưa gia nhập Tông Môn.") : (m!, s);
    }

    /// <summary>Boss hôm nay của tông; chưa có thì chốt máu: 60% × 2 lượt × tổng sát thương tham chiếu của thành viên hoạt động.
    /// Người chưa từng đánh lấy trung bình của những người đã có; chưa ai có thì lấy selfRef (mô phỏng của người đang mở).</summary>
    private async Task<SectBoss> EnsureBossAsync(Sect s, string day, DateTime now, Func<double> selfRef)
    {
        var b = await db.SectBosses.AsNoTracking().FirstOrDefaultAsync(x => x.SectId == s.Id && x.Day == day);
        if (b is not null) return b;
        var cutoff = now.AddDays(-SectBossData.ActiveDays);
        var rows = await db.SectMembers.AsNoTracking().Where(x => x.SectId == s.Id)
            .Join(db.Players.IgnoreQueryFilters(), x => x.PlayerId, p => p.Id, (x, p) => new { x.BossRef, p.LastTickAt })
            .Where(x => x.LastTickAt >= cutoff).ToListAsync();
        var known = rows.Where(r => r.BossRef > 0).Select(r => r.BossRef).ToList();
        var fill = known.Count > 0 ? known.Average() : selfRef();
        var refs = rows.Select(r => r.BossRef > 0 ? r.BossRef : fill).DefaultIfEmpty(fill).ToList();
        var nb = new SectBoss { SectId = s.Id, Day = day, BossId = SectBossData.BossOf(day).Id, MaxHp = SectBossData.MaxHp(refs) };
        db.SectBosses.Add(nb);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)   // người khác vừa chốt cùng lúc: dùng bản của họ
        {
            db.Entry(nb).State = EntityState.Detached;
            return await db.SectBosses.AsNoTracking().FirstAsync(x => x.SectId == s.Id && x.Day == day);
        }
        db.Entry(nb).State = EntityState.Detached;
        return nb;
    }

    public async Task<SectBossDto> StateAsync(Guid me, DateTime now, Func<double> selfRef)
    {
        var (_, s) = await MineAsync(me);
        var day = SectData.DayOf(now);
        var b = await EnsureBossAsync(s, day, now, selfRef);
        var hits = await db.SectBossHits.AsNoTracking().Where(x => x.SectId == s.Id && x.Day == day)
            .Join(db.Players.IgnoreQueryFilters(), x => x.PlayerId, p => p.Id, (x, p) => new { x.PlayerId, p.Name, x.Hits, x.Damage, x.ChestClaimed })
            .ToListAsync();
        var mine = hits.FirstOrDefault(x => x.PlayerId == me);
        var def = SectBossData.BossOf(day);
        var defeated = b.DefeatedAt is not null;
        var ranking = hits.Where(x => x.Hits > 0).OrderByDescending(x => x.Damage).ThenBy(x => x.PlayerId)
            .Select(x => new SectBossRankDto(x.PlayerId, x.Name, x.Damage, x.Hits, x.PlayerId == me)).ToList();
        return new SectBossDto(day, def.Id, def.Name, def.Image, def.Desc, b.MaxHp, b.Damage, defeated,
            SectBossData.HitsPerDay, Math.Max(0, SectBossData.HitsPerDay - (mine?.Hits ?? 0)), mine?.Hits ?? 0, mine?.Damage ?? 0,
            defeated && (mine?.Hits ?? 0) > 0 && !(mine?.ChestClaimed ?? false), mine?.ChestClaimed ?? false,
            s.BuffDay == day, defeated && s.BuffDay == SectBossData.NextDay(day), SectBossData.TopBonus, ranking);
    }

    private async Task EnsureHitRowAsync(long sectId, string day, Guid me)
    {
        if (await db.SectBossHits.AnyAsync(x => x.SectId == sectId && x.Day == day && x.PlayerId == me)) return;
        var row = new SectBossHit { SectId = sectId, Day = day, PlayerId = me };
        db.SectBossHits.Add(row);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { /* hàng vừa được tạo bởi yêu cầu song song */ }
        finally { db.Entry(row).State = EntityState.Detached; }
    }

    /// <summary>Giữ một lượt đánh (UPDATE có điều kiện Hits &lt; tối đa). Trả số thứ tự lượt (1..), Boss hôm nay và tông.</summary>
    public async Task<(int HitNo, SectBoss Boss, Sect Sect)> ReserveHitAsync(Guid me, DateTime now, Func<double> selfRef)
    {
        var (_, s) = await MineAsync(me);
        var day = SectData.DayOf(now);
        var boss = await EnsureBossAsync(s, day, now, selfRef);
        if (boss.DefeatedAt is not null) throw new GameException("Boss tông hôm nay đã bị hạ.");
        await EnsureHitRowAsync(s.Id, day, me);
        var max = SectBossData.HitsPerDay;
        if (await db.SectBossHits.Where(x => x.SectId == s.Id && x.Day == day && x.PlayerId == me && x.Hits < max)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Hits, x => x.Hits + 1)) == 0)
            throw new GameException("Hôm nay đã hết lượt đánh Boss tông.");
        var hitNo = await db.SectBossHits.AsNoTracking().Where(x => x.SectId == s.Id && x.Day == day && x.PlayerId == me).Select(x => x.Hits).FirstAsync();
        return (hitNo, boss, s);
    }

    /// <summary>Ghi sát thương của lượt vừa đánh: cộng vào Boss, người đánh, BossRef, cống hiến + quỹ; nếu vừa đủ máu thì hạ Boss
    /// (một UPDATE có điều kiện DefeatedAt IS NULL nên chỉ một lần) và đặt buff cho ngày mai. Trả (cống hiến nhận được, vừa hạ Boss).</summary>
    public async Task<(int Contribution, bool Defeated)> RecordHitAsync(Guid me, SectBoss boss, Sect sect, double damage, DateTime now, double? bossRef = null)
    {
        var (sid, day) = (sect.Id, boss.Day);
        await db.SectBossHits.Where(x => x.SectId == sid && x.Day == day && x.PlayerId == me)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Damage, x => x.Damage + damage));
        await db.SectBosses.Where(x => x.SectId == sid && x.Day == day)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Damage, x => x.Damage + damage));
        await db.SectMembers.Where(x => x.PlayerId == me).ExecuteUpdateAsync(u => u.SetProperty(x => x.BossRef, bossRef ?? damage));   // bossRef: công của người đánh (gốc tính máu Boss); thiếu thì dùng sát thương (test)
        var pts = SectBossData.Contribution(damage, boss.MaxHp);
        var server = new ServerSect(db);
        await server.AwardAsync(me, sid, pts);
        var defeated = await db.SectBosses.Where(x => x.SectId == sid && x.Day == day && x.DefeatedAt == null && x.Damage >= x.MaxHp)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.DefeatedAt, (DateTime?)now)) > 0;
        if (defeated)
        {
            var next = SectBossData.NextDay(day);
            await db.Sects.Where(x => x.Id == sid).ExecuteUpdateAsync(u => u.SetProperty(x => x.BuffDay, next));
            SectBuff.Clear();
            await server.SystemAsync(sid, $"{SectBossData.BossOf(day).Name} đã bị hạ! Cả tông nhận +{SectBossData.BuffQiPct}% tu vi ngày mai, đồng môn nhớ vào Bệ Phong Ấn nhận rương.", now);
        }
        return (pts, defeated);
    }

    /// <summary>Đánh dấu đã nhận rương (UPDATE có điều kiện) và cộng thưởng top. Trả cống hiến thưởng top (0 nếu ngoài top 3).
    /// Vật phẩm trong rương do GameService phát.</summary>
    public async Task<int> ClaimAsync(Guid me, DateTime now)
    {
        var (_, s) = await MineAsync(me);
        var day = SectData.DayOf(now);
        var boss = await db.SectBosses.AsNoTracking().FirstOrDefaultAsync(x => x.SectId == s.Id && x.Day == day);
        if (boss?.DefeatedAt is null) throw new GameException("Boss tông hôm nay chưa bị hạ.");
        var rows = await db.SectBossHits.AsNoTracking().Where(x => x.SectId == s.Id && x.Day == day).ToListAsync();
        var mine = rows.FirstOrDefault(x => x.PlayerId == me);
        if (mine is null || mine.Hits < 1) throw new GameException("Hôm nay bạn chưa đánh Boss tông nên không có rương.");
        if (await db.SectBossHits.Where(x => x.SectId == s.Id && x.Day == day && x.PlayerId == me && !x.ChestClaimed)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.ChestClaimed, true)) == 0)
            throw new GameException("Đã nhận rương hôm nay rồi.");
        var rank = SectBossData.Rank(rows.Select(r => (r.PlayerId, r.Damage)), me);
        var bonus = rank < SectBossData.TopBonus.Length ? SectBossData.TopBonus[rank] : 0;
        if (bonus > 0) await new ServerSect(db).AwardAsync(me, s.Id, bonus);
        return bonus;
    }
}
