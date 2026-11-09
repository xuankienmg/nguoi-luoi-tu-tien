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

    [Fact]
    public void BossChestEggQualitySplitIs90PercentLinhHuyenDiaAnd10PercentThien()
    {
        for (var realm = 0; realm < GameData.Realms.Count; realm++)
        for (var chapter = 0; chapter < PetData.PetEggDrops.Length; chapter++)
        for (var diff = 0; diff < GameData.DiffNames.Length; diff++)
        {
            var eggs = ChestRewards.Table(realm, GameData.ChestId(GameData.BossChest, diff), diff, chapter)
                .Where(r => PetData.EggQuality(r.Item) >= 0).ToArray();
            double Odds(int q) => eggs.Where(r => PetData.EggQuality(r.Item) == q).Sum(r => r.Probability);
            Assert.Equal(0.03 * 0.90, Odds(1) + Odds(2) + Odds(3), 10);
            Assert.Equal(0.03 * 0.10, Odds(4), 10);
            Assert.Equal(0, Odds(0) + Odds(5), 10);
        }
    }
}
