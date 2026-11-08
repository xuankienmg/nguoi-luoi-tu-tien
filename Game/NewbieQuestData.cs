using VanDao.Data;
namespace VanDao.Game;

/// <summary>
/// Nhiệm vụ tân thủ: chuỗi tuần tự từ lúc tạo nhân vật đến đột phá Trúc Cơ.
/// Mỗi nhiệm vụ đo bằng đúng một cách: Check (trạng thái Player) hoặc CountEvent (đếm hành động trong lúc nhiệm vụ đang mở).
/// Stage tính từ 0 (Tầng 1 = 0); Realm 0 = Luyện Khí. AreaMax về 1 khi sang chương mới nên điều kiện ải luôn kèm ChapterMax.
/// </summary>
public static class NewbieQuestData
{
    public record Reward(string Item, int Qty);
    public record QuestDef(string Id, string Title, string Hint, string? Tab,
                           string? CountEvent, int CountNeed, Func<Player, bool>? Check, Reward[] Rewards);

    public const string EvSell = "sell", EvBrew = "brew", EvFuse = "fuse";

    private static Reward[] R(params (string, int)[] r) => r.Select(x => new Reward(x.Item1, x.Item2)).ToArray();
    private static QuestDef S(string id, string title, string hint, string? tab, Func<Player, bool> check, Reward[] rw) => new(id, title, hint, tab, null, 0, check, rw);
    private static QuestDef E(string id, string title, string hint, string? tab, string ev, Reward[] rw) => new(id, title, hint, tab, ev, 1, null, rw);

    private static bool Stage(Player p, int stage) => p.Realm > 0 || p.Stage >= stage;
    private static bool Area(Player p, int area) => p.ChapterMax > 0 || p.AreaMax >= area;
    private static bool UpgradedSkill(Player p) => GameService.SkillLevels(p).Any(kv => kv.Key != SkillData.StartSkill && kv.Value >= 1
        && SkillData.ById.TryGetValue(kv.Key, out var s) && s.Tree != "basic");

    // cân bằng 11/2026: tổng 2.400 linh thạch (chương 1 rơi 3–8/chuyến; phí tầng LK ~220, đột phá Trúc Cơ 200); vé/đan ít hơn 1 vòng DailyLoginData
    public static readonly QuestDef[] Quests =
    {
        S("starter", "Bước vào tiên đồ", "Chọn công pháp và linh thú khởi đầu.", null, p => p.StarterChosen, R(("linh_thach", 100))),
        S("first_area", "Vượt ải đầu tiên", "Thám hiểm tự động sẽ đánh quái; vượt qua ải 1.", "map", p => Area(p, 2), R(("tu_khi_dan", 2))),
        S("equip", "Khoác giáp cầm binh", "Mở Hành trang, bấm Mặc tốt nhất.", "inv", p => p.Equipment.Count > 0, R(("linh_thach", 100))),
        S("skill", "Lĩnh ngộ chiêu thức", "Vào Công pháp, dùng điểm kỹ năng nâng một chiêu.", "skill", UpgradedSkill, R(("tu_khi_dan", 2))),
        S("lk3", "Luyện Khí tầng 3", "Tu vi tăng khi treo máy; đủ tu vi thì đột phá lên tầng kế.", "req", p => Stage(p, 2), R(("linh_thach", 200))),
        S("area3", "Tiến sâu vào ải 3", "Mở khóa ải 3 trên bản đồ.", "map", p => Area(p, 3), R(("tu_khi_dan", 2))),
        E("sell", "Đổi đồ lấy linh thạch", "Vào Luyện chế › Bán, bán một món đồ thừa.", "sell", EvSell, R(("tay_tuy_thao", 2), ("thach_dai_thao", 1), ("linh_thach", 1000))),
        S("pet", "Linh thú đồng hành", "Ấp trứng linh thú khởi đầu và thu linh thú.", "pet", p => p.Pets.Count > 0, R(("linh_thu_dan", 3))),
        E("brew", "Khai lò luyện đan", "Vào Luyện chế › Đan dược, khai lò luyện một mẻ đan.", "craft", EvBrew, R(("linh_thach", 300))),
        S("lk6", "Luyện Khí tầng 6", "Tiếp tục tu luyện và đột phá lên tầng 6.", "req", p => Stage(p, 5), R(("tu_khi_dan", 3))),
        S("boss", "Hạ Boss chương", "Thu thập lệnh bài từ quái rồi khiêu chiến Boss chương.", "map", p => p.BossKills >= 1, R(("ve_tai_xiu", 1))),
        E("fuse", "Luyện khí", "Vào Luyện chế › Luyện khí, ghép trang bị một lần.", "fuse", EvFuse, R(("linh_thach", 500))),
        S("lk9", "Luyện Khí tầng 9", "Đạt đỉnh Luyện Khí, chuẩn bị đột phá Trúc Cơ.", "req", p => Stage(p, 8), R(("hoan_cot_dan", 1), ("linh_thach", 200))),
        S("trucco", "Trúc Cơ thành tựu", "Đột phá đại cảnh giới lên Trúc Cơ.", "req", p => p.Realm >= 1, R(("ve_tai_xiu", 2), ("hoa_hinh_dan", 1))),
    };

    public static int Total => Quests.Length;
}
