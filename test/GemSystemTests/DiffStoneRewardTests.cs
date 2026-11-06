using VanDao.Game;
using Xunit;

namespace GemSystemTests;

/// <summary>Cân bằng 11/2026: linh thạch rơi ở mức khó cao dùng DiffStoneReward, thấp hơn DiffReward.</summary>
public class DiffStoneRewardTests
{
    private static double Trip(int d, int ci, int area)
    {
        var c = GameData.ChapterFor(d, ci);
        return GameData.AreaWaves(c, area, d).Sum(w => { var (lo, hi) = GameData.StoneRange(c, area, w.Tier, d); return (lo + hi) / 2.0; });
    }

    [Fact]
    public void StoneMultiplierNeverAboveRewardMultiplier()
    {
        Assert.Equal(GameData.DiffReward.Length, GameData.DiffStoneReward.Length);
        Assert.Equal(1, GameData.DiffStoneReward[0]);
        for (var d = 1; d < GameData.DiffStoneReward.Length; d++)
        {
            Assert.True(GameData.DiffStoneReward[d] <= GameData.DiffReward[d]);
            Assert.True(GameData.DiffStoneReward[d] > GameData.DiffStoneReward[d - 1]);
        }
    }

    [Fact]
    public void HigherDifficultyStillPaysMoreAtSameChapter()
    {
        for (var ci = 0; ci < GameData.OpenChapters; ci++)
            for (var d = 1; d < GameData.DiffNames.Length; d++)
                Assert.True(Trip(d, ci, GameData.AreasFor(d)) > Trip(d - 1, ci, GameData.AreasFor(d - 1)), $"mức {d} chương {ci + 1}");
        // Khó chương 1 không thấp hơn Thường chương cuối.
        Assert.True(Trip(1, 0, GameData.AreasFor(1)) >= Trip(0, GameData.OpenChapters - 1, GameData.AreasFor(0)));
    }

    [Fact]
    public void HardChapterOneStoneMatchesNewMultiplier()
    {
        // Khó chương 1, ải 15: 3.060 → 2.448 (×1,5 → ×1,2).
        Assert.InRange(Trip(1, 0, GameData.AreasFor(1)), 2440, 2456);
    }
}
