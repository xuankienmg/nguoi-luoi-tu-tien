using VanDao.Data;

namespace VanDao.Game;

/// <summary>Boss Tông Môn hằng ngày: chạy trận mô phỏng và phát thưởng. Luật và DB nằm ở SectBossData / ServerSectBoss.</summary>
public partial class GameService
{
    public ServerSectBoss SectBoss => new(db);

    /// <summary>Boss bản sao của chính người đánh: cùng chỉ số, công/thủ nhân hệ số, máu vô hạn, không linh thú / thần thông.</summary>
    public static PvpFighter SectBossFighter(PvpFighter me, SectBossData.Def def) =>
        me with { Name = def.Name, Atk = CombatNumber.Scale(me.Atk, SectBossData.AtkMult), Def = CombatNumber.Scale(me.Def, SectBossData.DefMult),
            Hp = CombatNumber.Max, Pet = null, Divine = 0 };

    /// <summary>Một lượt đánh: sát thương trực tiếp người đánh gây ra trong tối đa MaxRounds hiệp, số hiệp đã diễn ra, có bị đánh gục không.</summary>
    public static (double Damage, int Rounds, bool Died) SectBossSimulate(PvpFighter me, SectBossData.Def def, int seed)
    {
        var r = PvpCombat.Run(me, SectBossFighter(me, def), seed, SectBossData.MaxRounds);
        var dmg = r.Actions.Where(a => a.ByA).Aggregate(0m, (t, a) => CombatNumber.Add(t, a.Direct));
        return ((double)dmg, (r.Actions.Count + 1) / 2, r.Actions.Count > 0 && r.Actions[^1].HpA <= 0);
    }

    /// <summary>Mô phỏng theo (người, ngày, số lượt): cùng đầu vào cho cùng kết quả, nên không thể "reroll" bằng cách tải lại.</summary>
    public static (double Damage, int Rounds, bool Died) SectBossSim(Player p, string day, int salt)
    {
        var seed = unchecked(BitConverter.ToInt32(p.Id.ToByteArray(), 0) * 31 + int.Parse(day.Replace("-", "")) * 7 + salt);
        return SectBossSimulate(PvpFighterOf(p), SectBossData.BossOf(day), seed);
    }

    /// <summary>Trang Boss tông. Chốt máu lần đầu trong ngày cần mô phỏng của người mở, nên nạp nhân vật qua ActAsync.</summary>
    public async Task<SectBossDto> SectBossPageAsync(Guid id)
    {
        var now = DateTime.UtcNow; var day = SectData.DayOf(now);
        var (_, dto) = await ActAsync(id, p => SectBoss.StateAsync(p.Id, now, () => SectBossSim(p, day, 0).Damage));
        return dto;
    }

    public async Task<string> SectBossAttackAsync(Player p)
    {
        var now = DateTime.UtcNow; var day = SectData.DayOf(now);
        var (hitNo, boss, sect) = await SectBoss.ReserveHitAsync(p.Id, now, () => SectBossSim(p, day, 0).Damage);
        var sim = SectBossSim(p, day, hitNo);
        var (pts, defeated) = await SectBoss.RecordHitAsync(p.Id, boss, sect, sim.Damage, now);
        var name = SectBossData.BossOf(day).Name;
        var msg = $"Đánh {name}: gây {sim.Damage:N0} sát thương trong {sim.Rounds} hiệp{(sim.Died ? " (bị đánh gục)" : "")}, nhận {pts} cống hiến.";
        if (defeated) msg += $" Boss đã bị hạ! Nhận rương và cả tông được +{SectBossData.BuffQiPct}% tu vi ngày mai.";
        AddLog(p, msg, defeated ? "gold" : "");
        return msg;
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
