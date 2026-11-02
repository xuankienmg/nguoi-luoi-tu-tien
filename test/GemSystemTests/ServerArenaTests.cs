using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class ServerArenaTests : IDisposable
{
    private readonly GameDbContext db;
    private readonly ServerLeaderboard board;
    private readonly ServerArena arena;

    public ServerArenaTests()
    {
        db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        board = new ServerLeaderboard(db);
        arena = new ServerArena(db);
    }

    public void Dispose() => db.Dispose();

    /// <summary>Nhân vật có trên bảng xếp hạng (kèm ảnh chụp kỹ năng) và đã vào Lôi đài.</summary>
    private async Task<Player> Join(string name)
    {
        var p = new Player { Name = name };
        db.Players.Add(p);
        await board.StageAsync(p);
        await db.SaveChangesAsync();
        await arena.StateAsync(p);
        return p;
    }

    private async Task<long> RankOf(Player p) => (await arena.StateAsync(p)).Me!.Rank;

    private async Task Fight(Player attacker, Player defender, bool win)
    {
        var b = await arena.BeginAsync(attacker, defender.Id, GameService.PvpFighterOf(attacker));
        await arena.FinishAsync(attacker, b.Id, win);
    }

    /// <summary>Bỏ thời gian nghỉ giữa hai trận: lùi giờ các trận đã đánh.</summary>
    private Task Rest() => db.ArenaFights.ExecuteUpdateAsync(s => s.SetProperty(f => f.CreatedAt, f => f.CreatedAt.AddMinutes(-1)));

    [Fact]
    public async Task NewcomersJoinAtTheBottom()
    {
        var a = await Join("A");
        var b = await Join("B");
        Assert.Equal(1, await RankOf(a));
        var s = await arena.StateAsync(b);
        Assert.Equal((2L, 2L), (s.Me!.Rank, s.Total));
        Assert.Equal(ArenaService.Daily, s.Daily);
        Assert.Equal(0, s.Used);
    }

    [Fact]
    public async Task WinningUpwardSwapsAndCountsBothSides()
    {
        var a = await Join("A");
        var b = await Join("B");
        await Fight(b, a, win: true);
        Assert.Equal((1L, 2L), (await RankOf(b), await RankOf(a)));
        var sa = await arena.StateAsync(a);
        Assert.Equal((0, 1), (sa.Me!.Wins, sa.Me.Losses));
        var h = Assert.Single(sa.History);
        Assert.Equal((false, "B", false, 1L, 2L), (h.Attacker, h.OtherName, h.Win, h.FromRank, h.ToRank));
        var sb = await arena.StateAsync(b);
        Assert.Equal((1, 0, 1), (sb.Me!.Wins, sb.Me.Losses, sb.Used));
        Assert.Equal((true, true, 2L, 1L), (sb.History[0].Attacker, sb.History[0].Win, sb.History[0].FromRank, sb.History[0].ToRank));
        Assert.Equal(["arena"], (await board.TitlesAsync())[b.Id].Where(t => t.Id == "arena").Select(t => t.Id));
    }

    [Fact]
    public async Task LosingKeepsTheRank()
    {
        var a = await Join("A");
        var b = await Join("B");
        await Fight(b, a, win: false);
        Assert.Equal((1L, 2L), (await RankOf(a), await RankOf(b)));
        Assert.Equal(1, (await arena.StateAsync(a)).Me!.Wins);
    }

    [Fact]
    public async Task FightingDownwardIsPractice()
    {
        var a = await Join("A");
        var b = await Join("B");
        var begin = await arena.BeginAsync(a, b.Id, GameService.PvpFighterOf(a));
        Assert.True(begin.Practice);
        await arena.FinishAsync(a, begin.Id, true);
        var s = await arena.StateAsync(a);
        Assert.Equal((1L, 0, 0), (s.Me!.Rank, s.Me.Wins, s.Me.Losses));
        Assert.True(Assert.Single(s.History).Practice);
    }

    [Fact]
    public async Task TargetsAboveTheWindowAreRefused()
    {
        var players = new List<Player>();
        for (var i = 0; i < 8; i++) players.Add(await Join($"P{i}"));
        Assert.Equal(5, ServerArena.Window(8));
        await Assert.ThrowsAsync<GameException>(() => arena.BeginAsync(players[7], players[1].Id, GameService.PvpFighterOf(players[7])));
        var ok = await arena.BeginAsync(players[7], players[2].Id, GameService.PvpFighterOf(players[7]));
        Assert.False(ok.Practice);
    }

    [Fact]
    public async Task DailyLimitAndCooldownAreServerSide()
    {
        var a = await Join("A");
        var b = await Join("B");
        await Fight(b, a, win: false);
        await Assert.ThrowsAsync<GameException>(() => arena.BeginAsync(b, a.Id, GameService.PvpFighterOf(b)));   // còn điều tức
        await Rest();
        for (var i = 1; i < ArenaService.Daily; i++)
            db.ArenaFights.Add(new ArenaFight { AttackerId = b.Id, DefenderId = a.Id, Status = "done", CreatedAt = DateTime.UtcNow.AddSeconds(-30) });
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<GameException>(() => arena.BeginAsync(b, a.Id, GameService.PvpFighterOf(b)));
        Assert.Contains("lượt", ex.Message);
    }

    [Fact]
    public void VietnamDayStartsAtSeventeenUtc()
    {
        Assert.Equal(new DateTime(2026, 10, 30, 17, 0, 0), ServerArena.DayStart(new DateTime(2026, 10, 30, 18, 0, 0)));
        Assert.Equal(new DateTime(2026, 10, 29, 17, 0, 0), ServerArena.DayStart(new DateTime(2026, 10, 30, 16, 59, 0)));
    }

    [Fact]
    public async Task FlaggedPlayersCannotEnterAndAreHidden()
    {
        var a = await Join("A");
        var cheat = await Join("Cờ");
        await db.LeaderboardEntries.Where(e => e.PlayerId == cheat.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.Flagged, true));
        await Assert.ThrowsAsync<GameException>(() => arena.StateAsync(cheat));
        var s = await arena.StateAsync(a);
        Assert.Equal(1, s.Total);
        await Assert.ThrowsAsync<GameException>(() => arena.BeginAsync(a, cheat.Id, GameService.PvpFighterOf(a)));
    }

    [Fact]
    public async Task OpponentFightsWithTheirSavedLoadout()
    {
        var a = await Join("A");
        var b = await Join("B");
        var begin = await arena.BeginAsync(b, a.Id, GameService.PvpFighterOf(b));
        Assert.Equal("A", begin.FoeLoadout!.Value.GetProperty("Name").GetString());
        Assert.True(begin.FoeStats!.ContainsKey("atk"));
    }

    [Fact]
    public async Task GameServiceRunsTheWholeFightOnTheServer()
    {
        var game = new GameService(db, board: board, serverArena: arena);
        var a = await Join("A");
        var b = await Join("B");
        var (_, _, fight) = await game.ArenaFightAsync(b, a.Id);
        await game.SaveAsync(b);
        var f = Assert.Single(db.ArenaFights.AsNoTracking());
        Assert.Equal(("done", fight.Win), (f.Status, f.Win));
        Assert.Equal(fight.Win ? 1L : 2L, await RankOf(b));
        var dto = await game.ArenaAsync(b);
        Assert.True(dto.Enabled);
        Assert.Null(dto.Error);
    }
}
