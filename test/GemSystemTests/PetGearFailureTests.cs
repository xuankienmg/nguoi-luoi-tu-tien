using VanDao.Data;
using VanDao.Game;
namespace GemSystemTests;
public class PetGearFailureTests
{
    internal sealed class FixedRandom(double value) : Random
    {
        public override double NextDouble() => value;
        public override int Next(int maxValue) => 0;
    }
    [Theory]
    [InlineData(0, 1.00)] [InlineData(1, .95)] [InlineData(2, .85)]
    [InlineData(3, .75)] [InlineData(4, .65)] [InlineData(5, .55)]
    [InlineData(6, .45)] [InlineData(7, .25)] [InlineData(8, .15)]
    public void EveryTierUsesApprovedChanceAndBoundary(int tier, double chance)
    {
        Assert.Equal(chance, PetTraining.FuseChances[tier]);
        foreach (var roll in new[] { 0.0, chance - .000001, chance == 1 ? .999999 : chance })
        {
            var p = new Player(); var id = PetTraining.GearId("collar",tier);
            p.Add(id,5); p.Add("linh_thach",1_000_000);
            var result = new GameService(null!).FusePetGear(p,id,1,new FixedRandom(roll));
            var ok = roll < chance;
            Assert.Equal(ok ? 0 : 1, p.Count(id));
            Assert.Equal(ok ? 1 : 0, p.Count(PetTraining.GearId("collar",tier+1)));
            Assert.Equal(ok ? "good" : "bad", result.kind);
            Assert.Equal(1_000_000 - PetTraining.FuseStones(tier), p.Count("linh_thach"));
        }
    }
    [Fact]
    public void BatchFailuresReturnOnePerAttemptWithoutReusingRefunds()
    {
        var p = new Player(); var id = PetTraining.GearId("collar",8);
        p.Add(id,9); p.Add("linh_thach",1_000_000);
        var result = new GameService(null!).FusePetGear(p,id,0,new FixedRandom(.999));
        Assert.Equal(5,p.Count(id));
        Assert.Equal(1_000_000-PetTraining.FuseStones(8),p.Count("linh_thach"));
        Assert.Equal("petgear:fail",result.craft!.Recipe);
    }
    [Theory]
    [InlineData(0)] [InlineData(.999)]
    public void FullBagFailureCanReturnIngredientButSuccessWithExtraStackIsAtomic(double roll)
    {
        var p = new Player(); var id = PetTraining.GearId("collar",8);
        p.Add(id,6); p.Add("linh_thach",1_000_000);
        foreach (var item in GameData.AllItems)
        {
            if (GameService.BagUsed(p) >= GameService.BagCap(p)) break;
            if (p.Count(item.Id) == 0) p.Add(item.Id,1);
        }
        // Bỏ thành phẩm nếu có trong các món lấp túi, thay bằng món khác.
        var next = PetTraining.GearId("collar",9);
        if (p.Count(next) > 0)
        {
            p.Add(next,-p.Count(next));
            var filler = GameData.AllItems.First(x => x.Id != next && p.Count(x.Id) == 0 && x.Type == "petgear");
            p.Add(filler.Id,1);
        }
        Assert.Equal(GameService.BagCap(p),GameService.BagUsed(p));
        var game = new GameService(null!);
        if (roll == 0)
        {
            Assert.Throws<GameException>(() => game.FusePetGear(p,id,1,new FixedRandom(roll)));
            Assert.Equal(6,p.Count(id)); Assert.Equal(1_000_000,p.Count("linh_thach"));
        }
        else
        {
            game.FusePetGear(p,id,1,new FixedRandom(roll));
            Assert.Equal(2,p.Count(id));
            Assert.Equal(1_000_000-PetTraining.FuseStones(8),p.Count("linh_thach"));
        }
    }
    [Fact]
    public void MixedFailureReturnsOneSelectedIngredientAndChargesFullFee()
    {
        var p = new Player(); var a = PetTraining.GearId("collar", 8); var b = PetTraining.GearId("armor", 8);
        p.Add(a, 3); p.Add(b, 2); p.Add("linh_thach", 1_000_000);
        var result = new GameService(null!).FuseMixedPetGear(p, [a,a,a,b,b], new FixedRandom(.999));
        Assert.Equal(a, result.craft!.Output!.Item);
        Assert.Equal(1, p.Count(a)); Assert.Equal(0, p.Count(b));
        Assert.Equal(1_000_000 - PetTraining.FuseStones(8), p.Count("linh_thach"));
        Assert.Equal("bad", result.kind);
    }
    [Fact]
    public void QuickFailuresDoNotReuseRefundsOrOutputs()
    {
        var p = new Player(); var a = PetTraining.GearId("collar", 1); var b = PetTraining.GearId("armor", 1);
        p.Add(a, 7); p.Add(b, 3); p.Add("linh_thach", 1_000_000);
        var result = new GameService(null!).QuickFusePetGear(p, new FixedRandom(.999));
        Assert.Equal(2, p.Count(a) + p.Count(b));
        Assert.Equal(0, PetTraining.Slots.Sum(s => p.Count(PetTraining.GearId(s.Id,2))));
        Assert.Equal(1_000_000 - 2 * PetTraining.FuseStones(1), p.Count("linh_thach"));
        Assert.Contains("2 thất bại", result.msg);
    }
}
