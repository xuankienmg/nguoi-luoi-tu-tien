namespace VanDao.Game;

/// <summary>Four completed manuals share the existing progression and combat rules.</summary>
public static class ElementSkillData
{
    private record Cast(string Id, string Name, string Category, string Shape);
    private record Passive(string Id, string Name, double Atk = 0, double Def = 0, double Hp = 0,
        double Crit = 0, double Speed = 0, double Qi = 0, double Skill = 0, double Pen = 0, double Weaken = 0);

    public static readonly IReadOnlyList<SkillDef> Skills = Build().ToArray();

    private static IEnumerable<SkillDef> Build()
    {
        foreach (var s in Tree("tho", "EARTH", "tho_linh_dan", "Thổ Linh Đạn", "Đạn đá ngưng tụ hoàng khí, bắn vỡ thành đá vụn.",
            [new("thach_dan", "Thạch Đạn", "Công", "Tụ đá thành cự thạch phóng thẳng vào mục tiêu."),
             new("tho_tuong", "Thổ Tường", "Thủ", "Dựng tường đá rồi đẩy địa thứ công kích mục tiêu."),
             new("tho_giap", "Thổ Giáp", "Thủ", "Thạch giáp hóa cầu gai, va vào mục tiêu rồi vỡ tung."),
             new("son_nhac", "Sơn Nhạc Quyết", "Tuyệt kỹ", "Sơn ấn trấn xuống, địa chấn đánh vỡ mục tiêu."),
             new("hau_tho", "Hậu Thổ Giáng Lâm", "Công", "Hậu Thổ pháp ấn giáng xuống giữa thạch trụ hoàng kim."),
             new("hu_khong_tran_nhac", "Hư Không Trấn Nhạc", "Tuyệt kỹ", "Đá núi từ khe hư không hợp lại, trấn áp mục tiêu."),
             new("hon_nguyen_tho", "Hỗn Nguyên Hậu Thổ", "Tuyệt kỹ", "Tam sơn hợp nhất trong địa mạch, bùng nổ hoàng quang."),
             new("thai_so_tho", "Thái Sơ Hậu Thổ Ấn", "Tuyệt kỹ", "Địa mạch thái sơ dựng thành thần sơn, trấn sập mục tiêu.")],
            [140, 180, 225, 320, 390, 485, 600, 740], [14, 18, 24, 36, 28, 33, 38, 45],
            [new("tho_linh_can", "Thổ Linh Căn", Def: 2, Qi: 2),
             new("hau_tho_can_co", "Hậu Thổ Căn Cơ", Def: 4),
             new("thach_bi", "Thạch Bì", Hp: 3),
             new("dia_mach", "Địa Mạch", Def: 2, Hp: 1),
             new("tho_tam", "Thổ Tâm", Skill: 3),
             new("tho_linh_chan_y", "Thổ Linh Chân Ý", Skill: 3, Def: 2),
             new("bat_dong_son", "Bất Động Sơn", Def: 4, Hp: 2),
             new("tran_nhac", "Trấn Nhạc Chân Ý", Skill: 3, Pen: 1),
             new("hau_tho_phap_than", "Hậu Thổ Pháp Thân", Def: 3, Hp: 3),
             new("thai_hu_tho", "Thái Hư Thổ Đạo", Skill: 3, Def: 3),
             new("tho_dao_hop_nhat", "Thổ Đạo Hợp Nhất", Def: 4, Hp: 3, Skill: 3),
             new("tho_dao_dai_thua", "Đại Thừa Thổ Đạo", Def: 5, Hp: 4, Skill: 4)])) yield return s;

        foreach (var s in Tree("loi", "THUNDER", "loi_quang_cau", "Lôi Quang Cầu", "Điện cầu tím lam bắn thẳng, nổ thành tia điện.",
            [new("loi_kich", "Lôi Kích", "Công", "Gọi tia lôi điện giáng thẳng xuống mục tiêu."),
             new("loi_bo", "Lôi Bộ", "Thân pháp", "Lôi quang tạo dư ảnh lao tới, phóng điện công kích."),
             new("lien_loi", "Liên Hoàn Lôi", "Công", "Lôi châu nối thành xích điện, hội tụ đánh vào một mục tiêu."),
             new("thien_loi", "Thiên Lôi", "Tuyệt kỹ", "Thiên lôi từ lốc mây giáng xuống lôi trận."),
             new("cuu_tieu", "Cửu Tiêu Lôi Phạt", "Công", "Chín lôi thương hội tụ thành thiên phạt."),
             new("hu_khong_kiep_loi", "Hư Không Kiếp Lôi", "Tuyệt kỹ", "Kiếp lôi xuyên khe hư không, xé mục tiêu bằng điện quang."),
             new("hon_nguyen_loi", "Hỗn Nguyên Thiên Kiếp", "Tuyệt kỹ", "Tam trọng lôi hoàn hợp thành thần lôi chín nhánh."),
             new("thai_so_loi", "Thái Sơ Diệt Kiếp Lôi", "Tuyệt kỹ", "Thần lôi thái sơ giáng xuống, cửu thiên rung chuyển.")],
            [150, 195, 245, 345, 415, 515, 635, 785], [15, 20, 25, 38, 30, 35, 40, 48],
            [new("loi_linh_can", "Lôi Linh Căn", Atk: 2, Qi: 2),
             new("tu_dien", "Tử Điện", Atk: 3),
             new("loi_the", "Lôi Thể", Def: 3),
             new("dien_tam", "Điện Tâm", Crit: 1),
             new("loi_tam", "Lôi Tâm", Skill: 3),
             new("loi_linh_chan_y", "Lôi Linh Chân Ý", Skill: 3, Pen: 1),
             new("cuu_tieu_chan_y", "Cửu Tiêu Chân Ý", Atk: 3, Hp: 2),
             new("kiep_loi", "Kiếp Lôi Chân Ý", Skill: 3, Pen: 2),
             new("loi_than_phap_than", "Lôi Thần Pháp Thân", Atk: 3, Hp: 3),
             new("thai_hu_loi", "Thái Hư Lôi Đạo", Skill: 3, Pen: 1),
             new("loi_dao_hop_nhat", "Lôi Đạo Hợp Nhất", Atk: 4, Skill: 3, Crit: 1),
             new("loi_dao_dai_thua", "Đại Thừa Lôi Đạo", Atk: 5, Skill: 4, Crit: 1)])) yield return s;

        foreach (var s in Tree("bang", "ICE", "bang_linh_cham", "Băng Linh Châm", "Hàn khí hóa băng châm bắn thẳng, vỡ thành tinh thể.",
            [new("bang_tien", "Băng Tiễn", "Công", "Tinh thể hàn băng hóa mũi tên sắc bén bắn tới mục tiêu."),
             new("bang_thuan", "Băng Thuẫn", "Thủ", "Băng thuẫn phóng gai lạnh rồi vỡ thành mảnh băng công kích."),
             new("dong_dia", "Đông Địa", "Khống", "Băng thứ trồi lên từ hàn trận, đâm xuyên mục tiêu."),
             new("tuyet_doi", "Tuyệt Đối Linh Độ", "Tuyệt kỹ", "Hàn tâm ngưng thành băng miện rồi nổ tung tinh thể."),
             new("huyen_bang", "Huyền Băng Giáng Thế", "Công", "Cự thương huyền băng giáng xuống tuyết trận."),
             new("hu_khong_bang_nguc", "Hư Không Băng Ngục", "Tuyệt kỹ", "Hàn băng từ hư không kết lao rồi ép vỡ quanh mục tiêu."),
             new("hon_nguyen_bang", "Hỗn Nguyên Vĩnh Đông", "Tuyệt kỹ", "Vạn hàn hợp nhất thành băng liên, bùng nổ huyền quang."),
             new("thai_so_bang", "Thái Sơ Hàn Ngục", "Tuyệt kỹ", "Hàn khí thái sơ đóng băng cả hư không quanh mục tiêu.")],
            [140, 180, 225, 325, 390, 485, 600, 740], [14, 18, 24, 36, 28, 33, 38, 45],
            [new("bang_linh_can", "Băng Linh Căn", Hp: 2, Qi: 2),
             new("han_khi", "Hàn Khí", Def: 3),
             new("bang_co", "Băng Cơ", Hp: 3),
             new("ngung_han", "Ngưng Hàn", Weaken: 2),
             new("bang_tam", "Băng Tâm", Skill: 3),
             new("bang_linh_chan_y", "Băng Linh Chân Ý", Skill: 3, Weaken: 1),
             new("huyen_bang_chan_the", "Huyền Băng Chân Thể", Def: 3, Hp: 3),
             new("vinh_dong", "Vĩnh Đông Chân Ý", Skill: 3, Weaken: 1),
             new("bang_than_phap_than", "Băng Thần Pháp Thân", Def: 3, Hp: 3),
             new("thai_hu_bang", "Thái Hư Băng Đạo", Skill: 3, Hp: 3),
             new("bang_dao_hop_nhat", "Băng Đạo Hợp Nhất", Hp: 4, Def: 3, Skill: 3),
             new("bang_dao_dai_thua", "Đại Thừa Băng Đạo", Hp: 5, Def: 4, Skill: 4)])) yield return s;

        foreach (var s in Tree("gio", "WIND", "phong_linh_tien", "Phong Linh Tiễn", "Phong linh ngưng thành tiễn, lướt tới rồi tan thành gió.",
            [new("phong_nhan", "Phong Nhận", "Công", "Ngưng gió thành lưỡi liềm ngọc, chém thẳng vào mục tiêu."),
             new("phong_bo", "Phong Bộ", "Thân pháp", "Phong linh kéo dư ảnh lao tới, hợp thành phong trảm."),
             new("cuong_phong", "Cuồng Phong", "Khống", "Lốc gió cuộn lên, tung phong nhận công kích."),
             new("thien_phong", "Thiên Phong Quyết", "Tuyệt kỹ", "Ba phong trụ hợp thành thiên phong, bùng nổ muôn lưỡi gió."),
             new("cuu_thien_phong", "Cửu Thiên Phong Bạo", "Công", "Chín phong nhận xoáy quanh phong nhãn thành bão lớn."),
             new("hu_khong_phong_nhan", "Hư Không Phong Nhận", "Tuyệt kỹ", "Lưỡi gió xé khe hư không, giao nhau thành trảm kích."),
             new("hon_nguyen_phong", "Hỗn Nguyên Vạn Phong", "Tuyệt kỹ", "Vạn phong hợp nhất thành liên bạo, bùng nổ thanh quang."),
             new("thai_so_phong", "Thái Sơ Cửu Thiên Phong", "Tuyệt kỹ", "Gió thái sơ cuộn thành thiên trụ, xé nát mục tiêu.")],
            [145, 185, 235, 335, 400, 500, 620, 765], [14, 19, 24, 37, 29, 34, 39, 46],
            [new("phong_linh_can", "Phong Linh Căn", Atk: 1, Speed: 1, Qi: 2),
             new("khinh_than", "Khinh Thân", Speed: 1.5),
             new("phong_y", "Phong Y", Def: 3),
             new("phong_nhan_tam", "Phong Nhãn Tâm", Crit: 1),
             new("phong_tam", "Phong Tâm", Skill: 3),
             new("phong_linh_chan_y", "Phong Linh Chân Ý", Skill: 3, Speed: 1),
             new("tieu_dao_du", "Tiêu Dao Du", Speed: 1.5, Hp: 2),
             new("cuu_thien_chan_y", "Cửu Thiên Chân Ý", Skill: 3, Atk: 2),
             new("phong_than_phap_than", "Phong Thần Pháp Thân", Atk: 3, Hp: 3),
             new("thai_hu_phong", "Thái Hư Phong Đạo", Skill: 3, Pen: 1),
             new("phong_dao_hop_nhat", "Phong Đạo Hợp Nhất", Atk: 3, Skill: 3, Crit: 1),
             new("phong_dao_dai_thua", "Đại Thừa Phong Đạo", Atk: 4, Skill: 4, Crit: 1)])) yield return s;
    }

