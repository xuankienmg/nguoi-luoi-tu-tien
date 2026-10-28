namespace VanDao.Game;

/// <summary>Linh thảo bổ sung từ bộ ảnh dự trữ. Rơi độc lập khi thám hiểm (Thường theo chương; Khó / Ác mộng cũng theo đúng chương,
/// xem GameData.HerbDropDefsFor), không đổi bảng thưởng rương. Mỗi đan dùng 1 loại cùng bậc cảnh giới (RecipeHerbs).</summary>
public static class HerbExpansion
{
    public static readonly ItemDef[] Items =
    [
        new("bach_diep_thao", "Bách Diệp Thảo", "herb", "#78b861", "Bụi cỏ nhiều tầng lá nhỏ, tích linh khí đất trời trong từng gân lá.", "img/herb/herb_04.png", Rarity: "common", Kind: "Linh thảo", Use: "Phụ liệu luyện Tụ Khí Đan (1 cây mỗi viên) và Bổ Nguyên Đan · Hạ phẩm (2 cây mỗi viên). Thu thập khi thám hiểm chương 1–2. Khó / Ác Mộng: rơi ở đúng chương đó (số lượng × hệ số thưởng mức khó)."),
        new("thuy_lo_diep", "Thủy Lộ Diệp", "herb", "#73c4af", "Lá ngọc giữ những giọt sương không tan, dược tính ôn hòa giúp dung hợp đan dịch.", "img/herb/herb_09.png", Rarity: "common", Kind: "Linh thảo", Use: "Phụ liệu luyện Ngưng Kim Đan và Kim Đan Phá Chướng Đan (1 lá mỗi viên), Bổ Nguyên Đan · Trung phẩm (2 lá mỗi viên). Thu thập khi thám hiểm chương 2–3. Khó / Ác Mộng: rơi ở đúng chương đó (số lượng × hệ số thưởng mức khó)."),
        new("thach_dai_thao", "Thạch Đài Thảo", "herb", "#86a76b", "Rêu linh mọc thành đài trên đá cổ, rễ bám chắc và chứa sinh khí dịu nhẹ.", "img/herb/herb_10.png", Rarity: "common", Kind: "Linh thảo", Use: "Phụ liệu luyện Linh Thú Đan: 1 cây mỗi viên. Thu thập khi thám hiểm chương 1–3. Khó / Ác Mộng: rơi ở đúng chương đó (số lượng × hệ số thưởng mức khó)."),
        new("nguyet_lo_hoa", "Nguyệt Lộ Hoa", "herb", "#a4d9f0", "Hoa lam nhạt chỉ hé cánh khi có sương đêm, nhụy hoa kết ánh trăng thành giọt ngọc.", "img/herb/herb_03.png", Rarity: "uncommon", Kind: "Linh thảo", Use: "Phụ liệu luyện Trúc Cơ Đan và Trúc Cơ Phá Chướng Đan: 1 hoa mỗi viên. Thu thập từ ải 5, chương 1–2. Khó / Ác Mộng: rơi ở đúng chương đó (số lượng × hệ số thưởng mức khó)."),
        new("bang_tinh_thao", "Băng Tinh Thảo", "herb", "#89cfee", "Linh thảo có lá như tinh thể băng, hàn khí trong veo giúp thanh lọc tạp chất.", "img/herb/herb_08.png", Rarity: "uncommon", Kind: "Linh thảo", Use: "Phụ liệu luyện Tẩy Tủy Đan: 1 cây mỗi viên. Thu thập từ ải 5, chương 1–2. Khó / Ác Mộng: rơi ở đúng chương đó (số lượng × hệ số thưởng mức khó)."),
        new("phong_vu_thao", "Phong Vũ Thảo", "herb", "#8cc9bc", "Lá mảnh cong theo gió như lông vũ, mang linh tức nhẹ mà bền.", "img/herb/herb_14.png", Rarity: "uncommon", Kind: "Linh thảo", Use: "Phụ liệu luyện Hóa Hình Đan: 1 cây mỗi viên. Thu thập từ ải 5, chương 3–4. Khó / Ác Mộng: rơi ở đúng chương đó (số lượng × hệ số thưởng mức khó)."),
        new("kim_duong_lien", "Kim Dương Liên", "herb", "#efd16c", "Đóa sen vàng hấp thu nắng sớm, tâm sen tỏa dương khí tinh thuần.", "img/herb/herb_13.png", Rarity: "rare", Kind: "Linh thảo", Use: "Phụ liệu luyện Kết Anh Đan, Nguyên Anh Phá Chướng Đan và Bổ Nguyên Đan · Thượng phẩm: 1 đóa mỗi viên. Thu thập từ ải 10, chương 3–4. Khó / Ác Mộng: rơi ở đúng chương đó (số lượng × hệ số thưởng mức khó)."),
        new("linh_cot_chi", "Linh Cốt Chi", "herb", "#a7b7ec", "Linh chi xanh mọc từ linh cốt cổ, phiến nấm có vân ngọc dịu sáng.", "img/herb/herb_15.png", Rarity: "rare", Kind: "Linh thảo", Use: "Phụ liệu luyện Hóa Thần Đan, Hóa Thần Phá Chướng Đan và Thần Thú Huyết (1 nhánh mỗi viên), Ngộ Đạo Đan (2 nhánh mỗi viên). Thu thập từ ải 10, chương 4–5. Khó / Ác Mộng: rơi ở đúng chương đó (số lượng × hệ số thưởng mức khó)."),
        new("dia_mach_tien_dang", "Địa Mạch Tiên Đằng", "herb", "#c0a078", "Dây leo già cắm rễ sâu vào linh mạch, từng đốt thân lưu giữ địa khí ngưng tụ.", "img/herb/herb_18.png", Rarity: "rare", Kind: "Linh thảo", Use: "Linh thảo Hiếm bắt buộc cho Luyện Hư Đan và Phá Chướng Đan để vào Luyện Hư: 1 dây mỗi viên. Thu thập từ ải 10, chương 6–10 mức Thường; cũng rơi ở Khó / Ác Mộng."),
        new("thien_truc_duan", "Thiên Trúc Duẩn", "herb", "#a8cf85", "Măng trúc linh sinh trên đỉnh núi, từng lớp bẹ ôm trọn một luồng thanh khí.", "img/herb/herb_19.png", Rarity: "rare", Kind: "Linh thảo", Use: "Linh thảo Hiếm bắt buộc cho Hợp Thể Đan và Phá Chướng Đan để vào Hợp Thể: 1 măng mỗi viên. Thu thập từ ải 10, chương 7–10 mức Thường; cũng rơi ở Khó / Ác Mộng."),
        new("phuong_vu_chi", "Phượng Vũ Chi", "herb", "#efb765", "Linh chi kết phiến như lông phượng, tích tụ dương hỏa và đạo vận để ổn định đan thai cấp cao.", "img/herb/herb_20.png", Rarity: "rare", Kind: "Linh thảo", Use: "Linh thảo Hiếm bắt buộc: Đại Thừa Đan cần 2 nhánh, Độ Kiếp Đan cần 3 nhánh mỗi viên; Phá Chướng Đan vào hai cảnh giới này cần cùng lượng. Thu thập từ ải 10, chương 8–10 mức Thường; cũng rơi ở Khó / Ác Mộng."),
        new("hon_nguyen_linh_chung", "Hỗn Nguyên Linh Chủng", "herb", "#d5b486", "Hạt linh mộc cổ phong kín một tia hỗn nguyên khí, chỉ nảy mầm gần đạo mạch thâm sâu.", "img/herb/herb_05.png", Rarity: "rare", Kind: "Linh thảo", Use: "Linh thảo Hiếm bắt buộc cho Chân Tiên Đan và Phá Chướng Đan để vào Chân Tiên: 4 hạt mỗi viên. Thu thập từ ải 10, chương 9–10 mức Thường; cũng rơi ở Khó / Ác Mộng."),
    ];

