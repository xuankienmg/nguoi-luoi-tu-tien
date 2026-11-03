using VanDao.Data;

namespace VanDao.Game;

/// <summary>Chợ giao dịch: kiểm tra phía game, gọi nơi giữ tin rao (IMarketStore: Supabase hoặc schema game), rồi mới trừ / cộng hành trang.</summary>
public partial class GameService
{
    /// <summary>Mọi vật phẩm đều đăng bán được ở Chợ, trừ Linh thạch; hai loại vé quay và Thượng phẩm linh thạch chỉ admin trong Development được đăng bán.</summary>
    public static bool Tradable(ItemDef d, bool canSellTickets = false) =>
        AdminOnlyTrade(d.Id) ? canSellTickets : d.Id != "linh_thach";

    /// <summary>Vật phẩm chỉ admin được treo bán ở Chợ (CanSellSpinTickets).</summary>
    public static bool AdminOnlyTrade(string id) => IsSpinTicket(id) || id == "thuong_pham_linh_thach";

    /// <summary>Hàng Tạp Hóa bà Diem: không nhập tiền tệ, chìa khóa, vé, vật liệu linh thú hay rương sự kiện (luật cũ của Chợ).</summary>
    public static bool ShopStockable(ItemDef d) =>
        !IsSpinTicket(d.Id) && d.Type is not ("stone" or "key" or "ticket" or "petfood" or "petmaterial" or "eventchest");

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

    private IMarketStore Market(Player p, bool trading = true)
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

    public async Task<MarketDto> MarketAsync(Player p)
    {
        var empty = new MarketDto(_market is { Enabled: true }, MarketService.MinRealm, MarketService.TaxPct, MarketService.MaxOpen,
            MarketService.MaxDaily, MarketService.ListingDays, [], [], MarketBlocked(p), CanSellTickets: CanSellSpinTickets(p), Shop: ShopFor(p), Vip: VipFor(p));
        if (!empty.Enabled) return empty;
        try
        {
            var m = Market(p, trading: false);
            var unlimited = CanSellSpinTickets(p) && await m.UnlimitedAsync(p);
            var open = await m.OpenAsync();
            var mine = await m.MineAsync(p);
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
        if (AdminOnlyTrade(itemId) && !CanSellSpinTickets(p))
            throw new GameException("Vé quay và Thượng phẩm linh thạch chỉ cho phép nhân vật admin treo bán trong môi trường Development.");
        if (!GameData.ItemById.TryGetValue(itemId, out var d) || !Tradable(d, CanSellSpinTickets(p))) throw new GameException("Vật phẩm này không bán được ở Chợ.");
        if (qty < 1 || p.Count(itemId) < qty) throw new GameException("Không đủ vật phẩm trong hành trang.");
        if (price < 1 || price > MarketService.MaxPrice) throw new GameException("Giá không hợp lệ.");
        await m.ListAsync(p, MarketRealm(p), itemId, qty, price, CanSellSpinTickets(p));
        p.Add(itemId, -qty);     // chợ đã nhận ký gửi → món rời túi
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
        var receipts = await m.BuyAsync(p, MarketRealm(p), listingId);
        if (receipts is not { Count: 1 })
            throw new GameException("Không xác nhận được giao dịch. Hành trang và linh thạch chưa thay đổi.");
        var r = receipts[0];
        // Only an atomic receipt for this exact buyer and listing may grant an item.
        if (r is null || r.ListingId != listingId || r.BuyerId != p.Id || r.ItemId != l.ItemId || r.Qty != l.Qty || r.Price != l.Price
            || r.Qty is < 1 or > 9999 || r.Price is < 1 or > MarketService.MaxPrice)
            throw new GameException("Thông tin giao dịch không khớp. Hành trang và linh thạch chưa thay đổi.");
        p.Add("linh_thach", -r.Price);
        p.Add(r.ItemId, r.Qty);
        return Log(p, $"Chợ: mua {(r.Qty > 1 ? r.Qty + " " : "")}{d.Name} của {r.SellerName}: −{r.Price:N0} linh thạch.", "good");
    }

    public async Task<(string? msg, string? kind)> MarketCancelAsync(Player p, long listingId)
    {
        await Market(p, trading: false).CancelAsync(p, listingId);
        var (msg, kind) = await MarketClaimAsync(p);
        return (msg ?? "Đã hủy tin.", kind ?? "");
    }

    /// <summary>Nhận linh thạch của món đã bán và đồ trả về (tin hủy / hết hạn). Không có gì thì trả về (null, null).</summary>
    public async Task<(string? msg, string? kind)> MarketClaimAsync(Player p)
    {
        if (_market is not { Enabled: true }) return (null, null);
        var rows = await _market.ClaimAsync(p);
        (string, string)? last = null;
        foreach (var row in rows)
        {
            var r = GameData.ReplaceSpear(row.ItemId) is { } sword ? row with { ItemId = sword } : row;   // Thương đã bỏ → Kiếm cùng phẩm
            var name = GameData.ItemById.TryGetValue(r.ItemId, out var d) ? d.Name : r.ItemId;
            var what = $"{(r.Qty > 1 ? r.Qty + " " : "")}{name}";
            if (r.Status == "sold")
            {
                p.Add("linh_thach", r.Payout);
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
