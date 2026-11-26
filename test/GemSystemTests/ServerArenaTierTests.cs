using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>Hạng Lôi đài online: điểm thắng / thua, lên hạng + bảo vệ, bậc thang riêng từng hạng, thưởng ngày.</summary>
public class ServerArenaTierTests : IDisposable
{
    private readonly GameDbContext db;
    private readonly ServerLeaderboard board;
    private readonly ServerArena arena;

    public ServerArenaTierTests()
    {
        db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        board = new ServerLeaderboard(db);
        arena = new ServerArena(db);
    }

    public void Dispose() => db.Dispose();

    private async Task<Player> Join(string name, int points = 0)
    {
        var p = new Player { Name = name };
        db.Players.Add(p);
        await board.StageAsync(p);
        await db.SaveChangesAsync();
        await arena.StateAsync(p);
        if (points > 0) await db.ArenaRanks.Where(a => a.PlayerId == p.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Points, points));
        return p;
    }

    private async Task Fight(Player attacker, Player defender, bool win)
    {
        var b = await arena.BeginAsync(attacker, defender.Id, GameService.PvpFighterOf(attacker));
        await arena.FinishAsync(attacker, b.Id, win);
        await db.ArenaFights.ExecuteUpdateAsync(s => s.SetProperty(f => f.CreatedAt, f => f.CreatedAt.AddMinutes(-1)));   // bỏ thời gian nghỉ
    }

    private Task<ArenaRank> RankRow(Player p) => db.ArenaRanks.AsNoTracking().FirstAsync(a => a.PlayerId == p.Id);

    /// <summary>Cân bằng 11/2026: thắng người xếp dưới −2 điểm mỗi hạng chênh, sàn 5; người xếp trên giữ 25 + chênh (tối đa 40).</summary>
    [Theory]
    [InlineData(10, 10, 25)]
    [InlineData(10, 9, 26)]
    [InlineData(30, 1, 40)]
    [InlineData(10, 11, 23)]
    [InlineData(10, 12, 21)]
    [InlineData(10, 15, 15)]
    [InlineData(10, 18, 9)]
    [InlineData(10, 20, 5)]
    [InlineData(1, 5000, 5)]
    public void WinPointsShrinkAgainstLowerRanks(long me, long foe, int points) => Assert.Equal(points, ArenaTierData.WinPoints(me, foe));

    [Fact]
    public async Task WinningUpwardGivesBasePlusGapPoints()
    {
        var top = await Join("Top"); var mid = await Join("Mid"); var low = await Join("Low");
        await Fight(low, top, true);                       // thấp nhất (#3) thắng người #1: chênh 2 hạng
        Assert.Equal(ArenaTierData.WinBase + 2, (await RankRow(low)).Points);
        Assert.Equal(0, (await RankRow(top)).Points);      // người bị thách không đổi điểm
    }

    [Fact]
    public async Task LosingCostsPointsButNeverBelowZero()
    {
        var top = await Join("Top"); var me = await Join("Me", 10);
        await Fight(me, top, false);
        Assert.Equal(0, (await RankRow(me)).Points);       // 10 − 15 → chặn ở 0
        await Fight(me, top, false);
        Assert.Equal(0, (await RankRow(me)).Points);
    }

    [Fact]
    public async Task CrossingOldThresholdKeepsEveryoneOnOneLadderWithoutProtection()
    {
        var top = await Join("Top", 1000);
        var me = await Join("Me", 90);
        await Fight(me, top, true);
        var row = await RankRow(me);
        Assert.Equal(116, row.Points);
        Assert.Equal(0, row.Protect);
        var state = await arena.StateAsync(me);
        Assert.Equal(2, state.Total);
        Assert.Equal(2, state.Me!.Rank);
        await Fight(me, top, false);
        Assert.Equal(101, (await RankRow(me)).Points);
    }

    [Fact]
    public async Task OldProtectionDoesNotPreventPointLoss()
    {
        var top = await Join("Top", 1000);
        var me = await Join("Me", 250);
        await db.ArenaRanks.Where(a => a.PlayerId == me.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Protect, 3));
        await Fight(me, top, false);
        Assert.Equal(235, (await RankRow(me)).Points);
        Assert.Equal(0, (await RankRow(me)).Protect);
    }

    [Fact]
    public async Task GlobalBoardIncludesAllPointsAndUsesJoinOrderForTies()
    {
        var bronze = await Join("Bronze");
        var silver = await Join("Silver", 100);
        var gold = await Join("Gold", 1000);
        var tied = await Join("Tied", 100);
        var state = await arena.StateAsync(bronze);
        Assert.Equal(4, state.Total);
        Assert.Equal(4, state.Me!.Rank);
        var rows = await arena.TierBoardAsync(bronze, 0, 50);
        Assert.Equal(new[] { gold.Id, silver.Id, tied.Id, bronze.Id }, rows.Select(r => r.PlayerId));
        Assert.Equal(new long[] { 1, 2, 3, 4 }, rows.Select(r => r.Rank));
        await Fight(bronze, gold, true);
        Assert.Equal(28, (await RankRow(bronze)).Points);
    }

    [Fact]
    public async Task LosingDropsGlobalRankWhenPointsFallBelowAnotherPlayer()
    {
        var top = await Join("Top", 1000);
        var me = await Join("Me", 100);
        var other = await Join("Other", 90);
        Assert.Equal(2, (await arena.StateAsync(me)).Me!.Rank);
        await Fight(me, top, false);
        Assert.Equal(85, (await RankRow(me)).Points);
        Assert.Equal(3, (await arena.StateAsync(me)).Me!.Rank);
        Assert.Equal(2, (await arena.StateAsync(other)).Me!.Rank);
    }

    [Fact]
    public async Task DailyClaimNeedsARealFightAndWorksOnce()
    {
        var top = await Join("Top"); var me = await Join("Me");
        await Assert.ThrowsAsync<GameException>(() => arena.ClaimCheckAsync(me, false));   // chưa đánh trận thật
        await Fight(me, top, false);
        Assert.Equal(0, await arena.ClaimCheckAsync(me, false));
        await arena.ClaimMarkAsync(me, false);
        await Assert.ThrowsAsync<GameException>(() => arena.ClaimCheckAsync(me, false));   // nhận rồi
    }

    [Fact]
    public void TierThresholdsAndRewardsAreConsistent()
    {
        Assert.Equal(0, ArenaTierData.IndexOf(0));
        Assert.Equal(0, ArenaTierData.IndexOf(ArenaTierData.Tiers[1].Min - 1));
        Assert.Equal(1, ArenaTierData.IndexOf(ArenaTierData.Tiers[1].Min));
        Assert.Equal(5, ArenaTierData.IndexOf(99999));
        for (var t = 0; t < ArenaTierData.Tiers.Length; t++)
            foreach (var weekly in new[] { false, true })
                Assert.All(ArenaTierData.RewardsFor(t, 3, weekly), r => { Assert.True(GameData.ItemById.ContainsKey(r.Item), r.Item); Assert.True(r.Qty > 0); });
    }
}
