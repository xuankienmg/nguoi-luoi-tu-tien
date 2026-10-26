using VanDao.Data;

namespace VanDao.Game;

/// <summary>Chợ giao dịch: kiểm tra phía game, gọi Supabase (MarketService), rồi mới trừ / cộng hành trang.</summary>
public partial class GameService
{
    /// <summary>Linh thạch, chìa khóa và vật liệu linh thú không giao dịch (rương trang phục giao dịch được); hai loại vé quay chỉ admin trong Development được đăng bán.</summary>
    public static bool Tradable(ItemDef d, bool canSellTickets = false) =>
        IsSpinTicket(d.Id) ? canSellTickets : d.Type is not ("stone" or "key" or "ticket" or "petfood" or "petmaterial" or "eventchest");

    public static bool IsSpinTicket(string id) => id is GameData.SpinTicket or GameData.PetSpinTicket;

    public static bool CanSellSpinTickets(Player p, bool development) =>
        development && (p.Tainted || p.Admin.Length > 0 || p.AdminPoints > 0);

    private bool CanSellSpinTickets(Player p) => CanSellSpinTickets(p, _market?.Dev == true);

    /// <summary>Lý do không được mua / bán. Môi trường dev bỏ qua (thử chợ bằng nhân vật admin, cảnh giới thấp).</summary>
    private string? MarketBlocked(Player p) =>
        _market?.Dev == true ? null
        : p.Flagged ? "Dữ liệu nhân vật từng được mở bằng bản game cũ nên không được giao dịch ở Chợ."
        : p.Tainted ? "Nhân vật đã dùng admin mode nên không được giao dịch. Tu lại từ đầu (không bật cờ admin) để vào Chợ."
        : p.Realm < MarketService.MinRealm ? "Cần đạt Trúc Cơ mới mua bán được ở Chợ."
        : null;

    /// <summary>Cảnh giới gửi lên Supabase; dev nâng tối thiểu lên Trúc Cơ để hàm SQL không chặn.</summary>
    private int MarketRealm(Player p) => _market?.Dev == true ? Math.Max(p.Realm, MarketService.MinRealm) : p.Realm;

    private MarketService Market(Player p, bool trading = true)
    {
        if (_market is not { Enabled: true }) throw new GameException("Chợ chưa được cấu hình (appsettings.json → Leaderboard).");
        if (trading && MarketBlocked(p) is { } why) throw new GameException(why);
        return _market;
    }

    private static bool CanFit(Player p, string itemId, int n)
    {
        var free = BagCap(p) - BagUsed(p);
        return GameData.ItemById[itemId].Slot is not null ? free >= n : p.Count(itemId) > 0 || free >= 1;
    }

    private object Who(Player p) => new { p_id = p.Id, p_secret = p.LbSecret };

    public async Task<MarketDto> MarketAsync(Player p)
    {
        var empty = new MarketDto(_market is { Enabled: true }, MarketService.MinRealm, MarketService.TaxPct, MarketService.MaxOpen,
            MarketService.MaxDaily, MarketService.ListingDays, [], [], MarketBlocked(p), CanSellTickets: CanSellSpinTickets(p));
        if (!empty.Enabled) return empty;
        try
        {
            var m = Market(p, trading: false);
            var unlimited = CanSellSpinTickets(p) && await m.Rpc<bool>("market_listing_unlimited", Who(p));
            var open = await m.OpenAsync();
            var mine = await m.Rpc<List<MarketService.MineRow>>("market_mine", Who(p));
            return empty with
            {
                UnlimitedListings = unlimited,
                Listings = open.Where(x => GameData.ItemById.ContainsKey(x.ItemId))
                    .Select(x => new MarketListingDto(x.Id, x.SellerName, x.ItemId, x.Qty, x.Price, x.ExpiresAt, x.SellerId == p.Id)).ToList(),
                Mine = mine.Select(x => new MarketMineDto(x.Id, x.Role, x.ItemId, x.Qty, x.Price, x.Payout, x.Status, x.OtherName, x.CreatedAt, x.ExpiresAt, x.ClosedAt)).ToList(),
            };
        }
        catch (GameException ex) { return empty with { Error = ex.Message }; }
    }

