using VanDao.Data;

namespace VanDao.Game;

public record SetBonus(string Stat, double Value, string Text);
public record SetMilestone(int Count, SetBonus[] Bonuses);
public record EquipmentSetDef(string Id, string Name, SetMilestone[] Milestones);
public record EquippedSetState(string Id, string Name, int Count, int[] Active, int? Next);
public record EffectState(string Id, string Name, double Raw, double Effective, double? Cap, string Unit);
public record EquipmentEffectsState(EquippedSetState[] Sets, EffectState[] Effects, Dictionary<string, double> SetBonuses, DivineAbilityState? Divine = null);

/// <summary>Pure calculation: no Stats/Rate calls, no persisted derived bonuses.</summary>
public static class EquipmentEffects
{
    private static EquipmentSetDef Set(string id, string name, SetBonus first, SetBonus second) => new(id, name, new[]
    {
        new SetMilestone(3, new[] { new SetBonus("set_pct", 3, "+3% công, thủ, máu") }),
        new SetMilestone(5, new[] { first }), new SetMilestone(8, new[] { second }),
        new SetMilestone(10, new[] { new SetBonus("set_pct", 5, "+5% công, thủ, máu"), new SetBonus("hap_linh", 3, "+3% tốc độ tu luyện") })
    });
    public static readonly EquipmentSetDef[] Sets =
    {
        Set("thanh_van", "Thanh Vân", new("hap_linh", 5, "+5% tốc độ tu luyện"), new("tu_linh", 5, "+5% tu vi thám hiểm")),
        Set("huyen_bang", "Huyền Băng", new("ho_the", 3, "Giảm 3% sát thương nhận"), new("tho_thuan", 5, "Khiên đầu trận +5% máu")),
        Set("xich_kim", "Xích Kim", new("linh_ap", 5, "+5% sát thương kỹ năng"), new("pha_giap", 5, "Bỏ qua 5% phòng thủ quái")),
        Set("tu_van", "Tử Vân", new("crit", 2, "+2 điểm % bạo kích"), new("thien_menh", 10, "Tỉ lệ bạo kích +10%")),
        Set("thai_hu", "Thái Hư", new("than_phap", 3, "+3 điểm % né tránh"), new("speed", 4, "+4 điểm % tốc độ thám hiểm")),
        Set("huyen_long", "Huyền Long", new("linh_ap", 5, "+5% sát thương kỹ năng"), new("thien_uy", 5, "Giảm 5% công quái")),
        Set("thien_hop", "Thiên Hợp", new("ho_the", 5, "Giảm 5% sát thương nhận"), new("crit", 3, "+3 điểm % bạo kích")),
        Set("dai_thua", "Đại Thừa", new("linh_ap", 6, "+6% sát thương kỹ năng"), new("thien_menh", 12, "Tỉ lệ bạo kích +12%")),
        Set("thien_kiep", "Thiên Kiếp", new("linh_ap", 8, "+8% sát thương kỹ năng"), new("ho_the", 6, "Giảm 6% sát thương nhận")),
        Set("thai_so", "Thái Sơ", new("linh_ap", 10, "+10% sát thương kỹ năng"), new("pha_giap", 8, "Bỏ qua 8% phòng thủ quái"))
    };
    public static string? SetId(string? name) => Sets.FirstOrDefault(s => s.Name == name)?.Id;
    public static readonly double[] QualityScale = { 1, 1, 1, 1, 1, 1.05, 1.10, 1.15, 1.20, 1.25 };
    public static ItemEffect Scale(ItemEffect effect, int quality)
    {
        if (effect.Id == "hap_linh") return effect;
        var digits = effect.Id == "tinh_tam" ? 0 : effect.Id == "thien_menh" ? 2 : 1;
        var value = Math.Round(effect.Value * QualityScale[quality], digits, MidpointRounding.AwayFromZero);
        var name = effect.Id == "than_phap" ? "Vân bộ" : effect.Name;
        var amount = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var text = effect.Id == "thien_loi" ? $"Mỗi lượt {amount}% cơ hội giáng sét {effect.Value2:0.#}% công, bỏ qua phòng thủ"
            : effect.Id == "thien_menh" ? $"Tỉ lệ bạo kích +{amount}%"
            : effect.Id == "tinh_tam" ? $"Vào trận với {amount} nộ"
            : System.Text.RegularExpressions.Regex.Replace(effect.Text, @"\d+(?:\.\d+)?(?=%)", amount);
        return effect with { Name = name, Value = value, Text = text };
    }

