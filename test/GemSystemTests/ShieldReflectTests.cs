using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class ShieldReflectTests
{
    [Fact]
    public void EquippedShieldsAndReflectionUseFullTotalsWithoutCaps()
    {
        var player=new Player { Realm=9 };
        foreach(var slot in GameData.Realms[9].EquipSlots)
            player.Equipment.Add(new() { Slot=slot,ItemId=GameData.AllItems.First(i=>i.Slot==slot&&i.MinRealm==9&&i.Rarity=="supreme").Id });
        var effects=EquipmentEffects.Calculate(player).Effects;
        foreach(var id in new[] { "tho_thuan","dia_mach" })
        {
            var effect=Assert.Single(effects,e=>e.Id==id);
            Assert.Null(effect.Cap);Assert.Equal(effect.Raw,effect.Effective);
        }
        Assert.True(GameService.GearEffects(player).Shield>20);
        Assert.True(GameService.GearEffects(player).Reflect>15);
    }

    [Fact]
    public void ArenaDoesNotTrimShieldOrReflectionAndPreservesDamageReductionCap()
    {
        var fighter=PvpFighter.FromStats("A",0,new() { ["atk"]=100,["hp"]=10000 }) with { GuardPct=250,Reflect=250,Shield=100 };
        var sanitized=ArenaService.Sanitize(fighter,"A",0,null);
        Assert.Equal(250,sanitized.GuardPct);Assert.Equal(250,sanitized.Reflect);
        Assert.Equal(60,sanitized.Shield); // This field means damage reduction, not barrier HP.
    }

    [Fact]
    public void PetShieldCanExceedThirtyPercentAndStackAcrossCasts()
    {
        var pet=new PetCombat(PetData.ById["bang_tinh_tho"],2,new(100,1000,10),pet:new(100,1_000_000,10));
        long hp=1000;
        var first=pet.Cast(0,ref hp,10000,10000,0,1);
        Assert.True(first.Guard>300);
        var second=pet.Cast(1,ref hp,10000,10000,0,1);
        Assert.Equal(first.Guard+second.Guard,pet.Shield);
        Assert.DoesNotContain("tối đa 30%",PetData.SkillDescription("frost",2,0));
    }
}
