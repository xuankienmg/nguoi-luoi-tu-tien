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
    public async Task PromotionGivesProtectionAndEntersTheNewTier()
    {
        var top = await Join("Top");
        var me = await Join("Me", ArenaTierData.Tiers[1].Min - 10);     // còn 10 điểm nữa lên Bạc
        await Fight(me, top, true);
        var row = await RankRow(me);
        Assert.Equal(1, ArenaTierData.IndexOf(row.Points));
        Assert.Equal(ArenaTierData.ProtectMatches, row.Protect);
        var s = await arena.StateAsync(me);
        Assert.Equal((1L, 1L), (s.Me!.Rank, s.Total));                     // bậc thang Bạc mới chỉ có mình (chỗ đứng theo điểm)
        Assert.Equal(row.Points, s.Tier!.Points);
    }

    [Fact]
    public async Task ProtectedLossesDoNotCostPointsThenTheyDo()
    {
        var top = await Join("Top");
        var me = await Join("Me", ArenaTierData.Tiers[1].Min - 10);
        var silver = await Join("Silver", ArenaTierData.Tiers[1].Min + 100);   // người Bạc xếp trên (vào trước khi mình xuống cuối bậc thang Bạc)
        await Fight(me, top, true);                                         // lên Bạc, bảo vệ 3 trận
        var before = (await RankRow(me)).Points;
        for (var i = 0; i < ArenaTierData.ProtectMatches; i++) await Fight(me, silver, false);
        Assert.Equal(before, (await RankRow(me)).Points);
        Assert.Equal(0, (await RankRow(me)).Protect);
        await Fight(me, silver, false);
        Assert.Equal(before - ArenaTierData.LosePoints, (await RankRow(me)).Points);
    }

    [Fact]
    public async Task OnlySameTierCanBeChallenged()
    {
        var bronze = await Join("Bronze");
        var silver = await Join("Silver", ArenaTierData.Tiers[1].Min);
        var ex = await Assert.ThrowsAsync<GameException>(() => arena.BeginAsync(bronze, silver.Id, GameService.PvpFighterOf(bronze)));
        Assert.Contains("cùng hạng", ex.Message);
        var s = await arena.StateAsync(bronze);
        Assert.DoesNotContain(s.Top.Concat(s.Targets), r => r.PlayerId == silver.Id);   // không hiện người khác hạng
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
