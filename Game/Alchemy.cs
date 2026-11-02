using VanDao.Data;

namespace VanDao.Game;

public record AlchemyOddsDto(string Pill, int Tier, bool Unlocked, double Thuong, double Trung, double Ha);
public record AlchemyDto(int Rank, string Name, int Exp, int RankExp, int NextExp, List<AlchemyOddsDto> Odds);

/// <summary>Cấp luyện đan (Nhất → Thập Phẩm) và phẩm chất đan tiến giai (Hạ · Trung · Thượng).
/// Luyện đan cộng EXP theo bậc đan; đan bậc N cần cấp N Phẩm. Cấp càng vượt bậc đan, tỉ lệ ra Trung / Thượng phẩm càng cao.
/// Đột phá luôn dùng viên phẩm cao nhất trong túi trước; Trung +5, Thượng +10 điểm % tỉ lệ (lấy trung bình số viên dùng).</summary>
public static class Alchemy
{
    public const int MaxRank = 10;
    private static readonly string[] RankNums = { "Nhất", "Nhị", "Tam", "Tứ", "Ngũ", "Lục", "Thất", "Bát", "Cửu", "Thập" };

    public static string Title(int rank) => rank switch
    {
        <= 3 => "Luyện Đan Sư Sơ Cấp",
        <= 6 => "Luyện Đan Sư Trung Cấp",
        <= 8 => "Luyện Đan Tông Sư",
        9 => "Luyện Đan Đại Sư",
        _ => "Luyện Đan Đế Sư",
    };
    public static string RankName(int rank) => $"{RankNums[Math.Clamp(rank, 1, MaxRank) - 1]} Phẩm - {Title(rank)}";

    private static int Pow3(int n) { var r = 1; for (var i = 0; i < n; i++) r *= 3; return r; }
    /// <summary>Tổng EXP để đạt cấp rank: mỗi cấp cần khoảng 10 viên đan cùng bậc.</summary>
    public static int ExpAt(int rank) => 100 * (Pow3(Math.Clamp(rank, 1, MaxRank) - 1) - 1) / 2;
    public static int ExpPerPill(int tier) => 10 * Pow3(Math.Clamp(tier, 1, MaxRank) - 1);
    public static int RankOf(int exp) { var r = 1; while (r < MaxRank && exp >= ExpAt(r + 1)) r++; return r; }

    /// <summary>Nhân vật cũ chưa luyện lần nào (EXP 0) được tính cấp theo cảnh giới hiện tại, để không bị khóa đan đang cần.</summary>
    public static int Exp(Player p) => p.AlchemyExp == 0 && p.Realm > 0 ? ExpAt(Math.Min(MaxRank, p.Realm + 1)) : p.AlchemyExp;
    public static int Rank(Player p) => RankOf(Exp(p));

    public static void Gain(Player p, int tier, int n) =>
        p.AlchemyExp = (int)Math.Min(int.MaxValue, Exp(p) + (long)ExpPerPill(tier) * n);

    // ---- phẩm chất đan tiến giai ----
    private static readonly string[] Suffix = { "", "_trung", "_thuong" };
    public static readonly string[] QualityNames = { "Hạ phẩm", "Trung phẩm", "Thượng phẩm" };
    public static readonly double[] BreakBonus = { 0, .05, .10 };
    private static readonly string[] QualityColor = { "", "#7fb8ff", "#ffcf5a" };

    public static bool IsAdvancePill(string id) => Array.IndexOf(GameData.PillChain, id) >= 0;
    /// <summary>Đan luyện ra có phẩm Hạ / Trung / Thượng: đan tiến giai và đan hỗ trợ.</summary>
    public static bool HasQuality(string id) => IsAdvancePill(id) || SupportPills.ById.ContainsKey(id);
    public static string VariantId(string pill, int q) => pill + Suffix[q];
    /// <summary>(đan gốc, phẩm 0..2) nếu id là đan tiến giai hoặc biến thể của nó.</summary>
    public static (string Pill, int Q)? Parse(string id)
    {
        for (var q = Suffix.Length - 1; q >= 0; q--)
            if (id.EndsWith(Suffix[q]) && IsAdvancePill(id[..^Suffix[q].Length])) return (id[..^Suffix[q].Length], q);
        return null;
    }

