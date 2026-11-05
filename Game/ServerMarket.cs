using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>
/// Chợ online ở chế độ Server: tin rao nằm trong game."MarketListings", mọi thay đổi chạy trong transaction của
/// OnlineBoundary cùng thay đổi hành trang. Luật giống market_rules() (docs/market/supabase.sql).
/// Mua bằng một UPDATE có điều kiện Status = 'open': người mua thứ hai chờ khóa dòng rồi nhận 0 dòng.
/// Transaction người mua không sửa gì của người bán; tiền đến người bán qua Claim dưới khóa tài khoản của họ.
/// </summary>
public sealed class ServerMarket(GameDbContext db) : IMarketStore
{
    public bool Enabled => true;
    public bool Dev => false;
    private const int NameMax = 24;
    private const string Gone = "Món này đã có người mua, đã hủy hoặc hết hạn.";

    private static string Short(string name) => name.Length <= NameMax ? name : name[..NameMax];
    public static long PayoutOf(long price) => price - (long)Math.Ceiling(price * (decimal)MarketService.TaxPct / 100m);

    /// <summary>Tin của người bị gắn cờ trên bảng xếp hạng bị ẩn và không mua được (như view market_open).</summary>
    private IQueryable<MarketListing> Open(DateTime now) =>
        db.MarketListings.Where(l => l.Status == "open" && l.ExpiresAt > now
            && !db.LeaderboardEntries.Any(e => e.PlayerId == l.SellerId && e.Flagged));

    private static MarketService.OpenRow Row(MarketListing l) => new(l.Id, l.SellerId, l.SellerName, l.ItemId, l.Qty, l.Price, l.ExpiresAt, l.Currency);

    public Task<bool> UnlimitedAsync(Player p) => Task.FromResult(false);   // không có admin ở Server

    public async Task<List<MarketService.OpenRow>> OpenAsync() =>
        (await Open(DateTime.UtcNow).AsNoTracking().OrderByDescending(l => l.CreatedAt).Take(300).ToListAsync()).Select(Row).ToList();

    public async Task<MarketService.OpenRow?> OpenAsync(long id) =>
        await Open(DateTime.UtcNow).AsNoTracking().FirstOrDefaultAsync(l => l.Id == id) is { } l ? Row(l) : null;

    public async Task<List<MarketService.MineRow>> MineAsync(Player p)
    {
        var now = DateTime.UtcNow;
        var since = now.AddDays(-30);
        var rows = await db.MarketListings.AsNoTracking()
            .Where(l => (l.SellerId == p.Id && (l.Status == "open" || l.ClosedAt > since)) || (l.BuyerId == p.Id && l.ClosedAt > since))
            .ToListAsync();
        return rows.OrderByDescending(l => l.Status == "open").ThenByDescending(l => l.ClosedAt ?? l.CreatedAt).Take(60)
            .Select(l => new MarketService.MineRow(l.Id, l.SellerId == p.Id ? "sell" : "buy", l.ItemId, l.Qty, l.Price, l.Payout,
                l.Status == "open" && l.ExpiresAt <= now ? "expired" : l.Status, l.SellerId == p.Id ? l.BuyerName : l.SellerName,
                l.CreatedAt, l.ExpiresAt, l.ClosedAt, l.Currency))
            .ToList();
    }

    /// <summary>Cảnh giới tối thiểu và giới hạn giao dịch 24 giờ (tin rao + lần mua), như market_check_limits.</summary>
    private async Task CheckLimits(Player p, int realm)
    {
        if (realm < MarketService.MinRealm) throw new GameException("Cần đạt Trúc Cơ mới giao dịch ở Chợ.");
        var day = DateTime.UtcNow.AddDays(-1);
        var n = await db.MarketListings.CountAsync(l => (l.SellerId == p.Id && l.CreatedAt > day)
            || (l.BuyerId == p.Id && l.Status == "sold" && l.ClosedAt > day));
        if (n >= MarketService.MaxDaily) throw new GameException($"Đã đạt giới hạn {MarketService.MaxDaily} giao dịch trong 24 giờ.");
    }

