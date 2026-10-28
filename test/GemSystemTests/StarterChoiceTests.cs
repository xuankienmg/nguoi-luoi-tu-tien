using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class StarterChoiceTests
{
    [Fact]
    public async Task ChoiceUnlocksOneTreeWithoutSpendingPointsAndSurvivesReloadAndReset()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var player = await game.CreateAsync("Khởi đầu");
        Assert.False(player.StarterChosen);
        Assert.Equal(0, player.Count(PetData.StarterEgg));
        Assert.Equal(0, player.Count(CultivationManuals.Starter));
        var initial = await game.BuildStateAsync(player);
        Assert.True(initial.StarterChoice!.Required);
        Assert.Equal(7, initial.StarterChoice.Manuals.Length);
        Assert.NotEmpty(initial.StarterChoice.Pets);
        Assert.All(initial.StarterChoice.Pets, option => Assert.InRange(PetData.EggQuality(option.Egg), 0, 1));
        foreach (var highEgg in PetData.StarterEggs.Keys.Where(egg => PetData.EggQuality(egg) > 1))
            Assert.Throws<GameException>(() => game.ChooseStarter(player, "thuy", highEgg));
        Assert.Equal(0, initial.Rate);
        Assert.Throws<GameException>(() => game.ChooseStarter(player, "invalid", PetData.StarterEgg));
        Assert.Throws<GameException>(() => game.ChooseStarter(player, "thuy", "thu_dan_thuong_co"));
        Assert.False(player.StarterChosen);
        Assert.Empty(CultivationManuals.Unlocked(player));
        var egg = PetData.StarterEggs.First(kv => kv.Value == "bang_tinh_tho").Key;
        game.ChooseStarter(player, "thuy", egg);
        Assert.True(player.StarterChosen);
        Assert.Equal(new[] { "thuy" }, CultivationManuals.Unlocked(player));
        Assert.Equal("thuy", player.ActiveManual);
        Assert.Equal(CultivationManuals.BasicOf("thuy")!.Id, player.Slots()[0]);
        Assert.Empty(player.SkillLevels());
        Assert.Equal(1, player.Count(egg));
        Assert.Throws<GameException>(() => game.ChooseStarter(player, "hoa", egg));
        Assert.Equal(1, player.Count(egg));
        await db.SaveChangesAsync(); var id = player.Id; db.ChangeTracker.Clear();
        var reloaded = await game.LoadAsync(id);
        Assert.True(reloaded.StarterChosen);
        var fresh = await game.ResetAsync(reloaded);
        Assert.False(fresh.StarterChosen);
    }

    [Fact]
    public async Task EveryStarterEggHatchesItsGuaranteedSpecies()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated(); var game = new GameService(db);
        Assert.Equal(7, PetData.StarterEggs.Count);
        foreach (var (egg, species) in PetData.StarterEggs)
        {
            var p = new Player(); db.Players.Add(p); p.Add(egg, 1);
            Assert.True(GameData.ItemById.ContainsKey(egg));
            game.Hatch(p, egg);
            p.Hatch = $"0|{egg}|{DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()}";
            game.CollectHatch(p);
            Assert.Equal(species, Assert.Single(p.Pets).DefId);
            Assert.Equal(0, p.Count(egg));
        }
    }

    [Fact]
    public void ExistingCharactersSkipSelectionByDefault()
    {
        var player = new Player();
        Assert.True(player.StarterChosen);
        Assert.Throws<GameException>(() => new GameService(null!).ChooseStarter(player, "hoa", PetData.StarterEgg));
    }

    [Fact]
    public async Task AdminResetRequiresFreshStarterSelection()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated(); var game = new GameService(db);
        var player = await game.CreateAsync("Tu lại");
        game.ChooseStarter(player, "hoa", PetData.StarterEgg);
        await db.SaveChangesAsync();
        game.AdminAction(player, "resetall", null, 1);
        Assert.False(player.StarterChosen);
        Assert.Empty(CultivationManuals.Unlocked(player));
        game.ChooseStarter(player, "moc", PetData.StarterEgg);
        Assert.Equal("moc", player.ActiveManual);
    }

    [Fact]
    public async Task SchemaUpgradeKeepsOldCharactersOutOfStarterSelection()
    {
        // Upgrade closes its connection, so keep the in-memory database alive separately.
        using var keeper = new SqliteConnection("Data Source=starter-schema;Mode=Memory;Cache=Shared"); keeper.Open();
        using var connection = new SqliteConnection(keeper.ConnectionString); connection.Open();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var player = new Player(); db.Players.Add(player); await db.SaveChangesAsync();
        var id = player.Id; db.ChangeTracker.Clear();
        db.Database.ExecuteSqlRaw("ALTER TABLE Players DROP COLUMN StarterChosen");
        SchemaUpgrade.Apply(db);
        Assert.True((await db.Players.SingleAsync(p => p.Id == id)).StarterChosen);
    }
}
