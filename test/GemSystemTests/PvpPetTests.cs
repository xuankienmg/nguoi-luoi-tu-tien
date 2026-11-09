using VanDao.Game;
using Xunit;

namespace GemSystemTests;

public class PvpPetTests
{
    private static PvpFighter Fighter(string name, PvpPet? pet) =>
        PvpFighter.FromStats(name, 3, new() { ["atk"] = 500, ["hp"] = 4000, ["def"] = 200, ["crit"] = 10, ["speed"] = 50 }) with { Pet = pet };

    private static PvpPet PetOf(string id) => new(id, 1, 1, 0, 2, 300, 1500, 100);

    [Fact]
    public void Pet_casts_and_is_exposed_to_both_sides()
    {
        var def = PetData.ById.Values.First();
        var a = Fighter("A", PetOf(def.Id)); var d = Fighter("D", PetOf(def.Id));
        var r = PvpCombat.Run(a, d, 7);
        Assert.Contains(r.Actions, x => x.PetCasts.Count > 0);
        var (b, pv) = PvpCombat.ToBattle(r);
        Assert.NotNull(b.Pet); Assert.NotNull(pv.FoePet);
        Assert.NotEmpty(b.Pet!.Casts); Assert.NotEmpty(pv.FoePet!.Casts);
        Assert.All(pv.FoePet.Casts, c => Assert.True(c.At >= .85));
    }

    [Fact]
    public void Same_seed_same_result_and_no_pet_is_unchanged()
    {
        var def = PetData.ById.Values.First();
        var r1 = PvpCombat.Run(Fighter("A", PetOf(def.Id)), Fighter("D", null), 11);
        var r2 = PvpCombat.Run(Fighter("A", PetOf(def.Id)), Fighter("D", null), 11);
        Assert.Equal(r1.Actions.Count, r2.Actions.Count);
        Assert.Equal(r1.Actions.Last().HpD, r2.Actions.Last().HpD);
        var (b, pv) = PvpCombat.ToBattle(PvpCombat.Run(Fighter("A", null), Fighter("D", null), 11));
        Assert.Null(b.Pet); Assert.Null(pv.FoePet);
    }
}
