namespace VanDao.Game;

/// <summary>Linh thảo bổ sung từ bộ ảnh dự trữ. Rơi độc lập khi thám hiểm, không đổi bảng thưởng rương.</summary>
public static class HerbExpansion
{
    public static readonly ItemDef[] Items =
    [
        new("bach_diep_thao", "Bách Diệp Thảo", "herb", "#78b861", "Bụi cỏ nhiều tầng lá nhỏ, tích linh khí đất trời trong từng gân lá.", "img/herb/herb_04.png", Rarity: "common", Kind: "Linh thảo", Use: "Phụ liệu luyện Tụ Khí Đan: 1 cây mỗi viên. Thu thập khi thám hiểm chương 1–2."),
        new("thuy_lo_diep", "Thủy Lộ Diệp", "herb", "#73c4af", "Lá ngọc giữ những giọt sương không tan, dược tính ôn hòa giúp dung hợp đan dịch.", "img/herb/herb_09.png", Rarity: "common", Kind: "Linh thảo", Use: "Phụ liệu luyện Ngưng Kim Đan: 1 lá mỗi viên. Thu thập khi thám hiểm chương 2–3."),
        new("thach_dai_thao", "Thạch Đài Thảo", "herb", "#86a76b", "Rêu linh mọc thành đài trên đá cổ, rễ bám chắc và chứa sinh khí dịu nhẹ.", "img/herb/herb_10.png", Rarity: "common", Kind: "Linh thảo", Use: "Phụ liệu luyện Linh Thú Đan: 1 cây mỗi viên. Thu thập khi thám hiểm chương 1–3."),
        new("nguyet_lo_hoa", "Nguyệt Lộ Hoa", "herb", "#a4d9f0", "Hoa lam nhạt chỉ hé cánh khi có sương đêm, nhụy hoa kết ánh trăng thành giọt ngọc.", "img/herb/herb_03.png", Rarity: "uncommon", Kind: "Linh thảo", Use: "Phụ liệu luyện Trúc Cơ Đan: 1 hoa mỗi viên. Thu thập từ ải 5, chương 1–2."),
        new("bang_tinh_thao", "Băng Tinh Thảo", "herb", "#89cfee", "Linh thảo có lá như tinh thể băng, hàn khí trong veo giúp thanh lọc tạp chất.", "img/herb/herb_08.png", Rarity: "uncommon", Kind: "Linh thảo", Use: "Phụ liệu luyện Tẩy Tủy Đan: 1 cây mỗi viên. Thu thập từ ải 5, chương 1–2."),
        new("phong_vu_thao", "Phong Vũ Thảo", "herb", "#8cc9bc", "Lá mảnh cong theo gió như lông vũ, mang linh tức nhẹ mà bền.", "img/herb/herb_14.png", Rarity: "uncommon", Kind: "Linh thảo", Use: "Phụ liệu luyện Hóa Hình Đan: 1 cây mỗi viên. Thu thập từ ải 5, chương 3–4."),
        new("kim_duong_lien", "Kim Dương Liên", "herb", "#efd16c", "Đóa sen vàng hấp thu nắng sớm, tâm sen tỏa dương khí tinh thuần.", "img/herb/herb_13.png", Rarity: "rare", Kind: "Linh thảo", Use: "Phụ liệu luyện Kết Anh Đan: 1 đóa mỗi viên. Thu thập từ ải 10, chương 3–4."),
        new("linh_cot_chi", "Linh Cốt Chi", "herb", "#a7b7ec", "Linh chi xanh mọc từ linh cốt cổ, phiến nấm có vân ngọc dịu sáng.", "img/herb/herb_15.png", Rarity: "rare", Kind: "Linh thảo", Use: "Phụ liệu luyện Hóa Thần Đan: 1 nhánh mỗi viên. Thu thập từ ải 10, chương 4–5."),
        new("dia_mach_tien_dang", "Địa Mạch Tiên Đằng", "herb", "#c0a078", "Dây leo già cắm rễ sâu vào linh mạch, từng đốt thân lưu giữ địa khí ngưng tụ.", "img/herb/herb_18.png", Rarity: "rare", Kind: "Linh thảo", Use: "Phụ liệu luyện Luyện Hư Đan: 1 dây mỗi viên. Thu thập từ ải 10, chương 6–7."),
        new("thien_truc_duan", "Thiên Trúc Duẩn", "herb", "#a8cf85", "Măng trúc linh sinh trên đỉnh núi, từng lớp bẹ ôm trọn một luồng thanh khí.", "img/herb/herb_19.png", Rarity: "rare", Kind: "Linh thảo", Use: "Phụ liệu luyện Hợp Thể Đan: 1 măng mỗi viên. Thu thập từ ải 10, chương 7–9."),
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
        new("dia_mach_tien_dang", new[] {5, 6}, 10, 1, 1, .025, InChest: false),
        new("thien_truc_duan", new[] {6, 7, 8}, 10, 1, 1, .02, InChest: false),
    ];

    public static readonly IReadOnlyDictionary<string, string> RecipeHerbs = new Dictionary<string, string>
    {
        ["tu_khi_dan"] = "bach_diep_thao", ["truc_co_dan"] = "nguyet_lo_hoa",
        ["ngung_kim_dan"] = "thuy_lo_diep", ["ket_anh_dan"] = "kim_duong_lien",
        ["hoa_than_dan"] = "linh_cot_chi", ["luyen_hu_dan"] = "dia_mach_tien_dang",
        ["hop_the_dan"] = "thien_truc_duan", ["linh_thu_dan"] = "thach_dai_thao",
        ["hoa_hinh_dan"] = "phong_vu_thao", ["tay_tuy_dan"] = "bang_tinh_thao",
    };

    public static BrewDef ApplyRecipe(BrewDef recipe) => RecipeHerbs.TryGetValue(recipe.Out, out var herb)
        ? recipe with { In = new Dictionary<string, int>(recipe.In) { [herb] = 1 } }
        : recipe;
}
