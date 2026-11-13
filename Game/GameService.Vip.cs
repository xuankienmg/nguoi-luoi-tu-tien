using System.Globalization;
using System.Text.RegularExpressions;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>Qty: số nhận mỗi lần mua (đổi linh thạch: theo cảnh giới). Choices: linh thảo được chọn (nhóm Linh thảo). Owned: đã mở gói.</summary>
public record VipOfferDto(string Item, int Price, long Qty, string Group, List<string>? Choices = null, bool Owned = false);
public record VipShopDto(string Name, List<VipOfferDto> Offers, decimal Balance);

/// <summary>Tạp Hóa VIP: trả bằng Cực Phẩm Linh Thạch, danh sách cố định, không giới hạn số lượng (riêng Gói Tốc Hành mua một lần).</summary>
public partial class GameService
{
    public const string VipCurrency = "thuong_pham_linh_thach";
    public const string VipStoneOffer = "linh_thach";
    public const string VipHerbOffer = "vip_herb";
    public const string VipPackOffer = "goi_toc_hanh";
    public const string VipPetPackOffer = "goi_linh_thu";
    public const string VipShopName = "Tạp Hóa VIP";

    private static readonly int[] VipGemPrice = { 10, 20, 30, 50 };   // Tinh Thạch cấp 7–10
    /// <summary>Bí kíp bị động theo cảnh giới Luyện Hư → Kim Tiên (giá gốc trước khi giảm 50%: 40 · 60 · 80 · 120 · 160 · 200 → bán 20 · 30 · 40 · 60 · 80 · 100 Cực Phẩm).</summary>
    private static readonly int[] VipBookPrice = { 40, 60, 80, 120, 160, 200 };   // + Kim Tiên 200 (11/2026)
    /// <summary>Nội dung Gói Tốc Hành (ngoài mở thám hiểm ×2).</summary>
    public static readonly (string Item, int Qty)[] VipPackItems = [("bo_nguyen_dan_trung", 10), ("bo_nguyen_dan_thuong", 10)];
    /// <summary>Nội dung Gói Linh Thú (mua bao nhiêu lần cũng được).</summary>
    public static readonly (string Item, int Qty)[] VipPetPackItems = [("thu_dan_thuong_co", 2), ("linh_thu_dan", 1000)];

    private static readonly (string Item, int Price, long Qty, string Group)[] VipFixedHead =
    [
        (GameData.SpinTicket, 1, 1, "Vé quay"), (GameData.PetSpinTicket, 1, 1, "Vé quay"),
        .. GameData.AllItems.Where(d => d.Type == "skin").Select(d => (d.Id, 10, 1, "Trang phục")),
        .. Enumerable.Range(0, VipGemPrice.Length).SelectMany(i => GemData.GemsAtLevel(6 + i).Select(g => (g, VipGemPrice[i], 1, "Tinh Thạch"))),
        .. PassiveBooks.Items.Select(d => (d.Id, VipBookPrice[PassiveBooks.SkillOf(d.Id)!.MinRealm - 5], 1, "Bí kíp bị động")),
        ("thu_dan_thien", 30, 1, "Linh thú"), ("thu_dan_thuong_co", 50, 1, "Linh thú"),
        (VipHerbOffer, 1, 1, "Linh thảo"),
        (GameData.RenameCard, 20, 1, "Tiện ích VIP"),
        (SupportPills.TuLinh + "_thuong", 3, 1, "Tiện ích VIP"), (SupportPills.BeQuan + "_thuong", 3, 1, "Tiện ích VIP"),
    ];

    /// <summary>Danh sách cố định: món, giá Cực Phẩm, số nhận mỗi lần, nhóm (Gói đặc biệt đứng đầu). Chỉ số linh thạch đổi được phụ thuộc cảnh giới.</summary>
    private static IEnumerable<(string Item, int Price, long Qty, string Group)> VipCatalog(Player p) =>
        VipFixedHead.Prepend((VipPetPackOffer, 100, 1, "Gói đặc biệt")).Prepend((VipPackOffer, 100, 1, "Gói đặc biệt")).Append((VipStoneOffer, 1, (long)Math.Min(int.MaxValue, 20 * KyDuyen.StoneUnit(p.Realm)), "Tiện ích VIP"))
            .Select(VipHalfPrice);

    /// <summary>cân bằng 11/2026: giảm 50% toàn Tạp Hóa VIP. Giá ≥ 2: làm tròn lên (3 → 2); giá 1 không chia được nên giữ giá, nhân đôi số nhận.</summary>
    private static (string Item, int Price, long Qty, string Group) VipHalfPrice((string Item, int Price, long Qty, string Group) o) =>
        o.Price >= 2 ? (o.Item, (o.Price + 1) / 2, o.Qty, o.Group) : (o.Item, o.Price, o.Qty * 2, o.Group);

    /// <summary>Bậc linh thảo (thang 1–5 của linh thảo gốc), tính một lần. Linh thảo gốc: N trong mô tả "Linh thảo bậc N".
    /// Linh thảo bổ sung (HerbExpansion): với mỗi đan dùng nó, lấy bậc cao nhất của linh thảo gốc trong cùng đan phương (GameData.Brews),
    /// rồi lấy giá trị thấp nhất qua các đan. Không đan phương nào có linh thảo gốc thì lấy chương rơi sớm nhất + 1, giới hạn 1–5.</summary>
    public static readonly IReadOnlyDictionary<string, int> HerbTiers = BuildHerbTiers();

