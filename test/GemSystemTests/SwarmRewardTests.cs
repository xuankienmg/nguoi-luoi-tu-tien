using VanDao.Game;

namespace GemSystemTests;

public class SwarmRewardTests
{
    [Fact]
    public void Pills_follow_monster_points()
    {
        // 100 yêu thường (1đ) + 20 Thiết giáp (3đ) + 3 Boss (25đ) = 235 điểm → 23 Linh Thú Đan
        var kills = new Dictionary<string, int> { ["normal"] = 100, ["armored"] = 20, ["boss"] = 3 };
        Assert.Equal(23, SwarmData.Pills(kills, 600));
    }

    [Fact]
    public void Pills_not_capped_per_claim() =>
        Assert.Equal(1000, SwarmData.Pills(new Dictionary<string, int> { ["normal"] = 10000 }, 600));

    [Theory]
    [InlineData("bomber", 1, 60)]      // Bạo yêu chưa xuất hiện ở đợt 2
    [InlineData("normal", 5000, 60)]   // vượt tốc hạ tối đa theo thời gian sống
    [InlineData("boss", 13, 330)]      // đợt 12 chỉ có tối đa 12 Boss
    [InlineData("khong_co", 1, 60)]
    public void Impossible_results_rejected(string id, int n, int seconds) =>
        Assert.Throws<GameException>(() => SwarmData.Pills(new Dictionary<string, int> { [id] = n }, seconds));
}
