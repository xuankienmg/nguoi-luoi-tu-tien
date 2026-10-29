using VanDao.Game;

namespace GemSystemTests;

public class GearSupportLinesTests
{
    [Fact]
    public void EveryGearSlotHasTheRequestedSupportCountAndQiStartsAtHeaven()
    {
        int[] counts = [0,1,2,2,3,3,4,4,5,5];
        foreach (var item in GameData.GearVariants) {
            var q = Array.IndexOf(GameData.RarityOrder,item.Rarity);
            var effects = item.Effects ?? [];
            Assert.Equal(counts[q],effects.Length);
            Assert.Equal(effects.Length,effects.Select(e=>e.Id).Distinct().Count());
            Assert.Equal(0,item.QiPct); // Included once in the support effect from Heaven onward.
            Assert.DoesNotContain(effects,e=>e.Id=="crit_resist");
            if(q<4) Assert.DoesNotContain(effects,e=>e.Id is "hap_linh" or "tu_linh");
            else Assert.Contains(effects,e=>e.Id=="hap_linh"&&e.Value>0);
        }
    }
}
