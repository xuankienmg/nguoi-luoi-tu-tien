namespace VanDao.Game;

/// <summary>Địa Ngục 1 (mức 3) và Địa Ngục 2 (mức 4): 10 chương × (15 ải + Boss ải 16), mỗi ải 5 quái. Địa Ngục 1 dùng ảnh Tây Du Ký
/// (img/hell, tools/hell_map_art) trộn quái cuối game có sẵn; Địa Ngục 2 dùng ảnh mới ở img/hell2 (tools/hell_map_art_2).</summary>
public static class HellMapData
{
    /// <summary>Dữ liệu một mức Địa Ngục: tên chương, Boss, đội hình theo chủ đề và hệ số chỉ số (chốt bằng tools/hell_map_checks --tune).</summary>
    private sealed record Tier(string[] Names, string[] BossNames, string[] BossImages, (ChapterMon[] Commons, ChapterMon[] Elites)[] Themes, double[] HpTune, double AtkRatio, string Key, string[] Realms);

    private static ChapterMon Mon(string name, string image) => new(name, $"img/{image}_v4.png");
    private static ChapterMon Hell(string name, string image) => new(name, $"img/hell/{image}.png");
    private static ChapterMon Hell2(string name, string image) => new(name, $"img/hell2/{image}.png");

    // ---- Địa Ngục 1 ----
    // Five commons (stages 1–15 in thirds) and three elites (stages 5/10/15) per theme; new art clusters in its theme.
    private static readonly (ChapterMon[] Commons, ChapterMon[] Elites) Gate = (
        [Hell("Ngưu Đầu", "quai_nguu_dau"), Hell("Mã Diện", "quai_ma_dien"), Mon("Ngục Cốt Khuyển", "quai_cot_khuyen"), Hell("Hắc Vô Thường", "quai_hac_vo_thuong"), Hell("Bạch Vô Thường", "quai_bach_vo_thuong")],
        [Mon("Trấn Ngục Cốt Tướng", "tinh_cot_tuong"), Hell("Bạch Cốt Tinh", "tinh_bach_cot_tinh"), Mon("Xiềng Hồn Kiếm Khách", "tinh_huyet_y_kiem_khach")]);
    private static readonly (ChapterMon[] Commons, ChapterMon[] Elites) Lava = (
        [Mon("Ngục Cốt Khuyển", "quai_cot_khuyen"), Hell("Hỏa Diệm Sơn Tiểu Yêu", "quai_hoa_diem_tieu_yeu"), Mon("Dung Nham Thạch Tích", "quai_tinh_thach_tich"), Hell("Ngưu Đầu", "quai_nguu_dau"), Mon("Hắc Nham Thạch Nhân", "quai_reu_thach_nhan")],
        [Mon("Trấn Ngục Cốt Tướng", "tinh_cot_tuong"), Mon("Dung Nham Cốt Long", "tinh_cot_long"), Hell("Bạch Cốt Tinh", "tinh_bach_cot_tinh")]);
    private static readonly (ChapterMon[] Commons, ChapterMon[] Elites) Stone = (
        [Mon("Ngục Cốt Khuyển", "quai_cot_khuyen"), Mon("Hắc Nham Thạch Nhân", "quai_reu_thach_nhan"), Mon("Dung Nham Thạch Tích", "quai_tinh_thach_tich"), Mon("Xiềng Hồn Giáp Binh", "quai_thiet_giap_binh"), Mon("Luyện Hồn Ma Đằng", "quai_ma_dang")],
        [Mon("Trấn Ngục Cốt Tướng", "tinh_cot_tuong"), Mon("Dung Nham Cốt Long", "tinh_cot_long"), Mon("Xiềng Hồn Kiếm Khách", "tinh_huyet_y_kiem_khach")]);

