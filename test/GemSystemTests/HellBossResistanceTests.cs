using VanDao.Game;

namespace GemSystemTests;

public class HellBossResistanceTests
{
    [Theory]
    [InlineData(0, 600)]
    [InlineData(9, 400)]
    public void BossResistsEightyPercentOfUltimatesWhileBasicResistanceStaysUnchanged(int chapter, long basicDamage)
    {
        var boss = new HellMonsterCombat(new("Boss", "boss.png", 10000, 100, 0, "boss"), chapter, 0);
        Assert.Equal(200, boss.Resist(1000, ultimate: true));
        Assert.Equal(basicDamage, boss.Resist(1000));
        Assert.Equal(0, boss.Resist(0, ultimate: true));
        Assert.Contains("80%", HellMonsterSkills.BossPassive(chapter).Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("elite")]
    public void OtherHellMonstersDoNotGainBossUltimateResistance(string tier)
    {
        var monster = new HellMonsterCombat(new("Quái", "quai.png", 10000, 100, 0, tier), 9, 0);
        Assert.Equal(1000, monster.Resist(1000, ultimate: true));
        Assert.Equal(1000, monster.Resist(1000));
    }
}
