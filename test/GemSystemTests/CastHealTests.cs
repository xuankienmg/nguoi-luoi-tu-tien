using VanDao.Game;

namespace GemSystemTests;

/// <summary>Chiêu chủ động kèm hồi máu (cân bằng 11/2026): Mộc hồi 5% máu tối đa cuối lượt, Thủy hút 10% sát thương (tối đa 8% máu tối đa).</summary>
public class CastHealTests
{
    private static SkillDef ActiveOf(string el) => SkillData.Skills.First(s => s.Element == el && s.Tree == "active" && s.DmgBase > 0);

    [Fact]
    public void WoodActiveHealsFivePercentAtTurnEnd()
    {
        Assert.Equal((50m, 0m), SkillSecondaryEffects.CastHeal(ActiveOf("moc"), 999_999, 1000));
    }

    [Fact]
    public void WaterActiveLeechesTenPercentCappedAtEightPercentMaxHp()
    {
        Assert.Equal((0m, 30m), SkillSecondaryEffects.CastHeal(ActiveOf("thuy"), 300, 1000));
        Assert.Equal((0m, 80m), SkillSecondaryEffects.CastHeal(ActiveOf("thuy"), 1_000_000, 1000));
        Assert.Equal((0m, 0m), SkillSecondaryEffects.CastHeal(ActiveOf("thuy"), 0, 1000));   // trượt: không hút
    }

    [Fact]
    public void OtherElementsBasicsAndNonDamageSkillsDoNotHeal()
    {
        Assert.Equal((0m, 0m), SkillSecondaryEffects.CastHeal(ActiveOf("hoa"), 1000, 1000));
        Assert.Equal((0m, 0m), SkillSecondaryEffects.CastHeal(SkillData.Skills.First(s => s.Element == "thuy" && s.Tree == "basic"), 1000, 1000));
        var support = SkillData.Skills.FirstOrDefault(s => s.Element == "moc" && s.Tree == "active" && s.DmgBase <= 0);
        if (support is not null) Assert.Equal((0m, 0m), SkillSecondaryEffects.CastHeal(support, 1000, 1000));
    }
}