    // ---- Địa Ngục 2 ----
    // Ảnh mới: quái thường Huyết Toàn Ma Thú / Huyết Lôi Dực Hổ / Liệt Diệm Ma Lang; tinh anh Vạn Nhãn Ma Cầu / Lục Nham Ma Thần.
    // Chỗ chưa có tinh anh mới dùng lại Boss hoặc tinh anh của Địa Ngục 1 (không trùng Boss của chính chương đó); Boss dùng làm tinh anh có bản sao tinh_*.png vì client coi mọi ảnh boss_* là Boss.
    private static readonly ChapterMon HuyetToan = Hell2("Huyết Toàn Ma Thú", "quai_huyet_toan_ma_thu"), HuyetLoi = Hell2("Huyết Lôi Dực Hổ", "quai_huyet_loi_duc_ho"),
        LietDiem = Hell2("Liệt Diệm Ma Lang", "quai_liet_diem_ma_lang"), VanNhan = Hell2("Vạn Nhãn Ma Cầu", "tinh_van_nhan_ma_cau"), LucNham = Hell2("Lục Nham Ma Thần", "tinh_luc_nham_ma_than");
    private static readonly ChapterMon KhuyenCot = Mon("Ngục Cốt Khuyển", "quai_cot_khuyen"), CotTuong = Mon("Trấn Ngục Cốt Tướng", "tinh_cot_tuong"), CotLong = Mon("Dung Nham Cốt Long", "tinh_cot_long");
    private static readonly (ChapterMon[] Commons, ChapterMon[] Elites) Wild = (
        [LietDiem, HuyetLoi, Hell("Ngưu Đầu", "quai_nguu_dau"), Hell("Mã Diện", "quai_ma_dien"), HuyetToan],
        [VanNhan, Hell("Bạch Cốt Tinh", "tinh_bach_cot_tinh"), new("Thánh Anh Đại Vương", "img/hell2/tinh_thanh_anh_dai_vuong.png")]);
    private static readonly (ChapterMon[] Commons, ChapterMon[] Elites) Blood = (
        [HuyetToan, LietDiem, KhuyenCot, HuyetLoi, Hell("Hỏa Diệm Sơn Tiểu Yêu", "quai_hoa_diem_tieu_yeu")],
        [VanNhan, CotLong, new("Thiết Phiến Công Chúa", "img/hell2/tinh_thiet_phien_cong_chua.png")]);
    private static readonly (ChapterMon[] Commons, ChapterMon[] Elites) Rock = (
        [Mon("Hắc Nham Thạch Nhân", "quai_reu_thach_nhan"), Mon("Dung Nham Thạch Tích", "quai_tinh_thach_tich"), HuyetToan, Mon("Xiềng Hồn Giáp Binh", "quai_thiet_giap_binh"), Mon("Luyện Hồn Ma Đằng", "quai_ma_dang")],
        [LucNham, CotTuong, new("Hắc Hùng Tinh", "img/hell2/tinh_hac_hung_tinh.png")]);

    // One shared array: tools/hell_map_checks --tune writes it and both HP/defense and attack follow.
    private static readonly double[] Hell1Tune = [1, 1, 1, 2.30, 2.62, 2.99, 3.40, 3.89, 4.40, 4.50];
    private static readonly Tier Hell1Tier = new(
        ["Quỷ Môn Quan", "Hắc Thạch Nguyên", "Dung Nham Hà", "Xiềng Hồn Ngục", "Hỏa Cốt Thành", "Lò Luyện Hồn", "Huyết Diệm Uyên", "Nham Ma Điện", "Vạn Quỷ Đài", "Diêm La Thiên Cung"],
        ["Thôi Phán Quan", "Hắc Hùng Tinh", "Thánh Anh Đại Vương", "Xiềng Hồn Ám Ma", "Hỏa Cốt Đào Ngột", "Luyện Hồn Thao Thiết", "Thiết Phiến Công Chúa", "Ngưu Ma Vương", "Vạn Quỷ Cùng Kỳ Vương", "Diêm La Vương"],
        ["hell/boss_thoi_phan_quan", "hell/boss_hac_hung_tinh", "hell/boss_thanh_anh_dai_vuong", "boss_am_ma", "boss_dao_ngot", "boss_thao_thiet", "hell/boss_thiet_phien_cong_chua", "hell/boss_nguu_ma_vuong", "boss_cung_ky_vuong", "hell/boss_diem_la_vuong"],
        [Gate, Stone, Lava, Stone, Lava, Stone, Lava, Lava, Gate, Gate],
        // Chương 1–3 là nội dung Độ Kiếp; từ chương 4 cần Chân Tiên (Sơ kỳ q5 ở chương 4 … Hậu kỳ q9 ở chương 10).
        Hell1Tune, 1,
        "hoa_nguc_lenh", ["Độ Kiếp", "Độ Kiếp", "Độ Kiếp", "Chân Tiên", "Chân Tiên", "Chân Tiên", "Chân Tiên", "Chân Tiên Hậu kỳ", "Chân Tiên Hậu kỳ", "Chân Tiên Hậu kỳ"]);

