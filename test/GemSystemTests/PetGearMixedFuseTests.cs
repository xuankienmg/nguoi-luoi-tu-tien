using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class PetGearMixedFuseTests
{
    private static string Id(int slot, int tier) => PetTraining.GearId(PetTraining.Slots[slot].Id, tier);
    private static GameService Game() => new(null!);

    [Fact]
    public void MixedFuseConsumesThreeAndGivesOneOfNextTierInSomeSlot()
    {
        var p = new Player();
        p.Add(Id(0, 1), 1); p.Add(Id(1, 1), 1); p.Add(Id(2, 1), 1); p.Add("linh_thach", 100_000);
        var result = Game().FuseMixedPetGear(p, [Id(0, 1), Id(1, 1), Id(2, 1)], new Random(7));
        Assert.All(Enumerable.Range(0, 3), s => Assert.Equal(0, p.Count(Id(s, 1))));
        Assert.Equal(1, Enumerable.Range(0, 3).Sum(s => p.Count(Id(s, 2))));
        Assert.Equal(100_000 - PetTraining.FuseStones(1), p.Count("linh_thach"));
        Assert.Equal(1, result.craft!.Output!.Qty);
        Assert.Equal(2, PetTraining.Gear[result.craft.Output.Item].Tier);
    }

    [Fact]
    public void MixedFuseSlotIsRandomAndCoversEverySlot()
    {
        var rng = new Random(123);
        var seen = new HashSet<string>();
        for (var i = 0; i < 60; i++)
        {
            var p = new Player();
            p.Add(Id(0, 0), 2); p.Add(Id(1, 0), 1); p.Add("linh_thach", 100_000);   // 2 món cùng ô + 1 khác ô vẫn là hợp hỗn hợp
            seen.Add(Game().FuseMixedPetGear(p, [Id(0, 0), Id(0, 0), Id(1, 0)], rng).craft!.Output!.Item);
        }
        Assert.Equal(PetTraining.Slots.Length, seen.Count);
    }

    [Fact]
    public void MixedFuseRejectsDifferentTiersSameSlotMaxTierAndMissingItems()
    {
        var p = new Player();
        p.Add(Id(0, 0), 3); p.Add(Id(1, 1), 1); p.Add(Id(1, 5), 1); p.Add(Id(2, 5), 1); p.Add(Id(0, 5), 1); p.Add("linh_thach", 1_000_000);
        Assert.Throws<GameException>(() => Game().FuseMixedPetGear(p, [Id(0, 0), Id(0, 0), Id(1, 1)]));   // khác phẩm
        Assert.Throws<GameException>(() => Game().FuseMixedPetGear(p, [Id(0, 0), Id(0, 0), Id(0, 0)]));   // cùng ô: dùng hợp thường
        Assert.Throws<GameException>(() => Game().FuseMixedPetGear(p, [Id(0, 5), Id(1, 5), Id(2, 5)]));   // phẩm cao nhất
        Assert.Throws<GameException>(() => Game().FuseMixedPetGear(p, [Id(0, 0), Id(1, 0), Id(2, 0)]));   // thiếu món trong túi
        Assert.Throws<GameException>(() => Game().FuseMixedPetGear(p, [Id(0, 0), Id(1, 0)]));             // sai số lượng
        Assert.Equal(3, p.Count(Id(0, 0)));
        Assert.Equal(1_000_000, p.Count("linh_thach"));
    }

    [Fact]
    public void QuickFuseMergesLeftoverDifferentSlotsAsMixed()
    {
        var p = new Player();
        p.Add(Id(0, 0), 4); p.Add(Id(1, 0), 1); p.Add(Id(2, 0), 1); p.Add("linh_thach", 1_000_000);   // 1 lượt cùng ô (3) + lẻ 1,1,1 → 1 lượt hỗn hợp
        var r = Game().QuickFusePetGear(p, new Random(5));
        Assert.Equal(0, Enumerable.Range(0, 3).Sum(s => p.Count(Id(s, 0))));
        Assert.Equal(2, Enumerable.Range(0, 3).Sum(s => p.Count(Id(s, 1))));
        Assert.True(p.Count(Id(0, 1)) >= 1);
        Assert.Equal(1_000_000 - 2 * PetTraining.FuseStones(0), p.Count("linh_thach"));
        Assert.NotNull(r.craft);
    }

    [Fact]
    public void QuickFuseWorksWithOnlyMixedItems()
    {
        var p = new Player();
        p.Add(Id(0, 0), 1); p.Add(Id(1, 0), 1); p.Add(Id(2, 0), 1); p.Add("linh_thach", 100_000);
        Game().QuickFusePetGear(p);
        Assert.Equal(1, Enumerable.Range(0, 3).Sum(s => p.Count(Id(s, 1))));
        Assert.Equal(0, Enumerable.Range(0, 3).Sum(s => p.Count(Id(s, 0))));
    }

    [Fact]
    public void MixedFuseWithoutEnoughStonesChangesNothing()
    {
        var p = new Player();
        p.Add(Id(0, 0), 1); p.Add(Id(1, 0), 1); p.Add(Id(2, 0), 1); p.Add("linh_thach", PetTraining.FuseStones(0) - 1);
        Assert.Throws<GameException>(() => Game().FuseMixedPetGear(p, [Id(0, 0), Id(1, 0), Id(2, 0)]));
        Assert.Equal(1, p.Count(Id(0, 0)));
        Assert.Equal(PetTraining.FuseStones(0) - 1, p.Count("linh_thach"));
    }
}
