using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class EquipmentSetMilestoneTests
{
    [Fact]
    public void AllSetsUseThreeSevenTenAndActivateAtTheirBoundaries()
    {
        foreach (var set in EquipmentEffects.Sets) {
            Assert.Equal(new[]{3,7,10},set.Milestones.Select(m=>m.Count));
            Assert.Equal(2,set.Milestones[1].Bonuses.Length);
            var pieces=GameData.GearVariants.Where(i=>i.SetId==set.Id&&i.Rarity=="common")
                .DistinctBy(i=>i.Slot).Take(10).ToArray();
            Assert.Equal(10,pieces.Length);
            foreach(var count in new[]{2,3,6,7,9,10}) {
                var player=new Player { Realm=9 };
                foreach(var item in pieces.Take(count))player.Equipment.Add(new(){Slot=item.Slot!,ItemId=item.Id});
                var state=Assert.Single(EquipmentEffects.Calculate(player).Sets,s=>s.Id==set.Id);
                Assert.Equal(new[]{3,7,10}.Where(n=>n<=count),state.Active);
                Assert.Equal(count<3?3:count<7?7:count<10?10:(int?)null,state.Next);
            }
        }
    }
}
