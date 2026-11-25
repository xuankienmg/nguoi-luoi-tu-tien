using System.Text.Json;
using VanDao.Data;

namespace VanDao.Game;

public record PermanentPillDto(string Id, string Stat, string Label, double PerPill, int Used, int Cap, double Total, string Unit);

/// <summary>Single-grade pills. Persist only server-owned lifetime counts; derive bonuses from the catalog.</summary>
public static class PermanentPillData
{
    public record Def(string Id, string Name, string Stat, string Label, double Value, int Cap, string Herb, int HerbCount, string Catalyst, int CatalystCount, string Color)
    {
        public string Unit => Stat is "atk" or "hp" or "def" ? "" : "%";
    }
    // cân bằng 11/2026: nguyên liệu có sẵn; Tật Phong dùng Liên kích theo hệ chiến đấu hiện tại.
    public static readonly Def[] All =
    [
        new("thien_luc_dan", "Thiên Lực Đan", "atk", "Tấn công", 10, 100, "hoa_tam_hoa", 5, "tu_linh_thao", 3, "#f78c35"),
        new("huyet_nguyen_dan", "Huyết Nguyên Đan", "hp", "Máu", 100, 100, "huyet_can_thao", 5, "tu_linh_thao", 3, "#e84d60"),
        new("kim_cuong_dan", "Kim Cương Đan", "def", "Phòng thủ", 10, 100, "kim_duong_lien", 5, "bich_ngoc_linh_thao", 3, "#f3ce5d"),
        new("pha_quan_dan", "Phá Quân Đan", "crit", "Bạo kích", .2, 50, "loi_mach_thao", 10, "tu_van_sam", 5, "#a178fa"),
        new("bao_nguyen_dan", "Bạo Nguyên Đan", "critDamage", "ST bạo kích", .5, 50, "hoa_tam_hoa", 10, "tu_van_sam", 5, "#ff6035"),
        new("tat_phong_dan", "Tật Phong Đan", "combo", "Liên kích", .2, 50, "phong_vu_thao", 10, "bich_ngoc_linh_thao", 5, "#5be6b1"),
        new("vo_anh_dan", "Vô Ảnh Đan", "dodge", "Né tránh", .2, 25, "phong_vu_thao", 15, "bich_ngoc_linh_thao", 5, "#8279d6"),
        new("thien_nhan_dan", "Thiên Nhãn Đan", "accuracy", "Chính xác", .2, 50, "bach_diep_thao", 10, "bich_ngoc_linh_thao", 5, "#63d8ff"),
        new("pha_giap_dan", "Phá Giáp Đan", "pen", "Xuyên giáp", .2, 25, "kim_duong_lien", 15, "bich_ngoc_linh_thao", 5, "#dcb56a"),
        new("phe_huyet_dan", "Phệ Huyết Đan", "lifeSteal", "Hút máu", .1, 30, "huyet_can_thao", 15, "tu_van_sam", 5, "#c92e70")
    ];
    public static readonly IReadOnlyDictionary<string, Def> ById = All.ToDictionary(d => d.Id);
    public static IEnumerable<ItemDef> Items => All.Select(d => new ItemDef(d.Id, d.Name, "elixir", d.Color,
        "Đan dược tăng thuộc tính vĩnh viễn, chỉ có một phẩm cấp.", $"img/dan/permanent/{d.Id}.png",
        Rarity: "rare", Kind: "Đan vĩnh viễn", Tier: 1,
        Use: $"{d.Label} +{d.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}{d.Unit} vĩnh viễn mỗi viên. Giới hạn trọn đời: {d.Cap} viên mỗi nhân vật; không làm mới khi đột phá."));
    public static IEnumerable<BrewDef> Brews => All.Select(d => new BrewDef(d.Id, new() { [d.Herb] = d.HerbCount, [d.Catalyst] = d.CatalystCount }));

    private static Dictionary<string, int> Counts(Player p)
    {
        if (string.IsNullOrEmpty(p.PermanentPills)) return new();
        try
        {
            var saved = JsonSerializer.Deserialize<Dictionary<string, int>>(p.PermanentPills) ?? new();
            return All.ToDictionary(d => d.Id, d => Math.Clamp(saved.GetValueOrDefault(d.Id), 0, d.Cap));
        }
        catch (JsonException) { throw new GameException("Dữ liệu đan vĩnh viễn không hợp lệ."); }
    }
    public static int Used(Player p, string id) => Counts(p).GetValueOrDefault(id);
    public static double Total(Player p, string stat) => Math.Round(All.Where(d => d.Stat == stat).Sum(d => d.Value * Used(p, d.Id)), 6);
    public static List<PermanentPillDto> State(Player p) => All.Select(d => new PermanentPillDto(d.Id, d.Stat, d.Label, d.Value, Used(p, d.Id), d.Cap, Total(p, d.Stat), d.Unit)).ToList();
    public static (string msg, string kind) Use(Player p, string id, int n)
    {
        if (!ById.TryGetValue(id, out var d)) throw new GameException("Không phải đan vĩnh viễn.");
        if (n < 0) throw new GameException("Số lượng không hợp lệ.");
        var counts = Counts(p);
        var used = counts.GetValueOrDefault(id);
        if (used >= d.Cap) throw new GameException("Đã đạt giới hạn đan vĩnh viễn của nhân vật.");
        var have = p.CountInt(id);
        if (have < 1) throw new GameException("Không có vật phẩm này trong hành trang.");
        var take = Math.Min(d.Cap - used, n == 0 ? have : Math.Min(n, have));
        counts[id] = used + take;
        var saved = JsonSerializer.Serialize(counts);
        p.Add(id, -take);
        p.PermanentPills = saved;
        return ($"Dùng {take} {d.Name}: {d.Label} +{(take * d.Value).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}{d.Unit} vĩnh viễn ({used + take}/{d.Cap}).", "good");
    }
    public static GemBonuses CombatBonuses(Player p)
    {
        var gems = GemData.GetSocketedBonuses(p);
        return gems with { CritDamage = gems.CritDamage + Total(p, "critDamage"), LifeSteal = gems.LifeSteal + Total(p, "lifeSteal") };
    }
}
