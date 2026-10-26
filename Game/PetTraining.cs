namespace VanDao.Game;

/// <summary>Bồi dưỡng linh thú: tẩy luyện / bồi nguyên tư chất, trang bị linh thú, thức tỉnh huyết mạch (sau 5★), cộng hưởng ngũ hành.</summary>
public static class PetTraining
{
    // ================= Tư chất =================
    public const string RerollItem = "hoan_cot_dan";     // random lại các tư chất chưa khóa trong khoảng của phẩm
    /// <summary>Bồi Linh Đan đã bỏ khỏi game: còn trong túi thì đổi ra linh thạch (GameData.Legacy).</summary>
    public const string RemovedTrainItem = "boi_linh_dan";
    public const int MaxLocks = 2;
    /// <summary>Tẩy luyện: 1 Hoán Cốt Đan + 1 viên mỗi tư chất khóa, kèm linh thạch theo phẩm.</summary>
    public static int RerollPills(int locks) => 1 + Math.Clamp(locks, 0, MaxLocks);
    public static int RerollStones(int quality) => 5000 * (Math.Clamp(quality, 0, 5) + 1);
    /// <summary>Khoảng tư chất của con: theo phẩm riêng (Thượng Cổ theo loài, như lúc nở).</summary>
    public static (int Min, int Max) AptRange(VanDao.Data.Pet pet, PetDef d)
    {
        var q = d.Rarity == "ancient" ? PetData.SpeciesQuality(d) : PetData.QualityOf(pet, d);
        return (PetData.AptMin[q], PetData.AptMax[q]);
    }

    // ================= Trang bị linh thú =================
    public record GearSlotDef(string Id, string Name, string Stat, string StatName, string Icon);
    public static readonly GearSlotDef[] Slots =
    [
        new("collar", "Hạng Khuyên", "atk", "Công", "img/pet/gear/collar-cloud.png"),
        new("armor", "Móng Vuốt", "def", "Thủ", "img/pet/gear/claw-cloud.png"),
        new("charm", "Hộ Mệnh Phù", "hp", "Huyết", "img/pet/gear/charm-cloud.png"),
    ];
    public static readonly string[] TierNames = { "Phàm", "Linh", "Huyền", "Địa", "Thiên", "Thánh" };
    /// <summary>% chỉ số linh thú mỗi món theo phẩm Phàm → Thánh.</summary>
    public static readonly double[] GearPct = { 4, 8, 12, 17, 23, 30 };
    public const int FuseCount = 3;                    // 3 món cùng ô, cùng phẩm → 1 món phẩm kế
    public static int FuseStones(int tier) => 5000 * (tier + 1) * (tier + 1);
    public static string GearId(string slot, int tier) => $"ltb_{slot}_{tier}";
    public record GearDef(string Id, string Slot, int Tier, double Pct);
    public static readonly IReadOnlyDictionary<string, GearDef> Gear = Slots
        .SelectMany(s => Enumerable.Range(0, TierNames.Length).Select(t => new GearDef(GearId(s.Id, t), s.Id, t, GearPct[t])))
        .ToDictionary(g => g.Id);
    /// <summary>Phẩm cao nhất rơi ở map (Thiên); Thánh trở lên chỉ nhận từ hợp thành (Luyện chế › Linh thú).</summary>
    public const int MaxDropTier = 4;
    /// <summary>Trần phẩm trang bị rơi khi hạ Boss theo chương (0-based) và mức khó, tối đa MaxDropTier.</summary>
    public static int DropTierCap(int chapter, int diff) => Math.Clamp(Math.Max(0, chapter) / 2 + diff, 0, MaxDropTier);
    /// <summary>Trọng số phẩm rơi từ Phàm tới trần: phẩm thấp dễ ra hơn (trần 4: 5 · 4 · 3 · 2 · 1).</summary>
    public static int DropTierWeight(int tier, int cap) => tier < 0 || tier > cap ? 0 : cap - tier + 1;

