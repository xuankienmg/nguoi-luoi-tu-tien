using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>Lôi đài giả (dev) phân hạng Đồng → Cao Thủ như bản online: chỉ thấy / thách đấu người cùng hạng, thắng được điểm.</summary>
public class ArenaMockTierTests
{
    private static Player NewPlayer() => new() { Id = Guid.NewGuid(), Name = "Thử Hạng", Realm = 3 };

    [Fact]
    public void PlayerStartsInDongAndOnlySeesOwnTier()
    {
        var mock = new ArenaMock(); var p = NewPlayer();
        var s = mock.State(p);
        Assert.NotNull(s.Tier);
        Assert.Equal(0, ArenaTierData.IndexOf(s.Tier!.Points));
        Assert.All(s.Top.Concat(s.Targets), r => Assert.Equal(0, ArenaTierData.IndexOf(r.Points)));
    }

    [Fact]
    public void FakeOpponentsAreSpreadAcrossTiersSoDongIsASmallLadder()
    {
        // 36 đối thủ giả rải 6 hạng: người chơi (hạng Đồng) chỉ thấy khoảng 1/6 trong số đó, cộng chính mình
        var s = new ArenaMock().State(NewPlayer());
        Assert.InRange(s.Total, 3, 12);
        Assert.True(s.Total < 36);
    }

    [Fact]
    public void ChallengingAnotherTierIsRejected()
    {
        var mock = new ArenaMock(); var p = NewPlayer();
        var s = mock.State(p);
        // một đối thủ giả ở hạng khác: lấy qua hàng thách đấu của chính mình thì luôn cùng hạng; dùng Id giả đã biết (18 đối thủ đầu có Id cố định)
        var other = new Guid(1, 0x4d4f, 0x434b, 0, 0, 0, 0, 0, 0, 0, 1);
        if (s.Targets.All(t => t.PlayerId != other) && s.Top.All(t => t.PlayerId != other))
            Assert.Throws<GameException>(() => mock.Begin(p, other));
    }

    [Fact]
    public void WinningARankedFightAddsPointsAndLosingFromZeroKeepsZero()
    {
        var mock = new ArenaMock(); var p = NewPlayer();
        var s = mock.State(p);
        var foe = s.Top.First();                                           // xếp trên mình, cùng hạng
        var begin = mock.Begin(p, foe.PlayerId);
        Assert.False(begin.Practice);
        var done = mock.Finish(p, begin.Id, win: true);
        Assert.True(done.Ranked);
        Assert.InRange(done.Delta, ArenaTierData.WinBase, ArenaTierData.WinBase + ArenaTierData.WinGapMax);
        Assert.Equal(done.Delta, done.Points);
        Assert.Equal(done.Delta, mock.State(p).Tier!.Points);
        Assert.True(mock.State(p).Tier!.FoughtToday);

        var q = NewPlayer(); var s2 = mock.State(q);
        var b2 = mock.Begin(q, s2.Top.First().PlayerId);
        var lost = mock.Finish(q, b2.Id, win: false);
        Assert.Equal(0, lost.Delta);                                       // 0 điểm: không âm
        Assert.Equal(0, lost.Points);
    }

    [Fact]
    public void EverySameTierFightScoresPoints()
    {
        var top = ArenaTierData.Tiers.Length - 1;
        Assert.False(ArenaTierData.IsPractice(top, myRank: 1, foeRank: 5));     // hạng cao nhất: đánh người xếp dưới vẫn tính điểm
        Assert.False(ArenaTierData.IsPractice(top - 1, myRank: 1, foeRank: 5)); // 11/2026: mọi hạng đều tính điểm, không còn tỷ thí
        Assert.False(ArenaTierData.IsPractice(0, myRank: 5, foeRank: 1));       // đánh người xếp trên luôn tính điểm
    }

    [Fact]
    public void TierBoardListsOnlyThatTierSortedByPoints()
    {
        var mock = new ArenaMock(); var p = NewPlayer();
        for (var t = 0; t < ArenaTierData.Tiers.Length; t++)
        {
            var rows = mock.TierBoard(p, t, 50);
            Assert.All(rows, r => Assert.Equal(t, ArenaTierData.IndexOf(r.Points)));
            Assert.Equal(rows.OrderByDescending(r => r.Points).Select(r => r.PlayerId), rows.Select(r => r.PlayerId));
            Assert.Equal(Enumerable.Range(1, rows.Count).Select(i => (long)i), rows.Select(r => r.Rank));
        }
        Assert.Contains(mock.TierBoard(p, 0, 50), r => r.PlayerId == p.Id);      // người chơi mới ở hạng Đồng
        Assert.True(mock.TierBoard(p, 5, 2).Count <= 2);                          // giới hạn số người
    }
}
