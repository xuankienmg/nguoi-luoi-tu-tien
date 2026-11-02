using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class ServerMarketTests : IDisposable
{
    private const string Item = "tu_khi_dan";
    private readonly GameDbContext db;
    private readonly GameService game;

    public ServerMarketTests()
    {
        db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        game = new GameService(db, serverMarket: new ServerMarket(db));
    }

    public void Dispose() => db.Dispose();

    private async Task<Player> AddPlayer(string name, int realm = MarketService.MinRealm, int stones = 1000, int items = 5)
    {
        var p = new Player { Name = name, Realm = realm };
        p.Add("linh_thach", stones);
        p.Add(Item, items);
        db.Players.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    private async Task<T> Act<T>(Player p, Func<Task<T>> action)
    {
        var r = await action();
        await game.SaveAsync(p);
        return r;
    }

    private async Task<long> List(Player seller, int qty = 1, long price = 100)
    {
        await Act(seller, () => game.MarketListAsync(seller, Item, qty, price));
        return db.MarketListings.AsNoTracking().Where(l => l.SellerId == seller.Id).Max(l => l.Id);
    }

    [Fact]
    public async Task ListingMovesItemsIntoEscrow()
    {
        var seller = await AddPlayer("Bán");
        var id = await List(seller, qty: 2, price: 300);
        Assert.Equal(3, seller.Count(Item));
        var l = db.MarketListings.AsNoTracking().Single(x => x.Id == id);
        Assert.Equal(("open", 2, 300L), (l.Status, l.Qty, l.Price));
        Assert.InRange(l.ExpiresAt - l.CreatedAt, TimeSpan.FromDays(MarketService.ListingDays - 0.01), TimeSpan.FromDays(MarketService.ListingDays + 0.01));
        var market = await game.MarketAsync(seller);
        Assert.True(Assert.Single(market.Listings).Mine);
        Assert.Equal("sell", Assert.Single(market.Mine).Role);
    }

    [Fact]
    public async Task BuyerPaysNowAndSellerClaimsOnce()
    {
        var seller = await AddPlayer("Bán");
        var buyer = await AddPlayer("Mua");
        var id = await List(seller, price: 101);

        await Act(buyer, () => game.MarketBuyAsync(buyer, id));
        Assert.Equal(1000 - 101, buyer.Count("linh_thach"));
        Assert.Equal(6, buyer.Count(Item));
        Assert.Equal(1000, seller.Count("linh_thach"));      // người bán chưa đổi gì trong transaction của người mua

        await Act(seller, () => game.MarketClaimAsync(seller));
        Assert.Equal(1000 + ServerMarket.PayoutOf(101), seller.Count("linh_thach"));
        Assert.Equal(95, ServerMarket.PayoutOf(101));         // thuế 5% làm tròn lên
        var (msg, _) = await Act(seller, () => game.MarketClaimAsync(seller));
        Assert.Null(msg);
        Assert.Equal(1095, seller.Count("linh_thach"));
        Assert.Equal("buy", Assert.Single((await game.MarketAsync(buyer)).Mine).Role);
    }

    [Fact]
    public async Task SoldListingCannotBeBoughtAgain()
    {
        var seller = await AddPlayer("Bán");
        var first = await AddPlayer("Một");
        var second = await AddPlayer("Hai");
        var id = await List(seller);
        await Act(first, () => game.MarketBuyAsync(first, id));
        await Assert.ThrowsAsync<GameException>(() => game.MarketBuyAsync(second, id));
        Assert.Equal((1000, 5), (second.Count("linh_thach"), second.Count(Item)));
    }

    [Fact]
    public async Task OwnExpiredAndFlaggedListingsCannotBeBought()
    {
        var seller = await AddPlayer("Bán");
        var buyer = await AddPlayer("Mua");
        var own = await List(seller);
        await Assert.ThrowsAsync<GameException>(() => game.MarketBuyAsync(seller, own));

        var old = await List(seller);
        await db.MarketListings.Where(l => l.Id == old).ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        await Assert.ThrowsAsync<GameException>(() => game.MarketBuyAsync(buyer, old));

        db.LeaderboardEntries.Add(new LeaderboardEntry { PlayerId = seller.Id, Name = seller.Name, Flagged = true });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<GameException>(() => game.MarketBuyAsync(buyer, own));
        Assert.Empty((await game.MarketAsync(buyer)).Listings);
        Assert.Equal((1000, 5), (buyer.Count("linh_thach"), buyer.Count(Item)));
    }

    [Fact]
    public async Task CancelAndExpiryReturnItemsThroughClaim()
    {
        var seller = await AddPlayer("Bán");
        var cancelled = await List(seller, qty: 2);
        var expired = await List(seller, qty: 1);
        Assert.Equal(2, seller.Count(Item));

        await Act(seller, () => game.MarketCancelAsync(seller, cancelled));
        Assert.Equal(4, seller.Count(Item));
        await Assert.ThrowsAsync<GameException>(() => game.MarketCancelAsync(seller, cancelled));

        await db.MarketListings.Where(l => l.Id == expired).ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        Assert.Equal("expired", (await game.MarketAsync(seller)).Mine.Single(m => m.Id == expired).Status);
        await Act(seller, () => game.MarketClaimAsync(seller));
        Assert.Equal(5, seller.Count(Item));
        Assert.Equal("expired", db.MarketListings.AsNoTracking().Single(l => l.Id == expired).Status);
    }

    [Fact]
    public async Task LimitsMatchTheMarketRules()
    {
        var low = await AddPlayer("Luyện Khí", realm: 0);
        await Assert.ThrowsAsync<GameException>(() => game.MarketListAsync(low, Item, 1, 100));

        var seller = await AddPlayer("Bán", items: 9);
        for (var i = 0; i < MarketService.MaxOpen; i++) await List(seller);
        await Assert.ThrowsAsync<GameException>(() => game.MarketListAsync(seller, Item, 1, 100));
        Assert.Equal(9 - MarketService.MaxOpen, seller.Count(Item));

        var busy = await AddPlayer("Bận");
        for (var i = 0; i < MarketService.MaxDaily; i++)
            db.MarketListings.Add(new MarketListing { SellerId = busy.Id, SellerName = busy.Name, ItemId = Item, Qty = 1, Price = 1, Status = "cancelled",
                CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(3), ClosedAt = DateTime.UtcNow, SellerClaimed = true });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<GameException>(() => game.MarketListAsync(busy, Item, 1, 100));
        var other = await List(await AddPlayer("Khác"));
        await Assert.ThrowsAsync<GameException>(() => game.MarketBuyAsync(busy, other));
    }
}
