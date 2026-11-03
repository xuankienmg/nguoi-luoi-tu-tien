using System.Globalization;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>Bought / Limit: số đã mua trong đợt hàng và giới hạn mỗi đợt (Limit 0 = không giới hạn, món cố định).</summary>
public record ShopOfferDto(string Item, long Price, bool Daily, int Bought, int Limit);
public record ShopDto(string Id, string Name, List<ShopOfferDto> Offers, long ResetAt);

/// <summary>Tạp Hóa bà Diem (GameData.Shop): món cố định + DailyCount món ngẫu nhiên đổi mỗi 12 giờ (0h và 12h UTC+7), chung cho mọi người.</summary>
public partial class GameService
{
    /// <summary>Đợt hàng chứa thời điểm now: bắt đầu lúc 0h hoặc 12h (UTC+7), kéo dài ShopPeriodHours.</summary>
    public const int ShopPeriodHours = 12;
    private static DateTimeOffset ShopPeriodStart(DateTimeOffset now)
    {
        var local = now.ToOffset(SpinDayOffset);
        return new(local.Date.AddHours(local.Hour / ShopPeriodHours * ShopPeriodHours), SpinDayOffset);
    }
    /// <summary>Khóa đợt hàng lưu ở Player.ShopDay: "yyyy-MM-dd" (đợt sáng) hoặc "yyyy-MM-dd-2" (đợt chiều).</summary>
    private static string ShopDayOf(DateTimeOffset now)
    {
        var start = ShopPeriodStart(now);
        return start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + (start.Hour >= ShopPeriodHours ? "-2" : "");
    }

    /// <summary>Giá Tinh Thạch cố định theo cấp 1–9 (cấp 10 không bán): cấp 1 = 50, mỗi cấp ×9 (chi phí hợp 9 viên).</summary>
    public static readonly long[] ShopGemPrices = Enumerable.Range(0, 9).Select(level => 50 * (long)Math.Pow(9, level)).ToArray();
    /// <summary>Linh thảo: giá bán NPC × hệ số này (món khác dùng GameData.Shop.DailyMarkup).</summary>
    public const double ShopHerbMarkup = 30;
    /// <summary>Trứng linh thú: giá bán NPC × hệ số này.</summary>
    public const double ShopEggMarkup = 600;
    /// <summary>Trang bị linh thú: giá bán NPC × hệ số này.</summary>
    public const double ShopPetGearMarkup = 100;
    /// <summary>Đan tiến giai (GameData.PillChain): phần linh thạch của đan phương × hệ số này (mua khỏi cần linh thảo nên đắt hơn tự luyện).</summary>
    public const double ShopPillBrewMarkup = 1.5;
    /// <summary>Đan tiến giai và đan hỗ trợ từ bậc này (Hóa Thần) trở lên: mọi phẩm tính theo đan phương của bản thường,
    /// Hạ · Trung · Thượng = ×ShopPillQualityMult (trước đây Trung / Thượng theo giá NPC, bị chặn 5.000 → chỉ 15.000 linh thạch).</summary>
    public const int ShopPillTierFrom = 5;
    public static readonly double[] ShopPillQualityMult = { 1, 2, 4 };

    /// <summary>(đan gốc, phẩm 0..2) của đan tiến giai hoặc đan hỗ trợ; món khác null.</summary>
    private static (string Pill, int Q)? ShopPillQuality(string id)
    {
        if (Alchemy.Parse(id) is { } advance) return advance;
        foreach (var s in SupportPills.ById.Keys)
            for (var q = 0; q < ShopPillQualityMult.Length; q++)
                if (Alchemy.VariantId(s, q) == id) return (s, q);
        return null;
    }

    /// <summary>Hàng ngày được bán: món giao dịch được ở Chợ và có giá bán NPC. Không bán trang bị, trang phục, rương,
    /// Phá Chướng Đan và Tinh Thạch cấp 10.</summary>
    public static IReadOnlyList<ItemDef> ShopDailyPool { get; } = GameData.AllItems
        .Where(d => d.Slot is null && d.Type is not ("skin" or "chest") && d.Id != GameData.RenameCard && ShopStockable(d) && GameData.SellPrice(d) > 0
            && GameData.BreakPillRealm(d.Id) < 0 && GemData.GemLevel(d.Id) < ShopGemPrices.Length)
        .OrderBy(d => d.Id, StringComparer.Ordinal).ToArray();

