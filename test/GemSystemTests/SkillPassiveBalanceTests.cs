using VanDao.Game;
using Xunit;

namespace GemSystemTests;

/// <summary>Bất biến cân bằng công pháp: mỗi bậc tối đa 2 bị động (gồm Căn cơ) và ngân sách bị động các hệ xấp xỉ nhau (kể cả bị động từ bí kíp).</summary>
public class SkillPassiveBalanceTests
{
    [Fact]
    public void EachRealmTierHasAtMostTwoPassivesPerElement()
    {
        var over = SkillData.Skills.Where(s => s.Tree == "passive").GroupBy(s => (s.Element, s.MinRealm))
            .Where(g => g.Count() > 2).Select(g => $"{g.Key.Element}/r{g.Key.MinRealm}={g.Count()}").ToList();
        Assert.Empty(over);
        // từ Luyện Khí đến Chân Tiên: đúng 2 bị động mỗi bậc (xem PassiveBookTests)
    }

    [Fact]
    public void PassiveRequirementsNeverPointToHigherRealm()
    {
        var bad = SkillData.Skills.Where(s => s.Tree == "passive")
            .SelectMany(s => s.Req.Select(r => (s, d: SkillData.ById[r.Split(':')[0]])))
            .Where(t => t.d.MinRealm > t.s.MinRealm).Select(t => $"{t.s.Id}<-{t.d.Id}").ToList();
        Assert.Empty(bad);
    }

    [Fact]
    public void PassiveBudgetIsSimilarAcrossElements()
    {
        // Giá điểm mỗi 1%/cấp: công/chiêu/xuyên 1 · bạo/né 1,5 · máu/thủ 0,6 · giảm công quái 2 · hồi/cấp 4 · hồi khẩn 1,5 · thiêu đốt cố định 8.
        double Score(SkillDef s) => (s.AtkPct + s.SkillPct + s.Pen) + 1.5 * (s.Crit + s.Speed) + .6 * (s.HpPct + s.DefPct)
            + 2 * s.MonAtkDown + 4 * s.HealOnCastPct + 1.5 * s.EmergencyHealPct;
        var totals = SkillData.Skills.Where(s => s.Tree == "passive").GroupBy(s => s.Element)
            .ToDictionary(g => g.Key, g => g.Sum(s => Score(s) * s.Factor(5)) + (g.Any(s => s.BurnPct > 0) ? 8 : 0));
        var mean = totals.Values.Average();
        foreach (var (el, t) in totals) Assert.InRange(t, mean * .97, mean * 1.03);
    }
}
