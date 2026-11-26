using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;
using VanDao.Online;

namespace GemSystemTests;

/// <summary>Khiêu chiến Lôi đài trên server: mỗi lệnh SQL là một lượt đi về Supabase (~80 ms), nên một trận phải dùng ít lệnh.
/// SQLite bỏ qua khóa advisory (Postgres thêm đúng 1 lệnh khóa).</summary>
public class ServerArenaRoundTripTests : IDisposable
{
    private readonly GameDbContext db;
    private readonly ServerLeaderboard board;
    private readonly ServerArena arena;

    public ServerArenaRoundTripTests()
    {
        db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:")
            .AddInterceptors(new DbTimingCommandInterceptor()).Options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        board = new ServerLeaderboard(db);
        arena = new ServerArena(db);
    }

    public void Dispose() => db.Dispose();

    private async Task<Player> Join(string name)
    {
        var p = new Player { Name = name };
        db.Players.Add(p);
        await board.StageAsync(p);
        await db.SaveChangesAsync();
        await arena.StateAsync(p);
        return p;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_fight_uses_at_most_five_sql_commands_and_keeps_results(bool win)
    {
        var a = await Join("A");
        var b = await Join("B");
        db.ChangeTracker.Clear();
        db.Timing = new DbTiming();
        var begin = await arena.BeginAsync(b, a.Id, GameService.PvpFighterOf(b));
        var done = await arena.FinishAsync(b, begin.Id, win);
        var sql = db.Timing.Commands;
        db.Timing = null;
        Assert.True(sql <= 5, $"một trận dùng {sql} lệnh SQL");

        var points = win ? ArenaTierData.WinPoints(2, 1) : 0;
        Assert.Equal((2L, win ? 1L : 2L, win ? points : 0, true), (done.OldRank, done.NewRank, done.Delta, done.Ranked));
        var sb = await arena.StateAsync(b);
        Assert.Equal((win ? 1 : 0, win ? 0 : 1, 1, points), (sb.Me!.Wins, sb.Me.Losses, sb.Used, sb.Tier!.Points));
        var sa = await arena.StateAsync(a);
        Assert.Equal((win ? 0 : 1, win ? 1 : 0), (sa.Me!.Wins, sa.Me.Losses));
        var h = Assert.Single(sb.History);
        Assert.Equal((true, "A", win, 2L), (h.Attacker, h.OtherName, h.Win, h.FromRank));
    }

    [Fact]
    public async Task Finishing_twice_or_another_players_fight_is_rejected()
    {
        var a = await Join("A");
        var b = await Join("B");
        var begin = await arena.BeginAsync(b, a.Id, GameService.PvpFighterOf(b));
        await Assert.ThrowsAsync<GameException>(() => arena.FinishAsync(a, begin.Id, true));
        await arena.FinishAsync(b, begin.Id, true);
        await Assert.ThrowsAsync<GameException>(() => arena.FinishAsync(b, begin.Id, true));
    }
}