    /// <summary>Giá một món hàng ngày: Tinh Thạch theo bảng cấp; đan tiến giai = linh thạch đan phương × 1,5
    /// (từ Hóa Thần, cả đan hỗ trợ: Trung phẩm ×2, Thượng phẩm ×4); trứng = giá NPC × 600;
    /// trang bị linh thú = giá NPC × 100; linh thảo = giá NPC × 30; món khác = giá NPC × DailyMarkup.</summary>
    public static long ShopDailyPrice(Player p, string itemId)
    {
        var level = GemData.GemLevel(itemId);
        if (level >= 0) return ShopGemPrices[level];
        if (ShopPillQuality(itemId) is { } pq && GameData.ItemById[pq.Pill].Tier >= ShopPillTierFrom
            && GameData.Brews.FirstOrDefault(b => b.Out == pq.Pill) is { } pillBrew)
            return (long)Math.Ceiling(pillBrew.In.GetValueOrDefault("linh_thach") * ShopPillBrewMarkup * ShopPillQualityMult[pq.Q]);
        if (GameData.PillChain.Contains(itemId) && GameData.Brews.FirstOrDefault(b => b.Out == itemId) is { } brew)
            return (long)Math.Ceiling(brew.In.GetValueOrDefault("linh_thach") * ShopPillBrewMarkup);
        var d = GameData.ItemById[itemId];
        return (long)Math.Ceiling(GameData.SellPrice(d) * d.Type switch
        {
            "egg" => ShopEggMarkup, "petgear" => ShopPetGearMarkup, "herb" => ShopHerbMarkup, _ => GameData.Shop.DailyMarkup,
        });
    }

    /// <summary>Món hàng của một đợt 12 giờ: gieo theo đợt nên mọi máy chủ / người chơi thấy cùng danh sách (giá tính riêng theo người chơi).</summary>
    /// <param name="roll">Số lần admin làm mới trong đợt (ShopRoll); 0 = danh sách chung.</param>
    public static List<string> ShopDaily(DateTimeOffset now, int roll = 0)
    {
        var start = ShopPeriodStart(now);
        var rng = new Random(unchecked(((start.Year * 10000 + start.Month * 100 + start.Day) * 2 + start.Hour / ShopPeriodHours) + roll * 7919));
        var pool = ShopDailyPool.ToList();
        var picked = new List<string>();
        while (picked.Count < GameData.Shop.DailyCount && pool.Count > 0)
        {
            var i = rng.Next(pool.Count);
            picked.Add(pool[i].Id);
            pool.RemoveAt(i);
        }
        return picked;
    }

    /// <summary>Admin làm mới Tạp Hóa: cờ "shoproll=đợt:lần" trong Player.Admin, chỉ có hiệu lực trong đợt đó và khi đang ở admin mode.</summary>
    private const string ShopRollFlag = "shoproll=";
    private static int ShopRoll(Player p, DateTimeOffset now)
    {
        if (!Player.AdminEnabled) return 0;
        var flag = p.Admin.Split(',').FirstOrDefault(f => f.StartsWith(ShopRollFlag + ShopDayOf(now) + ":"));
        return flag is not null && int.TryParse(flag[(flag.LastIndexOf(':') + 1)..], out var n) ? n : 0;
    }
    /// <summary>Danh sách hàng người chơi đang thấy: danh sách chung, admin đã làm mới thì theo lần làm mới.</summary>
    public static List<string> ShopDailyFor(Player p, DateTimeOffset now) => ShopDaily(now, ShopRoll(p, now));
    /// <summary>Lệnh admin "shopreset": đổi sang mẻ hàng khác của đợt này (chỉ nhân vật admin thấy) và xoá số đã mua trong đợt.</summary>
    private (string msg, string kind) AdminShopReset(Player p, DateTimeOffset now)
    {
        var roll = ShopRoll(p, now) + 1;
        p.Admin = string.Join(',', p.Admin.Split(',', StringSplitOptions.RemoveEmptyEntries).Where(f => !f.StartsWith(ShopRollFlag)).Append($"{ShopRollFlag}{ShopDayOf(now)}:{roll}"));
        p.ShopDay = ""; p.ShopBought = "";
        return Log(p, $"[Admin] Làm mới {GameData.Shop.Name}: mẻ hàng mới (lần {roll} trong đợt này), số đã mua về 0.", "good");
    }

