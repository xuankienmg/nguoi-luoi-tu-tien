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
    string Desc);

public static class GemData
{
    // =========================================================================
    // ĐỊNH NGHĨA VẬT PHẨM: ĐÁ (Khoáng Thạch / Phù Thạch) & NGỌC (Bảo Ngọc)
    // =========================================================================

    public static readonly IReadOnlyList<ItemDef> Items = new ItemDef[]
    {
        // ------------------ 1. ĐÁ SƠ CẤP (Phẩm Lam - uncommon) ------------------
        new(
            "stone_viem_so", "Viêm Thạch · Sơ", "stone", "#ff4d4f",
            "Phù thạch chứa chân hỏa thô ráp, kích phát linh khí cuồn cuộn trong vũ khí phòng cụ.",
            Icon: "img/core/core_01.png", Rarity: "uncommon", Kind: "Phù Thạch",
            Atk: 20,
            Effects: new[] { new ItemEffect("linh_ap", "Linh áp", 1, 3, 0, "+3% sát thương kỹ năng") },
            Special: "Khảm nạm: +20 Công · +3% Sát thương kỹ năng"
        ),
        new(
            "stone_bang_so", "Băng Thạch · Sơ", "stone", "#40a9ff",
            "Phù thạch ngưng tụ hàn băng ngàn năm, tỏa hàn khí gia cố hộ thể.",
            Icon: "img/core/core_04.png", Rarity: "uncommon", Kind: "Phù Thạch",
            Def: 20,
            Effects: new[] { new ItemEffect("ho_the", "Hộ thể", 1, 3, 0, "Giảm 3% sát thương nhận") },
            Special: "Khảm nạm: +20 Thủ · Giảm 3% sát thương nhận"
        ),
        new(
            "stone_loi_so", "Lôi Thạch · Sơ", "stone", "#9254de",
            "Phù thạch mang tàn dư của thiên lôi, ẩn chứa lôi đình cuồng bạo.",
            Icon: "img/core/core_07.png", Rarity: "uncommon", Kind: "Phù Thạch",
            CritPct: 3,
            Effects: new[] { new ItemEffect("thien_loi", "Thiên lôi", 1, 5, 20, "Mỗi lượt 5% cơ hội giáng sét 20% công") },
            Special: "Khảm nạm: +3% Bạo kích · 5% giáng Thiên lôi"
        ),
        new(
            "stone_kim_so", "Kim Thạch · Sơ", "stone", "#faad14",
            "Khoáng thạch kim tinh sắc bén, sắc bén vô song xuyên thấu hộ giáp.",
            Icon: "img/core/core_10.png", Rarity: "uncommon", Kind: "Phù Thạch",
            Effects: new[] {
                new ItemEffect("pha_giap", "Phá giáp", 1, 4, 0, "Bỏ qua 4% phòng thủ quái"),
                new ItemEffect("lien_kich", "Liên kích", 1, 3, 0, "3% cơ hội đánh thêm 1 đòn")
            },
            Special: "Khảm nạm: Bỏ qua 4% thủ quái · +3% Liên kích"
        ),
        new(
            "stone_tho_so", "Thổ Thạch · Sơ", "stone", "#a07040",
            "Khoáng thạch trầm tích ngàn trượng sâu, dày dặn vững chãi như núi non.",
            Icon: "img/core/core_14.png", Rarity: "uncommon", Kind: "Phù Thạch",
            Hp: 180,
            Effects: new[] { new ItemEffect("tho_thuan", "Thổ thuẫn", 1, 4, 0, "Khiên đầu trận +4% máu") },
            Special: "Khảm nạm: +180 Máu · Khiên đầu trận +4% máu"
        ),

        // ------------------ 2. ĐÁ CAO CẤP (Phẩm Cam - mythic) ------------------
        new(
            "stone_viem_cao", "Viêm Thạch · Cao", "stone", "#ff7a45",
            "Phù thạch ngưng tụ địa hỏa tinh túy, nóng rực rực rỡ, phá toái cấm chế.",
            Icon: "img/core/core_02.png", Rarity: "mythic", Kind: "Phù Thạch",
            Atk: 60,
            Effects: new[] { new ItemEffect("linh_ap", "Linh áp", 2, 8, 0, "+8% sát thương kỹ năng") },
            Special: "Khảm nạm: +60 Công · +8% Sát thương kỹ năng"
        ),
        new(
            "stone_bang_cao", "Băng Thạch · Cao", "stone", "#1890ff",
            "Phù thạch huyền băng vạn năm tuyệt đối, hàn khí thấu xương đóng băng vạn vật.",
            Icon: "img/core/core_05.png", Rarity: "mythic", Kind: "Phù Thạch",
            Def: 60,
            Effects: new[] { new ItemEffect("ho_the", "Hộ thể", 2, 8, 0, "Giảm 8% sát thương nhận") },
            Special: "Khảm nạm: +60 Thủ · Giảm 8% sát thương nhận"
        ),
        new(
            "stone_loi_cao", "Lôi Thạch · Cao", "stone", "#722ed1",
            "Phù thạch Cửu Thiên Huyền Lôi cô đọng, lôi quang chớp giật không ngừng.",
            Icon: "img/core/core_09.png", Rarity: "mythic", Kind: "Phù Thạch",
            CritPct: 8,
            Effects: new[] { new ItemEffect("thien_loi", "Thiên lôi", 2, 12, 40, "Mỗi lượt 12% cơ hội giáng sét 40% công") },
            Special: "Khảm nạm: +8% Bạo kích · 12% giáng Thiên lôi"
        ),
        new(
            "stone_kim_cao", "Kim Thạch · Cao", "stone", "#d48806",
            "Phù thạch Canh Kim thuần túy sắc bén nhất thế gian, trảm đoạn huyền cương.",
            Icon: "img/core/core_11.png", Rarity: "mythic", Kind: "Phù Thạch",
            Effects: new[] {
                new ItemEffect("pha_giap", "Phá giáp", 2, 10, 0, "Bỏ qua 10% phòng thủ quái"),
                new ItemEffect("lien_kich", "Liên kích", 2, 7, 0, "7% cơ hội đánh thêm 1 đòn")
            },
            Special: "Khảm nạm: Bỏ qua 10% thủ quái · +7% Liên kích"
        ),
        new(
            "stone_tho_cao", "Thổ Thạch · Cao", "stone", "#874d00",
            "Phù thạch Thái Sơn trầm tĩnh, tiếp dẫn đại địa tinh hoa, sinh cơ vô tận.",
            Icon: "img/core/core_13.png", Rarity: "mythic", Kind: "Phù Thạch",
            Hp: 500,
            Effects: new[] { new ItemEffect("tho_thuan", "Thổ thuẫn", 2, 10, 0, "Khiên đầu trận +10% máu") },
            Special: "Khảm nạm: +500 Máu · Khiên đầu trận +10% máu"
        ),

        // ------------------ 3. ĐÁ BIẾN DỊ & ĐÁ ĐỤC LỖ (Phẩm Đỏ / Cam) ------------------
        new(
            "stone_liet_loi", "Liệt Lôi Thạch", "stone", "#f5222d",
            "Hỏa diệm hòa quyện Lôi đình biến dị nhị hợp, thiêu đốt sát thương và sét đánh cuồng bạo.",
            Icon: "img/core/core_12.png", Rarity: "divine", Kind: "Dị Thạch",
            Atk: 90, CritPct: 5,
            Effects: new[] {
                new ItemEffect("thien_loi", "Thiên lôi", 3, 15, 50, "Mỗi lượt 15% cơ hội giáng sét 50% công"),
                new ItemEffect("linh_ap", "Linh áp", 3, 6, 0, "+6% sát thương kỹ năng")
            },
            Special: "Khảm nạm: +90 Công · +5% Bạo kích · 15% giáng Thiên lôi"
        ),
        new(
            "stone_huyen_bang", "Huyền Băng Hộ Thạch", "stone", "#13c2c2",
            "Băng hàn kết hợp Trọng thổ biến dị nhị hợp, băng giáp hộ thể hóa giải vạn đòn tấn công.",
            Icon: "img/core/core_08.png", Rarity: "divine", Kind: "Dị Thạch",
            Def: 90, Hp: 600,
            Effects: new[] {
                new ItemEffect("ho_the", "Hộ thể", 3, 10, 0, "Giảm 10% sát thương nhận"),
                new ItemEffect("tho_thuan", "Thổ thuẫn", 3, 10, 0, "Khiên đầu trận +10% máu")
            },
            Special: "Khảm nạm: +90 Thủ · +600 Máu · Giảm 10% sát thương · Khiên +10% máu"
        ),
        new(
            "stone_am_duong", "Âm Dương Thạch", "stone", "#eb2f96",
            "Chân hỏa và Cực hàn tương sinh tương khắc biến dị nhị hợp, vừa phản chấn vừa hồi phục sinh cơ.",
            Icon: "img/core/core_06.png", Rarity: "divine", Kind: "Dị Thạch",
            Atk: 60, Def: 60,
            Effects: new[] {
                new ItemEffect("dia_mach", "Địa mạch", 3, 5, 0, "Phản đòn 5% sát thương quái"),
                new ItemEffect("hoi_khi", "Hồi khí", 3, 3, 0, "Hồi 3% máu mỗi lượt")
            },
            Special: "Khảm nạm: +60 Công · +60 Thủ · Phản đòn 5% · Hồi 3% máu/lượt"
        ),
        new(
            "stone_socket", "Thiên Công Đục Thạch", "stone", "#ffd700",
            "Thần thạch đục mở socket, dùng để khai thông lỗ khảm trang bị thứ 2 và thứ 3.",
            Icon: "img/core/core_15.png", Rarity: "mythic", Kind: "Đục Thạch",
            Special: "Dùng để đục mở thêm 1 Lỗ Khảm cho bất kỳ ô trang bị nào (tối đa 3 lỗ)."
        ),

        // ------------------ 4. NGỌC CÙI (Phẩm Lam - rare) ------------------
        new(
            "gem_ruby_cui", "Hồng Ngọc Thô", "gem", "#ff4d4f",
            "Viên bảo ngọc sắc đỏ rực rỡ, tích tụ hỏa linh thiên địa thô sơ.",
            Icon: "img/core/core_01.png", Rarity: "rare", Kind: "Bảo Ngọc",
            Atk: 100,
            Special: "Khảm nạm: +100 Tấn công"
        ),
        new(
            "gem_sapphire_cui", "Lam Ngọc Thô", "gem", "#40a9ff",
            "Viên bảo ngọc sắc lam trong vắt, kết tinh thủy linh ôn nhuần bảo hộ.",
            Icon: "img/core/core_04.png", Rarity: "rare", Kind: "Bảo Ngọc",
            Def: 80,
            Special: "Khảm nạm: +80 Phòng thủ"
        ),
        new(
            "gem_topaz_cui", "Hoàng Ngọc Thô", "gem", "#faad14",
            "Viên bảo ngọc hoàng kim rực rỡ, hấp thu thổ linh trù phú bồi dưỡng sinh mệnh.",
            Icon: "img/core/core_10.png", Rarity: "rare", Kind: "Bảo Ngọc",
            Hp: 600,
            Special: "Khảm nạm: +600 Máu tối đa"
        ),
        new(
            "gem_emerald_cui", "Lục Ngọc Thô", "gem", "#52c41a",
            "Viên bảo ngọc lục bích tinh xảo, ẩn chứa mộc linh sinh trưởng nhẹ nhàng.",
            Icon: "img/core/core_03.png", Rarity: "rare", Kind: "Bảo Ngọc",
            SpeedPct: 4,
            Effects: new[] { new ItemEffect("hap_linh", "Hấp linh", 1, 4, 0, "+4% tốc độ tu luyện") },
            Special: "Khảm nạm: +4% Tu luyện · +4 Thân pháp"
        ),
        new(
            "gem_amethyst_cui", "Tử Ngọc Thô", "gem", "#9254de",
            "Viên bảo ngọc tím huyền bí, ngưng tụ linh hồn linh cảm thiên cơ.",
            Icon: "img/core/core_07.png", Rarity: "rare", Kind: "Bảo Ngọc",
            CritPct: 4,
            Effects: new[] { new ItemEffect("tinh_tam", "Tĩnh tâm", 1, 15, 0, "Vào trận với 15 nộ") },
            Special: "Khảm nạm: +4% Bạo kích · +15 Nộ đầu trận"
        ),

        // ------------------ 5. NGỌC XỊN (Phẩm Cam - mythic) ------------------
        new(
            "gem_ruby_xin", "Hồng Ngọc Hoàn Mỹ", "gem", "#f5222d",
            "Bảo ngọc xích huyết long tâm hoàn hảo không tì vết, bộc phát uy lực hủy thiên diệt địa.",
            Icon: "img/core/core_02.png", Rarity: "mythic", Kind: "Bảo Ngọc",
            Atk: 320,
            Special: "Khảm nạm: +320 Tấn công"
        ),
        new(
            "gem_sapphire_xin", "Lam Ngọc Hoàn Mỹ", "gem", "#1890ff",
            "Bảo ngọc cửu khúc u tuyền thuần khiết, ngưng kết hàn băng hộ giáp kiên cố bất hoại.",
            Icon: "img/core/core_05.png", Rarity: "mythic", Kind: "Bảo Ngọc",
            Def: 260,
            Special: "Khảm nạm: +260 Phòng thủ"
        ),
        new(
            "gem_topaz_xin", "Hoàng Ngọc Hoàn Mỹ", "gem", "#d48806",
            "Bảo ngọc hoàng long tích tụ vạn năm, khí huyết dồi dào tựa biển sâu.",
            Icon: "img/core/core_11.png", Rarity: "mythic", Kind: "Bảo Ngọc",
            Hp: 2200,
            Special: "Khảm nạm: +2.200 Máu tối đa"
        ),
        new(
            "gem_emerald_xin", "Lục Ngọc Hoàn Mỹ", "gem", "#389e0d",
            "Bảo ngọc bích ba trường thanh, thông suốt kinh mạch linh khí tuần hoàn nhanh gấp bội.",
            Icon: "img/core/core_06.png", Rarity: "mythic", Kind: "Bảo Ngọc",
            SpeedPct: 10,
            Effects: new[] { new ItemEffect("hap_linh", "Hấp linh", 2, 12, 0, "+12% tốc độ tu luyện") },
            Special: "Khảm nạm: +12% Tu luyện · +10 Thân pháp"
        ),
        new(
            "gem_amethyst_xin", "Tử Ngọc Hoàn Mỹ", "gem", "#722ed1",
            "Bảo ngọc tử vi tinh đẩu ngưng kết, dẫn phát sát chiêu bạo liệt chí tử.",
            Icon: "img/core/core_09.png", Rarity: "mythic", Kind: "Bảo Ngọc",
            CritPct: 12,
            Effects: new[] { new ItemEffect("tinh_tam", "Tĩnh tâm", 2, 35, 0, "Vào trận với 35 nộ") },
            Special: "Khảm nạm: +12% Bạo kích · +35 Nộ đầu trận"
        ),

        // ------------------ 6. THIÊN PHÙ BẢO NGỌC (Phẩm Đỏ - divine) ------------------
        new(
            "gem_thien_phu", "Thiên Phù Bảo Ngọc", "gem", "#eb2f96",
            "Dung hợp hoàn mỹ giữa Tiên Ngọc tinh túy và Đại Đạo Phù Thạch, uy chấn bát hoang.",
            Icon: "img/core/core_15.png", Rarity: "divine", Kind: "Tiên Bảo",
            Atk: 380, Hp: 2000, CritPct: 10,
            Effects: new[] { new ItemEffect("linh_ap", "Linh áp", 3, 10, 0, "+10% sát thương kỹ năng") },
            Special: "Khảm nạm: +380 Công · +2.000 Máu · +10% Bạo kích · +10% Sát thương kỹ năng"
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
        // ---- 1. ĐÁ + ĐÁ: Ghép thăng cấp (3 sơ -> 1 cao) ----
        new("fuse_stone_viem", "Luyện Viêm Thạch · Cao", "stone", "stone_viem_cao", 1,
            new() { ["stone_viem_so"] = 3 }, 200, "3 Viêm Thạch · Sơ thăng hoa thành 1 Viêm Thạch · Cao."),
        new("fuse_stone_bang", "Luyện Băng Thạch · Cao", "stone", "stone_bang_cao", 1,
            new() { ["stone_bang_so"] = 3 }, 200, "3 Băng Thạch · Sơ thăng hoa thành 1 Băng Thạch · Cao."),
        new("fuse_stone_loi", "Luyện Lôi Thạch · Cao", "stone", "stone_loi_cao", 1,
            new() { ["stone_loi_so"] = 3 }, 200, "3 Lôi Thạch · Sơ thăng hoa thành 1 Lôi Thạch · Cao."),
        new("fuse_stone_kim", "Luyện Kim Thạch · Cao", "stone", "stone_kim_cao", 1,
            new() { ["stone_kim_so"] = 3 }, 200, "3 Kim Thạch · Sơ thăng hoa thành 1 Kim Thạch · Cao."),
        new("fuse_stone_tho", "Luyện Thổ Thạch · Cao", "stone", "stone_tho_cao", 1,
            new() { ["stone_tho_so"] = 3 }, 200, "3 Thổ Thạch · Sơ thăng hoa thành 1 Thổ Thạch · Cao."),

        // ---- 2. ĐÁ + ĐÁ: Biến dị nhị hợp ----
        new("fuse_stone_liet_loi", "Hợp Liệt Lôi Thạch", "stone", "stone_liet_loi", 1,
            new() { ["stone_viem_cao"] = 1, ["stone_loi_cao"] = 1 }, 500, "Viêm Thạch Cao + Lôi Thạch Cao biến dị sinh ra Liệt Lôi Thạch (vừa đốt cháy vừa giáng sấm)."),
        new("fuse_stone_huyen_bang", "Hợp Huyền Băng Hộ Thạch", "stone", "stone_huyen_bang", 1,
            new() { ["stone_bang_cao"] = 1, ["stone_tho_cao"] = 1 }, 500, "Băng Thạch Cao + Thổ Thạch Cao biến dị sinh ra Huyền Băng Hộ Thạch (tăng thủ và tạo khiên)."),
        new("fuse_stone_am_duong", "Hợp Âm Dương Thạch", "stone", "stone_am_duong", 1,
            new() { ["stone_viem_cao"] = 1, ["stone_bang_cao"] = 1 }, 500, "Viêm Thạch Cao + Băng Thạch Cao tương dung sinh ra Âm Dương Thạch (phản đòn và hồi máu)."),

        // ---- 3. ĐÁ ĐỤC LỖ (Thiên Công Đục Thạch) ----
        new("fuse_stone_socket", "Luyện Thiên Công Đục Thạch", "stone", "stone_socket", 1,
            new() { ["stone_viem_so"] = 1, ["stone_bang_so"] = 1, ["stone_loi_so"] = 1, ["stone_kim_so"] = 1, ["stone_tho_so"] = 1 }, 1000,
            "Tụ hợp ngũ hành phù thạch tinh hoa rèn thành Thiên Công Đục Thạch mở thêm lỗ khảm."),

        // ---- 4. NGỌC + NGỌC: Ghép thuần (5 cùi -> 1 xịn) ----
        new("fuse_gem_ruby", "Thăng cấp Hồng Ngọc Hoàn Mỹ", "gem", "gem_ruby_xin", 1,
            new() { ["gem_ruby_cui"] = 5 }, 500, "5 Hồng Ngọc Thô dung hợp thành 1 Hồng Ngọc Hoàn Mỹ (+320 Công)."),
        new("fuse_gem_sapphire", "Thăng cấp Lam Ngọc Hoàn Mỹ", "gem", "gem_sapphire_xin", 1,
            new() { ["gem_sapphire_cui"] = 5 }, 500, "5 Lam Ngọc Thô dung hợp thành 1 Lam Ngọc Hoàn Mỹ (+260 Thủ)."),
        new("fuse_gem_topaz", "Thăng cấp Hoàng Ngọc Hoàn Mỹ", "gem", "gem_topaz_xin", 1,
            new() { ["gem_topaz_cui"] = 5 }, 500, "5 Hoàng Ngọc Thô dung hợp thành 1 Hoàng Ngọc Hoàn Mỹ (+2.200 Máu)."),
        new("fuse_gem_emerald", "Thăng cấp Lục Ngọc Hoàn Mỹ", "gem", "gem_emerald_xin", 1,
            new() { ["gem_emerald_cui"] = 5 }, 500, "5 Lục Ngọc Thô dung hợp thành 1 Lục Ngọc Hoàn Mỹ (+12% Tu luyện, +10 Thân pháp)."),
        new("fuse_gem_amethyst", "Thăng cấp Tử Ngọc Hoàn Mỹ", "gem", "gem_amethyst_xin", 1,
            new() { ["gem_amethyst_cui"] = 5 }, 500, "5 Tử Ngọc Thô dung hợp thành 1 Tử Ngọc Hoàn Mỹ (+12% Bạo kích, +35 Nộ)."),

        // ---- 5. ĐÁ + NGỌC: Thăng hoa Ngọc (1 cùi + 3 đá nguyên tố -> 1 xịn) ----
        new("ascend_ruby", "Thăng hoa Hồng Ngọc (dùng Đá)", "transcend", "gem_ruby_xin", 1,
            new() { ["gem_ruby_cui"] = 1, ["stone_viem_so"] = 3 }, 1000, "Dùng chân hỏa của 3 Viêm Thạch thăng hoa 1 Hồng Ngọc Thô thành Hồng Ngọc Hoàn Mỹ."),
        new("ascend_sapphire", "Thăng hoa Lam Ngọc (dùng Đá)", "transcend", "gem_sapphire_xin", 1,
            new() { ["gem_sapphire_cui"] = 1, ["stone_bang_so"] = 3 }, 1000, "Dùng hàn khí của 3 Băng Thạch thăng hoa 1 Lam Ngọc Thô thành Lam Ngọc Hoàn Mỹ."),
        new("ascend_topaz", "Thăng hoa Hoàng Ngọc (dùng Đá)", "transcend", "gem_topaz_xin", 1,
            new() { ["gem_topaz_cui"] = 1, ["stone_tho_so"] = 3 }, 1000, "Dùng đại địa của 3 Thổ Thạch thăng hoa 1 Hoàng Ngọc Thô thành Hoàng Ngọc Hoàn Mỹ."),
        new("ascend_emerald", "Thăng hoa Lục Ngọc (dùng Đá)", "transcend", "gem_emerald_xin", 1,
            new() { ["gem_emerald_cui"] = 1, ["stone_kim_so"] = 3 }, 1000, "Dùng Canh Kim của 3 Kim Thạch thăng hoa 1 Lục Ngọc Thô thành Lục Ngọc Hoàn Mỹ."),
        new("ascend_amethyst", "Thăng hoa Tử Ngọc (dùng Đá)", "transcend", "gem_amethyst_xin", 1,
            new() { ["gem_amethyst_cui"] = 1, ["stone_loi_so"] = 3 }, 1000, "Dùng thiên lôi của 3 Lôi Thạch thăng hoa 1 Tử Ngọc Thô thành Tử Ngọc Hoàn Mỹ."),

        // ---- 6. THIÊN PHÙ BẢO NGỌC (1 Ngọc Xịn + 1 Dị Thạch -> Thiên Phù) ----
        new("ascend_thien_phu", "Luyện Thiên Phù Bảo Ngọc", "transcend", "gem_thien_phu", 1,
            new() { ["gem_ruby_xin"] = 1, ["stone_liet_loi"] = 1 }, 5000, "Dung hợp đỉnh cao giữa Bảo Ngọc và Dị Thạch tạo ra Thiên Phù Bảo Ngọc chí tôn."),

        // ---- 7. LUYỆN NGỌC CÙI TỪ LINH THẢO ----
        new("craft_gem_ruby", "Luyện Hồng Ngọc Thô", "herb", "gem_ruby_cui", 1,
            new() { ["huyet_can_thao"] = 10 }, 500, "Ngưng luyện 10 Huyết Căn Thảo thành 1 Hồng Ngọc Thô."),
        new("craft_gem_sapphire", "Luyện Lam Ngọc Thô", "herb", "gem_sapphire_cui", 1,
            new() { ["tu_linh_thao"] = 10 }, 500, "Ngưng luyện 10 Tụ Linh Thảo thành 1 Lam Ngọc Thô."),
        new("craft_gem_topaz", "Luyện Hoàng Ngọc Thô", "herb", "gem_topaz_cui", 1,
            new() { ["thien_nien_sam"] = 10 }, 500, "Ngưng luyện 10 Thiên Niên Nhân Sâm thành 1 Hoàng Ngọc Thô."),
        new("craft_gem_emerald", "Luyện Lục Ngọc Thô", "herb", "gem_emerald_cui", 1,
            new() { ["bich_ngoc_linh_thao"] = 10 }, 500, "Ngưng luyện 10 Bích Ngọc Linh Thảo thành 1 Lục Ngọc Thô."),
        new("craft_gem_amethyst", "Luyện Tử Ngọc Thô", "herb", "gem_amethyst_cui", 1,
            new() { ["tu_van_sam"] = 10 }, 500, "Ngưng luyện 10 Tử Vân Sâm thành 1 Tử Ngọc Thô.")
    };

    public static readonly IReadOnlyDictionary<string, GemRecipeDef> RecipeById = Recipes.ToDictionary(r => r.Id);

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
            r.Id, r.Name, r.Category, r.ResultItem, r.ResultCount, r.Inputs, r.Stones, r.Desc
        )).ToList();

        return new GemStateDto(vault, slots, maxSockets, recipes);
    }
}