    public static readonly HerbDropDef[] Drops =
    [
        new("bach_diep_thao", new[] {0, 1}, 1, 1, 2, .16, InChest: false),
        new("thuy_lo_diep", new[] {1, 2}, 1, 1, 2, .14, InChest: false),
        new("thach_dai_thao", new[] {0, 1, 2}, 1, 1, 2, .12, InChest: false),
        new("nguyet_lo_hoa", new[] {0, 1}, 5, 1, 1, .08, InChest: false),
        new("bang_tinh_thao", new[] {0, 1}, 5, 1, 1, .07, InChest: false),
        new("phong_vu_thao", new[] {2, 3}, 5, 1, 1, .06, InChest: false),
        new("kim_duong_lien", new[] {2, 3}, 10, 1, 1, .04, InChest: false),
        new("linh_cot_chi", new[] {3, 4}, 10, 1, 1, .035, InChest: false),
        new("dia_mach_tien_dang", new[] {5, 6, 7, 8, 9}, 10, 1, 1, .025, InChest: false),
        new("thien_truc_duan", new[] {6, 7, 8, 9}, 10, 1, 1, .02, InChest: false),
        new("phuong_vu_chi", new[] {7, 8, 9}, 10, 1, 1, .018, InChest: false),
        new("hon_nguyen_linh_chung", new[] {8, 9}, 10, 1, 1, .012, InChest: false),
    ];

