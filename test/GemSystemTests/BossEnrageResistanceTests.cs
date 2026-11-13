using VanDao.Game;

namespace GemSystemTests;

public class BossEnrageResistanceTests
{
    [Fact]
    public void EnrageAddsHalfBaselineToSixBossRatingsWithoutMutatingBaseMonster()
    {
        var boss = new MonsterDef("Boss","boss.png",1000,100,50,Tier:"boss",
            Crit:20,CritResist:30,Accuracy:40,Dodge:50,ComboResist:60,Reflect:70,ReflectResist:80);
        var enraged = MonsterCombatRatings.EnrageResistances(boss,0);
        Assert.Equal(boss with {Crit=50,Reflect=100,Accuracy=70,CritResist=60,ComboResist=90,ReflectResist=110},enraged);   // cân bằng 11/2026: cuồng bạo cộng 50 × 0,6 = +30 ở Thường (cũ +50)
        Assert.Equal(30,boss.CritResist);
        Assert.Equal(60,boss.ComboResist);
        Assert.Equal(80,boss.ReflectResist);
        Assert.Equal(40,boss.Accuracy);
        Assert.Equal(40,CombatRatingRules.Opposed(100,enraged.CritResist));
        Assert.Equal(0,ComboCombat.Chance(90,enraged.ComboResist));
        foreach(var tier in new[]{"elite","normal"}) {
            var monster=boss with {Tier=tier};
            Assert.Equal(monster,MonsterCombatRatings.EnrageResistances(monster,3));
        }
    }

    [Theory]
    [InlineData(0,30)]   // cân bằng 11/2026: nửa mức nền × 0,6
    [InlineData(1,60)]
    [InlineData(2,90)]
    [InlineData(3,120)]
    [InlineData(4,150)]
    public void EnrageBonusScalesWithDifficultyAndKeepsFeaturedLead(int step,double bonus)
    {
        var boss = MonsterCombatRatings.Apply(new MonsterDef("Kiếm Đế","boss_kiem_de.png",1000,100,50,Tier:"boss",Id:"boss_kiem_de"),step);
        var enraged = MonsterCombatRatings.EnrageResistances(boss,step);
        Assert.Equal(boss.Crit+bonus,enraged.Crit);
        Assert.Equal(boss.CritResist+bonus,enraged.CritResist);
        Assert.Equal(boss.Accuracy+bonus,enraged.Accuracy);
        Assert.Equal(boss.Dodge,enraged.Dodge);
        Assert.Equal(boss.Combo,enraged.Combo);
        Assert.Equal(30,enraged.Crit-enraged.Accuracy);   // Kiếm Đế: chỉ số nổi bật Crit vẫn hơn (50 × 0,6)
    }
}