    public async Task<(string msg, string kind)> MarketListAsync(Player p, string itemId, int qty, long price)
    {
        var m = Market(p);
        if (IsSpinTicket(itemId) && !CanSellSpinTickets(p))
            throw new GameException("Vé quay chỉ cho phép nhân vật admin treo bán trong môi trường Development.");
        if (!GameData.ItemById.TryGetValue(itemId, out var d) || !Tradable(d, CanSellSpinTickets(p))) throw new GameException("Vật phẩm này không bán được ở Chợ.");
        if (qty < 1 || p.Count(itemId) < qty) throw new GameException("Không đủ vật phẩm trong hành trang.");
        if (price < 1 || price > MarketService.MaxPrice) throw new GameException("Giá không hợp lệ.");
        await m.Rpc<long>("market_list", new { p_id = p.Id, p_secret = p.LbSecret, p_name = p.Name, p_realm = MarketRealm(p), p_item = itemId, p_qty = qty, p_price = price, p_unlimited = CanSellSpinTickets(p) });
        p.Add(itemId, -qty);     // Supabase đã nhận ký gửi → món rời túi
        return Log(p, $"Chợ: treo bán {(qty > 1 ? qty + " " : "")}{d.Name} giá {price:N0} linh thạch.", "");
    }

    public async Task<(string msg, string kind)> MarketBuyAsync(Player p, long listingId)
    {
        var m = Market(p);
        var l = await m.OpenAsync(listingId) ?? throw new GameException("Món này đã có người mua, đã hủy hoặc hết hạn.");
        if (l.SellerId == p.Id) throw new GameException("Không mua được món của chính mình — hãy hủy tin ở mục Của tôi.");
        if (!GameData.ItemById.TryGetValue(l.ItemId, out var d)) throw new GameException("Vật phẩm này không có trong phiên bản game của bạn.");
        if (p.Count("linh_thach") < l.Price) throw new GameException($"Cần {l.Price:N0} linh thạch.");
        if (!CanFit(p, l.ItemId, l.Qty)) throw new GameException("Hành trang không đủ chỗ trống.");
        var receipts = await m.Rpc<List<MarketService.BuyRow>>("market_buy_v2",
            new { p_id = p.Id, p_secret = p.LbSecret, p_name = p.Name, p_realm = MarketRealm(p), p_listing = listingId });
        if (receipts is not { Count: 1 })
            throw new GameException("Không xác nhận được giao dịch. Hành trang và linh thạch chưa thay đổi.");
        var r = receipts[0];
        // Only an atomic receipt for this exact buyer and listing may grant an item.
        if (r is null || r.ListingId != listingId || r.BuyerId != p.Id || r.ItemId != l.ItemId || r.Qty != l.Qty || r.Price != l.Price
            || r.Qty is < 1 or > 9999 || r.Price is < 1 or > MarketService.MaxPrice)
            throw new GameException("Thông tin giao dịch không khớp. Hành trang và linh thạch chưa thay đổi.");
        p.Add("linh_thach", -(int)r.Price);
        p.Add(r.ItemId, r.Qty);
        return Log(p, $"Chợ: mua {(r.Qty > 1 ? r.Qty + " " : "")}{d.Name} của {r.SellerName}: −{r.Price:N0} linh thạch.", "good");
    }

    public async Task<(string? msg, string? kind)> MarketCancelAsync(Player p, long listingId)
    {
        await Market(p, trading: false).Rpc<bool>("market_cancel", new { p_id = p.Id, p_secret = p.LbSecret, p_listing = listingId });
        var (msg, kind) = await MarketClaimAsync(p);
        return (msg ?? "Đã hủy tin.", kind ?? "");
    }

    /// <summary>Nhận linh thạch của món đã bán và đồ trả về (tin hủy / hết hạn). Không có gì thì trả về (null, null).</summary>
    public async Task<(string? msg, string? kind)> MarketClaimAsync(Player p)
    {
        if (_market is not { Enabled: true } || p.LbSecret.Length == 0) return (null, null);
        var rows = await _market.Rpc<List<MarketService.ClaimRow>>("market_claim", Who(p));
        (string, string)? last = null;
        foreach (var row in rows)
        {
            var r = GameData.ReplaceSpear(row.ItemId) is { } sword ? row with { ItemId = sword } : row;   // Thương đã bỏ → Kiếm cùng phẩm
            var name = GameData.ItemById.TryGetValue(r.ItemId, out var d) ? d.Name : r.ItemId;
            var what = $"{(r.Qty > 1 ? r.Qty + " " : "")}{name}";
            if (r.Status == "sold")
            {
                p.Add("linh_thach", (int)Math.Min(r.Payout, int.MaxValue));
                last = Log(p, $"Chợ: {r.BuyerName} đã mua {what}: +{r.Payout:N0} linh thạch (đã trừ thuế {MarketService.TaxPct:0}%).", "gold");
            }
            else if (d is not null)
            {
                p.Add(r.ItemId, r.Qty);     // trả về túi kể cả khi đầy, để không mất đồ
                last = Log(p, $"Chợ: nhận lại {what} ({(r.Status == "expired" ? "tin hết hạn" : "đã hủy tin")}).", "");
            }
        }
        return last is { } x ? (rows.Count > 1 ? $"{x.Item1} (+{rows.Count - 1} giao dịch khác, xem nhật ký)" : x.Item1, x.Item2) : (null, null);
    }
}
