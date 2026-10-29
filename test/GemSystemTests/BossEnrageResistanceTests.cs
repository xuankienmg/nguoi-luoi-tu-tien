using VanDao.Game;

namespace GemSystemTests;

public class BossEnrageResistanceTests
{
    [Fact]
    public void EnrageDoublesSixBossRatingsWithoutMutatingBaseMonster()
    {
        var boss = new MonsterDef("Boss","boss.png",1000,100,50,Tier:"boss",
            Crit:20,CritResist:30,Accuracy:40,Dodge:50,ComboResist:60,Reflect:70,ReflectResist:80);
        var enraged = MonsterCombatRatings.EnrageResistances(boss);
        Assert.Equal(boss with {Accuracy=80,Reflect=140,CritResist=60,ComboResist=120,ReflectResist=160},enraged);
        Assert.Equal(30,boss.CritResist);
        Assert.Equal(60,boss.ComboResist);
        Assert.Equal(80,boss.ReflectResist);
        Assert.Equal(40,boss.Accuracy);
        Assert.Equal(20,CombatRatingRules.Opposed(80,enraged.CritResist));
        Assert.Equal(0,ComboCombat.Chance(100,enraged.ComboResist));
        foreach(var tier in new[]{"elite","normal"}) {
            var monster=boss with {Tier=tier};
            Assert.Equal(monster,MonsterCombatRatings.EnrageResistances(monster));
        }
    }
}
