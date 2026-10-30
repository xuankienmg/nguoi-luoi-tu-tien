using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class MonsterCombatRatingsTests
{
    [Fact]
    public void ResistanceBonusAddsFiftyPointsAfterTierScalingAndNeverStacksOnReapply()
    {
        Assert.Equal(new CombatRatings(40, 90, 90, 40, 40, 90, 40, 90), MonsterCombatRatings.For("unknown", "normal", 0));
        Assert.Equal(new CombatRatings(70, 120, 120, 70, 70, 120, 70, 120), MonsterCombatRatings.For("unknown", "elite", 0));
        Assert.Equal(new CombatRatings(100, 150, 100, 100, 100, 150, 100, 150), MonsterCombatRatings.For("boss_hac_giao", "boss", 0));
        var monster = MonsterCombatRatings.Apply(new("M", "m.png", 1, 1, 1), 3);
        Assert.Equal(210, monster.CritResist);
        Assert.Equal(monster, MonsterCombatRatings.Apply(monster, 3));
    }
    private sealed class FixedRandom(double value) : Random { public override double NextDouble() => value; }
    private static double[] Values(CombatRatings r) => [r.Crit, r.CritResist, r.Accuracy, r.Dodge, r.Combo, r.ComboResist, r.Reflect, r.ReflectResist];

    [Fact]
    public void BossProfilesDoNotLeakFromTheStatScalingTemplate()
    {
        Assert.Equal(250, GameData.AreaWaves(GameData.Chapters[0], 16, 1).Single().Combo);
        Assert.Equal(200, GameData.AreaWaves(GameData.Chapters[0], 16, 1).Single().Crit);
        Assert.Equal(250, GameData.AreaWaves(GameData.Chapters[1], 16, 1).Single().CritResist);
        Assert.Equal(450, HellMapData.AreaWaves(HellMapData.Chapter(0), 31).Single().Accuracy);
        Assert.Equal(450, HellMapData.AreaWaves(HellMapData.Chapter(9), 31).Single().ReflectResist);
    }

    [Fact]
    public void MonsterCriticalSubtractsFullResistanceBeforeRollingAndActivatesOnce()
    {
        var monster = new MonsterDef("M", "m.png", 1, 1, 1, Crit: 350);
        Assert.True(MonsterCombatRatings.RollCritical(new FixedRandom(.69), monster, 280));
        Assert.False(MonsterCombatRatings.RollCritical(new FixedRandom(.70), monster, 280));
        Assert.False(MonsterCombatRatings.RollCritical(new FixedRandom(0), monster, 350));
        Assert.True(MonsterCombatRatings.RollCritical(new FixedRandom(.999), monster, 0));
    }

    [Theory]
    [InlineData(0, 100, 150)]
    [InlineData(1, 200, 250)]
    [InlineData(2, 300, 350)]
    [InlineData(3, 400, 450)]
    [InlineData(4, 500, 550)]
    public void EveryBossRetainsItsFeaturedRatingBeforeTheResistanceBonus(int difficulty, double baseline, double featured)
    {
        // Resistances sit at baseline + 50; the featured rating is raised to baseline + 50 and does not stack on a resistance.
        foreach (var (id, profile) in MonsterCombatRatings.BossProfiles)
        {
            double[] expected = [baseline, baseline + 50, baseline, baseline, baseline, baseline + 50, baseline, baseline + 50];
            expected[(int)profile] = featured;
            Assert.Equal(expected, Values(MonsterCombatRatings.For(id, "boss", difficulty)));
        }
        Assert.Equal(150, MonsterCombatRatings.For("boss_tu_ty_ma_vien", "boss", 0).Combo);
        Assert.Equal(150, MonsterCombatRatings.For("boss_bang_phuong", "boss", 0).Dodge);
        Assert.Equal(150, MonsterCombatRatings.For("boss_kiem_de", "boss", 0).Crit);
    }

    [Theory]
    [InlineData(0, 40, 70)]
    [InlineData(1, 80, 140)]
    [InlineData(2, 120, 210)]
    [InlineData(3, 160, 280)]
    public void CommonAndEliteUseFractionsOfBossBaseline(int difficulty, double common, double elite)
    {
        Assert.Equal(new CombatRatings(common, common + 50, common + 50, common, common, common + 50, common, common + 50), MonsterCombatRatings.For("unknown", "", difficulty));
        Assert.Equal(new CombatRatings(common, common + 50, common + 50, common, common, common + 50, common, common + 50), MonsterCombatRatings.For("unknown", "normal", difficulty));
        Assert.Equal(new CombatRatings(elite, elite + 50, elite + 50, elite, elite, elite + 50, elite, elite + 50), MonsterCombatRatings.For("unknown", "elite", difficulty));
    }

    [Fact]
    public void GeneratedWavesHaveStableIdentityAndRatingsForTheirOwnBossAndDifficulty()
    {
        for (var difficulty = 0; difficulty <= 3; difficulty++)
        for (var chapter = 0; chapter < GameData.OpenChapters; chapter++)
        {
            var c = difficulty == 3 ? HellMapData.Chapter(chapter) : GameData.Chapters[chapter];
            foreach (var area in new[] { 1, 10, GameData.BossAreaFor(difficulty) })
            foreach (var monster in GameData.AreaWaves(c, area, difficulty))
            {
                Assert.False(string.IsNullOrEmpty(monster.Id));
                if (monster.Tier == "boss") Assert.True(MonsterCombatRatings.BossProfiles.ContainsKey(monster.Id), monster.Id);
                Assert.Equal(MonsterCombatRatings.For(monster.Id, monster.Tier, difficulty), CombatRatings.From(monster));
                var renamed = MonsterCombatRatings.Apply(monster with { Name = "Translated", Image = "different.png" }, difficulty);
                Assert.Equal(CombatRatings.From(monster), CombatRatings.From(renamed));
            }
            var trait = GameData.DiffTrait(c, difficulty);
            if (trait is not null) { Assert.Equal(0, trait.CritRes); Assert.Equal(0, trait.ExtraRes); }
        }
    }

    [Fact]
    public void UnknownBossHasDeterministicFallbackAndOldMonstersLoadWithoutId()
    {
        Assert.Equal(new CombatRatings(150, 150, 100, 100, 100, 150, 100, 150), MonsterCombatRatings.For("unknown", "boss", 0));
        var old = JsonSerializer.Deserialize<MonsterDef>("{\"Name\":\"Old\",\"Image\":\"img/boss_bang_phuong.png\",\"Tier\":\"boss\"}")!;
        var upgraded = MonsterCombatRatings.Apply(old, 0);
        Assert.Equal("boss_bang_phuong", upgraded.Id);
        Assert.Equal(150, upgraded.Dodge);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task MapDtoCarriesExactlyTheRatingsUsedByWaveGeneration(int difficulty)
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var p = new Player { Realm = 9, Chapter = 0, Diff = difficulty };
        db.Players.Add(p); await db.SaveChangesAsync();
        var state = await new GameService(db).BuildStateAsync(p);
        var c = difficulty == 3 ? HellMapData.Chapter(0) : GameData.Chapters[0];
        foreach (var area in state.Map.Areas)
        {
            var generated = GameData.AreaWaves(c, area.Index, difficulty);
            Assert.Equal(generated.Length, area.Waves!.Count);
            for (var i = 0; i < generated.Length; i++)
            {
                var w = area.Waves[i];
                Assert.Equal(CombatRatings.From(generated[i]), new CombatRatings(w.Crit, w.CritResist, w.Accuracy, w.Dodge, w.Combo, w.ComboResist, w.Reflect, w.ReflectResist));
            }
        }
    }
}
