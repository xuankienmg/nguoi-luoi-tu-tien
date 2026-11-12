using System.Globalization;

namespace VanDao.Game;

/// <summary>
/// Boss Tông Môn hằng ngày: luật và con số. Mỗi tông một Boss mỗi ngày (0h giờ Việt Nam), máu chung.
/// Mỗi người đánh HitsPerDay lượt; một lượt = trận PvpCombat tối đa MaxRounds hiệp với Boss bản sao của chính mình.
/// Con số khởi điểm: chỉnh khi cân bằng (skill can-bang-chi-so).
/// </summary>
public static class SectBossData
{
    public const int HitsPerDay = 2, MaxRounds = 15;
    public const int ActiveDays = 3;                  // thành viên vắng quá số ngày này không tính vào máu Boss
    public const double HpShare = 30;                 // máu Boss = 3000% × (tổng công đồng môn × DamagePerAtk) × số lượt (cân bằng 11/2026: ×10 so với 3, người có kỹ năng gây ~450 lần công/trận nên Boss vẫn chết quá sớm)
    public const double AtkMult = 0.5, DefMult = 1.2; // Boss bản sao: công/thủ nhân hệ số này (công là mức của 4 lượt đầu, xem AtkAtRound; cân bằng 11/2026: giảm 0,8 → 0,5)
    public const int AtkRampEvery = 4;                // cân bằng 11/2026: cứ mỗi 4 lượt trong trận, công Boss tăng thêm AtkRampPct (lượt 1–4: ×0,5, 5–8: ×1,0, 9–12: ×1,5, 13–15: ×2,0)
    public const double AtkRampPct = 1.0;             // 4 lượt đầu nhẹ tay để thành viên yếu nhất cũng sống đủ 8 lượt; càng về sau Boss càng mạnh
    /// <summary>Hệ số công của Boss bản sao ở lượt trong trận (round 0-based): AtkMult, cộng AtkRampPct sau mỗi AtkRampEvery lượt.</summary>
    public static double AtkAtRound(int round) => AtkMult * (1 + AtkRampPct * (Math.Max(0, round) / AtkRampEvery));
    public const double BuffQiPct = 3;                // buff tu vi toàn tông ngày hôm sau khi hạ Boss
    public const double ContribScale = 15000;         // cống hiến/lượt = clamp(15000 × sát thương / máu Boss, Min, Max) (nhân theo máu Boss để giữ cống hiến mỗi lượt)
    public const int ContribMin = 5, ContribMax = 30;
    public static readonly int[] TopBonus = [50, 30, 20];   // cống hiến thêm cho top 1/2/3 khi nhận rương

    // cân bằng 11/2026: rương Boss = linh thạch + đan tu vi chắc chắn, các món hiếm quay độc lập
    public const int ChestStoneMult = 3;              // linh thạch rương = 3 × StoneBase(cảnh giới)
    public const string ChestPill = "bo_nguyen_dan_trung";
    public const int ChestPills = 2;                  // Bổ Nguyên Đan Trung phẩm
    public const int ChestPetPills = 50;              // Linh Thú Đan
    public const int DaoQuality = 8, HuyenTienQuality = 9;   // GameData.QualityName: Đạo, Huyền Tiên
    public record ChestRoll(string ItemId, int Qty, double Chance, int MinRealm = 0);
    /// <summary>Món hiếm, mỗi dòng quay riêng. Trang bị Đạo / Huyền Tiên do GameService chọn món theo bộ cảnh giới (SpinPool).</summary>
    public static readonly ChestRoll[] ChestRolls =
    [
        new("bo_nguyen_dan_thuong", 1, .20),
        new(GameData.SpinTicket, 1, .10),
        new(GameData.PetSpinTicket, 1, .05),
        new("ngo_dao_dan", 1, .03, MinRealm: 3),
        new("hoa_hinh_dan", 1, .02),
        new(GameService.VipCurrency, 5, .01),     // 5 Cực phẩm linh thạch
    ];
    public const double DaoGearChance = .01, HuyenTienGearChance = .005;

    /// <summary>Mở một rương: (mã món, số lượng). Linh thạch tính theo cảnh giới; trang bị lấy từ bộ cảnh giới cao nhất ≤ cảnh giới người chơi.</summary>
    public static List<(string Id, long Qty)> OpenChest(int realm, Random rng)
    {
        var got = new List<(string, long)> { ("linh_thach", ChestStoneMult * SectData.StoneBase(realm)), (ChestPill, ChestPills), ("linh_thu_dan", ChestPetPills) };
        foreach (var r in ChestRolls)
            if (realm >= r.MinRealm && rng.NextDouble() < r.Chance) got.Add((r.ItemId, r.Qty));
        var pool = GameData.SpinPool(realm);
        foreach (var (q, chance) in new[] { (HuyenTienQuality, HuyenTienGearChance), (DaoQuality, DaoGearChance) })
            if (pool.Length > 0 && rng.NextDouble() < chance) got.Add((GameData.GearId(pool[rng.Next(pool.Length)], q), 1));
        return got;
    }

    public record Def(string Id, string Name, string Image, string Desc);
    public static readonly Def[] All =
    [
        new("boss_co_thu_yeu_vuong", "Cổ Thụ Yêu Vương", "img/boss_co_thu_yeu_vuong.png",
            "Cây cổ vạn năm thành yêu, rễ bám khắp rừng sâu, bị phong ấn dưới Bệ Phong Ấn của tông."),
        new("boss_huyen_nham_cu_nhan", "Huyền Nham Cự Nhân", "img/boss_huyen_nham_cu_nhan.png",
            "Người đá khổng lồ ngủ dưới linh mạch, mỗi đêm lại phá phong ấn thức dậy."),
    ];

    private static DateOnly Parse(string day) => DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static Def BossOf(string day) => All[Parse(day).DayNumber % All.Length];
    public static string NextDay(string day) => Parse(day).AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Một trận 15 lượt gây xấp xỉ 12 lần công của người đánh (đo ở mọi cảnh giới): quy công về sát thương một lượt đánh để HpShare giữ nghĩa "tỉ lệ máu so với sát thương".</summary>
    public const double DamagePerAtk = 12;

    /// <summary>Máu Boss tính từ CÔNG của từng thành viên hoạt động (refs = công, lưu ở SectMembers.BossRef); luôn ≥ 1.
    /// Công người khiêu chiến cũng là gốc của công Boss bản sao (SectBossFighter), nên cả máu và công Boss đều theo công người đánh.</summary>
    public static double MaxHp(IEnumerable<double> refs) => Math.Max(1, Math.Round(HpShare * HitsPerDay * DamagePerAtk * refs.Sum()));

    public static int Contribution(double damage, double maxHp) =>
        (int)Math.Clamp(Math.Round(ContribScale * damage / Math.Max(1, maxHp)), ContribMin, ContribMax);

    /// <summary>Hạng 0-based theo sát thương giảm dần, hòa thì theo Id; không gây sát thương thì int.MaxValue.</summary>
    public static int Rank(IEnumerable<(Guid Id, double Damage)> rows, Guid me)
    {
        var list = rows.Where(r => r.Damage > 0).OrderByDescending(r => r.Damage).ThenBy(r => r.Id).ToList();
        var i = list.FindIndex(r => r.Id == me);
        return i < 0 ? int.MaxValue : i;
    }
}
