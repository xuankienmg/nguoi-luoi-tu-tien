using VanDao.Game;
using VanDao.Data;

namespace GemSystemTests;

public class ComboTests
{
    private sealed class FixedRandom(double value) : Random { public override double NextDouble() => value; }

    [Theory]
    [InlineData(0,0,0)]
    [InlineData(10,25,0)]
    [InlineData(25,0,25)]
    [InlineData(50,25,25)]
    [InlineData(200,0,200)]
    public void ChanceSubtractsOpposingResistanceWithoutUpperCap(double own,double enemy,double expected)
        => Assert.Equal(expected,ComboCombat.Chance(own,enemy),8);

    [Fact]
    public void ComboIsRandomInsteadOfAccumulatingCharge()
    {
        Assert.True(ComboCombat.Roll(new FixedRandom(.24),50,25));
        Assert.False(ComboCombat.Roll(new FixedRandom(.25),50,25));
        Assert.False(ComboCombat.Roll(new FixedRandom(0),25,50));
    }

    [Fact]
    public void ComboExtraAttackDoesNotGrantRageAndEqualResistanceDoesNotProc()
    {
        var a=PvpFighter.FromStats("A",0,new() { ["atk"]=100,["hp"]=10000 }) with { Crit=0,ExtraHit=100 };
        var d=a with { Name="D",ExtraHit=0,Gems=null };
        var hit=Enumerable.Range(0,50).Select(seed=>PvpCombat.Run(a,d,seed).Actions[0]).First(action=>action.Extra);
        Assert.True(hit.Extra);Assert.True(hit.ExtraDmg>0);
        Assert.Equal(25,hit.RageA);
        var equal=PvpCombat.Run(a,d with { ComboResist=100 },42).Actions[0];
        Assert.False(equal.Extra);Assert.Equal(25,equal.RageA);
        var chain=PvpCombat.Run(a with { ExtraHit=0,Gems=new(ExtraAttacks:1) },d,42).Actions[0];
        Assert.True(chain.Extra);Assert.Equal(50,chain.RageA);
    }

    [Fact]
    public void WindGemsGrantComboAndPveComboDoesNotGrantRage()
    {
        Assert.DoesNotContain(GemProgression.CreateAll().SelectMany(g=>g.Effects!),e=>e.Id=="gem_attack_speed");
        Assert.Contains(GemData.ById["gem_phong_lv10"].Effects!,e=>e.Id=="lien_kich");
        var found=false;
        for(var sample=0;sample<64&&!found;sample++)
        {
            var p=new Player { Id=new Guid(sample,0,0,new byte[8]),Realm=3,Chapter=9,BodyHp=1_000_000_000_000 };
            p.Equipment.Add(new() { Slot="weapon",ItemId=GameData.AllItems.First(i=>i.Slot=="weapon"&&i.MinRealm==3&&i.Rarity=="common").Id });
            // Commons now have 80% combo resistance; this ring plus wind gem exceeds that rating.
            p.Equipment.Add(new() { Slot="ring",ItemId=GameData.AllItems.First(i=>i.Slot=="ring"&&i.MinRealm==3&&i.Rarity=="supreme").Id });
            GemData.SetSlots(p,new() { ["weapon"]=new() { Sockets=new() { "gem_phong_lv10" } } });
            var battle=GameService.BattleAt(p,1,new DateTime(2026,10,28,0,0,0,DateTimeKind.Utc));
            for(var turn=0;turn<Math.Min(3,battle.Hits);turn++)
            {
                if(battle.ExtraDamage![turn]<=0||battle.BasicDamage![turn]<=0)continue;
                var before=turn==0?0:battle.Rage![turn-1];
                Assert.Equal(Math.Min(100,before+25),battle.Rage![turn]);
                found=true;break;
            }
        }
        Assert.True(found,"Expected a deterministic early combo hit to verify rage.");
    }
}
