using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class QuickGearFusionTests
{
    private sealed class SuccessRandom : Random { public override double NextDouble() => 0; }

    [Fact]
    public void QuickFusionDoesNotReuseOutputsOrConsumeHighestRarity()
    {
        var p = new Player();
        var gear = GameData.AllItems.Where(i => i.Slot != null && i.Base != null).ToList();
        var low = gear.First(i => i.Rarity == "common").Id;
        var next = gear.First(i => i.Rarity == "uncommon").Id;
        var top = gear.First(i => i.Rarity == "supreme").Id;
        p.Add(low, 18); p.Add(next, 8); p.Add(top, 9);
        var result = new GameService(null!).QuickFuseGear(p, new SuccessRandom());
        Assert.Equal(0, p.Count(low));
        Assert.Equal(10, gear.Where(i => i.Rarity == "uncommon").Sum(i => p.Count(i.Id)));
        Assert.Equal(9, p.Count(top));
        Assert.Equal(2, result.craft.Outputs!.Sum(i => i.Qty));
    }

    [Fact]
    public void InsufficientGearDoesNotChangeInventory()
    {
        var p = new Player();
        var id = GameData.AllItems.First(i => i.Slot != null && i.Base != null && i.Rarity == "common").Id;
        p.Add(id, 8);
        Assert.Throws<GameException>(() => new GameService(null!).QuickFuseGear(p));
        Assert.Equal(8, p.Count(id));
    }
}
