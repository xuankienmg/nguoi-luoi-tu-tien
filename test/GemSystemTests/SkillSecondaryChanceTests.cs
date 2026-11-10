using VanDao.Game;

namespace GemSystemTests;

/// <summary>Hiệu ứng phụ chiêu chủ động (cân bằng 11/2026): chủ động 1%/cấp, bị động cùng hệ +0.5%/cấp, tổng tối đa 50%.</summary>
public class SkillSecondaryChanceTests
{
    private static readonly string[] Elements = ["hoa", "thuy", "moc", "tho", "loi", "bang", "gio"];
    private static SkillDef ActiveOf(string el) => SkillData.Skills.First(s => s.Element == el && s.SecondaryEffect is not null);
    private static IEnumerable<SkillDef> PassivesOf(string el) => SkillData.Skills.Where(s => s.Element == el && s.Tree == "passive");

    [Fact]
    public void ActiveAloneGivesAtMostFivePercent()
    {
        foreach (var el in Elements)
        {
            var s = ActiveOf(el);
            Assert.Equal(5, SkillSecondaryEffects.Chance(s, s.MaxLv, el));
            Assert.Equal(5, SkillSecondaryEffects.Chance(s, 99, el));
        }
    }

    [Fact]
    public void SameElementPassivesAddHalfPercentPerLevelUpToFifty()
    {
        foreach (var el in Elements)
        {
            var s = ActiveOf(el);
            var one = PassivesOf(el).First();
            var bonus = SkillSecondaryEffects.PassiveBonuses(new Dictionary<string, int> { [one.Id] = one.MaxLv });
            Assert.Equal(5 + 0.5 * one.MaxLv, SkillSecondaryEffects.Chance(s, s.MaxLv, el, bonus));

            var all = PassivesOf(el).ToDictionary(p => p.Id, p => p.MaxLv);
            Assert.True(SkillSecondaryEffects.PassiveBonuses(all)[el] + 5 > 50, el);   // đủ bị động thì chạm trần
            Assert.Equal(50, SkillSecondaryEffects.Chance(s, s.MaxLv, el, SkillSecondaryEffects.PassiveBonuses(all)));
        }
    }

    [Fact]
    public void OtherElementPassivesDoNotCount()
    {
        var fire = ActiveOf("hoa");
        var water = PassivesOf("thuy").ToDictionary(p => p.Id, p => p.MaxLv);
        Assert.Equal(5, SkillSecondaryEffects.Chance(fire, fire.MaxLv, "hoa", SkillSecondaryEffects.PassiveBonuses(water)));
    }

    [Fact]
    public void MonsterDebuffsNeedSameElementManualButWindDoesNot()
    {
        foreach (var el in Elements)
        {
            var s = ActiveOf(el);
            var other = el == "hoa" ? "thuy" : "hoa";
            var expected = s.SecondaryEffect!.Target == "hero" ? 5 : 0;
            Assert.Equal(expected, SkillSecondaryEffects.Chance(s, s.MaxLv, other));
        }
    }

    [Fact]
    public void WoodPoisonsAndWindAlsoRaisesAccuracy()
    {
        Assert.Equal("poison", ActiveOf("moc").SecondaryEffect!.Kind);
        Assert.Equal("monster", ActiveOf("moc").SecondaryEffect!.Target);
        var wind = ActiveOf("gio");
        var combat = new SkillEffectCombat("gio");
        Assert.NotNull(combat.TryApply(wind, wind.MaxLv, 0, new FixedRandom(0), true));
        Assert.Equal(20, combat.DodgeBonus(0));
        Assert.Equal(20, combat.AccuracyBonus(1));
        Assert.Equal(0, combat.AccuracyBonus(2));
    }

    private sealed class FixedRandom(double value) : Random { public override double NextDouble() => value; }
}
