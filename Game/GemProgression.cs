namespace VanDao.Game;

/// <summary>Ten gem levels use the same grades as equipment. Existing IDs retain their saved grade.</summary>
public static class GemProgression
{
    public static readonly string[] Qualities = { "common", "uncommon", "rare", "epic", "legendary", "mythic", "divine", "immortal", "dao", "supreme" };
    public static readonly string[] QualityNames = { "Phàm", "Linh", "Huyền", "Địa", "Thiên", "Thánh", "Thần", "Tiên", "Đạo", "Huyền Tiên" };
    private static readonly string[] Colors = { "#FFFFFF", "#00FF7F", "#00E5FF", "#1E90FF", "#BF00FF", "#FF8C00", "#FF0033", "#FF69B4", "#FFD700", "#E0FFFF" };
    public static readonly string[] Themes = { "hoa", "loi", "bang", "tho", "phong", "quang", "am", "linh", "phuc_nguyen", "vu_tru" };
    private static readonly string[] Names = { "Hỏa Tinh", "Lôi Tinh", "Băng Tinh", "Thổ Tinh", "Phong Tinh", "Quang Tinh", "Ám Tinh", "Tinh Thạch", "Phục Nguyên Tinh", "Vũ Trụ Tinh" };

    public static string Id(string theme, int tier) => tier switch
    {
        1 => $"gem_{theme}_t0", 2 => $"gem_{theme}_t1", 5 => $"gem_{theme}_t2", 6 => $"gem_{theme}_t3",
        _ => $"gem_{theme}_lv{tier + 1}"
    };

    public static IEnumerable<ItemDef> CreateAll() => Themes.SelectMany(theme => Enumerable.Range(0, 10).Select(t => Create(theme, t)));

    public static ItemDef RebalanceLegacy(ItemDef old)
    {
        var theme = old.Icon?.Split('/').LastOrDefault() switch
        {
            "core_01.png" => "quang", "core_02.png" => "linh", "core_03.png" => "tho",
            "core_04.png" => "hoa", "core_05.png" => "bang", "core_06.png" => "phuc_nguyen",
            "core_07.png" => "loi", "core_08.png" => "phong", "core_09.png" => "quang",
            "core_10.png" => "phong", "core_11.png" => "loi", "core_12.png" => "am",
            "core_13.png" => "vu_tru", "core_14.png" => "tho", _ => "vu_tru"
        };
        return Create(theme, Math.Max(0, Array.IndexOf(Qualities, old.Rarity))) with
        {
            Id = old.Id, Name = old.Name.Split(" · ")[0] + $" · Cấp {Array.IndexOf(Qualities, old.Rarity) + 1}", Icon = old.Icon
        };
    }