    public async Task ListAsync(Player p, int realm, string itemId, int qty, long price, bool unlimited, string currency = MarketService.Stone)
    {
        await CheckLimits(p, realm);
        if (itemId == "" || MarketService.Currencies.Contains(itemId)) throw new GameException("Vật phẩm này không bán được ở Chợ.");
        if (!MarketService.Currencies.Contains(currency)) throw new GameException("Loại tiền tệ không hợp lệ.");
        if (qty is < 1 or > 9999) throw new GameException("Số lượng không hợp lệ.");
        if (price < 1 || price > MarketService.MaxPrice) throw new GameException("Giá không hợp lệ.");
        var now = DateTime.UtcNow;
        if (await db.MarketListings.CountAsync(l => l.SellerId == p.Id && l.Status == "open" && l.ExpiresAt > now) >= MarketService.MaxOpen)
            throw new GameException($"Tối đa {MarketService.MaxOpen} món đang treo bán.");
        db.MarketListings.Add(new MarketListing { SellerId = p.Id, SellerName = Short(p.Name), ItemId = itemId, Qty = qty, Price = price, Currency = currency,
            CreatedAt = now, ExpiresAt = now.AddDays(MarketService.ListingDays) });
    }

    public async Task<List<MarketService.BuyRow>> BuyAsync(Player p, int realm, long listingId)
    {
        await CheckLimits(p, realm);
        var now = DateTime.UtcNow;
        var l = await Open(now).AsNoTracking().FirstOrDefaultAsync(x => x.Id == listingId) ?? throw new GameException(Gone);
        if (l.SellerId == p.Id) throw new GameException(Gone);
        var payout = PayoutOf(l.Price);
        var buyer = Short(p.Name);
        // nguyên tử: điều kiện kiểm tra lại sau khi chờ khóa dòng, nên hai người mua cùng lúc chỉ một người được 1 dòng
        var sold = await db.MarketListings
            .Where(x => x.Id == listingId && x.Status == "open" && x.BuyerId == null && x.ExpiresAt > now && x.SellerId != p.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "sold").SetProperty(x => x.BuyerId, p.Id)
                .SetProperty(x => x.BuyerName, buyer).SetProperty(x => x.ClosedAt, now).SetProperty(x => x.Payout, payout));
        if (sold != 1) throw new GameException(Gone);
        return [new(l.ItemId, l.Qty, l.Price, l.SellerName, l.Id, p.Id, l.Currency)];
    }

    public async Task CancelAsync(Player p, long listingId)
    {
        var n = await db.MarketListings.Where(l => l.Id == listingId && l.SellerId == p.Id && l.Status == "open")
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.Status, "cancelled").SetProperty(l => l.ClosedAt, DateTime.UtcNow));
        if (n != 1) throw new GameException("Tin này đã bán hoặc không còn treo.");
    }

    /// <summary>Có gì để nhận không (tin hết hạn chưa đóng, tin đã đóng chưa nhận): một EXISTS rẻ, khỏi nạp nhân vật mỗi 60 giây.</summary>
    public Task<bool> HasClaimAsync(Guid sellerId)
    {
        var now = DateTime.UtcNow;
        return db.MarketListings.AnyAsync(l => l.SellerId == sellerId
            && ((l.Status == "open" && l.ExpiresAt <= now) || (l.Status != "open" && !l.SellerClaimed)));
    }

    public async Task<List<MarketService.ClaimRow>> ClaimAsync(Player p)
    {
        var now = DateTime.UtcNow;
        await db.MarketListings.Where(l => l.SellerId == p.Id && l.Status == "open" && l.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.Status, "expired").SetProperty(l => l.ClosedAt, now));
        // tin đã đóng không đổi nội dung nữa; chỉ chủ tin (dưới khóa tài khoản) bật SellerClaimed, cùng transaction với linh thạch / đồ trả về
        var rows = await db.MarketListings.AsNoTracking().Where(l => l.SellerId == p.Id && l.Status != "open" && !l.SellerClaimed).ToListAsync();
        if (rows.Count == 0) return [];
        var ids = rows.Select(l => l.Id).ToList();
        await db.MarketListings.Where(l => ids.Contains(l.Id) && !l.SellerClaimed).ExecuteUpdateAsync(s => s.SetProperty(l => l.SellerClaimed, true));
        return rows.Select(l => new MarketService.ClaimRow(l.Id, l.Status, l.ItemId, l.Qty, l.Payout, l.BuyerName, l.Currency)).ToList();
    }
}
