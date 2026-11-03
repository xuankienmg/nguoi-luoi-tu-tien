using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>Chế độ Server đọc database ít nhất có thể ở mỗi lần đồng bộ (mỗi lần đọc đều tính vào Egress của Supabase).</summary>
public class ServerEgressTests : IDisposable
{
    private readonly GameDbContext db;

    public ServerEgressTests()
    {
        db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
    }

    public void Dispose() => db.Dispose();

    private async Task<Player> AddPlayer(string name)
    {
        var p = new Player { Name = name };
        db.Players.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task UnchangedSaveSkipsReadingTheLeaderboardRow()
    {
        var board = new ServerLeaderboard(db, new ServerLeaderboardCache());
        var p = await AddPlayer("Lâm Phong");
        await board.StageAsync(p);
        await db.SaveChangesAsync();

        // xóa dòng thẳng trong database: nếu StageAsync còn đọc lại thì sẽ tạo dòng mới
        await db.LeaderboardEntries.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
        await board.StageAsync(p);
        await db.SaveChangesAsync();
        Assert.Empty(db.LeaderboardEntries.AsNoTracking());

        // có thay đổi thì vẫn ghi như cũ
        p.Name = "Lâm Phong Vũ";
        await board.StageAsync(p);
        await db.SaveChangesAsync();
        Assert.Equal("Lâm Phong Vũ", Assert.Single(db.LeaderboardEntries.AsNoTracking()).Name);
    }

    [Fact]
    public async Task ForcedSaveStillWritesWithTheMemo()
    {
        var board = new ServerLeaderboard(db, new ServerLeaderboardCache());
        var p = await AddPlayer("Vân Y");
        await board.StageAsync(p);
        await db.SaveChangesAsync();
        await db.LeaderboardEntries.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();

        await board.StageAsync(p, force: true);   // Lôi đài cần ảnh chụp mới nhất
        await db.SaveChangesAsync();
        Assert.Single(db.LeaderboardEntries.AsNoTracking());
    }

    [Fact]
    public async Task NewPlayerShowsOnTheCachedBoardRightAway()
    {
        var cache = new ServerLeaderboardCache();
        var board = new ServerLeaderboard(db, cache);
        var first = await AddPlayer("Người đầu");
        await board.StageAsync(first);
        await db.SaveChangesAsync();
        Assert.Single((await board.GetAsync(first, "realm")).Rows);

        var second = await AddPlayer("Người sau");
        await board.StageAsync(second);
        await db.SaveChangesAsync();
        Assert.Equal(2, (await board.GetAsync(second, "realm")).Rows.Count);
    }

    [Fact]
    public async Task LogsAreReusedUntilANewLineArrives()
    {
        var cache = new ServerLogCache();
        var game = new GameService(db, logCache: cache);
        var p = await AddPlayer("Nhật Ký");
        db.Logs.Add(new LogEntry { PlayerId = p.Id, Text = "Dòng một" });
        await db.SaveChangesAsync();
        Assert.Equal("Dòng một", (await game.BuildStateAsync(p)).Logs[0].Text);

        // sửa chữ thẳng trong database mà không thêm dòng: lần sau vẫn dùng danh sách đã đọc
        await db.Logs.ExecuteUpdateAsync(s => s.SetProperty(l => l.Text, "Đã sửa"));
        Assert.Equal("Dòng một", (await game.BuildStateAsync(p)).Logs[0].Text);

        db.Logs.Add(new LogEntry { PlayerId = p.Id, Text = "Dòng hai" });
        await db.SaveChangesAsync();
        var logs = (await game.BuildStateAsync(p)).Logs;
        Assert.Equal(new[] { "Dòng hai", "Đã sửa" }, logs.Select(l => l.Text));
    }
}
