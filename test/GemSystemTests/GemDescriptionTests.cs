using VanDao.Game;

namespace GemSystemTests;

public class GemDescriptionTests
{
    [Fact]
    public void AllGemEffectsUseCompactDescriptionsWithoutParentheses()
    {
        foreach(var gem in GemData.Items) {
            foreach(var effect in gem.Effects??[]) {
                Assert.DoesNotContain("(",effect.Text);
                Assert.DoesNotContain(")",effect.Text);
                Assert.DoesNotContain("nền cảnh giới",effect.Text);
            }
        }
        Assert.Contains(GemData.ById["gem_hoa_lv10"].Effects!,e=>e.Id=="gem_base_atk"&&e.Text=="+10% công");
    }
}
