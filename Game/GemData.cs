using System.Text.Json;
using VanDao.Data;

namespace VanDao.Game;

public class GemSlotData
{
    public List<string> Sockets { get; set; } = new();
    public int Extra { get; set; }
}

public record GemBonuses(
    int Atk = 0,
    int Def = 0,
    int Hp = 0,
    double Crit = 0,
    double Speed = 0,
    double QiPct = 0,
    double Pen = 0,
    double Guard = 0,
    double SkillPct = 0,
    double Rage = 0,
    double ExtraHit = 0,
    double Shield = 0,
    double Reflect = 0,
    double Regen = 0,
    double Lightning = 0,
    double LightningPct = 0);

public record GemRecipeDef(
    string Id,
    string Name,
    string Category,
    string ResultItem,
    int ResultCount,
    Dictionary<string, int> Inputs,
    int Stones,
    string Desc,
    double Chance = 1.0);

public record FusionEvaluation(
    bool IsValid,
    string? RecipeId,
    string Name,
    string Desc,
    string? ResultItem,
    List<string> PossibleResults,
    int ResultCount,
    int Stones,
    double Chance,
    Dictionary<string, int> RequiredInputs,
    string? Hint
);

public static class GemData
{
    public static readonly HashSet<string> SoStones = new()
    {
        "stone_viem_so", "stone_bang_so", "stone_loi_so", "stone_kim_so", "stone_tho_so"
    };

    public static readonly HashSet<string> CaoStones = new()
    {
        "stone_viem_cao", "stone_bang_cao", "stone_loi_cao", "stone_kim_cao", "stone_tho_cao"
    };

    public static readonly HashSet<string> DivineStones = new()
    {
        "stone_liet_loi", "stone_huyen_bang", "stone_am_duong"
    };

    public static readonly HashSet<string> CuiGems = new()
    {
        "gem_ruby_cui", "gem_sapphire_cui", "gem_topaz_cui", "gem_emerald_cui", "gem_amethyst_cui"
    };

    public static readonly HashSet<string> XinGems = new()
    {
        "gem_ruby_xin", "gem_sapphire_xin", "gem_topaz_xin", "gem_emerald_xin", "gem_amethyst_xin"
    };

    public static readonly Dictionary<string, string> SoToCaoMap = new()
    {
        ["stone_viem_so"] = "stone_viem_cao",
        ["stone_bang_so"] = "stone_bang_cao",
        ["stone_loi_so"] = "stone_loi_cao",
        ["stone_kim_so"] = "stone_kim_cao",
        ["stone_tho_so"] = "stone_tho_cao",
    };

    public static readonly Dictionary<string, string> CuiToXinMap = new()
    {
        ["gem_ruby_cui"] = "gem_ruby_xin",
        ["gem_sapphire_cui"] = "gem_sapphire_xin",
        ["gem_topaz_cui"] = "gem_topaz_xin",
        ["gem_emerald_cui"] = "gem_emerald_xin",
        ["gem_amethyst_cui"] = "gem_amethyst_xin",
    };

    public static readonly Dictionary<string, string> HerbToCuiGemMap = new()
    {
        // Fire (gem_ruby_cui): huyet_can_thao, hoa_tam_hoa, bat_tu_phuong_hoa, phuong_vu_chi, am_duong_ban_dao
        ["huyet_can_thao"] = "gem_ruby_cui",
        ["hoa_tam_hoa"] = "gem_ruby_cui",
        ["bat_tu_phuong_hoa"] = "gem_ruby_cui",
        ["phuong_vu_chi"] = "gem_ruby_cui",
        ["am_duong_ban_dao"] = "gem_ruby_cui",

        // Water/Ice (gem_sapphire_cui): tu_linh_thao, thuy_lo_diep, nguyet_lo_hoa, bang_tinh_thao, cuu_diep_hon_lien
        ["tu_linh_thao"] = "gem_sapphire_cui",
        ["thuy_lo_diep"] = "gem_sapphire_cui",
        ["nguyet_lo_hoa"] = "gem_sapphire_cui",
        ["bang_tinh_thao"] = "gem_sapphire_cui",
        ["cuu_diep_hon_lien"] = "gem_sapphire_cui",

        // Earth/Gold (gem_topaz_cui): thien_nien_sam, thach_dai_thao, kim_duong_lien, dia_mach_tien_dang, hon_nguyen_linh_chung
        ["thien_nien_sam"] = "gem_topaz_cui",
        ["thach_dai_thao"] = "gem_topaz_cui",
        ["kim_duong_lien"] = "gem_topaz_cui",
        ["dia_mach_tien_dang"] = "gem_topaz_cui",
        ["hon_nguyen_linh_chung"] = "gem_topaz_cui",

        // Wood/Wind (gem_emerald_cui): bich_ngoc_linh_thao, tay_tuy_thao, bach_diep_thao, phong_vu_thao, kien_moc_mieu, thien_truc_duan
        ["bich_ngoc_linh_thao"] = "gem_emerald_cui",
        ["tay_tuy_thao"] = "gem_emerald_cui",
        ["bach_diep_thao"] = "gem_emerald_cui",
        ["phong_vu_thao"] = "gem_emerald_cui",
        ["kien_moc_mieu"] = "gem_emerald_cui",
        ["thien_truc_duan"] = "gem_emerald_cui",

        // Lightning/Soul (gem_amethyst_cui): tu_van_sam, loi_mach_thao, linh_cot_chi, hon_nguyen_dao_lien
        ["tu_van_sam"] = "gem_amethyst_cui",
        ["loi_mach_thao"] = "gem_amethyst_cui",
        ["linh_cot_chi"] = "gem_amethyst_cui",
        ["hon_nguyen_dao_lien"] = "gem_amethyst_cui"
    };

    public static string HerbToGemCui(string herbId) =>
        HerbToCuiGemMap.GetValueOrDefault(herbId, "gem_ruby_cui");

    public static bool IsHerb(string id) =>
        HerbToCuiGemMap.ContainsKey(id) || (GameData.ItemById.TryGetValue(id, out var it) && it.Type == "herb");

    public static readonly HashSet<string> HighTierItems = new()
    {
        "gem_ruby_xin", "gem_sapphire_xin", "gem_topaz_xin", "gem_emerald_xin", "gem_amethyst_xin",
        "stone_liet_loi", "stone_huyen_bang", "stone_am_duong",
        "stone_socket", "gem_thien_phu"
    };

    public static int GetItemPowerScore(string id)
    {
        if (id == "gem_thien_phu") return 14;
        if (DivineStones.Contains(id) || id == "stone_socket") return 10;
        if (XinGems.Contains(id)) return 8;
        if (CaoStones.Contains(id)) return 6;
        if (CuiGems.Contains(id)) return 3;
        if (SoStones.Contains(id)) return 2;
        if (IsHerb(id)) return 1;
        return 1;
    }

    public static int SlotsOf(string id, int qty) => IsHerb(id) ? Math.Max(1, qty / 10) : qty;
    // =========================================================================
    // ĐỊNH NGHĨA VẬT PHẨM: ĐÁ (Khoáng Thạch / Phù Thạch) & NGỌC (Bảo Ngọc)
    // =========================================================================

