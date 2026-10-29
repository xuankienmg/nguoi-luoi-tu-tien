using VanDao.Game;

namespace GemSystemTests;

public class GemCombatRatingBudgetTests
{
    [Fact]
    public void PrimaryBudgetsUseOneSpecialtyAndExactLevelValues()
    {
        Assert.Equal(new CombatRatings(Crit: 2), GemRatingBudget.For("hoa", 1));
        Assert.Equal(new CombatRatings(Accuracy: 10), GemRatingBudget.For("quang", 5));
        Assert.Equal(new CombatRatings(ComboResist: 30), GemRatingBudget.For("tho", 10));
        Assert.Equal(new CombatRatings(ReflectResist: 30), GemRatingBudget.For("vu_tru", 10));
        Assert.Equal(new CombatRatings(), GemRatingBudget.For("linh", 10));
        Assert.Equal(new CombatRatings(), GemRatingBudget.For("phuc_nguyen", 10));
    }

    [Theory]
    [InlineData("hoa", 0)]
    [InlineData("hoa", 11)]
    [InlineData("unknown", 1)]
    [InlineData(null, 1)]
    public void UnsupportedInputsAreRejected(string? theme, int level)
        => Assert.Throws<ArgumentOutOfRangeException>(() => GemRatingBudget.For(theme!, level));

    [Fact]
    public void GeneratedGemsUseTheFullBudgetCurveWithoutDuplicatePrimaryEffects()
    {
        var values = new double[] { 2, 4, 6, 8, 10, 13, 16, 20, 25, 30 };
        var generated = GemProgression.CreateAll().ToDictionary(i => i.Id);
        var effectIds = new Dictionary<string, string> {
            ["loi"] = "lien_kich", ["bang"] = "gem_crit_resist", ["tho"] = "gem_combo_resist",
            ["phong"] = "gem_dodge", ["quang"] = "gem_accuracy", ["am"] = "dia_mach", ["vu_tru"] = "gem_reflect_resist"
        };
        for (var tier = 0; tier < 10; tier++)
        {
            Assert.Equal(values[tier], generated[GemProgression.Id("hoa", tier)].CritPct);
            foreach (var (theme, effectId) in effectIds)
            {
                var item = generated[GemProgression.Id(theme, tier)];
                Assert.Equal(values[tier], Assert.Single(item.Effects!, e => e.Id == effectId).Value);
                var ratings = GemRatingBudget.For(theme, tier + 1);
                Assert.Equal(values[tier], new[] { ratings.Crit, ratings.CritResist, ratings.Accuracy, ratings.Dodge,
                    ratings.Combo, ratings.ComboResist, ratings.Reflect, ratings.ReflectResist }.Sum());
            }
        }
    }

    [Fact]
    public void NewSpecialtiesPreserveSecondaryRatingsAndUtilityEffects()
    {
        var gems = GemProgression.CreateAll().ToDictionary(i => i.Id);
        ItemDef Gem(string theme) => gems[GemProgression.Id(theme, 9)];
        double Effect(string theme, string id) => Assert.Single(Gem(theme).Effects!, e => e.Id == id).Value;
        Assert.Equal(7.75, Gem("loi").CritPct);
        Assert.Equal(30, Effect("loi", "lien_kich"));
        Assert.Equal(5, Effect("tho", "gem_crit_resist"));
        Assert.Equal(30, Effect("tho", "gem_combo_resist"));
        Assert.Equal(5, Effect("vu_tru", "gem_dodge"));
        Assert.Equal(30, Effect("vu_tru", "gem_reflect_resist"));
        Assert.Equal(30, Effect("phong", "gem_dodge"));
        Assert.Equal(10, Effect("phong", "lien_kich"));
        Assert.Equal(6.4, Effect("phong", "gem_move_speed"));
        Assert.Equal(20, Effect("hoa", "gem_crit_damage"));
        Assert.Equal(10, Effect("bang", "gem_block"));
        Assert.Equal(5, Effect("am", "gem_lifesteal"));
        Assert.Equal(1, Effect("am", "gem_extra_attack"));
        Assert.Equal(7, Effect("linh", "gem_rage_hit"));
        Assert.Equal(1.28, Effect("phuc_nguyen", "gem_regen"));
        Assert.All(gems.Values, item => Assert.Equal(item.Effects!.Length, item.Effects.Select(e => e.Id).Distinct().Count()));
        Assert.Equal("attack", Gem("phong").GemCategory);
        Assert.Equal("attack", Gem("am").GemCategory);
    }