    /// <summary>Số món hàng ngày mỗi người được mua mỗi đợt, theo độ hiếm: Phàm 10 · Linh 7 · Huyền 5 · Địa 3 · Thiên 2 · cao hơn 1.</summary>
    private static readonly int[] ShopLimitByRarity = { 10, 7, 5, 3, 2, 1 };
    public static int ShopDailyLimit(string itemId) =>
        ShopLimitByRarity[Math.Clamp(Array.IndexOf(GameData.RarityOrder, GameData.ItemById[itemId].Rarity), 0, ShopLimitByRarity.Length - 1)];

    /// <summary>Đã mua trong đợt hàng hiện tại: "món:số,món:số" (bản cũ "món" = 1).</summary>
    private static Dictionary<string, int> ShopBoughtToday(Player p, DateTimeOffset now) =>
        p.ShopDay != ShopDayOf(now) ? new() : p.ShopBought.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(':'))
            .GroupBy(x => x[0]).ToDictionary(g => g.Key, g => g.Sum(x => x.Length > 1 && int.TryParse(x[1], out var n) ? Math.Max(0, n) : 1));

    public static ShopDto ShopFor(Player p, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        var bought = ShopBoughtToday(p, now);
        var reset = ShopPeriodStart(now).AddHours(ShopPeriodHours).ToUnixTimeMilliseconds();
        return new(GameData.Shop.Id, GameData.Shop.Name,
            GameData.Shop.Fixed.Select(x => new ShopOfferDto(x.Item, x.Price, false, 0, 0))
                .Concat(ShopDailyFor(p, now).Select(id => new ShopOfferDto(id, ShopDailyPrice(p, id), true, bought.GetValueOrDefault(id), ShopDailyLimit(id)))).ToList(), reset);
    }

    /// <summary>Mua ở Tạp Hóa bằng linh thạch. Món hàng ngày: tối đa ShopDailyLimit mỗi người mỗi đợt 12 giờ.</summary>
    public (string msg, string kind) ShopBuy(Player p, string shopId, string itemId, int n = 1, DateTimeOffset? at = null)
    {
        if (shopId != GameData.Shop.Id) throw new GameException("Không tìm thấy shop.");
        var now = at ?? DateTimeOffset.UtcNow;
        var fixedItem = GameData.Shop.Fixed.FirstOrDefault(i => i.Item == itemId);
        var daily = fixedItem is null && ShopDailyFor(p, now).Contains(itemId);
        if (fixedItem is null && !daily) throw new GameException("Đợt này tạp hóa không bán món này.");
        if (n is < 1 or > 99) throw new GameException("Số lượng không hợp lệ.");
        var bought = ShopBoughtToday(p, now);
        if (daily)
        {
            var left = ShopDailyLimit(itemId) - bought.GetValueOrDefault(itemId);
            if (left <= 0) throw new GameException("Đã mua hết phần món này, đợt hàng sau quay lại nhé.");
            if (n > left) throw new GameException($"Đợt này chỉ còn mua được {left} món này.");
        }
        var cost = (daily ? ShopDailyPrice(p, itemId) : fixedItem!.Price) * n;
        if (p.Count("linh_thach") < cost) throw new GameException($"Cần {cost:N0} linh thạch.");
        if (!CanFit(p, itemId, n)) throw new GameException("Hành trang không đủ chỗ trống.");
        p.Add("linh_thach", -cost);
        p.Add(itemId, n);
        if (daily)
        {
            bought[itemId] = bought.GetValueOrDefault(itemId) + n;
            p.ShopDay = ShopDayOf(now);
            p.ShopBought = string.Join(',', bought.Select(kv => $"{kv.Key}:{kv.Value}"));
        }
        return Log(p, $"{GameData.Shop.Name}: mua {(n > 1 ? $"{n} " : "")}{GameData.ItemById[itemId].Name}: −{cost:N0} linh thạch.", "good");
    }
}
