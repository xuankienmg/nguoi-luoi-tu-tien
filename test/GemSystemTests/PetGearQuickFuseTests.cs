using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class PetGearQuickFuseTests
{
    [Fact]
    public void QuickFuseFusesEveryGroupWithoutReusingOutputs()
    {
        var p = new Player();
        string Id(string slot, int tier) => PetTraining.GearId(slot, tier);
        var a = PetTraining.Slots[0].Id; var b = PetTraining.Slots[1].Id;
        p.Add(Id(a, 0), 7); p.Add(Id(a, 1), 2); p.Add(Id(b, 1), 3); p.Add(Id(b, 5), 9);
        p.Add("linh_thach", 1_000_000);
        var result = new GameService(null!).QuickFusePetGear(p);
        Assert.Equal(1, p.Count(Id(a, 0)));
        Assert.Equal(4, p.Count(Id(a, 1)));                          // 2 cũ + 2 mới, không hợp tiếp
        Assert.Equal(0, p.Count(Id(a, 2)));
        Assert.Equal(0, p.Count(Id(b, 1)));
        Assert.Equal(1, p.Count(Id(b, 2)));
        Assert.Equal(9, p.Count(Id(b, 5)));                          // phẩm cao nhất không hợp
        Assert.Equal(1_000_000 - 2 * PetTraining.FuseStones(0) - PetTraining.FuseStones(1), p.Count("linh_thach"));
        Assert.Equal(3, result.craft!.Outputs!.Sum(i => i.Qty));
    }

    [Fact]
    public void QuickFuseStopsWhenStonesRunOut()
    {
        var p = new Player();
        var id = PetTraining.GearId(PetTraining.Slots[0].Id, 0);
        p.Add(id, 9); p.Add("linh_thach", PetTraining.FuseStones(0) * 2);
        new GameService(null!).QuickFusePetGear(p);
        Assert.Equal(3, p.Count(id));
        Assert.Equal(0, p.Count("linh_thach"));
    }

    [Fact]
    public void QuickFuseWithoutGroupsDoesNotChangeInventory()
    {
        var p = new Player();
        var id = PetTraining.GearId(PetTraining.Slots[0].Id, 0);
        p.Add(id, 2); p.Add("linh_thach", 1_000_000);
        Assert.Throws<GameException>(() => new GameService(null!).QuickFusePetGear(p));
        Assert.Equal(2, p.Count(id));
        Assert.Equal(1_000_000, p.Count("linh_thach"));
    }
}
