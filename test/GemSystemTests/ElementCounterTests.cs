using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class ElementCounterTests
{
    [Theory]
    [InlineData("moc", "tho")] [InlineData("hoa", "loi")] [InlineData("tho", "thuy")] [InlineData("loi", "moc")] [InlineData("thuy", "hoa")]
    [InlineData("gio", "tho")] [InlineData("tho", "bang")] [InlineData("loi", "gio")] [InlineData("bang", "hoa")]
    public void CounterCycleAppliesBothDirections(string strong, string weak)
    {
        Assert.Equal(1, ElementCounter.Edge(strong, weak));
        Assert.Equal(-1, ElementCounter.Edge(weak, strong));
        Assert.Equal(1.2, ElementCounter.Multiplier(strong, weak), 10);
        Assert.Equal(0.8, ElementCounter.Multiplier(weak, strong), 10);
    }

    [Theory]
    [InlineData("hoa", "hoa")] [InlineData("moc", "gio")] [InlineData("thuy", "bang")] [InlineData("moc", "hoa")]
    [InlineData("hoa", "")] [InlineData("", "loi")] [InlineData("", "")]
    public void NeutralPairsKeepDamage(string attacker, string defender)
    {
        Assert.Equal(0, ElementCounter.Edge(attacker, defender));
        Assert.Equal(1, ElementCounter.Multiplier(attacker, defender));
    }

    [Fact]
    public void MultiElementPetUsesMostFavourableElement()
    {
        Assert.Equal("thuy", ElementCounter.Best(["moc", "thuy"], "hoa"));
        Assert.Equal("moc", ElementCounter.Best(["moc", "thuy"], "tho"));   // Mộc khắc Thổ, Thủy bị Thổ khắc
        Assert.Equal("", ElementCounter.Best([], "hoa"));
    }

    [Fact]
    public void PetResonanceKeepsTheSameGenerationCycle()
    {
        Assert.True(PetTraining.Supports("moc", "hoa"));
        Assert.True(PetTraining.Supports("thuy", "moc"));
        Assert.True(PetTraining.Supports("gio", "moc"));
        Assert.False(PetTraining.Supports("moc", "tho"));
        Assert.False(PetTraining.Supports("hoa", "hoa"));
    }

    [Theory]
    [InlineData("img/quai_bang_ho.png", "bang")]
    [InlineData("img/quai_bang_ho_v2.png", "bang")]          // biến thể U Minh giữ hệ gốc
    [InlineData("img/hell/boss_nguu_ma_vuong.png", "hoa")]   // boss Địa Ngục
    [InlineData("img/quai_bach_vo_thuong.png", "")]          // "_vo..." không phải hậu tố biến thể
    [InlineData("img/khong_co.png", "")]
    public void MonsterElementComesFromBaseImage(string image, string element) =>
        Assert.Equal(element, MonsterElements.Of(image));

    [Fact]
    public void MonsterTableOnlyUsesKnownElements()
    {
        Assert.All(MonsterElements.ById.Values, e => Assert.True(ElementCounter.Group(e) >= 0, e));
        Assert.All(MonsterElements.ById.Keys, k => Assert.Equal(k, MonsterElements.Key($"img/{k}.png")));
    }
}
