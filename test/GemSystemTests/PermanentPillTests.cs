using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class PermanentPillTests
{
    [Fact]
    public void SpecificationCapsAndBonusesAreExact()
    {
        Assert.Equal(new[] { 100, 100, 100, 50, 50, 50, 25, 50, 25, 30 }, PermanentPillData.All.Select(d => d.Cap));
        Assert.Equal(new[] { 10d, 100, 10, .2, .5, .2, .2, .2, .2, .1 }, PermanentPillData.All.Select(d => d.Value));
        Assert.All(PermanentPillData.All, d => Assert.True(GameService.Tradable(GameData.ItemById[d.Id])));
    }

    [Fact]
    public void OnlineUseRejectsClientUploadedStatsAndCounts()
    {
        using var payload = System.Text.Json.JsonDocument.Parse("{\"permanentPills\":{\"thien_luc_dan\":0},\"atk\":999999}");
        Assert.False(VanDao.Online.OnlineBoundary.BodyAllowed("/api/players/00000000-0000-0000-0000-000000000001/use/thien_luc_dan", payload.RootElement));
    }

    [Fact]
    public void SingleGradeBrewingStillWorksAtConsumptionCap()
    {
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
        var game = new GameService(db); var p = new Player();
        p.Add("thien_luc_dan", 100); game.Use(p, "thien_luc_dan", 0);
        var recipe = GameData.Brews.Single(b => b.Out == "thien_luc_dan");
        foreach (var (id, n) in recipe.In) p.Add(id, n * 10);
        var result = game.Brew(p, "thien_luc_dan", 10, new SuccessfulBrew());
        Assert.Equal(10m, p.Count("thien_luc_dan"));
        Assert.Equal(100, PermanentPillData.Used(p, "thien_luc_dan"));
        Assert.All(recipe.In, kv => Assert.Equal(0m, p.Count(kv.Key)));
        Assert.All(result.craft.Outputs!, item => Assert.Equal("thien_luc_dan", item.Item));
    }
    private class SuccessfulBrew : Random { public override double NextDouble() => .99; }

    [Fact]
    public void ActualPveUsesPenetrationCriticalDamageAndLifeSteal()
    {
        var p = new Player { Realm = 5, Id = Guid.Parse("12345678-0000-0000-0000-000000000001"), ExploreReadyAt = new DateTime(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc) };
        var foe = new MonsterDef("Test", "", 1000000000m, 1000, 100);
        var baseline = GameService.BattleAt(p, 1, forecast: true, foe: foe);
        foreach (var id in new[] { "pha_giap_dan", "bao_nguyen_dan", "phe_huyet_dan" })
        { p.Add(id, 100); PermanentPillData.Use(p, id, 0); }
        var boosted = GameService.BattleAt(p, 1, forecast: true, foe: foe);
        Assert.Equal(95m, boosted.MonsterDef);
        Assert.Contains(boosted.Heal!, h => h > 0);
        var crit = Array.FindIndex(baseline.BasicCritical!, c => c);
        Assert.True(crit >= 0);
        Assert.True(boosted.BasicDamage![crit] > baseline.BasicDamage![crit]);
    }

    [Fact]
    public async Task ResetCannotBypassCharacterLifetimeCaps()
    {
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync(); await db.Database.EnsureCreatedAsync();
        var game = new GameService(db); var p = await game.CreateAsync("Lifetime");
        p.Add("thien_luc_dan", 100); game.Use(p, "thien_luc_dan", 0); await db.SaveChangesAsync();
        var fresh = await game.ResetAsync(p);
        Assert.Equal(p.Id, fresh.Id);
        Assert.Equal(100, PermanentPillData.Used(fresh, "thien_luc_dan"));
        Assert.Equal(1000, PermanentPillData.Total(fresh, "atk"));
    }

    [Fact]
    public void PvePenetrationAndLeechUsePermanentBonuses()
    {
        var p = new Player();
        p.Add("pha_giap_dan", 25); PermanentPillData.Use(p, "pha_giap_dan", 0);
        p.Add("phe_huyet_dan", 30); PermanentPillData.Use(p, "phe_huyet_dan", 0);
        Assert.Equal(5, GameService.GearEffects(p).Pen, 6);
        Assert.Equal(3m, GemCombat.LifeSteal(100, 100, PermanentPillData.CombatBonuses(p)));
        Assert.Equal(1m, GemCombat.LifeSteal(100, 20, PermanentPillData.CombatBonuses(p))); // 0.6 rounds to one
    }

    [Fact]
    public void OldSqliteCharactersUpgradeWithZeroLifetimeUsage()
    {
        using var anchor = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=pill-upgrade;Mode=Memory;Cache=Shared");
        anchor.Open();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=pill-upgrade;Mode=Memory;Cache=Shared").Options);
        db.Database.OpenConnection(); db.Database.EnsureCreated();
        db.Players.Add(new Player { Name = "Cũ" }); db.SaveChanges(); db.ChangeTracker.Clear();
        db.Database.ExecuteSqlRaw("ALTER TABLE Players DROP COLUMN PermanentPills");
        SchemaUpgrade.Apply(db); SchemaUpgrade.Apply(db);
        var p = db.Players.Single();
        Assert.All(PermanentPillData.State(p), d => Assert.Equal(0, d.Used));
    }

    [Fact]
    public void TenSingleGradeRecipesUseExistingMaterials()
    {
        Assert.Equal(10, PermanentPillData.All.Length);
        foreach (var d in PermanentPillData.All)
        {
            Assert.False(Alchemy.HasQuality(d.Id));
            Assert.False(GameData.ItemById.ContainsKey(d.Id + "_trung"));
            var recipe = Assert.Single(GameData.Brews.Where(b => b.Out == d.Id));
            Assert.All(recipe.In, kv => { Assert.True(GameData.ItemById.ContainsKey(kv.Key)); Assert.True(kv.Value > 0); });
        }
    }

    [Fact]
    public void BulkConsumptionClampsToLifetimeCapAndPreservesSurplus()
    {
        var p = new Player();
        p.Add("thien_luc_dan", 150);
        PermanentPillData.Use(p, "thien_luc_dan", 35);
        Assert.Equal(35, PermanentPillData.Used(p, "thien_luc_dan"));
        PermanentPillData.Use(p, "thien_luc_dan", int.MaxValue);
        Assert.Equal(100, PermanentPillData.Used(p, "thien_luc_dan"));
        Assert.Equal(50m, p.Count("thien_luc_dan"));
        Assert.Equal(1000, PermanentPillData.Total(p, "atk"));
        Assert.Throws<GameException>(() => PermanentPillData.Use(p, "thien_luc_dan", 0));
        Assert.Equal(50m, p.Count("thien_luc_dan"));
        Assert.Equal(0, PermanentPillData.Used(new Player(), "thien_luc_dan"));
    }

    [Fact]
    public void InvalidRequestsDoNotMutateInventoryOrCounters()
    {
        var p = new Player(); p.Add("pha_quan_dan", 3);
        Assert.Throws<GameException>(() => PermanentPillData.Use(p, "pha_quan_dan", -1));
        Assert.Throws<GameException>(() => PermanentPillData.Use(p, "missing", 1));
        Assert.Throws<GameException>(() => PermanentPillData.Use(p, "vo_anh_dan", 1));
        Assert.Equal(3m, p.Count("pha_quan_dan"));
        Assert.Equal(0, PermanentPillData.Used(p, "pha_quan_dan"));
    }

    [Fact]
    public async Task BonusesPersistAndAreIncludedInPveAndArena()
    {
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync(); await db.Database.EnsureCreatedAsync();
        var game = new GameService(db);
        var p = await game.CreateAsync("Vĩnh viễn");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Skills = ""; p.Pets.Clear(); p.Equipment.Clear();
        var before = GameService.Stats(p, buffs: false);
        foreach (var d in PermanentPillData.All) { p.Add(d.Id, d.Cap + 5); game.Use(p, d.Id, 0); }
        var after = GameService.Stats(p, buffs: false);
        Assert.Equal(before.Atk + 1020, after.Atk);
        Assert.Equal(before.Hp + 10000, after.Hp);
        Assert.Equal(before.Def + 1000, after.Def);
        Assert.Equal(before.Crit + 10, after.Crit, 6);
        Assert.Equal(before.Dodge + 5, after.Dodge, 6);
        Assert.Equal(before.Accuracy + 10, after.Accuracy, 6);
        Assert.Equal(before.Combo + 10, after.Combo, 6);
        var fighter = GameService.PvpFighterOf(p);
        Assert.Equal(.75, fighter.CritMult, 6);
        Assert.Equal(5, fighter.Pen, 6);
        Assert.Equal(3, fighter.Gems!.LifeSteal, 6);
        var saved = p.PermanentPills;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var loaded = await db.Players.SingleAsync(x => x.Id == p.Id);
        Assert.Equal(saved, loaded.PermanentPills);
        loaded.Realm = 1; loaded.Stage = 0;
        Assert.All(PermanentPillData.All, d => Assert.Equal(d.Cap, PermanentPillData.Used(loaded, d.Id)));
    }
}
