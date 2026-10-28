namespace VanDao.Game;

/// <summary>Địa Ngục 1: Tây Du Ký underworld art (img/hell, see tools/hell_map_art) mixed with existing late-game monsters.</summary>
public static class HellMapData
{
    private static readonly string[] Names = ["Quỷ Môn Quan", "Hắc Thạch Nguyên", "Dung Nham Hà", "Xiềng Hồn Ngục", "Hỏa Cốt Thành", "Lò Luyện Hồn", "Huyết Diệm Uyên", "Nham Ma Điện", "Vạn Quỷ Đài", "Diêm La Thiên Cung"];
    private static readonly string[] BossNames = ["Thôi Phán Quan", "Hắc Hùng Tinh", "Thánh Anh Đại Vương", "Xiềng Hồn Ám Ma", "Hỏa Cốt Đào Ngột", "Luyện Hồn Thao Thiết", "Thiết Phiến Công Chúa", "Ngưu Ma Vương", "Vạn Quỷ Cùng Kỳ Vương", "Diêm La Vương"];
    private static readonly string[] BossImages = ["hell/boss_thoi_phan_quan", "hell/boss_hac_hung_tinh", "hell/boss_thanh_anh_dai_vuong", "boss_am_ma", "boss_dao_ngot", "boss_thao_thiet", "hell/boss_thiet_phien_cong_chua", "hell/boss_nguu_ma_vuong", "boss_cung_ky_vuong", "hell/boss_diem_la_vuong"];
    private static ChapterMon Mon(string name, string image) => new(name, $"img/{image}_v4.png");
    private static ChapterMon Hell(string name, string image) => new(name, $"img/hell/{image}.png");
    // Five commons (stages 1–30 in sixths) and three elites (stages 10/20/30) per theme; new art clusters in its theme.
    private static readonly (ChapterMon[] Commons, ChapterMon[] Elites) Gate = (
        [Hell("Ngưu Đầu", "quai_nguu_dau"), Hell("Mã Diện", "quai_ma_dien"), Mon("Ngục Cốt Khuyển", "quai_cot_khuyen"), Hell("Hắc Vô Thường", "quai_hac_vo_thuong"), Hell("Bạch Vô Thường", "quai_bach_vo_thuong")],
        [Mon("Trấn Ngục Cốt Tướng", "tinh_cot_tuong"), Hell("Bạch Cốt Tinh", "tinh_bach_cot_tinh"), Mon("Xiềng Hồn Kiếm Khách", "tinh_huyet_y_kiem_khach")]);
    private static readonly (ChapterMon[] Commons, ChapterMon[] Elites) Lava = (
        [Mon("Ngục Cốt Khuyển", "quai_cot_khuyen"), Hell("Hỏa Diệm Sơn Tiểu Yêu", "quai_hoa_diem_tieu_yeu"), Mon("Dung Nham Thạch Tích", "quai_tinh_thach_tich"), Hell("Ngưu Đầu", "quai_nguu_dau"), Mon("Hắc Nham Thạch Nhân", "quai_reu_thach_nhan")],
        [Mon("Trấn Ngục Cốt Tướng", "tinh_cot_tuong"), Mon("Dung Nham Cốt Long", "tinh_cot_long"), Hell("Bạch Cốt Tinh", "tinh_bach_cot_tinh")]);
    private static readonly (ChapterMon[] Commons, ChapterMon[] Elites) Stone = (
        [Mon("Ngục Cốt Khuyển", "quai_cot_khuyen"), Mon("Hắc Nham Thạch Nhân", "quai_reu_thach_nhan"), Mon("Dung Nham Thạch Tích", "quai_tinh_thach_tich"), Mon("Xiềng Hồn Giáp Binh", "quai_thiet_giap_binh"), Mon("Luyện Hồn Ma Đằng", "quai_ma_dang")],
        [Mon("Trấn Ngục Cốt Tướng", "tinh_cot_tuong"), Mon("Dung Nham Cốt Long", "tinh_cot_long"), Mon("Xiềng Hồn Kiếm Khách", "tinh_huyet_y_kiem_khach")]);
    private static readonly (ChapterMon[] Commons, ChapterMon[] Elites)[] Themes = [Gate, Stone, Lava, Stone, Lava, Stone, Lava, Lava, Gate, Gate];
    public static ChapterDef Chapter(int index)
    {
        index = Math.Clamp(index, 0, 9);
        var source = GameData.Chapters[9];
        return new ChapterDef {
            Index = index, Name = Names[index], Realm = index < 3 ? "Độ Kiếp" : index < 7 ? "Chân Tiên" : "Chân Tiên Hậu kỳ",
            Places = [Names[index] + " · Ngoại vực", Names[index] + " · Nham hà", Names[index] + " · Nội ngục"],
            Commons = Themes[index].Commons, Elites = Themes[index].Elites,
            Monster = source.Monster, Boss = source.Boss with { Name = BossNames[index], Image = $"img/{BossImages[index]}.png" },
            BossHp = source.BossHp, Trait = new(40 + index * 5.5, 35, 30, 30 + index, 30 + index), BossLair = Names[index] + " · Vương điện",
            KeyItem = "hoa_nguc_lenh", Drops = source.Drops, Quality = source.Quality
        };
    }