    private static IEnumerable<SkillDef> Tree(string element, string code, string basicId, string basicName, string basicEffect,
        Cast[] active, int[] damage, int[] growth, Passive[] passive)
    {
        yield return new(basicId, element, basicName, "Đòn thường", 0, 0, 0, 0, 0, [],
            basicEffect + " Sát thương 115% công.", element + "_" + basicId, DmgBase: 115,
            Code: code + "_BASIC_001", AnimId: code + "_" + basicId.ToUpperInvariant());
        int[] rows = [0, 1, 2, 4, 5, 6, 7, 8], costs = [1, 5, 10, 3, 5, 6, 8, 10];   // chiêu cuối: Đại Thừa
        for (var i = 0; i < active.Length; i++) {
            var a = active[i];
            yield return new(a.Id, element, a.Name, a.Category, rows[i], 1, 5, i, costs[i],
                i == 0 ? [] : [active[i - 1].Id + (i <= 2 ? ":5" : ":3")],
                $"{a.Shape} Sát thương {damage[i]}% công, thêm {growth[i]}% mỗi cấp.", element + "_" + a.Id,
                DmgBase: damage[i], DmgPerLv: growth[i], Code: $"{code}_ACTIVE_{i + 1:000}", AnimId: code + "_" + a.Id.ToUpperInvariant());
        }
        int[] pRows = [0, 1, 2, 2, 2, 3, 4, 4, 5, 6, 7, 8], pCols = [1, 1, 0, 2, 1, 1, 0, 2, 1, 1, 1, 1];
        int[] realms = [0, 0, 0, 0, 1, 2, 3, 3, 4, 5, 6, 7], pCosts = [1, 1, 1, 1, 2, 5, 5, 5, 4, 5, 6, 7];
        string[][] req = [[], [passive[0].Id + ":1"], [passive[1].Id + ":2"], [passive[1].Id + ":2"],
            [passive[1].Id + ":3"], [active[1].Id + ":3", passive[4].Id + ":5"],
            [passive[5].Id + ":1"], [passive[5].Id + ":1"], [passive[6].Id + ":1", passive[7].Id + ":1"],
            [passive[8].Id + ":3"], [passive[9].Id + ":3"], [passive[10].Id + ":3"]];
        for (var i = 0; i < passive.Length; i++) {
            var p = passive[i];
            var stats = new[] { ("Tấn công", p.Atk), ("Phòng thủ", p.Def), ("Máu", p.Hp), ("Bạo kích", p.Crit),
                ("Né tránh", p.Speed), ("Tốc độ tu luyện", p.Qi), ("Sát thương chiêu thức", p.Skill), ("Xuyên thấu", p.Pen) }
                .Where(s => s.Item2 > 0).Select(s => $"{s.Item1} +{s.Item2.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}% mỗi cấp").ToList();
            if (p.Weaken > 0) stats.Add($"Công quái −{p.Weaken:0.#}% mỗi cấp (tổng giảm tối đa 40%)");
            yield return new(p.Id, element, p.Name, i == 0 ? "Căn cơ" : "Bị động", pRows[i], pCols[i], 5, realms[i], pCosts[i], req[i],
                string.Join("; ", stats) + ".", i == 0 ? "root" : "passive", AtkPct: p.Atk, DefPct: p.Def, HpPct: p.Hp,
                Crit: p.Crit, Speed: p.Speed, QiPct: p.Qi, MonAtkDown: p.Weaken, SkillPct: p.Skill, Pen: p.Pen, Code: $"{code}_PASSIVE_{i:000}");
        }
    }
}