    /// <summary>Tỉ lệ (Thượng, Trung) khi luyện đan bậc tier ở cấp rank: gốc 10% / 30%, mỗi cấp vượt bậc đan +4 / +3 điểm %.</summary>
    public static (double Thuong, double Trung) Odds(int rank, int tier, double thuongBonus = 0)
    {
        var d = Math.Max(0, rank - tier);
        return (Math.Min(.40, .10 + .04 * d) + thuongBonus, Math.Min(.45, .30 + .03 * d));
    }
    public static int Roll(Random rng, int rank, int tier, double thuongBonus = 0)
    {
        var (t, m) = Odds(rank, tier, thuongBonus);
        var x = rng.NextDouble();
        return x < t ? 2 : x < t + m ? 1 : 0;
    }

    /// <summary>Biến thể Trung / Thượng phẩm của từng đan tiến giai (Hạ phẩm giữ id cũ).</summary>
    public static IEnumerable<ItemDef> Variants(IEnumerable<ItemDef> items) => items.Where(it => IsAdvancePill(it.Id))
        .SelectMany(it => new[] { 1, 2 }.Select(q => it with
        {
            Id = VariantId(it.Id, q), Name = $"{it.Name} · {QualityNames[q]}", Color = QualityColor[q],
            Desc = $"{it.Desc} Đan {QualityNames[q]}, đan văn rõ nét hơn.",
            Use = $"{it.Use} {QualityNames[q]}: mỗi viên dùng khi đột phá +{BreakBonus[q] * 100:0} điểm % tỉ lệ (đột phá luôn dùng viên phẩm cao nhất trước).",
        }));

    // ---- chi phí đột phá: đan tiến giai tính gộp mọi phẩm ----
    public static decimal Have(Player p, string id) =>
        IsAdvancePill(id) ? Enumerable.Range(0, Suffix.Length).Sum(q => p.Count(VariantId(id, q))) : p.Count(id);
    public static bool Has(Player p, IReadOnlyDictionary<string, int> cost) => cost.All(kv => Have(p, kv.Key) >= kv.Value);

    /// <summary>Số viên mỗi phẩm sẽ dùng cho từng đan tiến giai trong cost: Thượng → Trung → Hạ.</summary>
    private static IEnumerable<(string Id, int Q, int N)> Plan(Player p, IReadOnlyDictionary<string, int> cost)
    {
        foreach (var (id, need) in cost.Where(kv => IsAdvancePill(kv.Key)))
        {
            var left = need;
            for (var q = Suffix.Length - 1; q >= 0 && left > 0; q--)
            {
                var take = (int)Math.Min(left, p.Count(VariantId(id, q)));
                if (take > 0) { yield return (VariantId(id, q), q, take); left -= take; }
            }
        }
    }

    /// <summary>Tỉ lệ đột phá cộng thêm: trung bình phẩm của các viên đan tiến giai sẽ dùng.</summary>
    public static double Bonus(Player p, IReadOnlyDictionary<string, int> cost)
    {
        var plan = Plan(p, cost).ToList();
        var n = plan.Sum(x => x.N);
        return n == 0 ? 0 : Math.Round(plan.Sum(x => BreakBonus[x.Q] * x.N) / n, 4);
    }

    public static void Pay(Player p, IReadOnlyDictionary<string, int> cost)
    {
        foreach (var (id, _, n) in Plan(p, cost).ToList()) p.Add(id, -n);
        foreach (var (id, n) in cost.Where(kv => !IsAdvancePill(kv.Key))) p.Add(id, -n);
    }

    public static AlchemyDto State(Player p)
    {
        int exp = Exp(p), rank = RankOf(exp);
        var bonus = SupportPills.Active(p, SupportPills.DanTam, DateTime.UtcNow) is not null ? SupportPills.DanTamThuongBonus : 0;
        var odds = GameData.PillChain.Concat(SupportPills.All.Select(d => d.Id)).Select(id =>
        {
            var tier = GameData.ItemById[id].Tier;
            var (t, m) = Odds(rank, tier, bonus);
            return new AlchemyOddsDto(id, tier, rank >= tier, t, m, Math.Round(1 - t - m, 4));
        }).ToList();
        return new AlchemyDto(rank, RankName(rank), exp, ExpAt(rank), rank < MaxRank ? ExpAt(rank + 1) : ExpAt(MaxRank), odds);
    }
}
