using VanDao.Game;
using VanDao.Data;

namespace GemSystemTests;

/// <summary>Luật tuyệt kỹ: lượt đầy nộ tung tuyệt kỹ (gắn cờ Ultimate), không bên nào né được.</summary>
public class UltimateRulesTests
{
    [Fact]
    public void PvpUltimateCannotBeDodgedWhileBasicAttackStillCan()
    {
        var a = PvpFighter.FromStats("A", 0, new() { ["atk"] = 100, ["hp"] = 100000 }) with
            { Skills = new() { new("xich_diem_thien", 1) }, Divine = 0 };
        var d = a with { Name = "D", Skills = new(), Dodge = 1000 };
        var basic = PvpCombat.Run(a with { Rage = 0 }, d, 42).Actions.First(x => x.ByA);
        Assert.True(basic.Dodged);                                         // đòn thường vẫn bị né
        var ult = PvpCombat.Run(a with { Rage = SkillData.RageMax }, d, 42).Actions.First(x => x.ByA);
        Assert.False(ult.Dodged);
        var cast = Assert.Single(ult.Casts);
        Assert.True(cast.Ultimate);
        Assert.True(cast.Dmg > 0);
    }

    [Fact]
    public void MonsterUltimateCounterIsNeverDodged()
    {
        var ultimateCounters = 0;
        for (var day = 1; day <= 20; day++)
        {
            var p = new Player { Id=Guid.Parse("40000000-0000-0000-0000-000000000009"),Realm=4,Chapter=0,Diff=0,BodyHp=1_000_000_000_000,BodyDef=1_000_000,MapVer=1 };
            var battle = GameService.BattleAt(p, GameData.BossAreaFor(4), new DateTime(2026,10,day,0,0,0,DateTimeKind.Utc));
            for (var i = 0; i < battle.MonsterRage!.Length; i++)
                if (!battle.MonSkip[i] && battle.MonsterRage[i].Ultimate) { ultimateCounters++; Assert.False(battle.Dodged![i]); }
        }
        Assert.True(ultimateCounters > 0, "boss phải tung tuyệt kỹ nộ trong mẫu thử");
    }
}
