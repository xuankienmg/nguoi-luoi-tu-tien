using System.Reflection;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;
using Xunit;
using Xunit.Abstractions;

namespace GemSystemTests;

/// <summary>Mức Thiên Giới chạy qua GameService thật (SQLite trong RAM): mở mức, chọn tầng, bản đồ, tick thám hiểm.</summary>
public class EndlessFlowTests(ITestOutputHelper output) : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public void Dispose() => db.Dispose();

    private async Task<(GameService Game, Player P)> Setup(bool unlocked)
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Vô tận");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Realm = 10; p.Stage = GameData.Realms[10].Stages.Length - 1;
        p.ExploreReadyAt = DateTime.UtcNow.AddHours(1);
        if (unlocked) p.Cleared = string.Join(";", Enumerable.Range(0, 5).SelectMany(d => Enumerable.Range(0, 10).Select(c => $"{d}.{c}")).Where(x => x != "5.0"));
        return (game, p);
    }

    private static void Tick(GameService game, Player p, DateTime now) =>
        typeof(GameService).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(game, [p, now]);

    [Fact]
    public async Task LockedUntilHellTwoChapterTenBossIsDown()
    {
        var (game, p) = await Setup(false);
        Assert.Throws<GameException>(() => game.SelectDiff(p, EndlessData.Diff));
        p.Cleared = "4.9";
        game.SelectDiff(p, EndlessData.Diff);
        Assert.Equal(EndlessData.Diff, p.Diff);
        Assert.Equal(0, p.Chapter);
        Assert.Equal(1, p.Area);
    }

    [Fact]
    public async Task MapStateShowsFloorsWithoutBuildingThreeHundredAreas()
    {
        var (game, p) = await Setup(true);
        game.SelectDiff(p, EndlessData.Diff);
        p.DiffProg = "5.0=23";
        game.SelectArea(p, 22);
        var map = (await game.BuildStateAsync(p)).Map;
        Assert.Equal(EndlessData.Diff, map.Diff);
        Assert.Equal(22, map.Area);
        Assert.Equal(23, map.AreaMax);
        Assert.Single(map.Areas);
        var e = map.Endless!;
        Assert.Equal((22, 23, EndlessData.MaxFloor, 30), (e.Floor, e.Top, e.MaxFloor, e.NextBoss));
        Assert.Contains(e.Preview, a => a.Index == 23 && a.Open);
        Assert.Contains(e.Preview, a => a.Index == 30 && !a.Open && a.Waves!.Single().Tier == "boss");
        Assert.Throws<GameException>(() => game.SelectArea(p, 24));   // chưa mở
        Assert.Throws<GameException>(() => game.SelectArea(p, 0));
        var st = await game.BuildStateAsync(p);
        output.WriteLine($"hero power={GameService.Power(p):E2}; " + string.Join(" | ", e.Preview.Select(a => $"{a.Index}: win={a.Win} hits={a.Hits} {a.Monster} hp={a.Waves![0].Hp:E2} atk={a.Waves[0].Atk:E2}")));
    }

    [Fact]
    public async Task PreviewFollowsSelectedLowerFloor()
    {
        var (game, p) = await Setup(true);
        game.SelectDiff(p, EndlessData.Diff);
        p.DiffProg = "5.0=40";
        game.SelectArea(p, 5);
        var e = (await game.BuildStateAsync(p)).Map.Endless!;
        Assert.Equal(new[] { 3, 4, 5, 6, 7, 40 }, e.Preview.Select(a => a.Index).ToArray());   // quanh tầng đang chọn + tầng cao nhất (cũng là Boss kế)
        game.SelectArea(p, 1);
        e = (await game.BuildStateAsync(p)).Map.Endless!;
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 40 }, e.Preview.Select(a => a.Index).ToArray());
    }

    [Fact]
    public async Task TickKeepsProgressAndNeverTouchesOtherDifficulties()
    {
        var (game, p) = await Setup(true);
        game.SelectDiff(p, EndlessData.Diff);
        p.DiffProg = "5.0=25";
        game.SelectArea(p, 25);
        var before = p.Cleared;
        p.ExploreReadyAt = DateTime.UtcNow.AddMinutes(-30);
        p.LastTickAt = DateTime.UtcNow.AddMinutes(-30);
        Tick(game, p, DateTime.UtcNow);
        Assert.Equal(before, p.Cleared);                          // Thiên Giới không đánh dấu "đã hạ Boss" chương nào
        Assert.Equal(EndlessData.Diff, p.Diff);
        Assert.Equal(0, p.Chapter);
        Assert.InRange(p.Area, 1, 26);
        var unlocked = int.Parse(p.DiffProg.Split(';').Single(x => x.StartsWith("5.0=")).Split('=')[1]);
        Assert.InRange(unlocked, 25, 26);
        output.WriteLine($"area={p.Area} unlocked={unlocked} logs={string.Join(" // ", p.Logs.Select(l => l.Text).TakeLast(3))}");
    }
}