    public static readonly IReadOnlyList<ItemDef> Items = new ItemDef[]
    {
        // ------------------ 1. CỔ PHÙ SƠ CẤP (Phẩm Lam - uncommon) ------------------
        new(
            "stone_viem_so", "Viêm Cổ Phù · Sơ", "stone", "#ff4d4f",
            "Cổ phù chứa chân hỏa viễn cổ thô ráp, kích phát linh khí cuồn cuộn trong vũ khí phòng cụ.",
            Icon: "img/core/core_01.png", Rarity: "uncommon", Kind: "Cổ Phù",
            Atk: 20,
            Effects: new[] { new ItemEffect("linh_ap", "Linh áp", 1, 3, 0, "+3% sát thương kỹ năng") },
            Special: "Chú linh: +20 Công · +3% Sát thương kỹ năng"
        ),
        new(
            "stone_bang_so", "Băng Cổ Phù · Sơ", "stone", "#40a9ff",
            "Cổ phù ngưng tụ hàn băng ngàn năm, tỏa hàn khí gia cố hộ thể.",
            Icon: "img/core/core_04.png", Rarity: "uncommon", Kind: "Cổ Phù",
            Def: 20,
            Effects: new[] { new ItemEffect("ho_the", "Hộ thể", 1, 3, 0, "Giảm 3% sát thương nhận") },
            Special: "Chú linh: +20 Thủ · Giảm 3% sát thương nhận"
        ),
        new(
            "stone_loi_so", "Lôi Cổ Phù · Sơ", "stone", "#9254de",
            "Cổ phù mang tàn dư của thiên lôi, ẩn chứa lôi đình cuồng bạo.",
            Icon: "img/core/core_07.png", Rarity: "uncommon", Kind: "Cổ Phù",
            CritPct: 3,
            Effects: new[] { new ItemEffect("thien_loi", "Thiên lôi", 1, 5, 20, "Mỗi lượt 5% cơ hội giáng sét 20% công") },
            Special: "Chú linh: +3% Bạo kích · 5% giáng Thiên lôi"
        ),
        new(
            "stone_kim_so", "Kim Cổ Phù · Sơ", "stone", "#faad14",
            "Cổ phù Canh Kim sắc bén, sắc bén vô song xuyên thấu hộ giáp.",
            Icon: "img/core/core_10.png", Rarity: "uncommon", Kind: "Cổ Phù",
            Effects: new[] {
                new ItemEffect("pha_giap", "Phá giáp", 1, 4, 0, "Bỏ qua 4% phòng thủ quái"),
                new ItemEffect("lien_kich", "Liên kích", 1, 3, 0, "3% cơ hội đánh thêm 1 đòn")
            },
            Special: "Chú linh: Bỏ qua 4% thủ quái · +3% Liên kích"
        ),
        new(
            "stone_tho_so", "Thổ Cổ Phù · Sơ", "stone", "#a07040",
            "Cổ phù trầm tích ngàn trượng sâu, dày dặn vững chãi như núi non.",
            Icon: "img/core/core_14.png", Rarity: "uncommon", Kind: "Cổ Phù",
            Hp: 180,
            Effects: new[] { new ItemEffect("tho_thuan", "Thổ thuẫn", 1, 4, 0, "Khiên đầu trận +4% máu") },
            Special: "Chú linh: +180 Máu · Khiên đầu trận +4% máu"
        ),

        // ------------------ 2. CỔ PHÙ CAO CẤP (Phẩm Cam - mythic) ------------------
        new(
            "stone_viem_cao", "Viêm Cổ Phù · Cao", "stone", "#ff7a45",
            "Cổ phù ngưng tụ địa hỏa tinh túy, nóng rực rực rỡ, phá toái cấm chế.",
            Icon: "img/core/core_02.png", Rarity: "mythic", Kind: "Cổ Phù",
            Atk: 60,
            Effects: new[] { new ItemEffect("linh_ap", "Linh áp", 2, 8, 0, "+8% sát thương kỹ năng") },
            Special: "Chú linh: +60 Công · +8% Sát thương kỹ năng"
        ),
        new(
            "stone_bang_cao", "Băng Cổ Phù · Cao", "stone", "#1890ff",
            "Cổ phù huyền băng vạn năm tuyệt đối, hàn khí thấu xương đóng băng vạn vật.",
            Icon: "img/core/core_05.png", Rarity: "mythic", Kind: "Cổ Phù",
            Def: 60,
            Effects: new[] { new ItemEffect("ho_the", "Hộ thể", 2, 8, 0, "Giảm 8% sát thương nhận") },
            Special: "Chú linh: +60 Thủ · Giảm 8% sát thương nhận"
        ),
        new(
            "stone_loi_cao", "Lôi Cổ Phù · Cao", "stone", "#722ed1",
            "Cổ phù Cửu Thiên Huyền Lôi cô đọng, lôi quang chớp giật không ngừng.",
            Icon: "img/core/core_09.png", Rarity: "mythic", Kind: "Cổ Phù",
            CritPct: 8,
            Effects: new[] { new ItemEffect("thien_loi", "Thiên lôi", 2, 12, 40, "Mỗi lượt 12% cơ hội giáng sét 40% công") },
            Special: "Chú linh: +8% Bạo kích · 12% giáng Thiên lôi"
        ),
        new(
            "stone_kim_cao", "Kim Cổ Phù · Cao", "stone", "#d48806",
            "Cổ phù Canh Kim thuần túy sắc bén nhất thế gian, trảm đoạn huyền cương.",
            Icon: "img/core/core_11.png", Rarity: "mythic", Kind: "Cổ Phù",
            Effects: new[] {
                new ItemEffect("pha_giap", "Phá giáp", 2, 10, 0, "Bỏ qua 10% phòng thủ quái"),
                new ItemEffect("lien_kich", "Liên kích", 2, 7, 0, "7% cơ hội đánh thêm 1 đòn")
            },
            Special: "Chú linh: Bỏ qua 10% thủ quái · +7% Liên kích"
        ),
        new(
            "stone_tho_cao", "Thổ Cổ Phù · Cao", "stone", "#874d00",
            "Cổ phù Thái Sơn trầm tĩnh, tiếp dẫn đại địa tinh hoa, sinh cơ vô tận.",
            Icon: "img/core/core_13.png", Rarity: "mythic", Kind: "Cổ Phù",
            Hp: 500,
            Effects: new[] { new ItemEffect("tho_thuan", "Thổ thuẫn", 2, 10, 0, "Khiên đầu trận +10% máu") },
            Special: "Chú linh: +500 Máu · Khiên đầu trận +10% máu"
        ),

        // ------------------ 3. CỔ PHÙ BIẾN DỊ & KHAI KHIẾU (Phẩm Đỏ / Cam) ------------------
        new(
            "stone_liet_loi", "Liệt Lôi Cổ Phù", "stone", "#f5222d",
            "Hỏa diệm hòa quyện Lôi đình biến dị nhị hợp, thiêu đốt sát thương và sét đánh cuồng bạo.",
            Icon: "img/core/core_12.png", Rarity: "divine", Kind: "Cổ Phù",
            Atk: 90, CritPct: 5,
            Effects: new[] {
                new ItemEffect("thien_loi", "Thiên lôi", 3, 15, 50, "Mỗi lượt 15% cơ hội giáng sét 50% công"),
                new ItemEffect("linh_ap", "Linh áp", 3, 6, 0, "+6% sát thương kỹ năng")
            },
            Special: "Chú linh: +90 Công · +5% Bạo kích · 15% giáng Thiên lôi"
        ),
        new(
            "stone_huyen_bang", "Huyền Băng Cổ Phù", "stone", "#13c2c2",
            "Băng hàn kết hợp Trọng thổ biến dị nhị hợp, băng giáp hộ thể hóa giải vạn đòn tấn công.",
            Icon: "img/core/core_08.png", Rarity: "divine", Kind: "Cổ Phù",
            Def: 90, Hp: 600,
            Effects: new[] {
                new ItemEffect("ho_the", "Hộ thể", 3, 10, 0, "Giảm 10% sát thương nhận"),
                new ItemEffect("tho_thuan", "Thổ thuẫn", 3, 10, 0, "Khiên đầu trận +10% máu")
            },
            Special: "Chú linh: +90 Thủ · +600 Máu · Giảm 10% sát thương · Khiên +10% máu"
        ),
        new(
            "stone_am_duong", "Âm Dương Cổ Phù", "stone", "#eb2f96",
            "Chân hỏa và Cực hàn tương sinh tương khắc biến dị nhị hợp, vừa phản chấn vừa hồi phục sinh cơ.",
            Icon: "img/core/core_06.png", Rarity: "divine", Kind: "Cổ Phù",
            Atk: 60, Def: 60,
            Effects: new[] {
                new ItemEffect("dia_mach", "Địa mạch", 3, 5, 0, "Phản đòn 5% sát thương quái"),
                new ItemEffect("hoi_khi", "Hồi khí", 3, 3, 0, "Hồi 3% máu mỗi lượt")
            },
            Special: "Chú linh: +60 Công · +60 Thủ · Phản đòn 5% · Hồi 3% máu/lượt"
        ),
        new(
            "stone_socket", "Khai Khiếu Cổ Phù", "stone", "#ffd700",
            "Cổ phù thần bí khai khiếu mở socket, dùng để khai thông lỗ khảm trang bị thứ 2 và thứ 3.",
            Icon: "img/core/core_15.png", Rarity: "mythic", Kind: "Cổ Phù",
            Special: "Dùng để đục mở thêm 1 Lỗ Khảm cho bất kỳ ô trang bị nào (tối đa 3 lỗ)."
        ),

        // ------------------ 4. TIÊN CHÂU THÔ (Phẩm Lam - rare) ------------------
        new(
            "gem_ruby_cui", "Xích Diễm Tiên Châu · Thô", "gem", "#ff4d4f",
            "Tiên châu sắc đỏ rực rỡ, tích tụ hỏa linh thiên địa thô sơ.",
            Icon: "img/core/core_01.png", Rarity: "rare", Kind: "Tiên Châu",
            Atk: 100,
            Special: "Chú linh: +100 Tấn công"
        ),
        new(
            "gem_sapphire_cui", "Huyền Băng Tiên Châu · Thô", "gem", "#40a9ff",
            "Tiên châu sắc lam trong vắt, kết tinh thủy linh ôn nhuần bảo hộ.",
            Icon: "img/core/core_04.png", Rarity: "rare", Kind: "Tiên Châu",
            Def: 80,
            Special: "Chú linh: +80 Phòng thủ"
        ),
        new(
            "gem_topaz_cui", "Thái Dương Tiên Châu · Thô", "gem", "#faad14",
            "Tiên châu hoàng kim rực rỡ, hấp thu thổ linh trù phú bồi dưỡng sinh mệnh.",
            Icon: "img/core/core_10.png", Rarity: "rare", Kind: "Tiên Châu",
            Hp: 600,
            Special: "Chú linh: +600 Máu tối đa"
        ),
        new(
            "gem_emerald_cui", "Thanh Mộc Tiên Châu · Thô", "gem", "#52c41a",
            "Tiên châu lục bích tinh xảo, ẩn chứa mộc linh sinh trưởng nhẹ nhàng.",
            Icon: "img/core/core_03.png", Rarity: "rare", Kind: "Tiên Châu",
            SpeedPct: 4,
            Effects: new[] { new ItemEffect("hap_linh", "Hấp linh", 1, 4, 0, "+4% tốc độ tu luyện") },
            Special: "Chú linh: +4% Tu luyện · +4 Thân pháp"
        ),
        new(
            "gem_amethyst_cui", "Tử Lôi Tiên Châu · Thô", "gem", "#9254de",
            "Tiên châu tím huyền bí, ngưng tụ linh hồn linh cảm thiên cơ.",
            Icon: "img/core/core_07.png", Rarity: "rare", Kind: "Tiên Châu",
            CritPct: 4,
            Effects: new[] { new ItemEffect("tinh_tam", "Tĩnh tâm", 1, 15, 0, "Vào trận với 15 nộ") },
            Special: "Chú linh: +4% Bạo kích · +15 Nộ đầu trận"
        ),

        // ------------------ 5. TIÊN CHÂU HOÀN MỸ (Phẩm Cam - mythic) ------------------
        new(
            "gem_ruby_xin", "Xích Diễm Tiên Châu · Hoàn Mỹ", "gem", "#f5222d",
            "Tiên châu xích huyết long tâm hoàn hảo không tì vết, bộc phát uy lực hủy thiên diệt địa.",
            Icon: "img/core/core_02.png", Rarity: "mythic", Kind: "Tiên Châu",
            Atk: 320,
            Special: "Chú linh: +320 Tấn công"
        ),
        new(
            "gem_sapphire_xin", "Huyền Băng Tiên Châu · Hoàn Mỹ", "gem", "#1890ff",
            "Tiên châu cửu khúc u tuyền thuần khiết, ngưng kết hàn băng hộ giáp kiên cố bất hoại.",
            Icon: "img/core/core_05.png", Rarity: "mythic", Kind: "Tiên Châu",
            Def: 260,
            Special: "Chú linh: +260 Phòng thủ"
        ),
        new(
            "gem_topaz_xin", "Thái Dương Tiên Châu · Hoàn Mỹ", "gem", "#d48806",
            "Tiên châu hoàng long tích tụ vạn năm, khí huyết dồi dào tựa biển sâu.",
            Icon: "img/core/core_11.png", Rarity: "mythic", Kind: "Tiên Châu",
            Hp: 2200,
            Special: "Chú linh: +2.200 Máu tối đa"
        ),
        new(
            "gem_emerald_xin", "Thanh Mộc Tiên Châu · Hoàn Mỹ", "gem", "#389e0d",
            "Tiên châu bích ba trường thanh, thông suốt kinh mạch linh khí tuần hoàn nhanh gấp bội.",
            Icon: "img/core/core_06.png", Rarity: "mythic", Kind: "Tiên Châu",
            SpeedPct: 10,
            Effects: new[] { new ItemEffect("hap_linh", "Hấp linh", 2, 12, 0, "+12% tốc độ tu luyện") },
            Special: "Chú linh: +12% Tu luyện · +10 Thân pháp"
        ),
        new(
            "gem_amethyst_xin", "Tử Lôi Tiên Châu · Hoàn Mỹ", "gem", "#722ed1",
            "Tiên châu tử vi tinh đẩu ngưng kết, dẫn phát sát chiêu bạo liệt chí tử.",
            Icon: "img/core/core_09.png", Rarity: "mythic", Kind: "Tiên Châu",
            CritPct: 12,
            Effects: new[] { new ItemEffect("tinh_tam", "Tĩnh tâm", 2, 35, 0, "Vào trận với 35 nộ") },
            Special: "Chú linh: +12% Bạo kích · +35 Nộ đầu trận"
        ),

        // ------------------ 6. CỔ PHÙ TIÊN CHÂU (Phẩm Đỏ - divine) ------------------
        new(
            "gem_thien_phu", "Cổ Phù Tiên Châu", "gem", "#eb2f96",
            "Dung hợp hoàn mỹ giữa Tiên Châu tinh túy và Đại Đạo Cổ Phù, uy chấn bát hoang.",
            Icon: "img/core/core_15.png", Rarity: "divine", Kind: "Tiên Châu",
            Atk: 380, Hp: 2000, CritPct: 10,
            Effects: new[] { new ItemEffect("linh_ap", "Linh áp", 3, 10, 0, "+10% sát thương kỹ năng") },
            Special: "Chú linh: +380 Công · +2.000 Máu · +10% Bạo kích · +10% Sát thương kỹ năng"
        ),
    };

