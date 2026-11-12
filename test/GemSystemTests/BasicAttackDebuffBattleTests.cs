using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>Trận thật: đòn thường gây hiệu ứng bất lợi, hiệu ứng mới đè hiệu ứng cũ.</summary>
public class BasicAttackDebuffBattleTests
{
    private static Player Maxed(string element, string basic)
    {
        var p = new Player { Id = Guid.Parse("40000000-0000-0000-0000-0000000000b1"), Realm = 0, Chapter = 9, Diff = 0, BodyHp = 1_000_000_000_000, MapVer = 1,
            LearnedManuals = element, ActiveManual = element, SkillSlots = basic + "," };
        p.SetSkillLevels(SkillData.Skills.Where(s => s.Element == element && s.Tree != "basic").ToDictionary(s => s.Id, s => s.MaxLv));
        return p;
    }
    private static IEnumerable<BattleDto> Trips(Player p) =>
        Enumerable.Range(0, 80).Select(i => GameService.BattleAt(p, 1, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(i * 13).AddSeconds(i * 37)));

    [Fact]
    public void WaterBasicWeakensAndReplacesNothingElse()
    {
        var hits = 0;
        foreach (var b in Trips(Maxed("thuy", "thuy_cau")))
        {
            Assert.Equal(b.Hits, b.BasicSecondary!.Length);
            for (var i = 0; i < b.Hits; i++)
            {
                if (b.BasicSecondary[i] != "weaken") continue;
                hits++;
                if (b.MonHpLeft[i] > 0) Assert.True(b.Weakened![i]);
                Assert.Equal("", b.MonControl![i]);                       // suy yếu mới: không còn khống cũ
                if (i + 1 < b.Hits && b.BasicSecondary[i + 1] == "") Assert.False(b.Burned![i + 1]);   // thiêu đốt cũ đã mất
            }
        }
        Assert.True(hits > 0, "đòn thường hệ Thủy chưa gây Suy yếu lần nào");
    }

    [Fact]
    public void FireBasicIgnitesBurnThatTicksThisTurn()
    {
        var hits = 0;
        foreach (var b in Trips(Maxed("hoa", "hoa_cau")))
            for (var i = 0; i < b.Hits; i++)
            {
                if (b.BasicSecondary![i] != "burn") continue;
                hits++;
                Assert.True(b.Burned![i]);
                Assert.Equal("", b.MonControl![i]);
                Assert.False(b.Weakened![i]);
            }
        Assert.True(hits > 0, "đòn thường hệ Hỏa chưa gây Thiêu đốt lần nào");
    }
}
