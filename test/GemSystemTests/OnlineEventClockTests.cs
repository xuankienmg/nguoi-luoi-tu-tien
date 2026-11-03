using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanDao.Data;
using VanDao.Game;
using VanDao.Online;

namespace GemSystemTests;

/// <summary>Máy chủ online không đọc app_config (Supabase tắt ở chế độ Server), nên giờ sự kiện phải lấy từ đồng hồ máy chủ;
/// nếu không mọi tài khoản không phải admin đều thấy "Mất kết nối máy chủ" ở Kỳ duyên và vòng quay sự kiện.</summary>
public class OnlineEventClockTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public void Dispose() => db.Dispose();

    private static VersionService Version(string mode) =>
        new(new LeaderboardOptions(), NullLogger<VersionService>.Instance, null, new OnlineOptions { Mode = mode });

    [Fact]
    public void OnlineServerUsesItsOwnClock()
    {
        var now = Version("Server").ServerNow;
        Assert.NotNull(now);
        Assert.InRange(now!.Value, DateTimeOffset.UtcNow.AddSeconds(-5), DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Fact]
    public void OfflineWithoutServerContactStaysOffline() => Assert.Null(Version("Offline").ServerNow);

    [Fact]
    public async Task PlayersOnTheOnlineServerSeeEventsOnline()
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db, version: Version("Server"));
        var p = await game.CreateAsync("Đạo hữu");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        Assert.True(game.KyDuyenStateFor(p).Online);
        Assert.True(game.EventStateFor(p).Online);
    }
}