    public static readonly IReadOnlyDictionary<string, ItemDef> ById = Items.ToDictionary(i => i.Id);

    public static bool IsGemOrStone(string? itemId) => !string.IsNullOrEmpty(itemId) && ById.ContainsKey(itemId);

    public static bool IsStone(string? itemId) => !string.IsNullOrEmpty(itemId) && ById.TryGetValue(itemId, out var it) && it.Type == "stone";

    public static bool IsGem(string? itemId) => !string.IsNullOrEmpty(itemId) && ById.TryGetValue(itemId, out var it) && it.Type == "gem";

    // =========================================================================
    // LƯU TRỮ BẢO KHỐ (GEM VAULT) TRÊN PLAYER
    // =========================================================================

    public static Dictionary<string, int> GetVault(Player p)
    {
        if (string.IsNullOrWhiteSpace(p.GemVault)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(p.GemVault) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public static void SetVault(Player p, Dictionary<string, int> vault)
    {
        p.GemVault = JsonSerializer.Serialize(vault.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    public static int GetCount(Player p, string itemId) =>
        string.IsNullOrEmpty(itemId) ? 0 : GetVault(p).GetValueOrDefault(itemId, 0);

    public static void Add(Player p, string itemId, int count)
    {
        if (count == 0 || string.IsNullOrEmpty(itemId)) return;
        var vault = GetVault(p);
        var cur = vault.GetValueOrDefault(itemId, 0) + count;
        if (cur > 0) vault[itemId] = cur;
        else vault.Remove(itemId);
        SetVault(p, vault);
    }

    // =========================================================================
    // LƯU TRỮ LỖ KHẢM (SOCKETS) THEO 10 Ô TRANG BỊ TRÊN PLAYER
    // =========================================================================

    public static readonly string[] EquipSlots = { "weapon", "offhand", "head", "body", "hands", "legs", "feet", "necklace", "charm", "ring" };

    public static readonly Dictionary<string, string> SlotNames = new()
    {
        ["weapon"] = "Vũ khí",
        ["offhand"] = "Pháp bảo",
        ["head"] = "Mũ",
        ["body"] = "Áo",
        ["hands"] = "Hộ uyển",
        ["legs"] = "Quần",
        ["feet"] = "Giày",
        ["necklace"] = "Dây chuyền",
        ["charm"] = "Hộ phù",
        ["ring"] = "Nhẫn",
    };

    public static Dictionary<string, GemSlotData> GetSlots(Player p)
    {
        if (string.IsNullOrWhiteSpace(p.GemSlots)) return new();
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, GemSlotData>>(p.GemSlots);
            if (dict is null) return new();
            foreach (var kv in dict)
            {
                if (kv.Value is not null && kv.Value.Sockets is null)
                    kv.Value.Sockets = new();
            }
            return dict;
        }
        catch
        {
            return new();
        }
    }

    public static void SetSlots(Player p, Dictionary<string, GemSlotData> data)
    {
        p.GemSlots = JsonSerializer.Serialize(data);
    }

    public static int MaxSockets(Player p, string slot)
    {
        var baseSockets = p.Realm >= 4 ? 3 : p.Realm >= 2 ? 2 : 1;
        var data = GetSlots(p);
        var extra = data.TryGetValue(slot, out var d) && d is not null ? d.Extra : 0;
        return Math.Min(3, baseSockets + extra);
    }

    public static List<string> GetSocketedItems(Player p, string slot)
    {
        var data = GetSlots(p);
        if (data.TryGetValue(slot, out var d) && d?.Sockets is not null) return d.Sockets;
        return new();
    }

    public static GemBonuses GetSocketedBonuses(Player p)
    {
        var slots = GetSlots(p);
        var baseSockets = p.Realm >= 4 ? 3 : p.Realm >= 2 ? 2 : 1;
        var atk = 0; var def = 0; var hp = 0;
        var crit = 0.0; var speed = 0.0; var qiPct = 0.0;
        var pen = 0.0; var guard = 0.0; var skillPct = 0.0;
        var rage = 0.0; var extraHit = 0.0; var shield = 0.0;
        var reflect = 0.0; var regen = 0.0; var lightning = 0.0; var lightningPct = 0.0;

        foreach (var slot in EquipSlots)
        {
            if (!slots.TryGetValue(slot, out var slotData) || slotData is null || slotData.Sockets is null) continue;
            var maxSockets = Math.Min(3, baseSockets + slotData.Extra);
            for (var i = 0; i < Math.Min(slotData.Sockets.Count, maxSockets); i++)
            {
                var id = slotData.Sockets[i];
                if (string.IsNullOrEmpty(id) || !ById.TryGetValue(id, out var it)) continue;

                atk += it.Atk;
                def += it.Def;
                hp += it.Hp;
                crit += it.CritPct;
                speed += it.SpeedPct;

                if (it.Effects is not null)
                {
                    foreach (var fx in it.Effects)
                    {
                        switch (fx.Id)
                        {
                            case "linh_ap": skillPct += fx.Value; break;
                            case "ho_the": guard += fx.Value; break;
                            case "thien_loi":
                                lightning += fx.Value;
                                lightningPct = Math.Max(lightningPct, fx.Value2);
                                break;
                            case "pha_giap": pen += fx.Value; break;
                            case "lien_kich": extraHit += fx.Value; break;
                            case "tho_thuan": shield += fx.Value; break;
                            case "dia_mach": reflect += fx.Value; break;
                            case "hoi_khi": regen += fx.Value; break;
                            case "hap_linh": qiPct += fx.Value; break;
                            case "tinh_tam": rage += fx.Value; break;
                        }
                    }
                }
            }
        }

        return new GemBonuses(
            atk, def, hp, crit, speed, qiPct, pen, guard, skillPct,
            rage, extraHit, shield, reflect, regen, lightning, lightningPct);
    }

    // =========================================================================
    // CƠ CHẾ RƠI ĐỒ BẢN ĐỒ (MAP DROP MECHANICS)
    // =========================================================================

    public static StageLootGroup GetStageStoneGroup(int chapter, int diff, int area)
    {
        // Ải thường: ~38% rơi Đá
        var chance = 0.38;
        var choices = new List<StageLootChoice>();

        if (diff == 0)
        {
            // Mức Thường: 5 loại đá sơ cấp, chia đều tỉ lệ
            choices.Add(new("stone_viem_so", 1, 1, 0.20));
            choices.Add(new("stone_bang_so", 1, 1, 0.20));
            choices.Add(new("stone_loi_so", 1, 1, 0.20));
            choices.Add(new("stone_kim_so", 1, 1, 0.20));
            choices.Add(new("stone_tho_so", 1, 1, 0.20));
        }
        else
        {
            // Mức Khó & Ác mộng: 80% đá sơ cấp, 20% đá cao cấp
            choices.Add(new("stone_viem_so", 1, 1 + diff, 0.16));
            choices.Add(new("stone_bang_so", 1, 1 + diff, 0.16));
            choices.Add(new("stone_loi_so", 1, 1 + diff, 0.16));
            choices.Add(new("stone_kim_so", 1, 1 + diff, 0.16));
            choices.Add(new("stone_tho_so", 1, 1 + diff, 0.16));

            choices.Add(new("stone_viem_cao", 1, 1, 0.04));
            choices.Add(new("stone_bang_cao", 1, 1, 0.04));
            choices.Add(new("stone_loi_cao", 1, 1, 0.04));
            choices.Add(new("stone_kim_cao", 1, 1, 0.04));
            choices.Add(new("stone_tho_cao", 1, 1, 0.04));
        }

        return new StageLootGroup(chance, choices.ToArray());
    }

    public static StageLootGroup GetStageGemGroup(int chapter, int diff, int area)
    {
        // Ải thường: ~12% rơi Ngọc (trong đó 90% là ngọc cùi, 10% ngọc xịn)
        var chance = 0.12;
        var cuiWeight = 0.90 / 5.0; // 0.18 each
        var xinWeight = 0.10 / 5.0; // 0.02 each

        var choices = new StageLootChoice[]
        {
            new("gem_ruby_cui", 1, 1, cuiWeight),
            new("gem_sapphire_cui", 1, 1, cuiWeight),
            new("gem_topaz_cui", 1, 1, cuiWeight),
            new("gem_emerald_cui", 1, 1, cuiWeight),
            new("gem_amethyst_cui", 1, 1, cuiWeight),

            new("gem_ruby_xin", 1, 1, xinWeight),
            new("gem_sapphire_xin", 1, 1, xinWeight),
            new("gem_topaz_xin", 1, 1, xinWeight),
            new("gem_emerald_xin", 1, 1, xinWeight),
            new("gem_amethyst_xin", 1, 1, xinWeight),
        };

        return new StageLootGroup(chance, choices);
    }

    public static (string Gem, string Stone) BossDrops(int chapter, int diff)
    {
        // Ải Boss 16: 100% rơi 1 Ngọc Xịn và Đá Cao Cấp theo từng chương
        var theme = Math.Max(0, chapter) % 5;
        return theme switch
        {
            0 => ("gem_ruby_xin", "stone_viem_cao"),
            1 => ("gem_sapphire_xin", "stone_bang_cao"),
            2 => ("gem_topaz_xin", "stone_tho_cao"),
            3 => ("gem_amethyst_xin", "stone_loi_cao"),
            _ => ("gem_emerald_xin", "stone_kim_cao")
        };
    }

    // =========================================================================
    // LÒ HỢP NHẤT & NÂNG CẤP (HORADRIC CUBE)
    // =========================================================================

    public static readonly IReadOnlyList<GemRecipeDef> Recipes = new GemRecipeDef[]
    {
        // ---- 1. CỔ PHÙ + CỔ PHÙ: Ghép thăng cấp (3 sơ -> 1 cao) (Tỉ lệ: 85%) ----
        new("fuse_stone_viem", "Luyện Viêm Cổ Phù · Cao", "stone", "stone_viem_cao", 1,
            new() { ["stone_viem_so"] = 3 }, 200, "3 Viêm Cổ Phù · Sơ thăng hoa thành 1 Viêm Cổ Phù · Cao.", 0.85),
        new("fuse_stone_bang", "Luyện Băng Cổ Phù · Cao", "stone", "stone_bang_cao", 1,
            new() { ["stone_bang_so"] = 3 }, 200, "3 Băng Cổ Phù · Sơ thăng hoa thành 1 Băng Cổ Phù · Cao.", 0.85),
        new("fuse_stone_loi", "Luyện Lôi Cổ Phù · Cao", "stone", "stone_loi_cao", 1,
            new() { ["stone_loi_so"] = 3 }, 200, "3 Lôi Cổ Phù · Sơ thăng hoa thành 1 Lôi Cổ Phù · Cao.", 0.85),
        new("fuse_stone_kim", "Luyện Kim Cổ Phù · Cao", "stone", "stone_kim_cao", 1,
            new() { ["stone_kim_so"] = 3 }, 200, "3 Kim Cổ Phù · Sơ thăng hoa thành 1 Kim Cổ Phù · Cao.", 0.85),
        new("fuse_stone_tho", "Luyện Thổ Cổ Phù · Cao", "stone", "stone_tho_cao", 1,
            new() { ["stone_tho_so"] = 3 }, 200, "3 Thổ Cổ Phù · Sơ thăng hoa thành 1 Thổ Cổ Phù · Cao.", 0.85),

        // ---- 2. CỔ PHÙ + CỔ PHÙ: Biến dị nhị hợp (Tỉ lệ: 75%) ----
        new("fuse_stone_liet_loi", "Hợp Liệt Lôi Cổ Phù", "stone", "stone_liet_loi", 1,
            new() { ["stone_viem_cao"] = 1, ["stone_loi_cao"] = 1 }, 500, "Viêm Cổ Phù Cao + Lôi Cổ Phù Cao biến dị sinh ra Liệt Lôi Cổ Phù (vừa đốt cháy vừa giáng sấm).", 0.75),
        new("fuse_stone_huyen_bang", "Hợp Huyền Băng Cổ Phù", "stone", "stone_huyen_bang", 1,
            new() { ["stone_bang_cao"] = 1, ["stone_tho_cao"] = 1 }, 500, "Băng Cổ Phù Cao + Thổ Cổ Phù Cao biến dị sinh ra Huyền Băng Cổ Phù (tăng thủ và tạo khiên).", 0.75),
        new("fuse_stone_am_duong", "Hợp Âm Dương Cổ Phù", "stone", "stone_am_duong", 1,
            new() { ["stone_viem_cao"] = 1, ["stone_bang_cao"] = 1 }, 500, "Viêm Cổ Phù Cao + Băng Cổ Phù Cao tương dung sinh ra Âm Dương Cổ Phù (phản đòn và hồi máu).", 0.75),

        // ---- 3. ĐỤC LỖ (Khai Khiếu Cổ Phù - Tỉ lệ: 100%) ----
        new("fuse_stone_socket", "Luyện Khai Khiếu Cổ Phù", "stone", "stone_socket", 1,
            new() { ["stone_viem_so"] = 1, ["stone_bang_so"] = 1, ["stone_loi_so"] = 1, ["stone_kim_so"] = 1, ["stone_tho_so"] = 1 }, 1000,
            "Tụ hợp ngũ hành cổ phù tinh hoa rèn thành Khai Khiếu Cổ Phù mở thêm lỗ khảm.", 1.0),

        // ---- 4. TIÊN CHÂU + TIÊN CHÂU: Ghép thuần (3 cùi -> 1 xịn) (Tỉ lệ: 75%, 400 linh thạch) ----
        new("fuse_gem_ruby", "Thăng cấp Xích Diễm Tiên Châu · Hoàn Mỹ", "gem", "gem_ruby_xin", 1,
            new() { ["gem_ruby_cui"] = 3 }, 400, "3 Xích Diễm Tiên Châu · Thô dung hợp thành 1 Xích Diễm Tiên Châu · Hoàn Mỹ (+320 Công).", 0.75),
        new("fuse_gem_sapphire", "Thăng cấp Huyền Băng Tiên Châu · Hoàn Mỹ", "gem", "gem_sapphire_xin", 1,
            new() { ["gem_sapphire_cui"] = 3 }, 400, "3 Huyền Băng Tiên Châu · Thô dung hợp thành 1 Huyền Băng Tiên Châu · Hoàn Mỹ (+260 Thủ).", 0.75),
        new("fuse_gem_topaz", "Thăng cấp Thái Dương Tiên Châu · Hoàn Mỹ", "gem", "gem_topaz_xin", 1,
            new() { ["gem_topaz_cui"] = 3 }, 400, "3 Thái Dương Tiên Châu · Thô dung hợp thành 1 Thái Dương Tiên Châu · Hoàn Mỹ (+2.200 Máu).", 0.75),
        new("fuse_gem_emerald", "Thăng cấp Thanh Mộc Tiên Châu · Hoàn Mỹ", "gem", "gem_emerald_xin", 1,
            new() { ["gem_emerald_cui"] = 3 }, 400, "3 Thanh Mộc Tiên Châu · Thô dung hợp thành 1 Thanh Mộc Tiên Châu · Hoàn Mỹ (+12% Tu luyện, +10 Thân pháp).", 0.75),
        new("fuse_gem_amethyst", "Thăng cấp Tử Lôi Tiên Châu · Hoàn Mỹ", "gem", "gem_amethyst_xin", 1,
            new() { ["gem_amethyst_cui"] = 3 }, 400, "3 Tử Lôi Tiên Châu · Thô dung hợp thành 1 Tử Lôi Tiên Châu · Hoàn Mỹ (+12% Bạo kích, +35 Nộ).", 0.75),

        // ---- 5. CỔ PHÙ + TIÊN CHÂU: Thăng hoa Tiên Châu (1 thô + 3 cổ phù -> 1 hoàn mỹ) (Tỉ lệ: 75%) ----
        new("ascend_ruby", "Thăng hoa Xích Diễm Tiên Châu (dùng Cổ Phù)", "transcend", "gem_ruby_xin", 1,
            new() { ["gem_ruby_cui"] = 1, ["stone_viem_so"] = 3 }, 1000, "Dùng chân hỏa của 3 Viêm Cổ Phù thăng hoa 1 Xích Diễm Tiên Châu · Thô thành Xích Diễm Tiên Châu · Hoàn Mỹ.", 0.75),
        new("ascend_sapphire", "Thăng hoa Huyền Băng Tiên Châu (dùng Cổ Phù)", "transcend", "gem_sapphire_xin", 1,
            new() { ["gem_sapphire_cui"] = 1, ["stone_bang_so"] = 3 }, 1000, "Dùng hàn khí của 3 Băng Cổ Phù thăng hoa 1 Huyền Băng Tiên Châu · Thô thành Huyền Băng Tiên Châu · Hoàn Mỹ.", 0.75),
        new("ascend_topaz", "Thăng hoa Thái Dương Tiên Châu (dùng Cổ Phù)", "transcend", "gem_topaz_xin", 1,
            new() { ["gem_topaz_cui"] = 1, ["stone_tho_so"] = 3 }, 1000, "Dùng đại địa của 3 Thổ Cổ Phù thăng hoa 1 Thái Dương Tiên Châu · Thô thành Thái Dương Tiên Châu · Hoàn Mỹ.", 0.75),
        new("ascend_emerald", "Thăng hoa Thanh Mộc Tiên Châu (dùng Cổ Phù)", "transcend", "gem_emerald_xin", 1,
            new() { ["gem_emerald_cui"] = 1, ["stone_kim_so"] = 3 }, 1000, "Dùng Canh Kim của 3 Kim Cổ Phù thăng hoa 1 Thanh Mộc Tiên Châu · Thô thành Thanh Mộc Tiên Châu · Hoàn Mỹ.", 0.75),
        new("ascend_amethyst", "Thăng hoa Tử Lôi Tiên Châu (dùng Cổ Phù)", "transcend", "gem_amethyst_xin", 1,
            new() { ["gem_amethyst_cui"] = 1, ["stone_loi_so"] = 3 }, 1000, "Dùng thiên lôi của 3 Lôi Cổ Phù thăng hoa 1 Tử Lôi Tiên Châu · Thô thành Tử Lôi Tiên Châu · Hoàn Mỹ.", 0.75),

        // ---- 6. CỔ PHÙ TIÊN CHÂU (1 Tiên Châu Hoàn Mỹ + 1 Cổ PhÙ Biến Dị -> Cổ Phù Tiên Châu) (Tỉ lệ: 65%) ----
        new("ascend_thien_phu", "Luyện Cổ Phù Tiên Châu", "transcend", "gem_thien_phu", 1,
            new() { ["gem_ruby_xin"] = 1, ["stone_liet_loi"] = 1 }, 5000, "Dung hợp đỉnh cao giữa Tiên Châu và Cổ Phù tạo ra Cổ Phù Tiên Châu chí tôn.", 0.65),

        // ---- 7. LUYỆN TIÊN CHÂU TỪ LINH THẢO (Tỉ lệ: 85%) ----
        new("craft_gem_ruby", "Luyện Xích Diễm Tiên Châu · Thô", "herb", "gem_ruby_cui", 1,
            new() { ["huyet_can_thao"] = 10 }, 500, "Ngưng luyện 10 Huyết Căn Thảo thành 1 Xích Diễm Tiên Châu · Thô.", 0.85),
        new("craft_gem_sapphire", "Luyện Huyền Băng Tiên Châu · Thô", "herb", "gem_sapphire_cui", 1,
            new() { ["tu_linh_thao"] = 10 }, 500, "Ngưng luyện 10 Tụ Linh Thảo thành 1 Huyền Băng Tiên Châu · Thô.", 0.85),
        new("craft_gem_topaz", "Luyện Thái Dương Tiên Châu · Thô", "herb", "gem_topaz_cui", 1,
            new() { ["thien_nien_sam"] = 10 }, 500, "Ngưng luyện 10 Thiên Niên Nhân Sâm thành 1 Thái Dương Tiên Châu · Thô.", 0.85),
        new("craft_gem_emerald", "Luyện Thanh Mộc Tiên Châu · Thô", "herb", "gem_emerald_cui", 1,
            new() { ["bich_ngoc_linh_thao"] = 10 }, 500, "Ngưng luyện 10 Bích Ngọc Linh Thảo thành 1 Thanh Mộc Tiên Châu · Thô.", 0.85),
        new("craft_gem_amethyst", "Luyện Tử Lôi Tiên Châu · Thô", "herb", "gem_amethyst_cui", 1,
            new() { ["tu_van_sam"] = 10 }, 500, "Ngưng luyện 10 Tử Vân Sâm thành 1 Tử Lôi Tiên Châu · Thô.", 0.85)
    };

    public static readonly IReadOnlyDictionary<string, GemRecipeDef> RecipeById = Recipes.ToDictionary(r => r.Id);

    // =========================================================================
    // ĐÁNH GIÁ DUNG HỢP (HORADRIC CUBE DYNAMIC EVALUATION)
    // =========================================================================

    public static FusionEvaluation EvaluateFusion(Dictionary<string, int>? inputs)
    {
        var activeInputs = (inputs ?? new())
            .Where(kv => kv.Value > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var totalSlots = activeInputs.Sum(kv => SlotsOf(kv.Key, kv.Value));

        if (totalSlots == 0)
        {
            return new FusionEvaluation(
                false,
                null,
                "Lò Hợp Đang Trống",
                "Chọn nguyên liệu từ Kho bên trái hoặc bấm Gợi ý công thức",
                null,
                new(),
                0,
                0,
                0,
                new(),
                "Chọn nguyên liệu từ Kho bên trái để bắt đầu"
            );
        }

        // 1. Kiểm tra khớp chính xác bất kỳ công thức cố định nào (Recipes)
        foreach (var r in Recipes)
        {
            if (r.Inputs.Count == activeInputs.Count &&
                r.Inputs.All(kv => activeInputs.TryGetValue(kv.Key, out var c) && c == kv.Value))
            {
                return new FusionEvaluation(
                    true,
                    r.Id,
                    r.Name,
                    r.Desc,
                    r.ResultItem,
                    new() { r.ResultItem },
                    r.ResultCount,
                    r.Stones,
                    r.Chance,
                    new(r.Inputs),
                    null
                );
            }
        }

        if (totalSlots > 5)
        {
            return new FusionEvaluation(
                false,
                null,
                "Lò Đã Quá Tải",
                "Lò chỉ chứa tối đa 5 ô nguyên liệu.",
                null,
                new(),
                0,
                0,
                0,
                new(activeInputs),
                "Lò chỉ chứa tối đa 5 ô nguyên liệu."
            );
        }

        // 2. Dung hợp Linh Thảo thuần (1 đến 5 ô, cùng loại hoặc khác loại)
        if (activeInputs.Keys.All(IsHerb) && totalSlots >= 1 && totalSlots <= 5)
        {
            var cuiPool = new List<string>();
            foreach (var (k, v) in activeInputs)
            {
                var s = SlotsOf(k, v);
                var cui = HerbToGemCui(k);
                for (int i = 0; i < s; i++) cuiPool.Add(cui);
            }
            var xinPool = cuiPool.Select(cui => CuiToXinMap[cui]).ToList();

            if (totalSlots == 1)
            {
                return new FusionEvaluation(
                    true,
                    "herb_cui_1",
                    "Ngưng Luyện Tiên Châu · Thô",
                    "Ngưng luyện thảo dược thành Tiên Châu Thô.",
                    cuiPool[0],
                    new() { cuiPool[0] },
                    1,
                    500,
                    0.85,
                    new(activeInputs),
                    null
                );
            }
            if (totalSlots == 2)
            {
                var distinct = cuiPool.Distinct().ToList();
                return new FusionEvaluation(
                    true,
                    "herb_cui_2",
                    "Song Thảo Ngưng Châu",
                    "Ngưng luyện 2 phần linh thảo thành Tiên Châu Thô với tỉ lệ cao.",
                    distinct.Count == 1 ? distinct[0] : null,
                    cuiPool,
                    1,
                    600,
                    0.90,
                    new(activeInputs),
                    null
                );
            }
            if (totalSlots == 3)
            {
                var distinct = cuiPool.Distinct().ToList();
                return new FusionEvaluation(
                    true,
                    "herb_cui_3",
                    "Tam Thảo Tụ Linh (×2 Tiên Châu Thô)",
                    "Tam thảo tụ linh, dung hợp 3 phần linh thảo bội thu nhận liền 2 Tiên Châu Thô!",
                    distinct.Count == 1 ? distinct[0] : null,
                    cuiPool,
                    2,
                    750,
                    0.85,
                    new(activeInputs),
                    null
                );
            }
            if (totalSlots == 4)
            {
                var distinct = xinPool.Distinct().ToList();
                return new FusionEvaluation(
                    true,
                    "herb_xin_4",
                    "Tứ Thảo Thăng Hoa · Tiên Châu Hoàn Mỹ",
                    "Tứ thảo thăng hoa linh khí, đột phá dược tính rèn thành Tiên Châu Hoàn Mỹ!",
                    distinct.Count == 1 ? distinct[0] : null,
                    xinPool,
                    1,
                    900,
                    0.75,
                    new(activeInputs),
                    null
                );
            }
            if (totalSlots == 5)
            {
                var distinct = xinPool.Distinct().ToList();
                return new FusionEvaluation(
                    true,
                    "herb_xin_5",
                    "Ngũ Thảo Đại Đạo · Tiên Châu Hoàn Mỹ",
                    "Đại dung hợp 5 phần linh thảo thăng hoa đạo vận thành Tiên Châu Hoàn Mỹ!",
                    distinct.Count == 1 ? distinct[0] : null,
                    xinPool,
                    1,
                    1000,
                    0.85,
                    new(activeInputs),
                    null
                );
            }
        }

        // 3. Dung hợp Cổ Phù Sơ thuần (2 đến 5 ô, cùng loại hoặc khác hệ)
        if (activeInputs.Keys.All(k => SoStones.Contains(k)) && totalSlots >= 2 && totalSlots <= 5)
        {
            var pool = new List<string>();
            foreach (var (k, v) in activeInputs)
            {
                var s = SlotsOf(k, v);
                var cao = SoToCaoMap[k];
                for (int i = 0; i < s; i++) pool.Add(cao);
            }
            var distinct = pool.Distinct().ToList();

            return totalSlots switch
            {
                2 => new FusionEvaluation(
                    true,
                    "free_so_stones",
                    "Dung Hợp Cổ Phù · Cao (2 Ô)",
                    "Dung hợp 2 Cổ Phù Sơ cấp thành 1 Cổ Phù Cao cấp.",
                    distinct.Count == 1 ? distinct[0] : null,
                    pool,
                    1,
                    150,
                    0.60,
                    new(activeInputs),
                    null
                ),
                3 => new FusionEvaluation(
                    true,
                    "free_so_stones",
                    "Dung Hợp Cổ Phù · Cao",
                    "Dung hợp 3 Cổ Phù Sơ cấp khác hệ thành 1 Cổ Phù Cao cấp.",
                    distinct.Count == 1 ? distinct[0] : null,
                    pool,
                    1,
                    250,
                    0.75,
                    new(activeInputs),
                    null
                ),
                4 => new FusionEvaluation(
                    true,
                    "free_so_stones",
                    "Tứ Tượng Cổ Phù · Cao",
                    "Dung hợp 4 Cổ Phù Sơ cấp thành 1 Cổ Phù Cao cấp với tỉ lệ cao.",
                    distinct.Count == 1 ? distinct[0] : null,
                    pool,
                    1,
                    300,
                    0.90,
                    new(activeInputs),
                    null
                ),
                _ => new FusionEvaluation(
                    true,
                    "free_so_stones",
                    "Ngũ Tụ Cổ Phù · Cao (×2)",
                    "Đại dung hợp 5 Cổ Phù Sơ thành 2 Cổ Phù Cao cấp!",
                    distinct.Count == 1 ? distinct[0] : null,
                    pool,
                    2,
                    400,
                    0.90,
                    new(activeInputs),
                    null
                )
            };
        }

        // 4. Dung hợp Cổ Phù Cao thuần (2 đến 5 ô, cùng loại hoặc khác hệ)
        if (activeInputs.Keys.All(k => CaoStones.Contains(k)) && totalSlots >= 2 && totalSlots <= 5)
        {
            var divinePool = new List<string> { "stone_liet_loi", "stone_huyen_bang", "stone_am_duong" };

            return totalSlots switch
            {
                2 => new FusionEvaluation(
                    true,
                    "free_cao_stones",
                    "Biến Dị Cổ Phù Cao Cấp",
                    "Dung hợp 2 Cổ Phù Cao cấp thành 1 Cổ Phù Biến Dị (Liệt Lôi, Huyền Băng hoặc Âm Dương).",
                    null,
                    divinePool,
                    1,
                    600,
                    0.70,
                    new(activeInputs),
                    null
                ),
                3 => new FusionEvaluation(
                    true,
                    "free_cao_stones",
                    "Tam Tài Biến Dị Cổ Phù",
                    "Dung hợp 3 Cổ Phù Cao cấp thăng hoa thành 1 Cổ Phù Biến Dị.",
                    null,
                    divinePool,
                    1,
                    750,
                    0.82,
                    new(activeInputs),
                    null
                ),
                4 => new FusionEvaluation(
                    true,
                    "free_cao_stones",
                    "Tứ Tượng Biến Dị Cổ Phù",
                    "Dung hợp 4 Cổ Phù Cao cấp thăng hoa thành 1 Cổ Phù Biến Dị với tỉ lệ cao.",
                    null,
                    divinePool,
                    1,
                    900,
                    0.90,
                    new(activeInputs),
                    null
                ),
                _ => new FusionEvaluation(
                    true,
                    "free_cao_to_thien_phu",
                    "Ngũ Khí Triều Nguyên · Cổ Phù Tiên Châu",
                    "Dung hợp 5 Cổ Phù Cao cấp thăng hoa thẳng lên Cổ Phù Tiên Châu phẩm Đỏ!",
                    "gem_thien_phu",
                    new() { "gem_thien_phu" },
                    1,
                    2000,
                    0.80,
                    new(activeInputs),
                    null
                )
            };
        }

        // 5. Dung hợp Tiên Châu Thô thuần (2 đến 5 ô, cùng loại hoặc khác loại)
        if (activeInputs.Keys.All(k => CuiGems.Contains(k)) && totalSlots >= 2 && totalSlots <= 5)
        {
            var pool = new List<string>();
            foreach (var (k, v) in activeInputs)
            {
                var s = SlotsOf(k, v);
                var xin = CuiToXinMap[k];
                for (int i = 0; i < s; i++) pool.Add(xin);
            }
            var distinct = pool.Distinct().ToList();

            return totalSlots switch
            {
                2 => new FusionEvaluation(
                    true,
                    "free_cui_gems",
                    "Dung Hợp Tiên Châu · Hoàn Mỹ (2 Ô)",
                    "Dung hợp 2 Tiên Châu Thô thăng hoa lên Tiên Châu Hoàn Mỹ.",
                    distinct.Count == 1 ? distinct[0] : null,
                    pool,
                    1,
                    300,
                    0.55,
                    new(activeInputs),
                    null
                ),
                3 => new FusionEvaluation(
                    true,
                    "free_cui_gems",
                    "Dung Hợp Tiên Châu · Hoàn Mỹ",
                    "Dung hợp 3 Tiên Châu Thô thành 1 Tiên Châu Hoàn Mỹ.",
                    distinct.Count == 1 ? distinct[0] : null,
                    pool,
                    1,
                    450,
                    0.68,
                    new(activeInputs),
                    null
                ),
                4 => new FusionEvaluation(
                    true,
                    "free_cui_gems",
                    "Tứ Tượng Tiên Châu · Hoàn Mỹ",
                    "Dung hợp 4 Tiên Châu Thô thăng hoa lên Tiên Châu Hoàn Mỹ với tỉ lệ cao.",
                    distinct.Count == 1 ? distinct[0] : null,
                    pool,
                    1,
                    500,
                    0.88,
                    new(activeInputs),
                    null
                ),
                _ => new FusionEvaluation(
                    true,
                    "free_cui_gems",
                    "Ngũ Châu Hợp Nhất · Hoàn Mỹ (×2)",
                    "Đại dung hợp 5 Tiên Châu Thô nhận liền 2 Tiên Châu Hoàn Mỹ!",
                    distinct.Count == 1 ? distinct[0] : null,
                    pool,
                    2,
                    650,
                    0.90,
                    new(activeInputs),
                    null
                )
            };
        }

        // 6. Dung hợp Cao Giai thuần (XinGems, DivineStones, stone_socket, gem_thien_phu - 2 đến 5 ô)
        if (activeInputs.Keys.All(k => HighTierItems.Contains(k)) && totalSlots >= 2 && totalSlots <= 5)
        {
            return totalSlots switch
            {
                2 => new FusionEvaluation(
                    true,
                    "free_high_tier",
                    "Song Bảo Quy Nhất · Cổ Phù Tiên Châu",
                    "Dung hợp 2 bảo vật thượng thừa thăng hoa thành Cổ Phù Tiên Châu phẩm Đỏ.",
                    "gem_thien_phu",
                    new() { "gem_thien_phu" },
                    1,
                    3000,
                    0.68,
                    new(activeInputs),
                    null
                ),
                3 => new FusionEvaluation(
                    true,
                    "free_high_tier",
                    "Tam Hoa Tụ Đỉnh · Cổ Phù Tiên Châu",
                    "Tam hoa tụ đỉnh, 3 bảo vật kết tinh thành Cổ Phù Tiên Châu phẩm Đỏ.",
                    "gem_thien_phu",
                    new() { "gem_thien_phu" },
                    1,
                    3500,
                    0.80,
                    new(activeInputs),
                    null
                ),
                4 => new FusionEvaluation(
                    true,
                    "free_high_tier",
                    "Tứ Linh Quy Nhất · Cổ Phù Tiên Châu",
                    "Tứ linh quy nhất, 4 bảo vật tạo thành Cổ Phù Tiên Châu phẩm Đỏ với tỉ lệ cao.",
                    "gem_thien_phu",
                    new() { "gem_thien_phu" },
                    1,
                    4000,
                    0.90,
                    new(activeInputs),
                    null
                ),
                _ => new FusionEvaluation(
                    true,
                    "free_high_tier",
                    "Ngũ Hành Chí Tôn · Cổ Phù Tiên Châu (×2)",
                    "Ngũ hành đại viên mãn, dung hợp 5 bảo vật thượng phẩm nhận liền 2 Cổ Phù Tiên Châu phẩm Đỏ!",
                    "gem_thien_phu",
                    new() { "gem_thien_phu" },
                    2,
                    5000,
                    0.92,
                    new(activeInputs),
                    null
                )
            };
        }

        // 7. Dung Hợp Vạn Năng Hỗn Hợp (2 đến 5 ô bất kỳ loại/phẩm nào khác nhau)
        if (totalSlots >= 2 && totalSlots <= 5)
        {
            int totalScore = activeInputs.Sum(kv => GetItemPowerScore(kv.Key) * SlotsOf(kv.Key, kv.Value));
            bool hasHighTierInput = activeInputs.Keys.Any(k => XinGems.Contains(k) || DivineStones.Contains(k) || k == "gem_thien_phu" || k == "stone_socket");

            // Subcase 7a: High Tier / High Score -> Cổ Phù Tiên Châu
            if (totalScore >= 14 || (hasHighTierInput && totalScore >= 11))
            {
                var chance = Math.Round(Math.Clamp(0.55 + totalSlots * 0.06, 0.65, 0.90), 2);
                return new FusionEvaluation(
                    true,
                    "mixed_to_thien_phu",
                    "Hỗn Nguyên Hợp Nhất · Cổ Phù Tiên Châu",
                    "Dung hợp đa bảo vật chấn động linh mạch, rèn thành Cổ Phù Tiên Châu phẩm Đỏ.",
                    "gem_thien_phu",
                    new() { "gem_thien_phu" },
                    1,
                    2500,
                    chance,
                    new(activeInputs),
                    null
                );
            }

            // Subcase 7b: Mid/High tier mix (totalScore >= 6)
            if (totalScore >= 6)
            {
                var pool = new List<string>();
                foreach (var (k, v) in activeInputs)
                {
                    int slots = SlotsOf(k, v);
                    if (CuiGems.Contains(k))
                    {
                        var xin = CuiToXinMap[k];
                        for (int i = 0; i < slots; i++) pool.Add(xin);
                    }
                    else if (CaoStones.Contains(k))
                    {
                        for (int i = 0; i < slots; i++)
                        {
                            pool.AddRange(new[] { "stone_liet_loi", "stone_huyen_bang", "stone_am_duong" });
                        }
                    }
                    else if (XinGems.Contains(k) || DivineStones.Contains(k))
                    {
                        for (int i = 0; i < slots; i++) pool.Add(k);
                    }
                }
                foreach (var (k, v) in activeInputs)
                {
                    int slots = SlotsOf(k, v);
                    if (SoStones.Contains(k))
                    {
                        if (!pool.Any(x => XinGems.Contains(x) || DivineStones.Contains(x)))
                        {
                            var cao = SoToCaoMap[k];
                            for (int i = 0; i < slots; i++) pool.Add(cao);
                        }
                    }
                    else if (IsHerb(k))
                    {
                        var cui = HerbToGemCui(k);
                        if (totalScore >= 8)
                        {
                            var xin = CuiToXinMap[cui];
                            for (int i = 0; i < slots; i++) pool.Add(xin);
                        }
                        else if (!pool.Any(x => XinGems.Contains(x) || DivineStones.Contains(x) || CaoStones.Contains(x)))
                        {
                            for (int i = 0; i < slots; i++) pool.Add(cui);
                        }
                    }
                }
                if (pool.Count == 0)
                {
                    pool.AddRange(XinGems);
                }
                var distinct = pool.Distinct().ToList();
                var chance = Math.Round(Math.Clamp(0.60 + totalSlots * 0.06, 0.68, 0.90), 2);
                var cost = 600 + totalSlots * 100;
                return new FusionEvaluation(
                    true,
                    "mixed_high",
                    "Hỗn Hợp Thăng Hoa Bảo Vật",
                    "Kết hợp linh tính của các nguyên liệu khác nhau để thăng hoa lên phẩm bậc cao hơn.",
                    distinct.Count == 1 ? distinct[0] : null,
                    pool,
                    1,
                    cost,
                    chance,
                    new(activeInputs),
                    null
                );
            }

            // Subcase 7c: Low tier mix (totalScore < 6)
            {
                var pool = new List<string>();
                foreach (var (k, v) in activeInputs)
                {
                    int slots = SlotsOf(k, v);
                    if (SoStones.Contains(k))
                    {
                        var cao = SoToCaoMap[k];
                        for (int i = 0; i < slots; i++) pool.Add(cao);
                    }
                    else if (IsHerb(k))
                    {
                        var cui = HerbToGemCui(k);
                        for (int i = 0; i < slots; i++) pool.Add(cui);
                    }
                    else if (CuiGems.Contains(k))
                    {
                        for (int i = 0; i < slots; i++) pool.Add(k);
                    }
                }
                if (pool.Count == 0)
                {
                    pool.AddRange(SoToCaoMap.Values);
                }
                var distinct = pool.Distinct().ToList();
                var chance = Math.Round(0.65 + totalSlots * 0.05, 2);
                return new FusionEvaluation(
                    true,
                    "mixed_low",
                    "Dung Hợp Linh Vật Sơ Cấp",
                    "Dung hợp các nguyên liệu khác loại thành Cổ Phù Cao hoặc Tiên Châu.",
                    distinct.Count == 1 ? distinct[0] : null,
                    pool,
                    1,
                    350,
                    chance,
                    new(activeInputs),
                    null
                );
            }
        }

        // Gợi ý thông minh khi chưa đủ hoặc sai tổ hợp
        string hint;
        if (totalSlots == 1)
        {
            var firstKey = activeInputs.Keys.First();
            if (SoStones.Contains(firstKey))
                hint = "Cần thêm ít nhất 1 Cổ Phù Sơ (hoặc nguyên liệu khác) để dung hợp";
            else if (CaoStones.Contains(firstKey))
                hint = "Cần thêm ít nhất 1 Cổ Phù Cao (hoặc nguyên liệu khác) để biến dị / dung hợp";
            else if (CuiGems.Contains(firstKey))
                hint = "Cần thêm ít nhất 1 Tiên Châu Thô (hoặc nguyên liệu khác) để dung hợp";
            else if (XinGems.Contains(firstKey))
                hint = "Cần thêm ít nhất 1 nguyên liệu nữa để dung hợp";
            else if (DivineStones.Contains(firstKey))
                hint = "Cần thêm ít nhất 1 nguyên liệu nữa để dung hợp";
            else
                hint = "Cần tối thiểu 2 ô nguyên liệu để dung hợp";
        }
        else
        {
            hint = "Tổ hợp nguyên liệu chưa phù hợp để hợp thành";
        }

        return new FusionEvaluation(
            false,
            null,
            "Chưa Đủ Nguyên Liệu",
            hint,
            null,
            new(),
            0,
            0,
            0,
            new(activeInputs),
            hint
        );
    }

    // =========================================================================
    // DTO BUILDER
    // =========================================================================

    public static GemStateDto BuildState(Player p)
    {
        var vault = GetVault(p);
        var slotsData = GetSlots(p);
        var slots = new Dictionary<string, List<string>>();
        var maxSockets = new Dictionary<string, int>();
        var baseSockets = p.Realm >= 4 ? 3 : p.Realm >= 2 ? 2 : 1;

        foreach (var s in EquipSlots)
        {
            var extra = slotsData.TryGetValue(s, out var d) && d is not null ? d.Extra : 0;
            var max = Math.Min(3, baseSockets + extra);
            maxSockets[s] = max;
            var list = slotsData.TryGetValue(s, out var sd) && sd?.Sockets is not null ? new List<string>(sd.Sockets) : new List<string>();
            while (list.Count < max) list.Add("");
            if (list.Count > max) list = list.Take(max).ToList();
            slots[s] = list;
        }

        var recipes = Recipes.Select(r => new GemRecipeDto(
            r.Id, r.Name, r.Category, r.ResultItem, r.ResultCount, r.Inputs, r.Stones, r.Desc, r.Chance
        )).ToList();

        return new GemStateDto(vault, slots, maxSockets, recipes);
    }
}
