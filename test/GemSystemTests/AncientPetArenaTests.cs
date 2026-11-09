using VanDao.Game;
using Xunit;

namespace GemSystemTests;

/// <summary>Thái Cổ Hỏa Kỳ Lân và Thái Cổ Minh Côn: hệ, khắc chế, tuyệt kỹ và Lôi đài.</summary>
public class AncientPetArenaTests
{
    private static PvpFighter Fighter(string name, PvpPet? pet) =>
        PvpFighter.FromStats(name, 3, new() { ["atk"] = 500, ["hp"] = 4000, ["def"] = 200, ["crit"] = 10, ["speed"] = 50 }) with { Pet = pet };

    private static PvpPet PetOf(string id) => new(id, 2, 2, 0, 5, 300, 1500, 100);

    [Fact]
    public void New_ancient_pets_are_dual_element_and_kunpeng_counters_both_qilin_elements()
    {
        var qilin = PetData.ById["thai_co_hoa_ky_lan"]; var kun = PetData.ById["con_bang_vu_tru"];
        Assert.Equal(["hoa", "tho"], qilin.Elements); Assert.Equal(["thuy", "gio"], kun.Elements);
        Assert.Equal("Thái Cổ Minh Côn", kun.Name);
        Assert.All(qilin.Elements, e => Assert.Equal(1, ElementCounter.Edge(ElementCounter.Best(kun.Elements, e), e)));
        Assert.Equal(4, PetData.Pool(5).Length);
    }

    [Theory]
    [InlineData("thai_co_hoa_ky_lan")]
    [InlineData("con_bang_vu_tru")]
    public void New_pets_cast_in_arena_for_both_sides(string id)
    {
        var r = PvpCombat.Run(Fighter("A", PetOf(id)), Fighter("D", PetOf(id)), 7);
        Assert.Contains(r.Actions, x => x.PetCasts.Count > 0);
        var (b, pv) = PvpCombat.ToBattle(r);
        Assert.NotNull(b.Pet); Assert.NotNull(pv.FoePet);
        Assert.Equal(PetData.ById[id].Name, b.Pet!.Name);
        Assert.StartsWith("img/pet/" + id + "_", b.Pet.Image);
        Assert.NotEmpty(b.Pet.Casts);
    }

    [Fact]
    public void Kunpeng_skill_shields_and_flame_qilin_burns()
    {
        var kun = PvpCombat.Run(Fighter("A", PetOf("con_bang_vu_tru")), Fighter("D", null), 3);
        Assert.Contains(kun.Actions.SelectMany(x => x.PetCasts), c => c.Kind == "ancient_whale" && c.Guard > 0);
        var fire = PvpCombat.Run(Fighter("A", PetOf("thai_co_hoa_ky_lan")), Fighter("D", null), 3);
        Assert.Contains(fire.Actions.SelectMany(x => x.PetCasts), c => c.Kind == "ancient_flame" && c.Dmg > 0);
    }

    [Theory]
    [InlineData("thai_co_hoa_ky_lan", 100)]
    [InlineData("con_bang_vu_tru", 100)]
    public void Final_form_needs_level_100_text(string id, int level) =>
        Assert.Contains("30 và " + level, PetData.ById[id].Desc);
}
