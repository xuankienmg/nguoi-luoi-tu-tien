using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class DonateCodeTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public DonateCodeTests() { db.Database.OpenConnection(); db.Database.EnsureCreated(); }
    public void Dispose() => db.Dispose();
    private static readonly Guid Admin = Guid.NewGuid(), Player1 = Guid.NewGuid(), Player2 = Guid.NewGuid();

    [Fact]
    public async Task CreateMakesAmountFromVnd()
    {
        var store = new DonateStore(db);
        var c = await store.CreateAsync(50_000, "Anh A", Admin);
        Assert.Matches(@"^NLTT-[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}$", c.Code);
        Assert.Equal(50, c.Amount);
        Assert.Equal("Anh A", c.Note);
        Assert.Single(await store.ListAsync());
    }

    [Theory]
    [InlineData(0)] [InlineData(1500)] [InlineData(-1000)] [InlineData(10_001_000)]
    public async Task CreateRejectsBadAmounts(long vnd) =>
        await Assert.ThrowsAsync<GameException>(() => new DonateStore(db).CreateAsync(vnd, "", Admin));

    [Fact]
    public async Task ClaimWorksOnceCaseInsensitive()
    {
        var store = new DonateStore(db);
        var c = await store.CreateAsync(20_000, "", Admin);
        Assert.Equal(20, await store.ClaimAsync(c.Code.ToLowerInvariant(), Player1, null, offline: true));
        Assert.Null(await store.ClaimAsync(c.Code, Player2, null, offline: false));
        var row = await db.DonateCodes.AsNoTracking().SingleAsync();
        Assert.Equal(Player1, row.UsedByPlayer);
        Assert.True(row.Offline);
        Assert.NotNull(row.UsedAt);
        Assert.Null(await store.ClaimAsync("NLTT-XXXX-XXXX", Player1, null, false));
    }

    [Fact]
    public async Task OfflineRetryBySamePlayerGetsAmountAgain()
    {
        var store = new DonateStore(db);
        var c = await store.CreateAsync(20_000, "", Admin);
        Assert.Equal(20, await store.ClaimAsync(c.Code, Player1, null, offline: true));
        Assert.Equal(20, await store.ClaimAsync(c.Code, Player1, null, offline: true));
        Assert.Null(await store.ClaimAsync(c.Code, Player2, null, offline: true));
        Assert.Null(await store.ClaimAsync(c.Code, Player1, null, offline: false));
    }

    [Fact]
    public async Task OnlineUsedCodeIsNotReturnedToOffline()
    {
        var store = new DonateStore(db);
        var c = await store.CreateAsync(20_000, "", Admin);
        Assert.Equal(20, await store.ClaimAsync(c.Code, Player1, null, offline: false));
        Assert.Null(await store.ClaimAsync(c.Code, Player1, null, offline: true));
    }

    [Fact]
    public async Task LongNoteIsTrimmedTo100()
    {
        var c = await new DonateStore(db).CreateAsync(1000, new string('a', 150), Admin);
        Assert.Equal(100, c.Note.Length);
    }

    [Fact]
    public async Task ListIsNewestFirstWithUsedStatus()
    {
        var store = new DonateStore(db);
        var a = await store.CreateAsync(1000, "a", Admin);
        await Task.Delay(20);
        var b = await store.CreateAsync(2000, "b", Admin);
        await store.ClaimAsync(a.Code, Player1, null, false);
        var list = await store.ListAsync();
        Assert.Equal(new[] { b.Code, a.Code }, list.Select(x => x.Code).ToArray());
        Assert.Null(list[0].UsedAt);
        Assert.NotNull(list[1].UsedAt);
        Assert.Equal(Player1, list[1].UsedByPlayer);
    }

    [Fact]
    public async Task ClaimRejectsBlankOrTooLongCodes()
    {
        var store = new DonateStore(db);
        Assert.Null(await store.ClaimAsync(null, Player1, null, false));
        Assert.Null(await store.ClaimAsync("  ", Player1, null, false));
        Assert.Null(await store.ClaimAsync(new string('A', 21), Player1, null, false));
    }

    [Fact]
    public async Task CreateAcceptsBoundaries()
    {
        var store = new DonateStore(db);
        Assert.Equal(1, (await store.CreateAsync(1_000, "", Admin)).Amount);
        Assert.Equal(10_000, (await store.CreateAsync(10_000_000, "", Admin)).Amount);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public HttpRequestMessage? Last;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { Last = r; return Task.FromResult(reply(r)); }
    }

    [Fact]
    public async Task OnlineRedeemAddsPremiumOnce()
    {
        var store = new DonateStore(db);
        var code = (await store.CreateAsync(30_000, "", Admin)).Code;
        var game = new GameService(db, donateStore: store);
        var p = await game.CreateAsync("Online");
        await game.RedeemCodeAsync(p, code);
        Assert.Equal(30, p.Count(GameService.VipCurrency));
        await Assert.ThrowsAsync<GameException>(() => game.RedeemCodeAsync(p, code));
        Assert.Equal(30, p.Count(GameService.VipCurrency));
    }

    [Fact]
    public async Task OfflineRedeemAsksTheBackend()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{\"amount\":15}") });
        var client = new DonateClient(new HttpClient(handler) { BaseAddress = new Uri("https://backend.test/") });
        var game = new GameService(db, donateClient: client);
        var p = await game.CreateAsync("Offline");
        await game.RedeemCodeAsync(p, "nltt-abcd-efgh");
        Assert.Equal(15, p.Count(GameService.VipCurrency));
        Assert.Equal("/api/donate/redeem", handler.Last!.RequestUri!.AbsolutePath);
        // backend trả lại số cũ cho cùng nhân vật: save offline không được cộng lần hai
        handler.Last = null;
        await Assert.ThrowsAsync<GameException>(() => game.RedeemCodeAsync(p, "NLTT-ABCD-EFGH"));
        Assert.Null(handler.Last);
        Assert.Equal(15, p.Count(GameService.VipCurrency));
    }

    [Fact]
    public async Task OfflineRedeemUsedCodeShowsError()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"x\"}") });
        var game = new GameService(db, donateClient: new DonateClient(new HttpClient(handler) { BaseAddress = new Uri("https://backend.test/") }));
        var p = await game.CreateAsync("Offline2");
        var ex = await Assert.ThrowsAsync<GameException>(() => game.RedeemCodeAsync(p, "NLTT-AAAA-BBBB"));
        Assert.Contains("đã được dùng", ex.Message);
        Assert.Equal(0, p.Count(GameService.VipCurrency));
    }

    [Fact]
    public async Task OfflineRedeemNetworkErrorAddsNothing()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("down"));
        var game = new GameService(db, donateClient: new DonateClient(new HttpClient(handler) { BaseAddress = new Uri("https://backend.test/") }));
        var p = await game.CreateAsync("Offline3");
        var ex = await Assert.ThrowsAsync<GameException>(() => game.RedeemCodeAsync(p, "NLTT-AAAA-BBBB"));
        Assert.Contains("Không kết nối", ex.Message);
        Assert.Equal(0, p.Count(GameService.VipCurrency));
    }

    [Fact]
    public async Task OfflineRedeemHtmlBodyShowsInvalidReply()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("<html>proxy</html>", System.Text.Encoding.UTF8, "text/html") });
        var game = new GameService(db, donateClient: new DonateClient(new HttpClient(handler) { BaseAddress = new Uri("https://backend.test/") }));
        var p = await game.CreateAsync("Offline4");
        var ex = await Assert.ThrowsAsync<GameException>(() => game.RedeemCodeAsync(p, "NLTT-AAAA-BBBB"));
        Assert.Contains("không hợp lệ", ex.Message);
        Assert.Equal(0, p.Count(GameService.VipCurrency));
    }

    [Fact]
    public async Task OfflineRedeemRateLimitedShowsWaitMessage()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage((System.Net.HttpStatusCode)429));
        var game = new GameService(db, donateClient: new DonateClient(new HttpClient(handler) { BaseAddress = new Uri("https://backend.test/") }));
        var p = await game.CreateAsync("Offline5");
        var ex = await Assert.ThrowsAsync<GameException>(() => game.RedeemCodeAsync(p, "NLTT-AAAA-BBBB"));
        Assert.Contains("quá nhanh", ex.Message);
    }

    [Fact]
    public async Task OfflineRedeemServerErrorShowsRetryMessage()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
        var game = new GameService(db, donateClient: new DonateClient(new HttpClient(handler) { BaseAddress = new Uri("https://backend.test/") }));
        var p = await game.CreateAsync("Offline6");
        var ex = await Assert.ThrowsAsync<GameException>(() => game.RedeemCodeAsync(p, "NLTT-AAAA-BBBB"));
        Assert.Contains("thử lại sau", ex.Message);
        Assert.Equal(0, p.Count(GameService.VipCurrency));
    }

    [Fact]
    public async Task RedeemWithoutStoreOrClientSaysUnsupported()
    {
        var game = new GameService(db);
        var p = await game.CreateAsync("NoBackend");
        var ex = await Assert.ThrowsAsync<GameException>(() => game.RedeemCodeAsync(p, "NLTT-AAAA-BBBB"));
        Assert.Contains("chưa nhập được code donate", ex.Message);
    }

    [Fact]
    public async Task GiftCodesStillWork()
    {
        var game = new GameService(db);
        var p = await game.CreateAsync("Gift");
        var gift = GameData.GiftCodes.Keys.First();
        await game.RedeemCodeAsync(p, gift);
        Assert.Contains(gift, p.Redeemed);
    }

    [Fact]
    public void IsDonateCodeDetectsPrefix()
    {
        Assert.True(DonateStore.IsDonateCode(" nltt-abcd-efgh "));
        Assert.False(DonateStore.IsDonateCode("NGUOILUOI"));
        Assert.False(DonateStore.IsDonateCode(null));
    }
}
