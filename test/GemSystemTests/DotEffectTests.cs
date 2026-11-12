using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>Thiêu đốt 5% × 2 lượt, Nhiễm độc 3% × 4 lượt (cân bằng 11/2026); mỗi lúc một loại, cái mới đè cái cũ.</summary>
public class DotEffectTests
{
    private static decimal[] Run(CombatBurn b, int turns) => Enumerable.Range(0, turns).Select(_ => b.Tick(1000, false)).ToArray();

    [Fact]
    public void BurnIsFivePercentForTwoTurns()
    {
        var b = new CombatBurn();
        Assert.Equal(50, b.Ignite(1000, false, "burn"));
        Assert.Equal(new decimal[] { 50, 0, 0 }, Run(b, 3));
    }

    [Fact]
    public void PoisonIsThreePercentForFourTurns()
    {
        var b = new CombatBurn();
        Assert.Equal(30, b.Ignite(1000, false, "poison"));
        Assert.Equal(new decimal[] { 30, 30, 30, 0 }, Run(b, 4));
        Assert.Equal("poison", b.Kind);
    }

    [Fact]
    public void NewerEffectReplacesOlderAndNeverTicksTwicePerTurn()
    {
        var b = new CombatBurn();
        b.Ignite(1000, false, "poison");
        Assert.Equal(30, b.Tick(1000, false));                  // lượt sau: độc trừ máu
        Assert.Equal(0, b.Ignite(1000, true, "burn"));          // cùng lượt dính đốt: không trừ thêm, đốt thay độc
        Assert.Equal("burn", b.Kind);
        Assert.Equal(new decimal[] { 50, 0 }, Run(b, 2));       // đốt còn 1 lượt, độc cũ mất
        Assert.Equal(50, b.Tick(1000, true));                   // thiêu đốt bị động luôn là đốt
    }

    [Fact]
    public void ApplyDoesNotDamageUntilEnemyTurnTick()
    {
        var b = new CombatBurn();
        b.Apply("poison");                                       // gây ở lượt tu sĩ: chưa trừ máu
        Assert.Equal(4, b.TurnsLeft);
        Assert.Equal(new decimal[] { 30, 30, 30, 30, 0 }, Run(b, 5));   // lượt địch này + 3 lượt địch sau
        b.Apply("burn");
        Assert.Equal(new decimal[] { 50, 50, 0 }, Run(b, 3));
    }

    [Fact]
    public void ControlChanceIsCappedAtTwentyFivePercent()
    {
        foreach (var el in new[] { "tho", "loi", "bang" })
        {
            var s = SkillData.Skills.First(x => x.Element == el && x.SecondaryEffect is not null);
            var all = SkillData.Skills.Where(x => x.Element == el && x.Tree == "passive").ToDictionary(x => x.Id, x => x.MaxLv);
            Assert.Equal(25, SkillSecondaryEffects.Chance(s, s.MaxLv, el, SkillSecondaryEffects.PassiveBonuses(all)));
        }
    }

    [Fact]
    public void WoodBasicPoisonLastsFourTurnsInRealBattle()
    {
        var p = new Player { Id = Guid.Parse("40000000-0000-0000-0000-0000000000b2"), Realm = 0, Chapter = 9, Diff = 0, BodyHp = 1_000_000_000_000, MapVer = 1,
            LearnedManuals = "moc", ActiveManual = "moc", SkillSlots = "thanh_diep_nhan," };
        p.SetSkillLevels(SkillData.Skills.Where(s => s.Element == "moc" && s.Tree != "basic").ToDictionary(s => s.Id, s => s.MaxLv));
        var full = 0;
        for (var k = 0; k < 60; k++)
        {
            var b = GameService.BattleAt(p, 1, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(k * 13).AddSeconds(k * 37));
            for (var i = 0; i + 4 < b.Hits; i++)
            {
                if (b.BasicSecondary![i] != "poison" || b.BurnDamage![i] <= 0 || b.MonHpLeft[i] <= 0) continue;   // quái gục ngay lượt này: đợt mới xóa hiệu ứng
                if (Enumerable.Range(i + 1, 3).Any(j => b.BasicSecondary[j] != "" || b.MonHpLeft[j] <= 0 || b.Casts.Any(c => c.Turn == j && c.Secondary != ""))) continue;
                Assert.All(Enumerable.Range(i, 4), j => { Assert.True(b.Burned![j]); Assert.Equal("poison", b.DotKind![j]); });
                Assert.Equal(CombatNumber.Round(b.MonsterHp * .03m), b.BurnDamage[i]);
                if (b.BasicSecondary[i + 4] == "") Assert.False(b.Burned![i + 4]);   // hết 4 lượt
                full++;
            }
        }
        Assert.True(full > 0, "chưa thấy lần nhiễm độc nào chạy đủ 4 lượt");
    }
}
