using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class PetGearProgressionTests
{
    [Fact]
    public void TenTiersKeepExistingBonusesAndAddFourCraftOnlyTiers()
    {
        Assert.Equal(new[] { "Phàm", "Linh", "Huyền", "Địa", "Thiên", "Thánh", "Thần", "Tiên", "Thượng Cổ", "Hỗn Độn" }, PetTraining.TierNames);
        Assert.Equal(new double[] { 4, 8, 12, 17, 23, 30, 40, 55, 75, 100 }, PetTraining.GearPct);
        Assert.Equal(5, PetTraining.FuseCount);
        Assert.Equal(4, PetTraining.MaxDropTier);
        foreach (var gear in PetTraining.Gear.Values)
        {
            Assert.True(GameData.ItemById.ContainsKey(gear.Id));
            Assert.Contains(PetTraining.TierNames[gear.Tier], GameData.ItemById[gear.Id].Name);
            Assert.Equal(PetTraining.GearPct[gear.Tier], gear.Pct);
            if (gear.Tier > 4) Assert.Contains("Không rơi ở map", GameData.ItemById[gear.Id].Desc);
        }
    }

    [Theory]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void FiveItemsCraftNextTierWithoutChangingSlot(int tier)
    {
        var p = new Player();
        var id = PetTraining.GearId(PetTraining.Slots[0].Id, tier);
        var next = PetTraining.GearId(PetTraining.Slots[0].Id, tier + 1);
        p.Add(id, 5); p.Add("linh_thach", 1_000_000);
        var result = new GameService(null!).FusePetGear(p, id, 1, new PetGearFailureTests.FixedRandom(0));
        Assert.Equal(next, result.craft!.Output!.Item);
        Assert.Equal(0, p.Count(id)); Assert.Equal(1, p.Count(next));
        Assert.Equal(1_000_000 - PetTraining.FuseStones(tier), p.Count("linh_thach"));
    }

    [Fact]
    public void FourItemsAndMaxTierCannotBeFusedAndRemainInBag()
    {
        var p = new Player(); var game = new GameService(null!);
        var low = PetTraining.GearId(PetTraining.Slots[0].Id, 0);
        var max = PetTraining.GearId(PetTraining.Slots[0].Id, 9);
        p.Add(low, 4); p.Add(max, 5); p.Add("linh_thach", 1_000_000);
        Assert.Throws<GameException>(() => game.FusePetGear(p, low, 1));
        Assert.Throws<GameException>(() => game.FusePetGear(p, max, 1));
        Assert.Equal(4, p.Count(low)); Assert.Equal(5, p.Count(max));
        Assert.Equal(1_000_000, p.Count("linh_thach"));
    }
}
