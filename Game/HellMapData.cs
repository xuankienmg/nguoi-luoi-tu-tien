namespace VanDao.Game;

/// <summary>Địa Ngục 1 reuses existing late-game monster artwork; no additional asset bundle.</summary>
public static class HellMapData
{
    private static readonly string[] Names = ["Quỷ Môn Quan", "Hắc Thạch Nguyên", "Dung Nham Hà", "Xiềng Hồn Ngục", "Hỏa Cốt Thành", "Lò Luyện Hồn", "Huyết Diệm Uyên", "Nham Ma Điện", "Vạn Quỷ Đài", "Diêm La Thiên Cung"];
    private static readonly string[] BossNames = ["Trấn Ngục Ma", "Hắc Thạch Cùng Kỳ", "Dung Nham Đào Ngột", "Xiềng Hồn Kiếm Đế", "Hỏa Cốt Thao Thiết", "Luyện Hồn Ma Chủ", "Huyết Diệm Đào Ngột", "Nham Ma Thao Thiết", "Vạn Quỷ Kiếm Đế", "Diêm La Cùng Kỳ Vương"];
    private static readonly string[] BossImages = ["boss_am_ma", "boss_cung_ky", "boss_dao_ngot", "boss_kiem_de", "boss_thao_thiet", "boss_am_ma", "boss_dao_ngot", "boss_thao_thiet", "boss_kiem_de", "boss_cung_ky_vuong"];
    public static ChapterDef Chapter(int index)
    {
        index = Math.Clamp(index, 0, 9);
        var source = GameData.Chapters[9];
        ChapterMon Mon(string name, string image) => new(name, $"img/{image}_v4.png");
        return new ChapterDef {
            Index = index, Name = Names[index], Realm = index < 3 ? "Độ Kiếp" : index < 7 ? "Chân Tiên" : "Chân Tiên Hậu kỳ",
            Places = [Names[index] + " · Ngoại vực", Names[index] + " · Nham hà", Names[index] + " · Nội ngục"],
            Commons = [Mon("Ngục Cốt Khuyển", "quai_cot_khuyen"), Mon("Hắc Nham Thạch Nhân", "quai_reu_thach_nhan"), Mon("Dung Nham Thạch Tích", "quai_tinh_thach_tich"), Mon("Xiềng Hồn Giáp Binh", "quai_thiet_giap_binh"), Mon("Luyện Hồn Ma Đằng", "quai_ma_dang")],
            Elites = [Mon("Trấn Ngục Cốt Tướng", "tinh_cot_tuong"), Mon("Dung Nham Cốt Long", "tinh_cot_long"), Mon("Xiềng Hồn Kiếm Khách", "tinh_huyet_y_kiem_khach")],
            Monster = source.Monster, Boss = source.Boss with { Name = BossNames[index], Image = $"img/{BossImages[index]}.png" },
            BossHp = source.BossHp, Trait = new(40 + index * 5.5, 35, 30, 30 + index, 30 + index), BossLair = Names[index] + " · Vương điện",
            KeyItem = "hoa_nguc_lenh", Drops = source.Drops, Quality = source.Quality
        };
    }

    public static MonsterDef[] AreaWaves(ChapterDef chapter, int area)
    {
        var boss = area >= 31;
        // Anchor to the final Nightmare chapter INCLUDING its late attack/defense multiplier.
        // First Hell stage is slightly above Nightmare's last normal stage, not a difficulty reset.
        var source = GameData.AreaWaves(GameData.Chapters[9], boss ? 16 : 15, 2);
        var progress = (Math.Clamp(area, 1, 30) - 1) / 29d;
        var scale = 1.10 * Math.Pow(1.10, chapter.Index) * (boss ? 1 : 1 + .55 * progress);
        MonsterDef Scale(MonsterDef m, ChapterMon look, string tier) => m with {
            Name = look.Name, Image = look.Image, Tier = tier,
            Hp = CombatNumber.Round(m.Hp * scale), Atk = CombatNumber.Round(m.Atk * 1.10 * Math.Pow(1.05, chapter.Index) * (1 + .55 * progress)), Def = CombatNumber.Round(m.Def * scale)
        };
        if (boss) return [source[0] with {
            Name = chapter.Boss.Name, Image = chapter.Boss.Image, Tier = "boss",
            Hp = CombatNumber.Round(900_000_000d * Math.Pow(1.08, chapter.Index)),
            Atk = CombatNumber.Round(source[0].Atk * .85 * Math.Pow(1.055, chapter.Index)),
            Def = CombatNumber.Round(source[0].Def * .90 * Math.Pow(1.06, chapter.Index))
        }];
        var kind = Math.Min(4, (Math.Clamp(area, 1, 30) - 1) / 6);
        // Every tenth stage ends its region with an elite, rather than making stage 16 a boss.
        var elite = area % 10 == 0;
        return [Scale(source[0], chapter.Commons[kind], "normal"),
            Scale(source[1], chapter.Commons[Math.Min(4, kind + 1)], "normal"),
            Scale(elite ? source[2] : source[0], elite ? chapter.Elites[Math.Clamp(area / 10 - 1, 0, 2)] : chapter.Commons[kind], elite ? "elite" : "normal")];
    }
}
