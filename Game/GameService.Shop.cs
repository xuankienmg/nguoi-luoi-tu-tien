using System.Globalization;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>Bought / Limit: số đã mua hôm nay và giới hạn mỗi ngày (Limit 0 = không giới hạn, món cố định).</summary>
public record ShopOfferDto(string Item, long Price, bool Daily, int Bought, int Limit);
public record ShopDto(string Id, string Name, List<ShopOfferDto> Offers, long ResetAt);

/// <summary>Tạp Hóa bà Diem (GameData.Shop): món cố định + DailyCount món ngẫu nhiên đổi lúc 0h UTC+7, chung cho mọi người.</summary>
public partial class GameService
{
    private static string ShopDayOf(DateTimeOffset now) => now.ToOffset(SpinDayOffset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Giá Tinh Thạch cố định theo cấp 1–9 (cấp 10 không bán): cấp 1 = 50, mỗi cấp ×9 (chi phí hợp 9 viên).</summary>
    public static readonly long[] ShopGemPrices = Enumerable.Range(0, 9).Select(level => 50 * (long)Math.Pow(9, level)).ToArray();
    /// <summary>Trứng linh thú và linh thảo: giá bán NPC × hệ số này (món khác dùng GameData.Shop.DailyMarkup).</summary>
    public const double ShopEggHerbMarkup = 30;

    /// <summary>Hàng ngày được bán: món giao dịch được ở Chợ và có giá bán NPC. Không bán trang bị, trang phục, rương,
    /// Phá Chướng Đan và Tinh Thạch cấp 10.</summary>
    public static IReadOnlyList<ItemDef> ShopDailyPool { get; } = GameData.AllItems
        .Where(d => d.Slot is null && d.Type is not ("skin" or "chest") && d.Id != GameData.RenameCard && Tradable(d) && GameData.SellPrice(d) > 0
            && GameData.BreakPillRealm(d.Id) < 0 && GemData.GemLevel(d.Id) < ShopGemPrices.Length)
        .OrderBy(d => d.Id, StringComparer.Ordinal).ToArray();

    /// <summary>Giá một món hàng ngày: Tinh Thạch theo bảng cấp; trứng / linh thảo = giá NPC × 30; món khác = giá NPC × DailyMarkup.</summary>
    public static long ShopDailyPrice(Player p, string itemId)
    {
        var level = GemData.GemLevel(itemId);
        if (level >= 0) return ShopGemPrices[level];
        var d = GameData.ItemById[itemId];
        return (long)Math.Ceiling(GameData.SellPrice(d) * (d.Type is "egg" or "herb" ? ShopEggHerbMarkup : GameData.Shop.DailyMarkup));
    }

    /// <summary>Món hàng ngày của một ngày: gieo theo ngày nên mọi máy chủ / người chơi thấy cùng danh sách (giá tính riêng theo người chơi).</summary>
    public static List<string> ShopDaily(DateTimeOffset now)
    {
        var day = now.ToOffset(SpinDayOffset).Date;
        var rng = new Random(day.Year * 10000 + day.Month * 100 + day.Day);
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

    /// <summary>Số món hàng ngày mỗi người được mua mỗi ngày, theo độ hiếm: Phàm 10 · Linh 7 · Huyền 5 · Địa 3 · Thiên 2 · cao hơn 1.</summary>
    private static readonly int[] ShopLimitByRarity = { 10, 7, 5, 3, 2, 1 };
    public static int ShopDailyLimit(string itemId) =>
        ShopLimitByRarity[Math.Clamp(Array.IndexOf(GameData.RarityOrder, GameData.ItemById[itemId].Rarity), 0, ShopLimitByRarity.Length - 1)];

    /// <summary>Đã mua hôm nay: "món:số,món:số" (bản cũ "món" = 1).</summary>
    private static Dictionary<string, int> ShopBoughtToday(Player p, DateTimeOffset now) =>
        p.ShopDay != ShopDayOf(now) ? new() : p.ShopBought.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(':'))
            .GroupBy(x => x[0]).ToDictionary(g => g.Key, g => g.Sum(x => x.Length > 1 && int.TryParse(x[1], out var n) ? Math.Max(0, n) : 1));

    public static ShopDto ShopFor(Player p, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        var bought = ShopBoughtToday(p, now);
        var local = now.ToOffset(SpinDayOffset);
        var reset = new DateTimeOffset(local.Date.AddDays(1), SpinDayOffset).ToUnixTimeMilliseconds();
        return new(GameData.Shop.Id, GameData.Shop.Name,
            GameData.Shop.Fixed.Select(x => new ShopOfferDto(x.Item, x.Price, false, 0, 0))
                .Concat(ShopDaily(now).Select(id => new ShopOfferDto(id, ShopDailyPrice(p, id), true, bought.GetValueOrDefault(id), ShopDailyLimit(id)))).ToList(), reset);
    }

    /// <summary>Mua ở Tạp Hóa bằng linh thạch. Món hàng ngày: tối đa ShopDailyLimit mỗi người mỗi ngày.</summary>
    public (string msg, string kind) ShopBuy(Player p, string shopId, string itemId, int n = 1, DateTimeOffset? at = null)
    {
        if (shopId != GameData.Shop.Id) throw new GameException("Không tìm thấy shop.");
        var now = at ?? DateTimeOffset.UtcNow;
        var fixedItem = GameData.Shop.Fixed.FirstOrDefault(i => i.Item == itemId);
        var daily = fixedItem is null && ShopDaily(now).Contains(itemId);
        if (fixedItem is null && !daily) throw new GameException("Hôm nay tạp hóa không bán món này.");
        if (n is < 1 or > 99) throw new GameException("Số lượng không hợp lệ.");
        var bought = ShopBoughtToday(p, now);
        if (daily)
        {
            var left = ShopDailyLimit(itemId) - bought.GetValueOrDefault(itemId);
            if (left <= 0) throw new GameException("Hôm nay đã mua hết phần món này, mai quay lại nhé.");
            if (n > left) throw new GameException($"Hôm nay chỉ còn mua được {left} món này.");
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
