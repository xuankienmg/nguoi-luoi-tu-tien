using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>Lôi đài giả dùng bảng chung theo điểm như bản online.</summary>
public class ArenaMockTierTests
{
    private static Player NewPlayer() => new() { Id = Guid.NewGuid(), Name = "Thử Hạng", Realm = 3 };

    [Fact]
    public void AllPlayersShareOneBoardSortedByPoints()
    {
        var mock = new ArenaMock(); var p = NewPlayer();
        var state = mock.State(p);
        var rows = mock.TierBoard(p, 0, 999);
        Assert.Equal(state.Total, rows.Count);
        Assert.Equal(GameData.Realms.Count * 3 + 1, state.Total);
        Assert.Equal(state.Total, state.Me!.Rank);
        Assert.Equal(rows.OrderByDescending(r => r.Points).Select(r => r.PlayerId), rows.Select(r => r.PlayerId));
        Assert.Equal(Enumerable.Range(1, rows.Count).Select(i => (long)i), rows.Select(r => r.Rank));
        Assert.Equal(rows.Select(r => r.PlayerId), mock.TierBoard(p, 5, 999).Select(r => r.PlayerId));
        Assert.Equal(rows.Select(r => r.PlayerId), state.Targets.Select(r => r.PlayerId));
    }

    [Fact]
    public void WinningARankedFightAddsPointsAndLosingFromZeroKeepsZero()
    {
        var mock = new ArenaMock(); var p = NewPlayer();
        var s = mock.State(p);
        var foe = s.Targets.Last(r => r.PlayerId != p.Id);                                           // đối thủ liền trên
        var begin = mock.Begin(p, foe.PlayerId);
        Assert.False(begin.Practice);
        var done = mock.Finish(p, begin.Id, win: true);
        Assert.True(done.Ranked);
        Assert.InRange(done.Delta, ArenaTierData.WinBase, ArenaTierData.WinBase + ArenaTierData.WinGapMax);
        Assert.Equal(done.Delta, done.Points);
        Assert.Equal(done.Delta, mock.State(p).Tier!.Points);
        Assert.True(mock.State(p).Tier!.FoughtToday);

        var q = NewPlayer(); var s2 = mock.State(q);
        var b2 = mock.Begin(q, s2.Targets.Last(r => r.PlayerId != q.Id).PlayerId);
        var lost = mock.Finish(q, b2.Id, win: false);
        Assert.Equal(0, lost.Delta);                                       // 0 điểm: không âm
        Assert.Equal(0, lost.Points);
    }

    [Fact]
    public void EveryFightScoresPoints()
    {
        var top = ArenaTierData.Tiers.Length - 1;
        Assert.False(ArenaTierData.IsPractice(top, myRank: 1, foeRank: 5));     // hạng cao nhất: đánh người xếp dưới vẫn tính điểm
        Assert.False(ArenaTierData.IsPractice(top - 1, myRank: 1, foeRank: 5)); // 11/2026: mọi hạng đều tính điểm, không còn tỷ thí
        Assert.False(ArenaTierData.IsPractice(0, myRank: 5, foeRank: 1));       // đánh người xếp trên luôn tính điểm
    }

}
