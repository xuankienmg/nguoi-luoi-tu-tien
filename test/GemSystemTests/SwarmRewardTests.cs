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

        foreach (var total in new[] { 1_667, 3_334, 5_000 })
        {
            game.EnterSwarm(player);
            game.ClaimSwarm(player, [], 0);
            Assert.Equal(total, player.Count(SwarmData.Reward));
            Assert.Throws<GameException>(() => game.ClaimSwarm(player, [], 0));
        }

        Assert.Throws<GameException>(() => game.EnterSwarm(player));
    }

    [Theory]
    [InlineData("bomber", 1, 60)]      // Bạo yêu chưa xuất hiện ở đợt 2
    [InlineData("normal", 5000, 60)]   // vượt tốc hạ tối đa theo thời gian sống
    [InlineData("boss", 13, 330)]      // đợt 12 chỉ có tối đa 12 Boss
    [InlineData("khong_co", 1, 60)]
    public void Impossible_results_rejected(string id, int n, int seconds) =>
        Assert.Throws<GameException>(() => SwarmData.ValidateRun(new Dictionary<string, int> { [id] = n }, seconds));
}