    // Trần của nhóm trang bị/bộ. Các nhóm có công pháp/kỹ năng vẫn áp trần tổng tại chiến đấu.
    private static readonly Dictionary<string, (string Name, string Unit, double? Cap)> Info = new()
    {
        ["hap_linh"] = ("Tu luyện", "%", null), ["tu_linh"] = ("Tu vi thám hiểm", "%", null),
        ["pha_giap"] = ("Phá giáp", "%", 80), ["linh_ap"] = ("Sát thương kỹ năng", "%", null),
        ["tinh_tam"] = ("Nộ đầu trận", " nộ", 75), ["ho_the"] = ("Giảm sát thương nhận", "%", 60),
        ["lien_kich"] = ("Liên kích", "%", 30), ["vung_than"] = ("Máu tối đa", "%", null),
        ["than_phap"] = ("Vân bộ: né tránh", "%", null), ["hoi_khi"] = ("Hồi máu mỗi lượt", "% máu", 5),
        ["tho_thuan"] = ("Khiên đầu trận", "% máu", 20), ["dia_chan"] = ("Đòn đầu", "% công", null),
        ["dia_mach"] = ("Phản đòn", "%", 15), ["thien_loi"] = ("Thiên lôi", "% cơ hội", 50),
        ["thien_uy"] = ("Giảm công quái", "%", 40), ["thien_menh"] = ("Tỉ lệ bạo kích", "%", null),
        ["set_pct"] = ("Công, thủ, máu từ bộ", "%", null), ["crit"] = ("Bạo kích từ bộ", " điểm %", null),
        ["crit_resist"] = ("Kháng bạo", "%", null),
        ["speed"] = ("Thân pháp từ bộ", " điểm %", 60)
    };
    public static ItemDef[] ValidItems(Player p) => p.Equipment.GroupBy(e => e.Slot).Where(g => g.Count() == 1)
        .Select(g => g.Single()).Where(e => GameData.Realms[p.Realm].EquipSlots.Contains(e.Slot))
        .Select(e => (Equipped: e, Item: GameData.ItemById.GetValueOrDefault(e.ItemId)))
        .Where(x => x.Item != null && x.Item.Slot == x.Equipped.Slot && x.Item.MinRealm <= p.Realm)
        .Select(x => x.Item!).ToArray();

    public static EquipmentEffectsState Calculate(Player p)
    {
        var items = ValidItems(p);
        var raw = new Dictionary<string, double>();
        var setBonuses = new Dictionary<string, double>();
        void Add(Dictionary<string, double> target, string key, double value) => target[key] = target.GetValueOrDefault(key) + value;
        foreach (var item in items)
            foreach (var effect in item.Effects ?? Array.Empty<ItemEffect>()) Add(raw, effect.Id, effect.Value);
        var states = Sets.Select(set =>
        {
            var count = items.Count(item => item.SetId == set.Id);
            var active = set.Milestones.Where(m => count >= m.Count).ToArray();
            foreach (var milestone in active)
                foreach (var bonus in milestone.Bonuses) { Add(raw, bonus.Stat, bonus.Value); Add(setBonuses, bonus.Stat, bonus.Value); }
            return new EquippedSetState(set.Id, set.Name, count, active.Select(m => m.Count).ToArray(),
                set.Milestones.FirstOrDefault(m => count < m.Count)?.Count);
        }).ToArray();
        var effects = raw.Select(kv =>
        {
            var info = Info[kv.Key];
            return new EffectState(kv.Key, info.Name, Math.Round(kv.Value, 4), Math.Round(Math.Min(kv.Value, info.Cap ?? double.MaxValue), 4), info.Cap, info.Unit);
        }).ToArray();
        return new(states, effects, setBonuses, DivineAbility.For(p));
    }
}
