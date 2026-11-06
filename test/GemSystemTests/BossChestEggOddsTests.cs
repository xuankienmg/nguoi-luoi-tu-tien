using VanDao.Game;

namespace GemSystemTests;

public class BossChestEggOddsTests
{
    [Fact]
    public void BossChestEggChanceIsFixedAcrossChaptersRealmsAndDifficulties()
    {
        for (var realm = 0; realm < GameData.Realms.Count; realm++)
        for (var chapter = 0; chapter < PetData.PetEggDrops.Length; chapter++)
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        {
            var eggs = ChestRewards.Table(realm, GameData.ChestId(GameData.BossChest, diff), diff, chapter)
                .Where(r => PetData.EggQuality(r.Item) >= 0).Sum(r => r.Probability);
            Assert.Equal(0.03, eggs, 10);
        }
    }
}