    // Địa Ngục 2 nối tiếp cuối Địa Ngục 1 (chương 1 hơi cao hơn Boss cuối Địa Ngục 1); cân bằng bằng tools/hell_map_checks.
    private static readonly Tier Hell2Tier = new(
        ["Hoàng Phong Lĩnh", "Hắc Phong Động", "Huyết Phủ Sơn", "Hỏa Vân Động", "Liệt Hỏa Uyên", "Thiết Phiến Sơn", "Cửu Vĩ Hồ Động", "Kim Sí Sơn", "Ngũ Long Kim Điện", "Kim Sư Thiên Đài"],
        ["Hoàng Phong Đại Vương", "Hắc Hùng Tinh", "Huyết Phủ Ma Ngưu", "Thánh Anh Đại Vương", "Hỏa Long", "Thiết Phiến Công Chúa", "Cửu Vĩ Hồ Tiên", "Kim Sí Đại Bàng", "Ngũ Đầu Kim Long", "Lục Đầu Kim Sư"],
        ["hell2/boss_hoang_phong_dai_vuong", "hell/boss_hac_hung_tinh", "hell2/boss_huyet_phu_ma_nguu", "hell/boss_thanh_anh_dai_vuong", "hell2/boss_hoa_long", "hell/boss_thiet_phien_cong_chua", "hell2/boss_cuu_vi_ho_tien", "hell2/boss_kim_si_dai_bang", "hell2/boss_ngu_dau_kim_long", "hell2/boss_luc_dau_kim_su"],
        [Wild, Blood, Blood, Rock, Blood, Wild, Wild, Rock, Rock, Blood],
        Grow(20, 1.12), .72,
        "hoa_nguc_lenh_2", Enumerable.Repeat("Chân Tiên Hậu kỳ", 10).ToArray());
    private static double[] Grow(double start, double step) => Enumerable.Range(0, 10).Select(i => Math.Round(start * Math.Pow(step, i), 3)).ToArray();

    private static Tier TierOf(int diff) => diff >= 4 ? Hell2Tier : Hell1Tier;

    public static ChapterDef Chapter(int index) => Chapter(3, index);
    public static ChapterDef Chapter(int diff, int index)
    {
        var t = TierOf(diff);
        index = Math.Clamp(index, 0, 9);
        var source = GameData.Chapters[9];
        return new ChapterDef {
            Index = index, Name = t.Names[index], Realm = t.Realms[index],
            Places = [t.Names[index] + " · Ngoại vực", t.Names[index] + " · Nham hà", t.Names[index] + " · Nội ngục"],
            Commons = t.Themes[index].Commons, Elites = t.Themes[index].Elites,
            Monster = source.Monster, Boss = source.Boss with { Name = t.BossNames[index], Image = $"img/{t.BossImages[index]}.png", Id = t.BossImages[index] },
            BossHp = source.BossHp, Trait = new(40 + index * 5.5, 35, 30), BossLair = t.Names[index] + " · Vương điện",
            KeyItem = t.Key, Drops = source.Drops, Quality = source.Quality
        };
    }

