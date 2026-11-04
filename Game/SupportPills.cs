using System.Globalization;
using VanDao.Data;

namespace VanDao.Game;

public record BuffDto(string Id, string Name, string Icon, int Quality, string QualityName, string Effect, long RemainingMs);

/// <summary>Đan hỗ trợ có thời hạn (Tụ Linh · Đan Tâm · Tật Hành · Thú Linh · Bế Quan), mỗi loại ba phẩm Hạ · Trung · Thượng.
/// Luyện như đan tiến giai (cần cấp luyện đan theo phẩm cấp, ra phẩm theo Alchemy.Odds). Mỗi loại chỉ một hiệu lực:
/// uống cùng phẩm cộng thời gian (tối đa Max), phẩm cao hơn nâng phẩm và giữ thời gian còn lại, phẩm thấp hơn không dùng được khi phẩm cao còn hiệu lực.
/// Hiệu lực lưu trong cột cũ Player.LastRefine dạng "loại:phẩm:hạn (ticks UTC)" ngăn bởi ';'.</summary>
public static class SupportPills
{
    /// <param name="Value">Giá trị theo phẩm Hạ / Trung / Thượng (Tụ Linh: % tu vi thêm · Đan Tâm, Thú Linh, Bế Quan: hệ số · Tật Hành: % thời gian thám hiểm giảm).</param>
    /// <param name="QualityIcons">Mỗi phẩm một ảnh: img/dan/&lt;id&gt;[_trung|_thuong].png.</param>
    public record Def(string Id, string Name, int Tier, string Color, string Desc, double[] Value, int Seconds, int MaxSeconds, Func<double, string> Effect, bool QualityIcons = false)
    {
        public string Icon(int q) => $"img/dan/{Id}{(QualityIcons ? Suffix[q] : "")}.png";
    }

    public const string TuLinh = "tu_linh_dan", DanTam = "dan_tam_dan", TatHanh = "tat_hanh_dan", ThuLinh = "thu_linh_dan", BeQuan = "be_quan_dan", ChienY = "chien_y_dan";
    /// <summary>Lượt cập nhật cách lượt trước ít nhất chừng này giây thì tính là vừa offline (Bế Quan Đan có tác dụng).</summary>
    public const double OfflineGapSeconds = 300;
    /// <summary>Đan Tâm Đan: cộng thêm tỉ lệ ra Thượng phẩm khi luyện.</summary>
    public const double DanTamThuongBonus = .03;

    public static readonly Def[] All =
    {
        new(TuLinh, "Tụ Linh Đan", 2, "#5fe0d6", "Đan hội tụ linh khí quanh thân, tu luyện như có trận pháp trợ lực.",
            new[] { .30, .50, .80 }, 1800, 4 * 3600, v => $"Tu vi từ tu luyện và thám hiểm +{v * 100:0}%"),
        new(DanTam, "Đan Tâm Đan", 3, "#ff6a6a", "Đan định tâm cho luyện đan sư, hỏa hầu vững như bàn thạch.",
            new[] { 1.5, 2.0, 3.0 }, 1800, 4 * 3600, v => $"EXP luyện đan ×{v:0.#}, tỉ lệ ra đan Thượng phẩm +{DanTamThuongBonus * 100:0}%"),
        new(TatHanh, "Tật Hành Đan", 4, "#9fe8b8", "Đan nhẹ thân như gió, bước chân thám hiểm thoăn thoắt.",
            new[] { .15, .25, .35 }, 1800, 4 * 3600, v => $"Thời gian mỗi chuyến thám hiểm −{v * 100:0}%"),
        new(ThuLinh, "Thú Linh Đan", 5, "#ffb84a", "Đan khơi dậy huyết mạch linh thú, ăn một miếng bằng ba miếng.",
            new[] { 1.5, 2.0, 2.5 }, 1800, 4 * 3600, v => $"EXP linh thú khi cho ăn Linh Thú Đan ×{v:0.#}"),
        new(BeQuan, "Bế Quan Đan", 6, "#a98cff", "Đan bế quan, nhập định càng lâu linh khí càng tụ.",
            new[] { 1.3, 1.5, 2.0 }, 8 * 3600, 24 * 3600, v => $"Tu vi tích lũy khi offline ×{v:0.#} (tính tối đa 2 giờ vắng mặt mỗi lần)"),
        // cân bằng 11/2026: đan chiến đấu đầu tiên, tiêu Hỗn Nguyên Đạo Liên + Kiến Mộc Miêu; không tính vào Lôi đài và Lực chiến xếp hạng
        new(ChienY, "Hỗn Nguyên Chiến Ý Đan", 6, "#ff5a3c", "Đan khơi chiến ý hỗn nguyên, linh lực sục sôi trong kinh mạch.",
            new[] { .10, .15, .20 }, 1800, 4 * 3600, v => $"Công và thủ +{v * 100:0}% khi thám hiểm/đánh Boss (không áp dụng ở Lôi đài)", QualityIcons: true),
    };
    public static readonly IReadOnlyDictionary<string, Def> ById = All.ToDictionary(d => d.Id);

    private static readonly string[] Suffix = { "", "_trung", "_thuong" };
    public static (Def Def, int Q)? Parse(string id)
    {
        for (var q = Suffix.Length - 1; q >= 0; q--)
            if (id.EndsWith(Suffix[q]) && ById.TryGetValue(id[..^Suffix[q].Length], out var d)) return (d, q);
        return null;
    }
    public static bool Is(string id) => Parse(id) is not null;