    [Theory]
    [InlineData("core_01.png", "quang", "gem_accuracy")]
    [InlineData("core_02.png", "linh", "gem_rage_hit")]
    [InlineData("core_03.png", "tho", "gem_combo_resist")]
    [InlineData("core_04.png", "hoa", "")]
    [InlineData("core_05.png", "bang", "gem_crit_resist")]
    [InlineData("core_06.png", "phuc_nguyen", "gem_regen")]
    [InlineData("core_07.png", "loi", "lien_kich")]
    [InlineData("core_08.png", "phong", "gem_dodge")]
    [InlineData("core_09.png", "quang", "gem_accuracy")]
    [InlineData("core_10.png", "phong", "gem_dodge")]
    [InlineData("core_11.png", "loi", "lien_kich")]
    [InlineData("core_12.png", "am", "dia_mach")]
    [InlineData("core_13.png", "vu_tru", "gem_reflect_resist")]
    [InlineData("core_14.png", "tho", "gem_combo_resist")]
    [InlineData("core_15.png", "vu_tru", "gem_reflect_resist")]
    public void LegacyGemsUseSameBudgetWithoutChangingIdentityOrGrade(string icon, string theme, string effectId)
    {
        for (var tier = 0; tier < 10; tier++)
        {
            var old = new ItemDef("saved_id", "Tên cũ · Thô", "gem", "#fff", "", Icon: "img/core/" + icon, Rarity: GemProgression.Qualities[tier]);
            var upgraded = GemProgression.RebalanceLegacy(old);
            var canonical = GemProgression.CreateAll().Single(i => i.Id == GemProgression.Id(theme, tier));
            Assert.Equal(old.Id, upgraded.Id); Assert.Equal(old.Icon, upgraded.Icon); Assert.Equal(old.Rarity, upgraded.Rarity);
            Assert.Equal(tier + 1, upgraded.GemLevel);
            Assert.Equal($"Tên cũ · Cấp {tier + 1}", upgraded.Name);
            Assert.Equal(canonical.CritPct, upgraded.CritPct);
            if (effectId.Length > 0)
                Assert.Equal(Assert.Single(canonical.Effects!, e => e.Id == effectId).Value, Assert.Single(upgraded.Effects!, e => e.Id == effectId).Value);
        }
    }

    [Fact]
    public void AllSavedLegacyAndStandardGemIdsRemainResolvable()
    {
        string[] legacy = ["stone_viem_so", "stone_bang_so", "stone_loi_so", "stone_kim_so", "stone_tho_so",
            "stone_viem_cao", "stone_bang_cao", "stone_loi_cao", "stone_kim_cao", "stone_tho_cao",
            "stone_liet_loi", "stone_huyen_bang", "stone_am_duong", "gem_ruby_cui", "gem_sapphire_cui",
            "gem_topaz_cui", "gem_emerald_cui", "gem_amethyst_cui", "gem_ruby_xin", "gem_sapphire_xin",
            "gem_topaz_xin", "gem_emerald_xin", "gem_amethyst_xin", "gem_thien_phu"];
        var expected = legacy.Concat(GemProgression.Themes.SelectMany(theme => Enumerable.Range(0, 10).Select(tier => GemProgression.Id(theme, tier)))).ToHashSet();
        Assert.Equal(124, expected.Count);
        Assert.True(expected.SetEquals(GemData.Items.Select(i => i.Id)));
        foreach (var id in expected)
        {
            Assert.True(GemData.ById.ContainsKey(id), id);
            Assert.True(GameData.ItemById.ContainsKey(id), id);
        }
    }
}
