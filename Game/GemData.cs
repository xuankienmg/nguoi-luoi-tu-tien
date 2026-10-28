using System.Text.Json;
using VanDao.Data;

namespace VanDao.Game;

public class GemSlotData
{
    public List<string> Sockets { get; set; } = new();
    public int Extra { get; set; }
}

public record GemBonuses(
    long Atk = 0,
    long Def = 0,
    long Hp = 0,
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
    double LightningPct = 0,
    double Accuracy = 0, double Dodge = 0, double CritDamage = 0, double Block = 0,
    double LifeSteal = 0, double MoveSpeed = 0, double AttackSpeed = 0,
    double Absorb = 0, double AbsorbHealChance = 0, double RagePerHit = 0, int ExtraAttacks = 0,
    double EliteReduction = 0, double BossReduction = 0, double CritResist = 0);

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
        // ------------------ 1. HUYỀN TINH SƠ CẤP (Phẩm Lam - uncommon) ------------------
        new(
            "stone_viem_so", "Huyền Nham Tinh · Sơ", "gem", "#ff4d4f",
            "Linh tinh chứa chân hỏa viễn cổ thô ráp, kích phát linh khí cuồn cuộn trong vũ khí phòng cụ.",
            Icon: "img/core/core_01.png", Rarity: "uncommon", Kind: "Tinh Thạch",
            Atk: 20,
            Effects: new[] { new ItemEffect("linh_ap", "Linh áp", 1, 3, 0, "+3% sát thương kỹ năng") },
            Special: "Khảm Tinh Thạch: +20 Công · +3% Sát thương kỹ năng"
        ),
        new(
            "stone_bang_so", "Xích Viêm Tinh · Sơ", "gem", "#40a9ff",
            "Linh tinh ngưng tụ hàn băng ngàn năm, tỏa hàn khí gia cố hộ thể.",
            Icon: "img/core/core_04.png", Rarity: "uncommon", Kind: "Tinh Thạch",
            Def: 20,
            Effects: new[] { new ItemEffect("ho_the", "Hộ thể", 1, 3, 0, "Giảm 3% sát thương nhận") },
            Special: "Khảm Tinh Thạch: +20 Thủ · Giảm 3% sát thương nhận"
        ),
        new(
            "stone_loi_so", "Thanh Lam Tinh · Sơ", "gem", "#9254de",
            "Linh tinh mang tàn dư của thiên lôi, ẩn chứa lôi đình cuồng bạo.",
            Icon: "img/core/core_07.png", Rarity: "uncommon", Kind: "Tinh Thạch",
            CritPct: 3,
            Effects: new[] { new ItemEffect("thien_loi", "Thiên lôi", 1, 5, 20, "Mỗi lượt 5% cơ hội giáng sét 20% công") },
            Special: "Khảm Tinh Thạch: +3% Bạo kích · 5% giáng Thiên lôi"
        ),
        new(
            "stone_kim_so", "Thanh Mộc Tinh · Sơ", "gem", "#faad14",
            "Linh tinh Canh Kim sắc bén, sắc bén vô song xuyên thấu hộ giáp.",
            Icon: "img/core/core_10.png", Rarity: "uncommon", Kind: "Tinh Thạch",
            Effects: new[] {
                new ItemEffect("pha_giap", "Phá giáp", 1, 4, 0, "Bỏ qua 4% phòng thủ quái"),
                new ItemEffect("lien_kich", "Liên kích", 1, 3, 0, "3% cơ hội đánh thêm 1 đòn")
            },
            Special: "Khảm Tinh Thạch: Bỏ qua 4% thủ quái · +3% Liên kích"
        ),
        new(
            "stone_tho_so", "Huyền Ấn Tinh · Sơ", "gem", "#a07040",
            "Linh tinh trầm tích ngàn trượng sâu, dày dặn vững chãi như núi non.",
            Icon: "img/core/core_14.png", Rarity: "uncommon", Kind: "Tinh Thạch",
            Hp: 180,
            Effects: new[] { new ItemEffect("tho_thuan", "Thổ thuẫn", 1, 4, 0, "Khiên đầu trận +4% máu") },
            Special: "Khảm Tinh Thạch: +180 Máu · Khiên đầu trận +4% máu"
        ),

        // ------------------ 2. HUYỀN TINH CAO CẤP (Phẩm Cam - mythic) ------------------
        new(
            "stone_viem_cao", "Bích Tinh Thạch · Cao", "gem", "#ff7a45",
            "Linh tinh ngưng tụ địa hỏa tinh túy, nóng rực rực rỡ, phá toái cấm chế.",
            Icon: "img/core/core_02.png", Rarity: "mythic", Kind: "Tinh Thạch",
            Atk: 60,
            Effects: new[] { new ItemEffect("linh_ap", "Linh áp", 2, 8, 0, "+8% sát thương kỹ năng") },
            Special: "Khảm Tinh Thạch: +60 Công · +8% Sát thương kỹ năng"
        ),
        new(
            "stone_bang_cao", "Hàn Băng Tinh · Cao", "gem", "#1890ff",
            "Linh tinh huyền băng vạn năm tuyệt đối, hàn khí thấu xương đóng băng vạn vật.",
            Icon: "img/core/core_05.png", Rarity: "mythic", Kind: "Tinh Thạch",
            Def: 60,
            Effects: new[] { new ItemEffect("ho_the", "Hộ thể", 2, 8, 0, "Giảm 8% sát thương nhận") },
            Special: "Khảm Tinh Thạch: +60 Thủ · Giảm 8% sát thương nhận"
        ),
        new(
            "stone_loi_cao", "Kim Dương Tinh · Cao", "gem", "#722ed1",
            "Linh tinh Cửu Thiên Huyền Lôi cô đọng, lôi quang chớp giật không ngừng.",
            Icon: "img/core/core_09.png", Rarity: "mythic", Kind: "Tinh Thạch",
            CritPct: 8,
            Effects: new[] { new ItemEffect("thien_loi", "Thiên lôi", 2, 12, 40, "Mỗi lượt 12% cơ hội giáng sét 40% công") },
            Special: "Khảm Tinh Thạch: +8% Bạo kích · 12% giáng Thiên lôi"
        ),
        new(
            "stone_kim_cao", "Tử Lôi Tinh · Cao", "gem", "#d48806",
            "Linh tinh Canh Kim thuần túy sắc bén nhất thế gian, trảm đoạn huyền cương.",
            Icon: "img/core/core_11.png", Rarity: "mythic", Kind: "Tinh Thạch",
            Effects: new[] {
                new ItemEffect("pha_giap", "Phá giáp", 2, 10, 0, "Bỏ qua 10% phòng thủ quái"),
                new ItemEffect("lien_kich", "Liên kích", 2, 7, 0, "7% cơ hội đánh thêm 1 đòn")
            },
            Special: "Khảm Tinh Thạch: Bỏ qua 10% thủ quái · +7% Liên kích"
        ),
        new(
            "stone_tho_cao", "Ngũ Hành Tinh · Cao", "gem", "#874d00",
            "Linh tinh Thái Sơn trầm tĩnh, tiếp dẫn đại địa tinh hoa, sinh cơ vô tận.",
            Icon: "img/core/core_13.png", Rarity: "mythic", Kind: "Tinh Thạch",
            Hp: 500,
            Effects: new[] { new ItemEffect("tho_thuan", "Thổ thuẫn", 2, 10, 0, "Khiên đầu trận +10% máu") },
            Special: "Khảm Tinh Thạch: +500 Máu · Khiên đầu trận +10% máu"
        ),

        // ------------------ 3. HUYỀN TINH BIẾN DỊ & KHAI KHIẾU (Phẩm Đỏ / Cam) ------------------
        new(
            "stone_liet_loi", "Huyết Hồn Tinh", "gem", "#f5222d",
            "Hỏa diệm hòa quyện Lôi đình biến dị nhị hợp, thiêu đốt sát thương và sét đánh cuồng bạo.",
            Icon: "img/core/core_12.png", Rarity: "divine", Kind: "Tinh Thạch",
            Atk: 90, CritPct: 5,
            Effects: new[] {
                new ItemEffect("thien_loi", "Thiên lôi", 3, 15, 50, "Mỗi lượt 15% cơ hội giáng sét 50% công"),
                new ItemEffect("linh_ap", "Linh áp", 3, 6, 0, "+6% sát thương kỹ năng")
            },
            Special: "Khảm Tinh Thạch: +90 Công · +5% Bạo kích · 15% giáng Thiên lôi"
        ),
        new(
            "stone_huyen_bang", "Nguyệt Hoa Tinh", "gem", "#13c2c2",
            "Băng hàn kết hợp Trọng thổ biến dị nhị hợp, băng giáp hộ thể hóa giải vạn đòn tấn công.",
            Icon: "img/core/core_08.png", Rarity: "divine", Kind: "Tinh Thạch",
            Def: 90, Hp: 600,
            Effects: new[] {
                new ItemEffect("ho_the", "Hộ thể", 3, 10, 0, "Giảm 10% sát thương nhận"),
                new ItemEffect("tho_thuan", "Thổ thuẫn", 3, 10, 0, "Khiên đầu trận +10% máu")
            },
            Special: "Khảm Tinh Thạch: +90 Thủ · +600 Máu · Giảm 10% sát thương · Khiên +10% máu"
        ),
        new(
            "stone_am_duong", "Tử Vân Tinh", "gem", "#eb2f96",
            "Chân hỏa và Cực hàn tương sinh tương khắc biến dị nhị hợp, vừa phản chấn vừa hồi phục sinh cơ.",
            Icon: "img/core/core_06.png", Rarity: "divine", Kind: "Tinh Thạch",
            Atk: 60, Def: 60,
            Effects: new[] {
                new ItemEffect("dia_mach", "Địa mạch", 3, 5, 0, "Phản đòn 5% sát thương quái"),
                new ItemEffect("hoi_khi", "Hồi khí", 3, 3, 0, "Hồi 3% máu mỗi lượt")
            },
            Special: "Khảm Tinh Thạch: +60 Công · +60 Thủ · Phản đòn 5% · Hồi 3% máu/lượt"
        ),

        // ------------------ 4. LINH TINH THÔ (Phẩm Lam - rare) ------------------
        new(
            "gem_ruby_cui", "Huyền Nham Tinh · Thô", "gem", "#ff4d4f",
            "Linh tinh sắc đỏ rực rỡ, tích tụ hỏa linh thiên địa thô sơ.",
            Icon: "img/core/core_01.png", Rarity: "rare", Kind: "Tinh Thạch",
            Atk: 100,
            Special: "Khảm Tinh Thạch: +100 Tấn công"
        ),
        new(
            "gem_sapphire_cui", "Xích Viêm Tinh · Thô", "gem", "#40a9ff",
            "Linh tinh sắc lam trong vắt, kết tinh thủy linh ôn nhuần bảo hộ.",
            Icon: "img/core/core_04.png", Rarity: "rare", Kind: "Tinh Thạch",
            Def: 80,
            Special: "Khảm Tinh Thạch: +80 Phòng thủ"
        ),
        new(
            "gem_topaz_cui", "Thanh Mộc Tinh · Thô", "gem", "#faad14",
            "Linh tinh hoàng kim rực rỡ, hấp thu thổ linh trù phú bồi dưỡng sinh mệnh.",
            Icon: "img/core/core_10.png", Rarity: "rare", Kind: "Tinh Thạch",
            Hp: 600,
            Special: "Khảm Tinh Thạch: +600 Máu tối đa"
        ),
        new(
            "gem_emerald_cui", "Địa Mạch Tinh · Thô", "gem", "#52c41a",
            "Linh tinh lục bích tinh xảo, ẩn chứa mộc linh sinh trưởng nhẹ nhàng.",
            Icon: "img/core/core_03.png", Rarity: "rare", Kind: "Tinh Thạch",
            SpeedPct: 4,
            Effects: new[] { new ItemEffect("hap_linh", "Hấp linh", 1, 4, 0, "+4% tốc độ tu luyện") },
            Special: "Khảm Tinh Thạch: +4% Tu luyện · +4 Thân pháp"
        ),
        new(
            "gem_amethyst_cui", "Thanh Lam Tinh · Thô", "gem", "#9254de",
            "Linh tinh tím huyền bí, ngưng tụ linh hồn linh cảm thiên cơ.",
            Icon: "img/core/core_07.png", Rarity: "rare", Kind: "Tinh Thạch",
            CritPct: 4,
            Effects: new[] { new ItemEffect("tinh_tam", "Tĩnh tâm", 1, 15, 0, "Vào trận với 15 nộ") },
            Special: "Khảm Tinh Thạch: +4% Bạo kích · +15 Nộ đầu trận"
        ),

        // ------------------ 5. LINH TINH HOÀN MỸ (Phẩm Cam - mythic) ------------------
        new(
            "gem_ruby_xin", "Bích Tinh Thạch · Hoàn Mỹ", "gem", "#f5222d",
            "Linh tinh xích huyết long tâm hoàn hảo không tì vết, bộc phát uy lực hủy thiên diệt địa.",
            Icon: "img/core/core_02.png", Rarity: "mythic", Kind: "Tinh Thạch",
            Atk: 320,
            Special: "Khảm Tinh Thạch: +320 Tấn công"
        ),
        new(
            "gem_sapphire_xin", "Hàn Băng Tinh · Hoàn Mỹ", "gem", "#1890ff",
            "Linh tinh cửu khúc u tuyền thuần khiết, ngưng kết hàn băng hộ giáp kiên cố bất hoại.",
            Icon: "img/core/core_05.png", Rarity: "mythic", Kind: "Tinh Thạch",
            Def: 260,
            Special: "Khảm Tinh Thạch: +260 Phòng thủ"
        ),
        new(
            "gem_topaz_xin", "Tử Lôi Tinh · Hoàn Mỹ", "gem", "#d48806",
            "Linh tinh hoàng long tích tụ vạn năm, khí huyết dồi dào tựa biển sâu.",
            Icon: "img/core/core_11.png", Rarity: "mythic", Kind: "Tinh Thạch",
            Hp: 2200,
            Special: "Khảm Tinh Thạch: +2.200 Máu tối đa"
        ),
        new(
            "gem_emerald_xin", "Tử Vân Tinh · Hoàn Mỹ", "gem", "#389e0d",
            "Linh tinh bích ba trường thanh, thông suốt kinh mạch linh khí tuần hoàn nhanh gấp bội.",
            Icon: "img/core/core_06.png", Rarity: "mythic", Kind: "Tinh Thạch",
            SpeedPct: 10,
            Effects: new[] { new ItemEffect("hap_linh", "Hấp linh", 2, 12, 0, "+12% tốc độ tu luyện") },
            Special: "Khảm Tinh Thạch: +12% Tu luyện · +10 Thân pháp"
        ),
        new(
            "gem_amethyst_xin", "Kim Dương Tinh · Hoàn Mỹ", "gem", "#722ed1",
            "Linh tinh tử vi tinh đẩu ngưng kết, dẫn phát sát chiêu bạo liệt chí tử.",
            Icon: "img/core/core_09.png", Rarity: "mythic", Kind: "Tinh Thạch",
            CritPct: 12,
            Effects: new[] { new ItemEffect("tinh_tam", "Tĩnh tâm", 2, 35, 0, "Vào trận với 35 nộ") },
            Special: "Khảm Tinh Thạch: +12% Bạo kích · +35 Nộ đầu trận"
        ),

        // ------------------ 6. HỖN NGUYÊN ĐẠO NGỌC (Phẩm Đỏ - divine) ------------------
        new(
            "gem_thien_phu", "Hỗn Nguyên Tinh", "gem", "#eb2f96",
            "Dung hợp hoàn mỹ giữa Tinh Thạch tinh túy và Đại Đạo Tinh Thạch, uy chấn bát hoang.",
            Icon: "img/core/core_15.png", Rarity: "divine", Kind: "Tinh Thạch",
            Atk: 380, Hp: 2000, CritPct: 10,
            Effects: new[] { new ItemEffect("linh_ap", "Linh áp", 3, 10, 0, "+10% sát thương kỹ năng") },
            Special: "Khảm Tinh Thạch: +380 Công · +2.000 Máu · +10% Bạo kích · +10% Sát thương kỹ năng"
        ),
    }.Select(GemProgression.RebalanceLegacy).Concat(GemProgression.CreateAll()).ToArray();

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
            return (JsonSerializer.Deserialize<Dictionary<string, int>>(p.GemVault) ?? new()).Where(kv => kv.Key != "stone_socket").ToDictionary(kv => kv.Key, kv => kv.Value);
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

    public static string SlotGemCategory(string slot) => slot is "head" or "feet" or "body" or "hands" or "legs" ? "defense" : "attack";
    public static bool CanSocket(string itemId, string slot) => EquipSlots.Contains(slot)
        && ById.TryGetValue(itemId, out var gem) && gem.GemCategory == SlotGemCategory(slot);

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

    private static string BagKey(string itemId, int copy) => $"bag|{itemId}|{copy}";
    public static GemSlotData BagCopy(Player p, string itemId, int copy) =>
        GetSlots(p).GetValueOrDefault(BagKey(itemId, copy)) ?? new();
    public static void RemoveBagCopies(Player p, string itemId, int count, int copy = 0)
    {
        if (count <= 0 || !p.GemSlots.Contains($"bag|{itemId}|")) return;
        var slots = GetSlots(p); var prefix = $"bag|{itemId}|";
        var saved = slots.Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(kv => (kv.Key, Index: int.TryParse(kv.Key[prefix.Length..], out var i) ? i : -1, kv.Value)).ToArray();
        foreach (var entry in saved) slots.Remove(entry.Key);
        foreach (var entry in saved)
        {
            if (entry.Index < 0) continue;
            if (entry.Index < copy) slots[entry.Key] = entry.Value;
            else if (entry.Index >= copy + count) slots[BagKey(itemId, entry.Index - count)] = entry.Value;
        }
        SetSlots(p, slots);
    }
    public static void StoreBagCopy(Player p, string itemId, int copy, GemSlotData data)
    {
        var slots = GetSlots(p);
        if (data.Sockets.Any(id => !string.IsNullOrEmpty(id))) slots[BagKey(itemId, copy)] = data;
        SetSlots(p, slots);
    }
    public static Dictionary<string, Dictionary<int, List<string>>> BagSocketState(Player p)
    {
        var result = new Dictionary<string, Dictionary<int, List<string>>>();
        foreach (var (key, data) in GetSlots(p))
        {
            var parts = key.Split('|');
            if (parts.Length != 3 || parts[0] != "bag" || !int.TryParse(parts[2], out var index)
                || index < 0 || index >= p.Count(parts[1]) || data is null) continue;
            if (!result.TryGetValue(parts[1], out var copies)) result[parts[1]] = copies = new();
            copies[index] = data.Sockets;
        }
        return result;
    }

    public static int MaxSockets(Player p, string slot)
    {
        var equipped = p.Equipment.FirstOrDefault(e => e.Slot == slot);
        if (equipped is null || !GameData.ItemById.TryGetValue(equipped.ItemId, out var item) || item.Slot != slot) return 0;
        var quality = Array.IndexOf(GameData.RarityOrder, item.Rarity);
        return quality < 0 ? 0 : quality < 4 ? 1 : quality < 7 ? 2 : 3;
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
        long atk = 0, def = 0, hp = 0;
        var foundation = GameData.BaseStats(GameData.Realms[p.Realm], p.Stage);
        var crit = 0.0; var speed = 0.0; var qiPct = 0.0;
        var pen = 0.0; var guard = 0.0; var skillPct = 0.0;
        var rage = 0.0; var extraHit = 0.0; var shield = 0.0;
        var reflect = 0.0; var regen = 0.0; var lightning = 0.0; var lightningPct = 0.0;
        var secondary = new Dictionary<string, double>();
        var absorbHealChance = 0d;

        foreach (var slot in EquipSlots)
        {
            if (!slots.TryGetValue(slot, out var slotData) || slotData is null || slotData.Sockets is null) continue;
            var maxSockets = MaxSockets(p, slot);
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
                        if (fx.Id.StartsWith("gem_")) secondary[fx.Id] = secondary.GetValueOrDefault(fx.Id) + fx.Value;
                        if (fx.Id == "gem_absorb") absorbHealChance = Math.Max(absorbHealChance, fx.Value2);
                        switch (fx.Id)
                        {
                            case "gem_base_atk": atk += CombatNumber.Round(foundation.Atk * fx.Value / 100); break;
                            case "gem_base_def": def += CombatNumber.Round(foundation.Def * fx.Value / 100); break;
                            case "gem_base_hp": hp += CombatNumber.Round(foundation.Hp * fx.Value / 100); break;
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
                            case "gem_regen": regen += fx.Value; break;
                            case "hap_linh": qiPct += fx.Value; break;
                            case "tinh_tam": rage += fx.Value; break;
                        }
                    }
                }
            }
        }

        double Bonus(string id, double cap) => Math.Clamp(secondary.GetValueOrDefault(id), 0, cap);
        return new GemBonuses(
            atk, def, hp, crit, speed, qiPct, pen, guard, skillPct,
            rage, Math.Min(30, extraHit), shield, reflect, Math.Min(10, regen), lightning, lightningPct,
            Bonus("gem_accuracy", double.MaxValue), Bonus("gem_dodge", double.MaxValue), Bonus("gem_crit_damage", double.MaxValue), Bonus("gem_block", 50),
            Bonus("gem_lifesteal", 10), Bonus("gem_move_speed", 60), Bonus("gem_attack_speed", 100),
            Bonus("gem_absorb", 40), Math.Clamp(absorbHealChance, 0, 50), Bonus("gem_rage_hit", 7),
            (int)Bonus("gem_extra_attack", 1), Bonus("gem_elite_reduction", 30), Bonus("gem_boss_reduction", 30), Bonus("gem_crit_resist", double.MaxValue));
    }

    // =========================================================================
    // CƠ CHẾ RƠI ĐỒ BẢN ĐỒ (MAP DROP MECHANICS)
    // =========================================================================

    public static StageLootGroup GetStageStoneGroup(int chapter, int diff, int area)
        => new(1 - (1 - .38) * (1 - .12), MapRewardPool(diff)
            .Select(g => new StageLootChoice(g.Item, 1, 1, g.Probability)).ToArray());

    // Compatibility accessor; StageGroups uses a single roll for all socketable stones.
    public static StageLootGroup GetStageGemGroup(int chapter, int diff, int area)
        => new(0, MapRewardPool(diff).Select(g => new StageLootChoice(g.Item, 1, 1, g.Probability)).ToArray());

    // =========================================================================
    // LÒ HỢP NHẤT & NÂNG CẤP (HORADRIC CUBE)
    // =========================================================================

    // Existing save IDs keep their stats and weapon-quality grades.
    public static readonly string[] GemQualities = GemProgression.Qualities;
    public static readonly double[] UpgradeChances = { .95, .90, .85, .80, .70, .60, .50, .40, .30, 0 };
    // Phí gỡ theo phẩm chất viên khảm, không theo phẩm trang bị.
    public static int UnsocketCost(string id) => Math.Max(1, GemLevel(id) + 1) * 1000;
    public static int GemLevel(string id) => ById.TryGetValue(id, out var item) && item.Type == "gem"
        ? Array.IndexOf(GemQualities, item.Rarity) : -1;
    public static List<string> GemsAtLevel(int level) => Items.Where(i => i.Type == "gem" && GemLevel(i.Id) == level).Select(i => i.Id).ToList();
    public static IReadOnlyList<(string Item, double Probability)> RewardPool(int difficulty)
    {
        var weights = Math.Clamp(difficulty, 0, 3) switch
        {
            0 => new[] { .45, .30, .18, .07, 0, 0, 0, 0, 0, 0 },
            1 => new[] { .25, .25, .20, .15, .10, .05, 0, 0, 0, 0 },
            2 => new[] { .15, .20, .20, .15, .12, .08, .06, .04, 0, 0 },
            _ => new[] { .08, .10, .12, .15, .15, .12, .10, .08, .07, .03 }
        };
        return Enumerable.Range(0, weights.Length).Where(t => weights[t] > 0)
            .SelectMany(t =>
            {
                var gems = GemsAtLevel(t);
                return gems.Select(id => (id, weights[t] / gems.Count));
            }).ToArray();
    }
    public static IReadOnlyList<(string Item, double Probability)> MapRewardPool(int difficulty)
    {
        var eligible = RewardPool(difficulty).Where(g => GemLevel(g.Item) <= 2).ToArray();
        var total = eligible.Sum(g => g.Probability);
        return eligible.Select(g => (g.Item, g.Probability / total)).ToArray();
    }
    public static double BossBonusChance(int difficulty) => Math.Min(.65, .35 + .10 * Math.Clamp(difficulty, 0, 3));
    public static DropDef[] BossBonusDrops(int difficulty) => MapRewardPool(difficulty)
        .Select(g => new DropDef(g.Item, 1, 1, BossBonusChance(difficulty) * g.Probability)).ToArray();
    public static string? RollBossBonus(int difficulty, Random random)
    {
        if (random.NextDouble() >= BossBonusChance(difficulty)) return null;
        var pool = MapRewardPool(difficulty);
        var roll = random.NextDouble();
        foreach (var gem in pool) if ((roll -= gem.Probability) < 0) return gem.Item;
        return pool[^1].Item;
    }
    public static double ChestDivineChance(string chest) => GameData.ChestBase(chest) switch
    {
        "ruong_thuong" => .001, "ruong_hiem" => .003, _ => .01
    };
    public static IReadOnlyList<(string Item, double Probability)> ChestRewardPool(string chest, int difficulty)
    {
        var divineShare = ChestDivineChance(chest) / ChestGemChance(GameData.ChestBase(chest));
        var lower = RewardPool(difficulty).Where(g => GemLevel(g.Item) < 6).ToArray();
        var total = lower.Sum(g => g.Probability);
        var divine = GemsAtLevel(6);
        return lower.Select(g => (g.Item, g.Probability / total * (1 - divineShare)))
            .Concat(divine.Select(id => (id, divineShare / divine.Count))).ToArray();
    }
    public static double ChestGemChance(string chest) => chest == GameData.BossChest ? .25 : chest == "ruong_hiem" ? .15 : .08;
    public static readonly IReadOnlyList<GemRecipeDef> Recipes = Array.Empty<GemRecipeDef>();
    public static readonly IReadOnlyDictionary<string, GemRecipeDef> RecipeById = new Dictionary<string, GemRecipeDef>();

    public static FusionEvaluation EvaluateFusion(Dictionary<string, int>? inputs)
    {
        var active = inputs ?? new();
        FusionEvaluation Invalid(string hint) => new(false, null, "Chưa thể hợp Tinh Thạch", hint, null, new(), 0, 0, 0, new(), hint);
        if (active.Count == 0 || active.Any(kv => kv.Value <= 0 || GemLevel(kv.Key) < 0))
            return Invalid("Chỉ nhận Tinh Thạch để hợp Tinh Thạch.");
        var level = GemLevel(active.Keys.First());
        if (active.Keys.Any(id => GemLevel(id) != level)) return Invalid("9 Tinh Thạch phải cùng phẩm chất, có thể khác loại.");
        if (level >= GemQualities.Length - 1) return Invalid("Tinh Thạch đã đạt phẩm chất cao nhất hiện có.");
        if (active.Values.Sum(n => (long)n) != 9) return Invalid("Cần đúng 9 Tinh Thạch cùng phẩm chất.");
        return new(true, "gem_upgrade_9", "Hợp 9 Tinh Thạch", "Thành công: 1 ngọc ngẫu nhiên phẩm kế tiếp. Thất bại: 1 ngọc ngẫu nhiên cùng phẩm.",
            null, GemsAtLevel(level + 1), 1, 0, UpgradeChances[level], new(active), null);
    }

    public static GemStateDto BuildState(Player p)
    {
        var vault = GetVault(p);
        var slotsData = GetSlots(p);
        var slots = new Dictionary<string, List<string>>();
        var maxSockets = new Dictionary<string, int>();

        foreach (var s in EquipSlots)
        {
            var max = MaxSockets(p, s);
            maxSockets[s] = max;
            var list = slotsData.TryGetValue(s, out var sd) && sd?.Sockets is not null ? new List<string>(sd.Sockets) : new List<string>();
            while (list.Count < max) list.Add("");
            if (list.Count > 3) list = list.Take(3).ToList();
            slots[s] = list;
        }

        var recipes = Recipes.Select(r => new GemRecipeDto(
            r.Id, r.Name, r.Category, r.ResultItem, r.ResultCount, r.Inputs, r.Stones, r.Desc, r.Chance
        )).ToList();

        return new GemStateDto(vault, slots, maxSockets, recipes, GetSocketedBonuses(p), Items.ToDictionary(i => i.Id, i => UnsocketCost(i.Id)), BagSocketState(p));
    }
}