    // ================= Thức tỉnh huyết mạch =================
    public const int MaxAwaken = 3;
    public const double AwakenBonus = .10;             // mỗi cấp +10% chỉ số (kỹ năng mạnh theo Linh Uy)
    public static readonly int[] AwakenBlood = { 10, 20, 40 };
    public static readonly int[] AwakenStones = { 100_000, 300_000, 1_000_000 };
    /// <summary>Sao của con hiến cùng loài cho từng cấp thức tỉnh.</summary>
    public static readonly int[] AwakenDonorStars = { 3, 4, 5 };
    public static Dictionary<string, int> AwakenCost(int level) => level >= MaxAwaken ? new()
        : new() { ["than_thu_huyet"] = AwakenBlood[level], ["linh_thach"] = AwakenStones[level] };
    public static readonly string[] AwakenNames = { "", "Thức Tỉnh I", "Thức Tỉnh II", "Thức Tỉnh III" };

    // ================= Cộng hưởng ngũ hành =================
    public const double ResonanceSame = .12, ResonanceSupport = .05;
    // vòng tương sinh: Mộc → Hỏa → Thổ → Lôi → Thủy → Mộc · Phong cùng nhóm Mộc, Băng cùng nhóm Thủy
    private static readonly string[][] Cycle = [["moc", "gio"], ["hoa"], ["tho"], ["loi"], ["thuy", "bang"]];
    private static int Group(string el) => Array.FindIndex(Cycle, g => g.Contains(el));
    public static bool Supports(string a, string b)
    {
        var ga = Group(a); var gb = Group(b);
        if (ga < 0 || gb < 0 || a == b) return false;
        return ga == gb || (ga + 1) % Cycle.Length == gb || (gb + 1) % Cycle.Length == ga;
    }
    /// <summary>Hệ số cộng hưởng của một hệ linh thú với công pháp đang kích hoạt: cùng hệ +12%, tương sinh +5%.</summary>
    public static (double Pct, string Kind) Resonance(string petElement, string manualElement) =>
        string.IsNullOrEmpty(manualElement) ? (0, "") : petElement == manualElement ? (ResonanceSame, "same") : Supports(petElement, manualElement) ? (ResonanceSupport, "support") : (0, "");
    /// <summary>Cộng hưởng của linh thú nhiều hệ (Thượng Cổ): cộng dồn cộng hưởng của từng hệ; Kind là mức cao nhất ("same" › "support").</summary>
    public static (double Pct, string Kind, (string Element, double Pct, string Kind)[] Parts) Resonance(IReadOnlyList<string> petElements, string manualElement)
    {
        var parts = petElements.Select(e => { var (pct, kind) = Resonance(e, manualElement); return (e, pct, kind); }).ToArray();
        var best = parts.Any(x => x.kind == "same") ? "same" : parts.Any(x => x.kind == "support") ? "support" : "";
        return (parts.Sum(x => x.pct), best, parts);
    }

    // ================= Vật phẩm =================
    public static readonly ItemDef[] Items = new[]
    {
        new ItemDef(RerollItem, "Hoán Cốt Đan", "petmaterial", "#9fd4ef", "Tẩy luyện tư chất linh thú: random lại các tư chất chưa khóa trong khoảng của phẩm (khóa thêm tư chất tốn thêm 1 viên mỗi tư chất). Rơi khi hạ Boss (chương và mức khó cao rơi nhiều, phẩm cao hơn).",
            Icon: "img/pet/hoa_hinh_dan.png", Tint: "hue-rotate(160deg) saturate(1.3) brightness(1.1)", Rarity: "rare", Kind: "Đan linh thú"),
    }.Concat(Gear.Values.Select(g =>
    {
        var s = Slots.First(x => x.Id == g.Slot);
        return new ItemDef(g.Id, $"{s.Name} {TierNames[g.Tier]}", "petgear", "#c9b27a",
            $"Trang bị linh thú · ô {s.Name}: +{g.Pct:0.#}% {s.StatName} linh thú. " + (g.Tier > MaxDropTier
                ? $"Không rơi ở map, chỉ nhận từ hợp thành {FuseCount} món {s.Name} {TierNames[g.Tier - 1]} (Luyện chế › Linh thú)."
                : $"Hợp thành {FuseCount} món cùng ô, cùng phẩm để lên phẩm kế (Luyện chế › Linh thú). Rơi ngẫu nhiên khi hạ Boss: ô và phẩm ngẫu nhiên, chương và mức khó cao mở phẩm cao hơn (tối đa {TierNames[MaxDropTier]})."),
            Icon: s.Icon, Rarity: g.Tier < 5 ? PetData.Qualities[g.Tier] : "mythic", Kind: "Trang bị linh thú", Sell: 500 * (g.Tier + 1) * (g.Tier + 1));
    })).ToArray();
}
