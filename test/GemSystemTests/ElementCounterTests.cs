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

    [Fact]
    public void FireHeroIsCounteredByIceMonsterWhileWaterHeroIsNeutral()
    {
        var at = new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);
        var (chapter, area) = (from c in Enumerable.Range(0, 10)
                               from a in Enumerable.Range(1, GameData.AreasFor(0))
                               where GameData.AreaWaves(GameData.ChapterFor(0, c), a, 0).Any(w => MonsterElements.Of(w.Image) == "bang")
                               select (c, a)).First();
        Player Make(string manual) => new()
        {
            Id = Guid.Parse("50000000-0000-0000-0000-000000000001"), Realm = 0, Chapter = chapter, Diff = 0,
            BodyHp = 1_000_000_000_000, MapVer = 1, LearnedManuals = "hoa,thuy", ActiveManual = manual,
        };
        Assert.Equal(GameService.Stats(Make("hoa")).Def, GameService.Stats(Make("thuy")).Def);   // chỉ hệ khác nhau

        var fire = GameService.BattleAt(Make("hoa"), area, at);
        var water = GameService.BattleAt(Make("thuy"), area, at);
        var i = fire.Waves!.FindIndex(w => w.Element == "bang");
        Assert.True(i >= 0);
        Assert.Equal(-1, fire.Waves[i].ElementEdge);
        Assert.Equal(0, water.Waves![i].ElementEdge);
        Assert.Equal(Math.Max(1, CombatNumber.Scale(water.Waves[i].MonsterDmg, 1.2)), fire.Waves[i].MonsterDmg);
        Assert.Equal(MonsterElements.Of(fire.Image), fire.MonsterElement);
    }

    [Fact]
    public void HeroBasicHitIsScaledByTheElementCounter()
    {
        var at = new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);
        var (chapter, area) = (from c in Enumerable.Range(0, 10)
                               from a in Enumerable.Range(1, GameData.AreasFor(0))
                               where GameData.AreaWaves(GameData.ChapterFor(0, c), a, 0).Any(w => MonsterElements.Of(w.Image) == "bang")
                               select (c, a)).First();
        Player Make(string manual) => new()
        {
            Id = Guid.Parse("50000000-0000-0000-0000-000000000001"), Realm = 0, Chapter = chapter, Diff = 0,
            BodyHp = 1_000_000_000_000, MapVer = 1, LearnedManuals = "hoa,thuy", ActiveManual = manual,
        };
        var fire = GameService.BattleAt(Make("hoa"), area, at);
        var water = GameService.BattleAt(Make("thuy"), area, at);
        Assert.Equal(fire.Basic, water.Basic);   // cùng đòn thường → chỉ khác hệ số tương khắc
        var i = fire.Waves!.FindIndex(w => w.Element == "bang");
        int End(BattleDto b) => i + 1 < b.Waves!.Count ? b.Waves[i + 1].Start : b.BasicDamage!.Length;
        // đòn thường không bạo, không hụt, không vỡ băng trong đợt quái Băng của mỗi trận
        decimal FirstPlain(BattleDto b) => Enumerable.Range(b.Waves![i].Start, End(b) - b.Waves[i].Start)
            .Where(t => b.BasicDamage![t] > 0 && !b.BasicCritical![t] && !(b.Shatter?[t] ?? false))
            .Select(t => b.BasicDamage![t]).First();
        var f = FirstPlain(fire); var w = FirstPlain(water);
        Assert.InRange(f, CombatNumber.Scale(w, 0.8) - 1, CombatNumber.Scale(w, 0.8) + 1);   // Băng khắc Hỏa: −20% so với Thủy (trung lập)
    }

    [Fact]
    public void PetBurnTickIgnoresTheElementCounter()
    {
        PetCastDto Cast(PetCombat pet, double takeK, double? dot) { decimal hp = 1000; return pet.Cast(0, ref hp, 1_000_000, 1_000_000, 0, takeK, dot); }
        decimal Burn(PetCombat pet) { decimal hp = 1000; return pet.BeginTurn(1, ref hp, 1_000_000, 0, 1)!.Dmg; }
        PetCombat New() => new(PetData.ById["chu_tuoc"], 2, new(100, 1000, 10), pet: new(1000, 1_000_000, 10));
        var neutral = New(); var strong = New();
        var n = Cast(neutral, 1, 1); var s = Cast(strong, 1.2, 1);
        Assert.True(s.Dmg > n.Dmg);                     // đòn trực tiếp chịu tương khắc
        Assert.Equal(Burn(neutral), Burn(strong));      // thiêu đốt thì không
    }

    [Fact]
    public void ArenaUsesBothFightersActiveManuals()
    {
        var a = PvpFighter.FromStats("A", 0, new() { ["atk"] = 100, ["hp"] = 10000 }) with { Crit = 0, BasicPct = 100, BasicCrit = 0, Manual = "thuy" };
        var neutral = PvpCombat.Run(a, a with { Name = "D", Manual = "thuy" }, 42);
        var strong = PvpCombat.Run(a, a with { Name = "D", Manual = "hoa" }, 42);
        var n0 = neutral.Actions.First(x => x.ByA).Basic;
        var s0 = strong.Actions.First(x => x.ByA).Basic;
        Assert.InRange(s0, n0 * 1.2m - 1, n0 * 1.2m + 1);

        var replay = PvpCombat.ToBattle(strong).battle;
        Assert.Equal("hoa", replay.MonsterElement);
        Assert.Equal(1, replay.ElementEdge);
    }
}
