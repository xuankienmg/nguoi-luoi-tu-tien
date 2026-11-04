using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>Quà đăng nhập hằng ngày: mỗi ngày một ô, vòng 7 ngày, bỏ lỡ ngày không mất tiến độ.</summary>
public class DailyLoginTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public void Dispose() => db.Dispose();

    private async Task<(GameService, Player)> Setup()
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db, version: new VersionService(new LeaderboardOptions(), NullLogger<VersionService>.Instance) { UseOwnClock = true });
        var p = await game.CreateAsync("Đạo hữu");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        return (game, p);
    }

    [Fact]
    public async Task ClaimsOncePerDayAndAdvances()
    {
        var (game, p) = await Setup();
        var before = game.DailyLoginStateFor(p);
        Assert.True(before.Online);
        Assert.False(before.ClaimedToday);
        Assert.Equal(0, before.Today);
        Assert.Equal(7, before.Days.Count);

        var tickets = p.Count("ve_tai_xiu");
        game.ClaimDailyLogin(p);
        Assert.Equal(tickets + DailyLoginData.Days[0].First(r => r.Item == "ve_tai_xiu").Qty, p.Count("ve_tai_xiu"));
        Assert.Equal(1, p.LoginStep);
        var after = game.DailyLoginStateFor(p);
        Assert.True(after.ClaimedToday);
        Assert.Equal(0, after.Today);
        Assert.Throws<GameException>(() => game.ClaimDailyLogin(p));

        p.LoginDay = "2000-01-01";   // vắng mặt nhiều ngày: vẫn nhận ô kế tiếp, không reset
        Assert.Equal(1, game.DailyLoginStateFor(p).Today);
        game.ClaimDailyLogin(p);
        Assert.Equal(2, p.LoginStep);
    }

    [Fact]
    public async Task DaySevenWrapsToDayOne()
    {
        var (game, p) = await Setup();
        p.LoginStep = 6; p.LoginDay = "2000-01-01";
        game.ClaimDailyLogin(p);
        Assert.Equal(0, p.LoginStep);
        var s = game.DailyLoginStateFor(p);
        Assert.True(s.ClaimedToday);
        Assert.Equal(6, s.Today);
    }

    [Fact]
    public void RewardsUseKnownItems() =>
        Assert.All(DailyLoginData.Days.SelectMany(d => d), r => Assert.True(GameData.ItemById.ContainsKey(r.Item), r.Item));
}