    /// <summary>Địa Ngục 1: hệ số chương lên máu, công và thủ của quái (tools/hell_map_checks --tune).</summary>
    public static double[] StatTune => Hell1Tier.HpTune;
    /// <summary>Mảng hệ số chương của mức Địa Ngục diff (3 hoặc 4); tools/hell_map_checks --tune ghi trực tiếp vào đây.</summary>
    public static double[] TuneFor(int diff) => TierOf(diff).HpTune;
    /// <summary>Extra HP on every Hell monster, bosses included (attack and defense unchanged).</summary>
    public const double HpBoost = 1.5;
    /// <summary>Máu mỗi quái khi mỗi ải 5 quái thay vì 3 (3/5): tổng máu một chuyến giữ như trước, tránh ải đông quái nặng gấp rưỡi.</summary>
    public const double WaveShare = (double)GameData.Waves / GameData.HellWaves;
    /// <summary>Boss HP as a multiple of the stage-15 elite before it, so the boss is also the chapter's toughest wave.</summary>
    public const double BossHpOverElite30 = 2;

    public static MonsterDef[] AreaWaves(ChapterDef chapter, int area, int diff = 3)
    {
        var t = TierOf(diff);
        var boss = area >= GameData.BossAreaFor(diff);
        var ci = Math.Clamp(chapter.Index, 0, 9);
        var tune = t.HpTune[ci]; var atkTune = tune * t.AtkRatio;
        // Anchor to the final Nightmare chapter INCLUDING its late attack/defense multiplier.
        // First Hell stage is slightly above Nightmare's last normal stage, not a difficulty reset.
        var source = GameData.AreaWaves(GameData.Chapters[9], boss ? 16 : 15, 2);
        var last = GameData.Areas;
        var progress = (Math.Clamp(area, 1, last) - 1) / (double)(last - 1);
        var scale = 1.10 * Math.Pow(1.10, ci) * (boss ? 1 : 1 + .55 * progress);
        MonsterDef Scale(MonsterDef m, ChapterMon look, string tier) => MonsterCombatRatings.Apply(m with {
            Name = look.Name, Image = look.Image, Tier = tier, Id = "",
            Hp = CombatNumber.Scale(m.Hp, scale * tune * HpBoost * WaveShare), Atk = CombatNumber.Scale(m.Atk, 1.10 * Math.Pow(1.05, ci) * (1 + .55 * progress) * atkTune), Def = CombatNumber.Scale(m.Def, scale * tune)
        }, diff);
        // The boss hits a little harder than the stage-15 elite before it, so the chapter's wall is its boss.
        var elite15Atk = GameData.AreaWaves(GameData.Chapters[9], 15, 2)[2].Atk * CombatNumber.Factor(1.10 * Math.Pow(1.05, ci) * 1.55);
        if (boss) return [MonsterCombatRatings.Apply(source[0] with {
            Name = chapter.Boss.Name, Image = chapter.Boss.Image, Tier = "boss", Id = chapter.Boss.Id,
            Hp = CombatNumber.Scale(AreaWaves(chapter, last, diff).Max(w => w.Hp), BossHpOverElite30 / WaveShare),
            Atk = CombatNumber.Scale(elite15Atk, 1.05 * atkTune),
            Def = CombatNumber.Scale(source[0].Def, .90 * Math.Pow(1.06, ci) * tune)
        }, diff)];
        // Five waves: the main species and the next one alternate; every fifth stage ends its region with an elite (never a boss at stage 16).
        var kind = Math.Min(4, (Math.Clamp(area, 1, last) - 1) / 3);
        var next = Math.Min(4, kind + 1);
        var elite = area % 5 == 0;
        var waves = new MonsterDef[GameData.HellWaves];
        for (var i = 0; i < waves.Length; i++)
        {
            var last5 = i == waves.Length - 1;
            if (last5 && elite) waves[i] = Scale(source[2], chapter.Elites[Math.Clamp(area / 5 - 1, 0, 2)], "elite");
            else waves[i] = Scale(source[0], chapter.Commons[i % 2 == 0 && !last5 ? kind : next], "normal");
        }
        return waves;
    }
}
