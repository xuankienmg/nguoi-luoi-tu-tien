using VanDao.Game;

namespace GemSystemTests;

/// <summary>Đòn thường chính gây hiệu ứng bất lợi cùng hệ: ½ tỉ lệ chiêu chủ động tốt nhất cùng hệ (cân bằng 11/2026).</summary>
public class BasicAttackDebuffTests
{
    private static readonly string[] Debuffs = ["hoa", "thuy", "moc", "tho", "loi", "bang"];
    private static SkillDef BasicOf(string el) => SkillData.Skills.First(s => s.Element == el && s.Tree == "basic");
    private static SkillDef ActiveOf(string el) => SkillData.Skills.First(s => s.Element == el && s.SecondaryEffect is not null);
    private static Dictionary<string, int> AllMaxed(string el) =>
        SkillData.Skills.Where(s => s.Element == el && s.Tree != "basic").ToDictionary(s => s.Id, s => s.MaxLv);

    [Fact]
    public void EveryDebuffElementHasBasicWithSameEffect()
    {
        foreach (var el in Debuffs)
            Assert.Equal(ActiveOf(el).SecondaryEffect!.Kind, SkillSecondaryEffects.ForBasic(BasicOf(el))?.Kind);
    }

    [Fact]
    public void BasicChanceIsHalfOfBestActiveChance()
    {
        foreach (var el in Debuffs)
        {
            var levels = AllMaxed(el);
            var passive = SkillSecondaryEffects.PassiveBonuses(levels);
            var active = ActiveOf(el);
            Assert.Equal(SkillSecondaryEffects.Chance(active, active.MaxLv, el, passive) / 2,
                SkillSecondaryEffects.BasicChance(BasicOf(el), levels, el, passive));
            Assert.Equal(ActiveOf(el).SecondaryEffect!.MaxChance / 2, SkillSecondaryEffects.BasicChance(BasicOf(el), levels, el, passive));   // chạm trần: 50% → 25%, khống 25% → 12,5%
            Assert.Equal(0, SkillSecondaryEffects.BasicChance(BasicOf(el), new Dictionary<string, int>(), el));   // chưa học gì
        }
    }

    [Fact]
    public void BasicNeedsSameElementManualAndSkipsWind()
    {
        var levels = AllMaxed("tho");
        Assert.Equal(0, SkillSecondaryEffects.BasicChance(BasicOf("tho"), levels, "hoa", SkillSecondaryEffects.PassiveBonuses(levels)));
        Assert.Null(SkillSecondaryEffects.ForBasic(BasicOf("gio")));
    }

    [Fact]
    public void BasicProcAppliesControlForThisTurn()
    {
        var levels = AllMaxed("tho");
        var combat = new SkillEffectCombat("tho", SkillSecondaryEffects.PassiveBonuses(levels));
        Assert.Null(combat.TryApplyBasic(BasicOf("tho"), levels, 3, new FixedRandom(.99), true));   // 99% ≥ 12,5%: trượt
        Assert.Equal("stun", combat.TryApplyBasic(BasicOf("tho"), levels, 3, new FixedRandom(.1), true)?.Kind);
        Assert.Equal("stun", combat.ControlKind(3));
        Assert.Null(combat.TryApplyBasic(BasicOf("tho"), levels, 4, new FixedRandom(0), false));   // quái đã gục: không gây
    }

    private sealed class FixedRandom(double value) : Random { public override double NextDouble() => value; }
}
