using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class ChestGearQualityTests
{
    [Fact]
    public void OnlyBossChestsCanAwardImmortalGearAcrossRealmsAndDifficulties()
    {
        var divine = Array.IndexOf(GameData.RarityOrder, "divine");
        var immortal = Array.IndexOf(GameData.RarityOrder, "immortal");
        foreach (var chest in GameData.Chests.Keys)
        for (var realm = 0; realm < GameData.Realms.Count; realm++)
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        {
            var table = ChestRewards.Table(realm, GameData.ChestId(chest, diff));
            var gear = table.Where(reward => reward.Group == "gear").ToArray();
            var max = chest == GameData.BossChest ? immortal : divine;
            Assert.NotEmpty(gear);
            Assert.All(gear, reward =>
                Assert.InRange(Array.IndexOf(GameData.RarityOrder, GameData.ItemById[reward.Item].Rarity), 0, max));
            Assert.Equal(1, table.Sum(reward => reward.Probability), 8);
        }

        Assert.Contains(ChestRewards.Table(7, GameData.BossChest),
            reward => reward.Group == "gear" && GameData.ItemById[reward.Item].Rarity == "immortal");
    }

    [Fact]
    public void EventChestAwardsDivineGear()
    {
        var player = new Player();
        player.Add(SpinEvent.ChestItem, 1);

        var result = new GameService(null!).UseItem(player, SpinEvent.ChestItem);

        Assert.Equal("divine", GameData.ItemById[result.craft!.Output.Item].Rarity);
    }

    [Fact]
    public void DisplayedChestGearOddsMatchTheRewardTable()
    {
        var player = new Player { Realm = 7 };
        foreach (var chest in GameService.ChestOdds(player))
        {
            var gear = ChestRewards.Table(player.Realm, chest.Chest, chapter: player.Chapter)
                .Where(reward => reward.Group == "gear").ToArray();
            var gearChance = gear.Sum(reward => reward.Probability);
            for (var q = 0; q < GameData.RarityOrder.Length; q++)
            {
                var chance = gear.Where(reward => GameData.ItemById[reward.Item].Rarity == GameData.RarityOrder[q])
                    .Sum(reward => reward.Probability);
                Assert.Equal(chance / gearChance * 100, chest.Quality[q], 8);
            }
        }
    }
}