    private static string Left(TimeSpan t)
    {
        var m = (int)Math.Ceiling(t.TotalMinutes);
        return m < 60 ? $"{m} phút" : m % 60 == 0 ? $"{m / 60} giờ" : $"{m / 60} giờ {m % 60} phút";
    }
    private static string Duration(int s) => s % 3600 == 0 ? $"{s / 3600} giờ" : $"{s / 60} phút";

    public static IEnumerable<ItemDef> Items => All.SelectMany(d => Enumerable.Range(0, 3).Select(q => new ItemDef(
        d.Id + Suffix[q], q == 0 ? d.Name : $"{d.Name} · {Alchemy.QualityNames[q]}", "elixir", d.Color, d.Desc, d.Icon(q),
        Rarity: GameData.RarityOrder[d.Tier - 1], Kind: "Đan hỗ trợ", Tier: d.Tier,
        Use: $"{Alchemy.QualityNames[q]}: {d.Effect(d.Value[q])} trong {Duration(d.Seconds)} (cộng dồn tối đa {Duration(d.MaxSeconds)}). " +
             "Uống viên cùng phẩm để cộng thời gian; phẩm cao hơn nâng phẩm và giữ thời gian còn lại. Luyện chế tại mục Đan dược.")));

    // ---- hiệu lực đang có ----
    private static Dictionary<string, (int Q, DateTime Until)> Read(Player p)
    {
        var d = new Dictionary<string, (int, DateTime)>();
        foreach (var part in (p.LastRefine ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var x = part.Split(':');
            if (x.Length == 3 && ById.ContainsKey(x[0]) && int.TryParse(x[1], out var q) && q is >= 0 and <= 2
                && long.TryParse(x[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks) && ticks > 0 && ticks <= DateTime.MaxValue.Ticks)
                d[x[0]] = (q, new DateTime(ticks, DateTimeKind.Utc));
        }
        return d;
    }
    private static void Write(Player p, Dictionary<string, (int Q, DateTime Until)> d, DateTime now) =>
        p.LastRefine = string.Join(';', d.Where(kv => kv.Value.Until > now).Select(kv => $"{kv.Key}:{kv.Value.Q}:{kv.Value.Until.Ticks.ToString(CultureInfo.InvariantCulture)}"));

    /// <summary>Hiệu lực loại id còn tác dụng tại thời điểm at: (phẩm, hạn) hoặc null.</summary>
    public static (int Q, DateTime Until)? Active(Player p, string id, DateTime at) =>
        Read(p).TryGetValue(id, out var b) && b.Until > at ? b : null;

    /// <summary>Giá trị hiệu lực tại at; không có hiệu lực thì trả về none.</summary>
    public static double Value(Player p, string id, DateTime at, double none) =>
        Active(p, id, at) is { } b ? ById[id].Value[b.Q] : none;

    /// <summary>Số giây trong khoảng [from, to) còn hiệu lực loại id (hiệu lực luôn bắt đầu trước from vì uống đan chạy Tick trước).</summary>
    public static double Overlap(Player p, string id, DateTime from, DateTime to) =>
        Active(p, id, from) is { } b ? Math.Max(0, ((b.Until < to ? b.Until : to) - from).TotalSeconds) : 0;

    /// <summary>Uống n viên (0 = đến khi đầy thời gian tối đa).</summary>
    public static (string msg, string kind) Use(Player p, string itemId, int n, DateTime now)
    {
        var (def, q) = Parse(itemId) ?? throw new GameException("Không phải đan hỗ trợ.");
        if (n < 0) throw new GameException("Số lượng không hợp lệ.");
        var have = p.CountInt(itemId);
        if (have < 1) throw new GameException("Không có vật phẩm này trong hành trang.");
        var all = Read(p);
        var cur = all.TryGetValue(def.Id, out var c) && c.Until > now ? c : ((int Q, DateTime Until)?)null;
        if (cur is { } hi && hi.Q > q)
            throw new GameException($"Đang có hiệu lực {def.Name} · {Alchemy.QualityNames[hi.Q]}; chờ hết hạn mới dùng được phẩm thấp hơn.");
        var left = cur is { } k ? (k.Until - now).TotalSeconds : 0;
        var room = def.MaxSeconds - left;
        if (room < 1) throw new GameException($"{def.Name} đã đạt thời gian tối đa {Duration(def.MaxSeconds)}.");
        var fit = Math.Max(1, (int)Math.Ceiling(room / def.Seconds));
        var used = Math.Min(n == 0 ? have : Math.Min(n, have), fit);
        var until = now.AddSeconds(Math.Min(def.MaxSeconds, left + (double)used * def.Seconds));
        all[def.Id] = (q, until);
        Write(p, all, now);
        p.Add(itemId, -used);
        var upgraded = cur is { } old && old.Q < q ? $" (nâng lên {Alchemy.QualityNames[q]})" : "";
        return ($"Dùng {used} {GameData.ItemById[itemId].Name}{upgraded}: {def.Effect(def.Value[q])}, còn {Left(until - now)}.", "good");
    }

    public static List<BuffDto> State(Player p, DateTime now) => Read(p).Where(kv => kv.Value.Until > now)
        .Select(kv => { var d = ById[kv.Key]; var q = kv.Value.Q;
            return new BuffDto(d.Id, d.Name, d.Icon(q), q, Alchemy.QualityNames[q], d.Effect(d.Value[q]), (long)(kv.Value.Until - now).TotalMilliseconds); })
        .OrderBy(b => Array.FindIndex(All, d => d.Id == b.Id)).ToList();
}