    private static ItemDef Create(string theme, int tier)
    {
        var index = Array.IndexOf(Themes, theme);
        var ratings = GemRatingBudget.For(theme, tier + 1);
        var scale = Math.Pow(1.45, tier);
        int Flat(int value) => (int)Math.Round(value * scale);
        var effects = new List<ItemEffect>();
        void Fx(string id, string name, double value, string text, double secondary = 0) =>
            effects.Add(new(id, name, 1, Math.Round(value, 2), secondary,
                System.Text.RegularExpressions.Regex.Replace(text, @"\s*\([^)]*\)", "").Trim()));
        var attack = theme is "hoa" or "loi" or "phong" or "quang" or "am" or "linh";
        var atk = 0; var def = 0; var hp = 0; var crit = ratings.Crit;
        var basePct = 10 * Math.Pow((tier + 1) / 10d, 2);
        // cân bằng 11/2026: chỉ số chính theo loại — Tấn công: Công + Sát thương kỹ năng; Phòng thủ: Máu + Thủ.
        // Mỗi hệ có chỉ số phụ riêng, không trùng nhau (tham khảo bảng chỉ số nhân vật).
        if (attack)
        {
            atk = Flat(15);
            var skill = Math.Round(1 + .8 * tier, 1);
            Fx("linh_ap", "Sát thương kỹ năng", skill, $"+{skill:0.#}% sát thương kỹ năng");
            Fx("gem_base_atk", "Chỉ số nền cảnh giới", basePct, $"+{basePct:0.##}% công");
        }
        else
        {
            hp = Flat(120); def = Flat(10);
            Fx("gem_base_hp", "Chỉ số nền cảnh giới", basePct, $"+{basePct:0.##}% máu");
        }
        switch (theme)
        {
            case "hoa": Fx("gem_crit_damage", "Sát thương bạo kích", 2 * (tier + 1), $"+{2 * (tier + 1)}% sát thương bạo kích"); break;
            case "loi": Fx("thien_loi", "Thiên lôi", 3 + tier, $"Mỗi lượt {3 + tier}% cơ hội giáng sét {20 + 4 * tier}% công", 20 + 4 * tier); break;
            case "phong": Fx("gem_move_speed", "Tốc độ chạy", 1 + .6 * tier, $"+{1 + .6 * tier:0.##}% tốc độ chạy"); break;
            case "quang": Fx("pha_giap", "Xuyên giáp", 1 + .5 * tier, $"Bỏ qua {1 + .5 * tier:0.##}% phòng thủ quái"); break;
            case "am":
                Fx("gem_lifesteal", "Hút máu", .5 * (tier + 1), $"Hút {.5 * (tier + 1):0.##}% sát thương trực tiếp thực gây");
                if (tier == 9) Fx("gem_extra_attack", "Thêm đòn tấn công", 1, "+1 đòn thường chắc chắn mỗi lượt (không cộng dồn)"); break;
            case "linh": Fx("gem_rage_hit", "Tăng nộ mỗi đòn", 1 + (int)Math.Round(6d * tier / 9), $"+{1 + (int)Math.Round(6d * tier / 9)} nộ mỗi đòn đánh trúng"); break;
            case "bang":
                Fx("gem_elite_reduction", "Giảm sát thương từ quái tinh anh", tier + 1, $"Giảm {tier + 1}% sát thương từ quái tinh anh (cộng dồn tối đa 30%)");
                Fx("gem_block", "Block", 1 + tier, $"{1 + tier}% cơ hội block 50% sát thương đòn đánh"); break;
            case "tho": Fx("gem_boss_reduction", "Giảm sát thương từ boss", tier + 1, $"Giảm {tier + 1}% sát thương từ boss (cộng dồn tối đa 30%)"); break;
            case "phuc_nguyen": Fx("gem_regen", "Hồi máu", .2 + .12 * tier, $"Hồi {.2 + .12 * tier:0.##}% máu tối đa mỗi lượt"); break;
            case "vu_tru": Fx("gem_absorb", "Hấp thụ sát thương", .5 + .5 * tier, $"Hấp thụ {.5 + .5 * tier:0.##}% sát thương; {5 + 2 * tier}% cơ hội chuyển thành máu", 5 + 2 * tier); break;
        }
        void Rating(string id, string name, double value, string? rule = null)
        {
            if (value <= 0) return;
            var amount = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            Fx(id, name, value, $"+{amount}% {name.ToLowerInvariant()}{rule}");
        }
        Rating("gem_crit_resist", "Kháng bạo kích", ratings.CritResist);
        Rating("gem_accuracy", "Chính xác", ratings.Accuracy);
        Rating("gem_dodge", "Né tránh", ratings.Dodge);
        Rating("lien_kich", "Liên kích", ratings.Combo, " (đối kháng Kháng liên kích; đòn thêm không cộng nộ)");
        Rating("gem_combo_resist", "Kháng liên kích", ratings.ComboResist);
        Rating("dia_mach", "Phản đòn", ratings.Reflect, " (cơ hội phản công; đối kháng Kháng phản đòn)");
        Rating("gem_reflect_resist", "Kháng phản đòn", ratings.ReflectResist);
        var lines = new List<string>();
        if (atk > 0) lines.Add($"+{atk} Công");
        if (def > 0) lines.Add($"+{def} Thủ");
        if (hp > 0) lines.Add($"+{hp} Máu");
        if (crit > 0) lines.Add($"+{crit:0.##}% Bạo kích");
        lines.AddRange(effects.Select(e => e.Text));
        return new(Id(theme, tier), $"{Names[index]} · Cấp {tier + 1}", "gem", Colors[tier],
            $"{Names[index]} cấp {tier + 1}/10 · phẩm {QualityNames[tier]}. Khảm vào trang bị để kích hoạt thuộc tính; gỡ ra thì viên vỡ luôn.",
            Icon: $"img/gems/{theme}.png", Rarity: Qualities[tier], Kind: "Tinh Thạch", GemLevel: tier + 1,
            GemCategory: attack ? "attack" : "defense",
            Atk: atk, Def: def, Hp: hp, CritPct: crit, Effects: effects.ToArray(), Special: "Khảm Tinh Thạch: " + string.Join(" · ", lines));
    }
}
