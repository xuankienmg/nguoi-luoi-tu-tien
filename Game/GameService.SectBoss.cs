using VanDao.Data;

namespace VanDao.Game;

/// <summary>Boss Tông Môn hằng ngày: chạy trận mô phỏng và phát thưởng. Luật và DB nằm ở SectBossData / ServerSectBoss.</summary>
public partial class GameService
{
    public ServerSectBoss SectBoss => new(db);

    /// <summary>Boss bản sao của chính người đánh: cùng chỉ số, công/thủ nhân hệ số, máu vô hạn, không linh thú / thần thông.</summary>
    public static PvpFighter SectBossFighter(PvpFighter me, SectBossData.Def def) =>
        me with { Name = def.Name, Def = CombatNumber.Scale(me.Def, SectBossData.DefMult),
            Hp = CombatNumber.Max, Pet = null, Divine = 0 };

    /// <summary>Trận đầy đủ của một lượt (tối đa MaxRounds hiệp) với Boss bản sao.</summary>
    public static PvpResult SectBossRun(PvpFighter me, SectBossData.Def def, int seed) =>
        PvpCombat.Run(me, SectBossFighter(me, def), seed, SectBossData.MaxRounds, SectBossData.AtkAtRound);   // công Boss = công của mình × AtkAtRound(lượt), tăng sau mỗi 4 lượt

    /// <summary>Tóm tắt một trận: sát thương trực tiếp người đánh gây ra, số hiệp đã diễn ra, có bị đánh gục không.</summary>
    private static (double Damage, int Rounds, bool Died) SectBossSummary(PvpResult r)
    {
        var dmg = r.Actions.Where(a => a.ByA).Aggregate(0m, (t, a) => CombatNumber.Add(t, a.Direct));
        return ((double)dmg, (r.Actions.Count + 1) / 2, r.Actions.Count > 0 && r.Actions[^1].HpA <= 0);
    }

    /// <summary>Một lượt đánh: sát thương trực tiếp người đánh gây ra trong tối đa MaxRounds hiệp, số hiệp đã diễn ra, có bị đánh gục không.</summary>
    public static (double Damage, int Rounds, bool Died) SectBossSimulate(PvpFighter me, SectBossData.Def def, int seed) =>
        SectBossSummary(SectBossRun(me, def, seed));

    private static int SectBossSeed(Player p, string day, int salt) =>
        unchecked(BitConverter.ToInt32(p.Id.ToByteArray(), 0) * 31 + int.Parse(day.Replace("-", "")) * 7 + salt);

    /// <summary>Mô phỏng theo (người, ngày, số lượt): cùng đầu vào cho cùng kết quả, nên không thể "reroll" bằng cách tải lại.</summary>
    public static (double Damage, int Rounds, bool Died) SectBossSim(Player p, string day, int salt) =>
        SectBossSimulate(PvpFighterOf(p), SectBossData.BossOf(day), SectBossSeed(p, day, salt));

    /// <summary>Trang Boss tông. Chốt máu lần đầu trong ngày cần mô phỏng của người mở, nên nạp nhân vật qua ActAsync.</summary>
    public async Task<SectBossDto> SectBossPageAsync(Guid id)
    {
        var now = DateTime.UtcNow; var day = SectData.DayOf(now);
        var (_, dto) = await ActAsync(id, p => SectBoss.StateAsync(p.Id, now, () => (double)PvpFighterOf(p).Atk));
        return dto;
    }

    public async Task<string> SectBossAttackAsync(Player p) => (await SectBossFightAsync(p)).Message;

    /// <summary>Đánh Boss tông một lượt theo cơ chế đánh mới: trả thông báo + diễn biến trận (máu Boss là máu chung của cả tông).</summary>
    public async Task<(string Message, DuelDto Duel)> SectBossFightAsync(Player p)
    {
        var now = DateTime.UtcNow; var day = SectData.DayOf(now);
        var (hitNo, boss, sect) = await SectBoss.ReserveHitAsync(p.Id, now, () => (double)PvpFighterOf(p).Atk);
        var def = SectBossData.BossOf(day);
        var r = SectBossRun(PvpFighterOf(p), def, SectBossSeed(p, day, hitNo));
        var sim = SectBossSummary(r);
        var remaining = Math.Max(0, boss.MaxHp - boss.Damage);   // máu chung còn lại trước lượt này
        var duel = DuelLog.Of(r, new DuelLog.FoeOverride(def.Name, def.Image, (decimal)boss.MaxHp, (decimal)remaining));
        var (pts, defeated) = await SectBoss.RecordHitAsync(p.Id, boss, sect, sim.Damage, now, (double)PvpFighterOf(p).Atk);
        var msg = $"Đánh {def.Name}: gây {sim.Damage:N0} sát thương trong {sim.Rounds} hiệp{(sim.Died ? " (bị đánh gục)" : "")}, nhận {pts} cống hiến.";
        if (defeated) msg += $" Boss đã bị hạ! Nhận rương và cả tông được +{SectBossData.BuffQiPct}% tu vi ngày mai.";
        AddLog(p, msg, defeated ? "gold" : "");
        return (msg, duel);
    }

    public async Task<string> SectBossClaimAsync(Player p)
    {
        var bonus = await SectBoss.ClaimAsync(p.Id, DateTime.UtcNow);
        var got = SectBossData.OpenChest(p.Realm, Random.Shared);
        foreach (var (id, qty) in got) p.Add(id, qty);
        var list = string.Join(", ", got.Select(g => $"{g.Qty:N0} {GameData.ItemById.GetValueOrDefault(g.Id)?.Name ?? g.Id}"));
        var msg = $"Mở rương Boss tông: {list}{(bonus > 0 ? $", thưởng top +{bonus} cống hiến" : "")}.";
        AddLog(p, msg, "gold");
        return msg;
    }
}