    private static Dictionary<string, int> BuildHerbTiers()
    {
        var tiers = new Dictionary<string, int>();
        foreach (var d in GameData.AllItems.Where(d => d.Type == "herb"))
            if (d.Use is { } use && Regex.Match(use, @"Linh thảo bậc (\d+)") is { Success: true } m)
                tiers[d.Id] = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var baseTiers = new Dictionary<string, int>(tiers);
        foreach (var d in HerbExpansion.Items)
        {
            var byRecipe = HerbExpansion.RecipeHerbs.Where(kv => kv.Value.Herb == d.Id)
                .SelectMany(kv => GameData.Brews.Where(b => b.Out == kv.Key))
                .Select(b => b.In.Keys.Where(baseTiers.ContainsKey).Select(k => baseTiers[k]).DefaultIfEmpty(0).Max())
                .Where(t => t > 0).ToList();
            var byDrop = HerbExpansion.Drops.Where(x => x.Item == d.Id).SelectMany(x => x.Chapters).Select(c => c + 1).ToList();
            tiers[d.Id] = byRecipe.Count > 0 ? byRecipe.Min() : Math.Clamp(byDrop.Count > 0 ? byDrop.Min() : 1, 1, 5);
        }
        return tiers;
    }

    /// <summary>Bậc linh thảo; 0 = không phải linh thảo.</summary>
    public static int HerbTier(string id) => HerbTiers.GetValueOrDefault(id);

    private static readonly int MaxHerbTier = HerbTiers.Values.Max();

    /// <summary>Linh thảo đổi được: bậc ≤ bậc của cảnh giới + 1 (Luyện Khí, Trúc Cơ = 1; Kim Đan = 2 …).</summary>
    public static List<string> VipHerbs(Player p)
    {
        var tier = Math.Min(Math.Max(1, p.Realm), MaxHerbTier) + 1;
        return GameData.AllItems.Where(d => HerbTier(d.Id) is > 0 and var t && t <= tier).Select(d => d.Id).ToList();
    }

    public static List<VipOfferDto> VipOffersFor(Player p) =>
        VipCatalog(p).Select(o => new VipOfferDto(o.Item, o.Price, o.Qty, o.Group,
            o.Item == VipHerbOffer ? VipHerbs(p) : null, o.Item == VipPackOffer && p.Speed2)).ToList();

    public static VipShopDto VipFor(Player p) => new(VipShopName, VipOffersFor(p), p.Count(VipCurrency));

    /// <summary>Mua ở Tạp Hóa VIP. itemId: mã món, hoặc mã linh thảo muốn đổi (nhóm Linh thảo). Số lượng chỉ giới hạn bởi Cực Phẩm đang có.</summary>
    public (string msg, string kind) VipBuy(Player p, string itemId, int n = 1)
    {
        if (n < 1) throw new GameException("Số lượng không hợp lệ.");
        if (itemId == VipHerbOffer) throw new GameException("Chọn linh thảo muốn đổi.");
        var offerId = VipHerbs(p).Contains(itemId) ? VipHerbOffer : itemId;
        var offer = VipCatalog(p).Where(o => o.Item == offerId).Select(o => ((string Item, int Price, long Qty, string Group)?)o).FirstOrDefault()
            ?? throw new GameException(HerbTier(itemId) > 0 ? "Linh thảo này vượt bậc được đổi ở cảnh giới hiện tại." : "Tạp Hóa VIP không bán món này.");
        if (offerId == VipPackOffer)
        {
            if (p.Speed2) throw new GameException("Đạo hữu đã mở Gói Tốc Hành rồi.");
            n = 1;
        }
        var cost = (long)offer.Price * n;
        if (p.Count(VipCurrency) < cost) throw new GameException($"Cần {cost:N0} Cực Phẩm Linh Thạch.");
        var get = offerId == VipStoneOffer ? "linh_thach" : itemId;
        var qty = (decimal)offer.Qty * n;
        var pack = offerId == VipPackOffer ? VipPackItems : offerId == VipPetPackOffer ? VipPetPackItems : null;
        if (pack is not null)
        {
            var newSlots = pack.Count(x => !IsCurrency(x.Item) && p.Count(x.Item) == 0);
            if (BagUsed(p) + newSlots > BagCap(p)) throw new GameException("Hành trang không đủ chỗ trống.");
        }
        else if (!GemData.IsGemOrStone(get) && !CanFit(p, get, (int)Math.Min(qty, int.MaxValue))) throw new GameException("Hành trang không đủ chỗ trống.");

        p.Add(VipCurrency, -cost);
        string got;
        if (offerId == VipPackOffer)
        {
            p.Speed2 = true; p.Speed2On = true;
            foreach (var (item, q) in VipPackItems) p.Add(item, q);
            got = "Gói Tốc Hành: mở thám hiểm ×2, " + string.Join(", ", VipPackItems.Select(x => $"+{x.Qty} {GameData.ItemById[x.Item].Name}"));
        }
        else if (offerId == VipPetPackOffer)
        {
            foreach (var (item, q) in VipPetPackItems) p.Add(item, (decimal)q * n);
            got = $"Gói Linh Thú ×{n:N0}: " + string.Join(", ", VipPetPackItems.Select(x => $"+{(decimal)x.Qty * n:N0} {GameData.ItemById[x.Item].Name}"));
        }
        else
        {
            p.Add(get, qty);
            got = $"+{qty:N0} {GameData.ItemById[get].Name}";
        }
        return Log(p, $"{VipShopName}: {got} (−{cost:N0} Cực Phẩm).", "good");
    }
}
