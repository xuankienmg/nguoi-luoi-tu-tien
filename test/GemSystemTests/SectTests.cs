using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

[Collection("SectBuffCache")]   // SectBuff có cache tĩnh: không chạy song song với SectBossTests
public class SectTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    private readonly DateTime now = DateTime.UtcNow;
    private ServerSect Sect => new(db);

    public SectTests() { db.Database.OpenConnection(); db.Database.EnsureCreated(); SectBuff.Clear(); ServerChat.ResetOnlineCacheForTests(); }
    public void Dispose() => db.Dispose();

    private async Task<Guid> NewPlayer(string name, int realm = 1, long stones = 0)
    {
        var p = await new GameService(db).CreateAsync(name);
        p.Realm = realm; p.LastTickAt = now;
        if (stones > 0) p.Add("linh_thach", stones);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return p.Id;
    }

    /// <summary>Tông có chủ là leader; members vào bằng đơn + duyệt.</summary>
    private async Task<(Guid Leader, long SectId)> NewSect(string name = "Thanh Vân", params Guid[] members)
    {
        var leader = await NewPlayer("Chủ " + name);
        await Sect.CreateAsync(leader, name, now);
        var id = (await db.Sects.AsNoTracking().SingleAsync(s => s.Name == name)).Id;
        foreach (var m in members)
        {
            await Sect.ApplyAsync(m, id, now);
            var app = await db.SectApplications.SingleAsync(a => a.PlayerId == m);
            await Sect.DecideAsync(leader, app.Id, true, now);
        }
        return (leader, id);
    }

    private Task<SectMember> Row(Guid id) => db.SectMembers.AsNoTracking().SingleAsync(m => m.PlayerId == id);

    [Fact]
    public async Task CreateChargesStonesNeedsRealmAndUniqueName()
    {
        var game = new GameService(db);
        var low = await NewPlayer("Phàm Nhân", realm: 0, stones: 1_000_000);
        await Assert.ThrowsAsync<GameException>(() => game.ActAsync(low, p => game.SectCreateAsync(p, "Thanh Vân")));
        var poor = await NewPlayer("Nghèo");
        await Assert.ThrowsAsync<GameException>(() => game.ActAsync(poor, p => game.SectCreateAsync(p, "Thanh Vân")));

        var rich = await NewPlayer("Giàu", stones: 1_000_000);
        var cost = SectData.CreateCost(1);
        var (p, _) = await game.ActAsync(rich, p => game.SectCreateAsync(p, "  Thanh   Vân "));
        Assert.InRange(p.Count("linh_thach"), 1_000_000 - cost, 1_000_000 - cost + 1_000);   // tick nhàn rỗi có thể rơi thêm ít linh thạch
        var sect = await db.Sects.AsNoTracking().SingleAsync();
        Assert.Equal("Thanh Vân", sect.Name);
        Assert.Equal(SectData.Leader, (await Row(rich)).Role);

        var other = await NewPlayer("Khác", stones: 1_000_000);
        await Assert.ThrowsAsync<GameException>(() => game.ActAsync(other, p => game.SectCreateAsync(p, "thanh vân")));
        await Assert.ThrowsAsync<GameException>(() => game.ActAsync(other, p => game.SectCreateAsync(p, "A")));
        await Assert.ThrowsAsync<GameException>(() => game.ActAsync(rich, p => game.SectCreateAsync(p, "Tông Hai")));
    }

    [Fact]
    public async Task ApplyThenApproveJoinsAndClearsOtherApplications()
    {
        var a = await NewPlayer("Đệ Tử");
        var (l1, s1) = await NewSect("Tông Một");
        var (_, s2) = await NewSect("Tông Hai");
        await Sect.ApplyAsync(a, s1, now);
        await Sect.ApplyAsync(a, s2, now);
        await Assert.ThrowsAsync<GameException>(() => Sect.ApplyAsync(a, s1, now));   // đơn trùng
        var app = await db.SectApplications.SingleAsync(x => x.PlayerId == a && x.SectId == s1);
        await Sect.DecideAsync(l1, app.Id, true, now);
        var row = await Row(a);
        Assert.Equal(s1, row.SectId);
        Assert.Equal(SectData.Disciple, row.Role);
        Assert.False(await db.SectApplications.AnyAsync(x => x.PlayerId == a));
        var page = await Sect.PageAsync(a, now);
        Assert.Equal(2, page.Members.Count);
        Assert.Contains(await db.ChatMessages.Where(m => m.SectId == s1).Select(m => m.Text).ToListAsync(), t => t.Contains("Đệ Tử gia nhập"));
    }

    [Fact]
    public async Task PendingApplicationsCappedAndRealmChecked()
    {
        var a = await NewPlayer("Tham");
        var ids = new List<long>();
        for (var i = 0; i < SectData.MaxPendingApps + 1; i++) ids.Add((await NewSect($"Tông {i}")).SectId);
        for (var i = 0; i < SectData.MaxPendingApps; i++) await Sect.ApplyAsync(a, ids[i], now);
        await Assert.ThrowsAsync<GameException>(() => Sect.ApplyAsync(a, ids[^1], now));

        var low = await NewPlayer("Phàm", realm: 0);
        await Assert.ThrowsAsync<GameException>(() => Sect.ApplyAsync(low, ids[0], now));
    }

    [Fact]
    public async Task AutoAcceptJoinsImmediatelyAboveMinRealm()
    {
        var (leader, id) = await NewSect();
        await Sect.SettingsAsync(leader, true, 2, now);
        var weak = await NewPlayer("Yếu", realm: 1);
        var strong = await NewPlayer("Mạnh", realm: 2);
        await Assert.ThrowsAsync<GameException>(() => Sect.ApplyAsync(weak, id, now));
        await Sect.ApplyAsync(strong, id, now);
        Assert.Equal(id, (await Row(strong)).SectId);
    }

    [Fact]
    public async Task SectListIsRankedByLevelThenFundThenMembersAndCarriesFund()
    {
        var (_, a) = await NewSect("Tông A");
        var (_, b) = await NewSect("Tông B");
        var (_, c) = await NewSect("Tông C");
        var viewer = await NewPlayer("Khách");
        foreach (var (id, level, fund) in new[] { (a, 2, 100L), (b, 2, 500L), (c, 3, 0L) })
        {
            var s = await db.Sects.SingleAsync(x => x.Id == id);
            s.Level = level; s.Fund = fund;
        }
        await db.SaveChangesAsync();
        var list = (await Sect.PageAsync(viewer, now)).List;
        Assert.Equal(["Tông C", "Tông B", "Tông A"], list.Select(s => s.Name).ToArray());
        Assert.Equal(500, list.Single(s => s.Name == "Tông B").Fund);
    }

    [Fact]
    public async Task LeavingKeepsContributionAndBlocksRejoinFor30Minutes()
    {
        var a = await NewPlayer("Lãng Khách");
        var (_, id) = await NewSect(members: a);
        await Sect.CheckinAsync(a, now);
        await Sect.LeaveAsync(a, now);
        var row = await Row(a);
        Assert.Null(row.SectId);
        Assert.Equal(SectData.CheckinPoints, row.Contribution);
        Assert.True((await Sect.PageAsync(a, now)).Me.RejoinWaitSec > 0);
        await Assert.ThrowsAsync<GameException>(() => Sect.ApplyAsync(a, id, now.AddMinutes(29)));
        await Sect.ApplyAsync(a, id, now.AddMinutes(30));
    }

    [Fact]
    public async Task LeaderMustTransferBeforeLeavingAndAloneLeaderDissolves()
    {
        var a = await NewPlayer("Phó");
        var (leader, id) = await NewSect(members: a);
        await Assert.ThrowsAsync<GameException>(() => Sect.LeaveAsync(leader, now));
        await Sect.TransferAsync(leader, a, now);
        Assert.Equal(a, (await db.Sects.AsNoTracking().SingleAsync()).LeaderId);
        Assert.Equal(SectData.Elder, (await Row(leader)).Role);
        await Sect.LeaveAsync(leader, now);
        await Sect.LeaveAsync(a, now);   // còn một mình → giải tán
        Assert.False(await db.Sects.AsNoTracking().AnyAsync());
        Assert.False(await db.ChatMessages.AnyAsync(m => m.SectId == id));
        Assert.Equal(0, (await Sect.PageAsync(a, now)).Me.RejoinWaitSec);   // giải tán không tính thời gian chờ
    }

    [Fact]
    public async Task RolePermissions()
    {
        var e1 = await NewPlayer("Lão Một");
        var e2 = await NewPlayer("Lão Hai");
        var e3 = await NewPlayer("Lão Ba");
        var d = await NewPlayer("Đệ");
        var (leader, id) = await NewSect(members: [e1, e2, e3, d]);
        await Assert.ThrowsAsync<GameException>(() => Sect.SetRoleAsync(e1, d, SectData.Elder, now));   // chỉ Tông chủ phong chức
        await Sect.SetRoleAsync(leader, e1, SectData.Elder, now);
        await Sect.SetRoleAsync(leader, e2, SectData.Elder, now);
        await Assert.ThrowsAsync<GameException>(() => Sect.SetRoleAsync(leader, e3, SectData.Elder, now));   // tối đa 2
        await Assert.ThrowsAsync<GameException>(() => Sect.KickAsync(e1, e2, now));   // Trưởng lão không đuổi Trưởng lão
        await Assert.ThrowsAsync<GameException>(() => Sect.KickAsync(d, e3, now));    // đệ tử không đuổi ai
        await Assert.ThrowsAsync<GameException>(() => Sect.UpgradeAsync(e1, now));
        await Sect.KickAsync(e1, d, now);
        Assert.Null((await Row(d)).SectId);
        await Sect.KickAsync(leader, e2, now);
        Assert.Null((await Row(e2)).SectId);

        var x = await NewPlayer("Xin");
        await Sect.ApplyAsync(x, id, now);
        var app = await db.SectApplications.SingleAsync();
        await Assert.ThrowsAsync<GameException>(() => Sect.DecideAsync(e3, app.Id, true, now));   // đệ tử không duyệt
        await Sect.DecideAsync(e1, app.Id, true, now);
        Assert.Equal(id, (await Row(x)).SectId);
    }

    [Fact]
    public async Task AwayLeaderIsReplacedByActiveElder()
    {
        var d = await NewPlayer("Chăm");
        var e = await NewPlayer("Lão");
        var (leader, _) = await NewSect(members: [d, e]);
        await Sect.SetRoleAsync(leader, e, SectData.Elder, now);
        await Sect.DonateAsync(d, 2, now);   // đệ tử cống hiến nhiều hơn nhưng Trưởng lão vẫn được ưu tiên
        await db.Players.IgnoreQueryFilters().Where(p => p.Id == leader)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastTickAt, now.AddDays(-SectData.LeaderAwayDays - 1)));
        var page = await Sect.PageAsync(d, now);
        Assert.Equal(e, (await db.Sects.AsNoTracking().SingleAsync()).LeaderId);
        Assert.Equal(SectData.Leader, (await Row(e)).Role);
        Assert.Equal(SectData.Disciple, (await Row(leader)).Role);
        Assert.Equal("Lão", page.Sect!.LeaderName);
    }

    [Fact]
    public async Task CheckinOncePerDayAndDonateThreeTimesPerDay()
    {
        var (leader, id) = await NewSect();
        await Sect.CheckinAsync(leader, now);
        await Assert.ThrowsAsync<GameException>(() => Sect.CheckinAsync(leader, now));
        await Sect.CheckinAsync(leader, now.AddDays(1));
        for (var i = 0; i < SectData.DonatesPerDay; i++) await Sect.DonateAsync(leader, 0, now);
        await Assert.ThrowsAsync<GameException>(() => Sect.DonateAsync(leader, 0, now));
        await Sect.DonateAsync(leader, 1, now.AddDays(1));
        var expected = 2 * SectData.CheckinPoints + SectData.DonatesPerDay * SectData.Donates[0].Points + SectData.Donates[1].Points;
        Assert.Equal(expected, (await Row(leader)).Contribution);
        Assert.Equal(expected, (await db.Sects.AsNoTracking().SingleAsync(s => s.Id == id)).Fund);
    }

    [Fact]
    public async Task DonateChargesStonesThroughGameService()
    {
        var game = new GameService(db);
        var a = await NewPlayer("Hào Phóng", stones: 1_000_000);
        await game.ActAsync(a, p => game.SectCreateAsync(p, "Kim Ngân"));
        var before = (await game.LoadAsync(a)).Count("linh_thach");
        var (p, _) = await game.ActAsync(a, p => game.SectDonateAsync(p, 2));
        Assert.InRange(p.Count("linh_thach"), before - SectData.DonateCost(2, 1), before - SectData.DonateCost(2, 1) + 1_000);
        Assert.Equal(SectData.Donates[2].Points, (await Row(a)).Contribution);
    }

    [Fact]
    public async Task UpgradeSpendsFundAndRaisesQiBuff()
    {
        var (leader, id) = await NewSect();
        await Assert.ThrowsAsync<GameException>(() => Sect.UpgradeAsync(leader, now));
        await db.Sects.Where(s => s.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Fund, 600));
        await Sect.UpgradeAsync(leader, now);
        var sect = await db.Sects.AsNoTracking().SingleAsync();
        Assert.Equal(2, sect.Level);
        Assert.Equal(100, sect.Fund);
        await SectBuff.EnsureAsync(db, leader, now);
        Assert.Equal(SectData.QiPct(2), SectBuff.QiPct(leader));
    }

    [Fact]
    public async Task BuffAppliesToCultivationRate()
    {
        var game = new GameService(db);
        var a = await NewPlayer("Tu");
        await db.Players.Where(p => p.Id == a).ExecuteUpdateAsync(s => s.SetProperty(p => p.StarterChosen, true));
        var before = (await game.BuildStateAsync(await game.LoadAsync(a))).Rate;
        db.ChangeTracker.Clear();
        await Sect.CreateAsync(a, "Tụ Linh", now);
        SectBuff.Forget(a);
        var after = (await game.BuildStateAsync(await game.LoadAsync(a))).Rate;
        Assert.True(before > 0);
        Assert.True(after > before);   // buff cộng chung nhóm % với trang bị/công pháp, không nhân riêng
    }

    [Fact]
    public async Task ShopRespectsLevelWeeklyLimitAndContribution()
    {
        var game = new GameService(db);
        var (leader, id) = await NewSect();
        await db.SectMembers.Where(m => m.PlayerId == leader).ExecuteUpdateAsync(s => s.SetProperty(x => x.Contribution, 10_000));
        await Assert.ThrowsAsync<GameException>(() => Sect.BuyAsync(leader, GameData.SpinTicket, 1, now));   // cần cấp 2
        await Assert.ThrowsAsync<GameException>(() => Sect.BuyAsync(leader, "linh_thach", 1, now));        // không bán
        var item = SectData.ShopById["bo_nguyen_dan_ha"];
        var (p, _) = await game.ActAsync(leader, p => game.SectBuyAsync(p, item.ItemId, item.WeeklyLimit));
        Assert.True(p.Count(item.ItemId) >= item.WeeklyLimit);
        Assert.Equal(10_000 - item.Price * item.WeeklyLimit, (await Row(leader)).Contribution);
        await Assert.ThrowsAsync<GameException>(() => Sect.BuyAsync(leader, item.ItemId, 1, DateTime.UtcNow));
        await Sect.BuyAsync(leader, item.ItemId, 1, DateTime.UtcNow.AddDays(7));   // tuần mới

        await db.SectMembers.Where(m => m.PlayerId == leader).ExecuteUpdateAsync(s => s.SetProperty(x => x.Contribution, 0));
        await Assert.ThrowsAsync<GameException>(() => Sect.BuyAsync(leader, "linh_thu_dan", 1, DateTime.UtcNow.AddDays(7)));
    }

    [Fact]
    public async Task ShopSellsLuyenHuPassiveBooksFromLevel4()
    {
        var books = SectData.LuyenHuBooks.ToList();
        Assert.Equal(7, books.Count);
        Assert.All(books, b => Assert.Equal(4, SectData.ShopById[b].MinLevel));
        var game = new GameService(db);
        var (leader, id) = await NewSect();
        await db.SectMembers.Where(m => m.PlayerId == leader).ExecuteUpdateAsync(s => s.SetProperty(x => x.Contribution, 10_000));
        await Assert.ThrowsAsync<GameException>(() => Sect.BuyAsync(leader, books[0], 1, now));   // tông cấp 1
        await db.Sects.Where(s => s.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Level, 4));
        var (p, _) = await game.ActAsync(leader, p => game.SectBuyAsync(p, books[0], 1));
        Assert.Equal(1, p.Count(books[0]));
    }

    [Fact]
    public async Task SectChatIsSeparateFromWorldChat()
    {
        var a = await NewPlayer("Đồng Môn");
        var outsider = await NewPlayer("Người Ngoài");
        var (leader, _) = await NewSect(members: a);
        await Sect.ChatPostAsync(a, "bí mật tông môn", 0, now);
        var mine = await Sect.ChatPageAsync(leader, 0, now);
        Assert.Contains(mine.Messages, m => m.Text == "bí mật tông môn");
        Assert.False((await Sect.ChatPageAsync(outsider, 0, now)).Enabled);
        await Assert.ThrowsAsync<GameException>(() => Sect.ChatPostAsync(outsider, "hi", 0, now));
        var world = await new ServerChat(db).PageAsync(outsider, 0, now);
        Assert.DoesNotContain(world.Messages, m => m.Text == "bí mật tông môn" || m.Text.Contains("gia nhập"));
    }
}