    public static readonly IReadOnlyDictionary<string, (string Herb, int Qty)> RecipeHerbs = new Dictionary<string, (string, int)>
    {
        ["tu_khi_dan"] = ("bach_diep_thao", 1), ["truc_co_dan"] = ("nguyet_lo_hoa", 1),
        ["ngung_kim_dan"] = ("thuy_lo_diep", 1), ["ket_anh_dan"] = ("kim_duong_lien", 1),
        ["hoa_than_dan"] = ("linh_cot_chi", 1), ["luyen_hu_dan"] = ("dia_mach_tien_dang", 1),
        ["hop_the_dan"] = ("thien_truc_duan", 1), ["linh_thu_dan"] = ("thach_dai_thao", 1),
        ["hoa_hinh_dan"] = ("phong_vu_thao", 1), ["tay_tuy_dan"] = ("bang_tinh_thao", 1),
        ["dai_thua_dan"] = ("phuong_vu_chi", 2), ["do_kiep_dan"] = ("phuong_vu_chi", 3),
        ["chan_tien_dan"] = ("hon_nguyen_linh_chung", 4),
        ["pha_chuong_dan_4"] = ("dia_mach_tien_dang", 1), ["pha_chuong_dan_5"] = ("thien_truc_duan", 1),
        ["pha_chuong_dan_6"] = ("phuong_vu_chi", 2), ["pha_chuong_dan_7"] = ("phuong_vu_chi", 3),
        ["pha_chuong_dan_8"] = ("hon_nguyen_linh_chung", 4),
        ["pha_chuong_dan_9"] = ("hon_nguyen_linh_chung", 4),
        // 10/2026: các đan còn lại cũng dùng 1 linh thảo bổ sung cùng bậc cảnh giới (giữ nguyên nguyên liệu cũ)
        ["bo_nguyen_dan_ha"] = ("bach_diep_thao", 2), ["bo_nguyen_dan_trung"] = ("thuy_lo_diep", 2), ["bo_nguyen_dan_thuong"] = ("kim_duong_lien", 1),
        ["pha_chuong_dan_0"] = ("nguyet_lo_hoa", 1), ["pha_chuong_dan_1"] = ("thuy_lo_diep", 1),
        ["pha_chuong_dan_2"] = ("kim_duong_lien", 1), ["pha_chuong_dan_3"] = ("linh_cot_chi", 1),
        ["than_thu_huyet"] = ("linh_cot_chi", 1), ["ngo_dao_dan"] = ("linh_cot_chi", 2),
    };

    public static BrewDef ApplyRecipe(BrewDef recipe) => RecipeHerbs.TryGetValue(recipe.Out, out var herb)
        ? recipe with { In = new Dictionary<string, int>(recipe.In) { [herb.Herb] = herb.Qty } }
        : recipe;
}
