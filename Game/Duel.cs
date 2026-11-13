namespace VanDao.Game;

// Cơ chế đánh mới (dùng chung toàn game): server mô phỏng cả trận bằng PvpCombat (đủ chỉ số + linh thú),
// client chỉ hiện popup nhẹ: hai ấn tròn, thanh máu và nhật ký nguồn sát thương (wwwroot/js/duel.js). Không còn hoạt ảnh canvas.

/// <summary>Một bên trong trận: tên, ảnh tròn, máu tối đa, máu lúc vào trận (null = đầy), linh thú xuất chiến (tên + ảnh, null nếu không mang).</summary>
public record DuelSideDto(string Name, string? Hero, decimal Hp, string? Pet = null, string? PetImage = null, decimal? Cur = null);
/// <summary>Một nguồn sát thương / hồi máu trong lượt: Kind (first · basic · skill · extra · bolt · burn · pet · reflect · petreflect · heal), tên hiển thị,
/// giá trị, bạo kích; Self = tác động lên chính bên ra đòn (phản đòn, hồi máu), còn lại là lên đối thủ.</summary>
public record DuelPartDto(string Kind, string Name, decimal Value, bool Crit = false, bool Self = false);
/// <summary>Một lượt trong trận: bên nào ra đòn, bị khống (mất lượt), đòn bị né / vô hiệu, từng nguồn sát thương, máu hai bên sau lượt.</summary>
public record DuelStepDto(bool ByMe, string Control, bool Dodged, bool Nullified, List<DuelPartDto> Parts, decimal HpMe, decimal HpFoe, int Round = 0);
/// <summary>Diễn biến một trận, từ góc nhìn của người chơi (Me).</summary>
public record DuelDto(DuelSideDto Me, DuelSideDto Foe, List<DuelStepDto> Steps);

public static class DuelLog
{
    /// <summary>Đối thủ không phải người chơi (Boss): tên, ảnh, máu tối đa và máu lúc vào trận (Boss tông có máu chung nhiều người đánh).
    /// Máu Boss trong trận tính lại từ sát thương trực tiếp người chơi gây ra, vì Boss mô phỏng có máu vô hạn.</summary>
    public record FoeOverride(string Name, string? Image, decimal MaxHp, decimal StartHp);

    /// <summary>PvpResult.A / D đã nhân HpMult trong PvpCombat.Run nên Hp ở đây là máu thật của trận.</summary>
    private static DuelSideDto SideOf(PvpFighter f)
    {
        var def = f.Pet is { } pp && PetData.ById.TryGetValue(pp.DefId, out var d) ? d : null;
        return new DuelSideDto(f.Name, f.Hero, Math.Max(1, f.Hp), def?.Name,
            def is null ? null : def.Forms[Math.Clamp(f.Pet!.Appearance, 0, def.Forms.Length - 1)].Image);
    }

    /// <summary>Diễn biến trận từ góc nhìn người khiêu chiến (A): mỗi hành động một bước, kèm máu hai bên sau đòn.</summary>
    public static DuelDto Of(PvpResult r, FoeOverride? foe = null)
    {
        var dealt = 0m;   // sát thương trực tiếp tích lũy của A lên Boss (chỉ dùng khi có FoeOverride)
        var steps = r.Actions.Select(x =>
        {
            var parts = new List<DuelPartDto>();
            void P(string kind, string name, decimal v, bool crit = false, bool self = false) { if (v > 0) parts.Add(new(kind, name, v, crit, self)); }
            P("first", "Đòn đầu", x.FirstHit);
            P("basic", "Đòn thường", x.Basic, x.BasicCrit);
            foreach (var c in x.Casts) P("skill", c.Name, c.Dmg, c.Critical);
            P("extra", "Liên kích", x.ExtraDmg, x.ExtraCrit);
            P("bolt", "Thiên lôi", x.Bolt);
            P("burn", "Thiêu đốt", x.Burn);
            foreach (var c in x.PetCasts) { P("pet", c.Skill, c.Dmg); P("heal", c.Skill, c.Heal, self: true); }
            P("reflect", "Phản đòn", x.ReflectHpDamage, self: true);                    // đối thủ phản đòn lên người ra đòn
            foreach (var c in x.OppPetCasts) P("petreflect", c.Skill, c.Dmg, self: true);   // linh thú đối thủ phản đòn
            P("heal", "Hồi máu", x.Heal, self: true);
            var hpFoe = x.HpD;
            if (foe is not null) { if (x.ByA) dealt += x.Direct; hpFoe = Math.Max(0, foe.StartHp - dealt); }
            return new DuelStepDto(x.ByA, x.Control, x.Dodged, x.Nullified, parts, x.HpA, hpFoe, x.Round);
        }).ToList();
        if (foe is not null)   // Boss mô phỏng có máu vô hạn nên trận vẫn chạy tiếp: diễn biến dừng ở lượt máu Boss (chung của tông) về 0
        {
            var end = steps.FindIndex(s => s.HpFoe <= 0);
            if (end >= 0 && end < steps.Count - 1) steps = steps.Take(end + 1).ToList();
        }
        var me = SideOf(r.A);
        var other = foe is null ? SideOf(r.D) : new DuelSideDto(foe.Name, foe.Image, foe.MaxHp, Cur: foe.StartHp);
        return new DuelDto(me, other, steps);
    }
}
