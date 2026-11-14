using Microsoft.Extensions.Logging.Abstractions;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class SwarmRewardTests
{
    [Fact]
    public void All_three_daily_entries_award_pills_once()
    {
        var version = new VersionService(new LeaderboardOptions(), NullLogger<VersionService>.Instance) { UseOwnClock = true };
        var game = new GameService(null!, version: version);
        var player = new Player();

        var kills = new Dictionary<string, int> { ["normal"] = 5_000 };   // đủ điểm để chạm trần mỗi lượt
        foreach (var total in new[] { 1_667, 3_334, 5_000 })
        {
            game.EnterSwarm(player);
            game.ClaimSwarm(player, kills, 600);
            Assert.Equal(total, player.Count(SwarmData.Reward));
            Assert.Throws<GameException>(() => game.ClaimSwarm(player, kills, 600));
        }

        Assert.Throws<GameException>(() => game.EnterSwarm(player));
    }

    [Fact]
    public void Finished_run_claimed_later_with_real_kills_awards_once_and_needs_entry()
    {
        var version = new VersionService(new LeaderboardOptions(), NullLogger<VersionService>.Instance) { UseOwnClock = true };
        var game = new GameService(null!, version: version);
        var player = new Player();
        var kills = new Dictionary<string, int> { ["normal"] = 120, ["swift"] = 40, ["armored"] = 15, ["boss"] = 1 };

        Assert.Throws<GameException>(() => game.ClaimSwarm(player, kills, 120));   // chưa vào bí cảnh
        game.EnterSwarm(player);
        game.ClaimSwarm(player, kills, 120);                                       // nhận ở màn chọn Bí Cảnh sau khi xong ván
        Assert.Equal(120 + 40 + 15 * 2 + 25, player.Count(SwarmData.Reward));     // thưởng theo điểm quái hạ
        Assert.Throws<GameException>(() => game.ClaimSwarm(player, kills, 120));   // không nhận lặp
    }

    [Fact]
    public void Pills_scale_with_kills_and_are_capped_per_entry()
    {
        Assert.Equal(0, SwarmData.PillsForRun(1, new Dictionary<string, int>()));
        Assert.Equal(100, SwarmData.PillsForRun(1, new Dictionary<string, int> { ["normal"] = 100 }));
        Assert.Equal(SwarmData.PillsForEntry(1), SwarmData.PillsForRun(1, new Dictionary<string, int> { ["normal"] = 99_999 }));
    }

    [Theory]
    [InlineData("bomber", 1, 60)]     // Bạo yêu chưa xuất hiện ở đợt 2
    [InlineData("normal", 5000, 60)]   // vượt tốc hạ tối đa theo thời gian sống
    [InlineData("boss", 13, 330)]      // đợt 12 chỉ có tối đa 12 Boss
    [InlineData("khong_co", 1, 60)]
    public void Impossible_results_rejected(string id, int n, int seconds) =>
        Assert.Throws<GameException>(() => SwarmData.ValidateRun(new Dictionary<string, int> { [id] = n }, seconds));
}