    /// <summary>Chapter multiplier on monster HP, attack and defense. Chapters 1–3 are Độ Kiếp content; from chapter 4 the
    /// map needs Chân Tiên (Sơ kỳ q5 for chapter 4 … Hậu kỳ q9 for chapter 10), fitted with tools/hell_map_checks --tune.</summary>
    public static readonly double[] StatTune = { 1, 1, 1, 2.30, 2.62, 2.99, 3.40, 3.89, 4.40, 4.50 };
    /// <summary>Extra HP on every Hell monster, bosses included (attack and defense unchanged).</summary>
    public const double HpBoost = 1.5;
    /// <summary>Boss HP as a multiple of the stage-30 elite before it, so the boss is also the chapter's toughest wave.</summary>
    public const double BossHpOverElite30 = 2;

    public static MonsterDef[] AreaWaves(ChapterDef chapter, int area)
    {
        var boss = area >= 31;
        var tune = StatTune[Math.Clamp(chapter.Index, 0, StatTune.Length - 1)];
        // Anchor to the final Nightmare chapter INCLUDING its late attack/defense multiplier.
        // First Hell stage is slightly above Nightmare's last normal stage, not a difficulty reset.
        var source = GameData.AreaWaves(GameData.Chapters[9], boss ? 16 : 15, 2);
        var progress = (Math.Clamp(area, 1, 30) - 1) / 29d;
        var scale = 1.10 * Math.Pow(1.10, chapter.Index) * (boss ? 1 : 1 + .55 * progress);
        MonsterDef Scale(MonsterDef m, ChapterMon look, string tier) => m with {
            Name = look.Name, Image = look.Image, Tier = tier,
            Hp = CombatNumber.Round(m.Hp * scale * tune * HpBoost), Atk = CombatNumber.Round(m.Atk * 1.10 * Math.Pow(1.05, chapter.Index) * (1 + .55 * progress) * tune), Def = CombatNumber.Round(m.Def * scale * tune)
        };
        // The boss hits a little harder than the stage-30 elite before it, so the chapter's wall is its boss.
        var elite30Atk = GameData.AreaWaves(GameData.Chapters[9], 15, 2)[2].Atk * 1.10 * Math.Pow(1.05, chapter.Index) * 1.55;
        if (boss) return [source[0] with {
            Name = chapter.Boss.Name, Image = chapter.Boss.Image, Tier = "boss",
            Hp = CombatNumber.Round(AreaWaves(chapter, 30).Max(w => w.Hp) * BossHpOverElite30),
            Atk = CombatNumber.Round(elite30Atk * 1.05 * tune),
            Def = CombatNumber.Round(source[0].Def * .90 * Math.Pow(1.06, chapter.Index) * tune)
        }];
        var kind = Math.Min(4, (Math.Clamp(area, 1, 30) - 1) / 6);
        // Every tenth stage ends its region with an elite, rather than making stage 16 a boss.
        var elite = area % 10 == 0;
        return [Scale(source[0], chapter.Commons[kind], "normal"),
            Scale(source[1], chapter.Commons[Math.Min(4, kind + 1)], "normal"),
            Scale(elite ? source[2] : source[0], elite ? chapter.Elites[Math.Clamp(area / 10 - 1, 0, 2)] : chapter.Commons[kind], elite ? "elite" : "normal")];
    }
}
