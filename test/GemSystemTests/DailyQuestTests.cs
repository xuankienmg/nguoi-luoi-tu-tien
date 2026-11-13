using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>Nhiệm vụ ngày/tuần (Hoạt Lực): đếm việc, cộng điểm tuần một lần, rương mốc, reset ngày/tuần, quà theo cảnh giới.</summary>
public class DailyQuestTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public void Dispose() => db.Dispose();

    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);
    private static readonly DateTimeOffset Wed = new(2026, 11, 11, 10, 0, 0, Vn);   // thứ Tư; thứ Hai của tuần là 09/11

    private async Task<(GameService, Player)> Setup(bool unlocked = true)
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db, version: new VersionService(new LeaderboardOptions(), NullLogger<VersionService>.Instance) { UseOwnClock = true });
        var p = await game.CreateAsync("Đạo hữu");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        if (unlocked) p.NewbieQuestStep = NewbieQuestData.Total;
        return (game, p);
    }

    private static int Progress(Player p, string id, DateTimeOffset at) =>
        GameService.DailyQuestStateAt(p, at)!.Tasks.Single(t => t.Id == id).Progress;

    [Fact]
    public async Task LockedUntilNewbieChainDone()
    {
        var (game, p) = await Setup(unlocked: false);
        GameService.RecordDailyAt(p, DailyQuestData.EvKill, 50, Wed);
        Assert.Equal("", p.DqCounts);
        Assert.Null(GameService.DailyQuestStateAt(p, Wed));
        Assert.Throws<GameException>(() => game.ClaimDqChestAt(p, 0, false, Wed));
    }

    [Fact]
    public async Task CountsCapAndAddWeekPointsOnce()
    {
        var (_, p) = await Setup();
        GameService.RecordDailyAt(p, DailyQuestData.EvKill, 30, Wed);
        Assert.Equal(30, Progress(p, DailyQuestData.EvKill, Wed));
        Assert.Equal(0, p.DqWeekPoints);
        GameService.RecordDailyAt(p, DailyQuestData.EvKill, 30, Wed);
        GameService.RecordDailyAt(p, DailyQuestData.EvKill, 30, Wed);
        Assert.Equal(50, Progress(p, DailyQuestData.EvKill, Wed));
        Assert.Equal(DailyQuestData.TaskById[DailyQuestData.EvKill].Points, p.DqWeekPoints);
        Assert.Equal(15, GameService.DailyQuestStateAt(p, Wed)!.Points);
        GameService.RecordDailyAt(p, "khong_co", 1, Wed);   // sự kiện lạ: bỏ qua
        Assert.Equal(15, GameService.DailyQuestStateAt(p, Wed)!.Points);
    }

    [Fact]
    public async Task NoProgressOrClaimWithoutServerTime()
    {
        var (game, p) = await Setup();
        GameService.RecordDailyAt(p, DailyQuestData.EvBoss, 1, null);
        Assert.Equal("", p.DqCounts);
        Assert.Throws<GameException>(() => game.ClaimDqChestAt(p, 0, false, null));
    }

    [Fact]
    public async Task ClaimDailyChestOnceWhenReached()
    {
        var (game, p) = await Setup();
        Assert.Throws<GameException>(() => game.ClaimDqChestAt(p, 0, false, Wed));   // 0 điểm
        GameService.RecordDailyAt(p, DailyQuestData.EvBoss, 1, Wed);    // 15
        GameService.RecordDailyAt(p, DailyQuestData.EvFeed, 1, Wed);    // 25
        var stones = p.Count("linh_thach");
        var pills = p.Count("bo_nguyen_dan_ha");
        game.ClaimDqChestAt(p, 0, false, Wed);
        Assert.Equal(stones + DailyQuestData.MinStones, p.Count("linh_thach"));   // Luyện Khí: 2% × 200 = 4 → sàn 10
        Assert.Equal(pills + 1, p.Count("bo_nguyen_dan_ha"));
        Assert.Throws<GameException>(() => game.ClaimDqChestAt(p, 0, false, Wed));   // đã nhận
        Assert.Throws<GameException>(() => game.ClaimDqChestAt(p, 1, false, Wed));   // cần 40
        Assert.Throws<GameException>(() => game.ClaimDqChestAt(p, 9, false, Wed));   // rương không có
        Assert.True(GameService.DailyQuestStateAt(p, Wed)!.Chests[0].Claimed);
    }

    [Fact]
    public async Task DayResetKeepsWeekAndMondayResetsWeek()
    {
        var (game, p) = await Setup();
        foreach (var t in DailyQuestData.Tasks) GameService.RecordDailyAt(p, t.Id, t.Need, Wed);
        Assert.Equal(DailyQuestData.MaxDaily, p.DqWeekPoints);
        game.ClaimDqChestAt(p, 4, false, Wed);

        var thu = Wed.AddDays(1);
        var s = GameService.DailyQuestStateAt(p, thu)!;
        Assert.Equal(0, s.Points);
        Assert.False(s.Chests[4].Claimed);
        Assert.Equal(DailyQuestData.MaxDaily, s.WeekPoints);
        GameService.RecordDailyAt(p, DailyQuestData.EvBoss, 1, thu);
        Assert.Equal(1, Progress(p, DailyQuestData.EvBoss, thu));
        Assert.Equal(0, Progress(p, DailyQuestData.EvKill, thu));
        Assert.Equal(DailyQuestData.MaxDaily + 15, p.DqWeekPoints);
        Assert.Throws<GameException>(() => game.ClaimDqChestAt(p, 0, true, thu));   // tuần có 115 + 15 = 130 < 150
    }

    [Fact]
    public async Task WeeklyChestByAccumulatedPointsAndResetsMonday()
    {
        var (game, p) = await Setup();
        foreach (var t in DailyQuestData.Tasks) GameService.RecordDailyAt(p, t.Id, t.Need, Wed);              // 115
        Assert.Equal(115, p.DqWeekPoints);
        Assert.Throws<GameException>(() => game.ClaimDqChestAt(p, 0, true, Wed));                             // cần 150
        foreach (var t in DailyQuestData.Tasks) GameService.RecordDailyAt(p, t.Id, t.Need, Wed.AddDays(1));   // 230
        Assert.Equal(230, p.DqWeekPoints);
        var tickets = p.Count("ve_tai_xiu");
        game.ClaimDqChestAt(p, 0, true, Wed.AddDays(1));
        Assert.Equal(tickets + 3, p.Count("ve_tai_xiu"));
        Assert.Throws<GameException>(() => game.ClaimDqChestAt(p, 0, true, Wed.AddDays(1)));
        Assert.Throws<GameException>(() => game.ClaimDqChestAt(p, 1, true, Wed.AddDays(1)));                  // cần 300

        var nextMon = new DateTimeOffset(2026, 11, 16, 0, 5, 0, Vn);
        var s = GameService.DailyQuestStateAt(p, nextMon)!;
        Assert.Equal(0, s.WeekPoints);
        Assert.False(s.WeekChests[0].Claimed);
        var sun = new DateTimeOffset(2026, 11, 15, 23, 55, 0, Vn);
        Assert.Equal(230, GameService.DailyQuestStateAt(p, sun)!.WeekPoints);
        Assert.Equal(nextMon.Date, DateTimeOffset.FromUnixTimeMilliseconds(GameService.DailyQuestStateAt(p, sun)!.WeekResetAt).ToOffset(Vn).Date);
    }

    [Fact]
    public void RewardsScaleWithRealm()
    {
        var top = DailyQuestData.DailyChests[4];
        var low = DailyQuestData.RewardsFor(top, 0);
        var high = DailyQuestData.RewardsFor(top, 4);
        Assert.Equal("linh_thach", low[0].Item);
        Assert.Equal(12, low[0].Qty);                            // 6% × 200
        Assert.Equal((int)Math.Round(45000 * .06), high[0].Qty);   // Hóa Thần: 6% × 45.000
        Assert.Contains(low, r => r.Item == "bo_nguyen_dan_ha");
        Assert.Contains(high, r => r.Item == "bo_nguyen_dan_thuong");
        Assert.Contains(DailyQuestData.RewardsFor(top, 2), r => r.Item == "bo_nguyen_dan_trung");
        Assert.All(DailyQuestData.DailyChests.Concat(DailyQuestData.WeeklyChests).SelectMany(c => DailyQuestData.RewardsFor(c, GameData.Realms.Count - 1)),
            r => Assert.True(GameData.ItemById.ContainsKey(r.Item), r.Item));
    }

    [Fact]
    public void FinalChestsGivePremiumStones()
    {
        const string premium = "thuong_pham_linh_thach";
        Assert.Contains(DailyQuestData.RewardsFor(DailyQuestData.DailyChests[^1], 0), r => r.Item == premium && r.Qty == 5);
        Assert.Contains(DailyQuestData.RewardsFor(DailyQuestData.WeeklyChests[^1], 0), r => r.Item == premium && r.Qty == 20);
        Assert.DoesNotContain(DailyQuestData.DailyChests[..^1].Concat(DailyQuestData.WeeklyChests[..^1]).SelectMany(c => c.Items), r => r.Item == premium);
    }

    [Fact]
    public async Task ActionsRecordThroughGameService()
    {
        var (game, p) = await Setup();
        game.ClaimDailyLogin(p);
        var s = game.DailyQuestStateFor(p)!;
        Assert.True(s.Online);
        Assert.Equal(1, s.Tasks.Single(t => t.Id == DailyQuestData.EvLogin).Progress);
        Assert.Equal(10, s.Points);
    }

    [Fact]
    public void ProgramMapsClaimRoutes()
    {
        var program = File.ReadAllText(Path.Combine(RepoRoot(), "Program.cs"));
        Assert.Contains("\"/players/{id:guid}/daily-quest/claim\"", program);
        Assert.Contains("\"/players/{id:guid}/weekly-quest/claim\"", program);
    }

    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string file = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", ".."));
}
