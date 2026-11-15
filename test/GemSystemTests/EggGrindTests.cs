using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class EggGrindTests
{
    [Fact]
    public async Task GrindEggsConvertsByQualityAndClampsToOwned()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var player = await game.CreateAsync("Nghiền trứng");
        var egg = PetData.EggIds[3];
        player.Add(egg, 5);
        var before = player.Count("linh_thu_dan");

        game.GrindEggs(player, egg, 2);
        Assert.Equal(3, player.Count(egg));
        Assert.Equal(before + 2 * PetData.EggGrindFood[3], player.Count("linh_thu_dan"));

        game.GrindEggs(player, egg, 99);
        Assert.Equal(0, player.Count(egg));
        Assert.Equal(before + 5 * PetData.EggGrindFood[3], player.Count("linh_thu_dan"));

        Assert.Throws<GameException>(() => game.GrindEggs(player, egg, 1));
        Assert.Throws<GameException>(() => game.GrindEggs(player, "linh_thach", 1));
    }

    [Fact]
    public void GrindYieldsLessThanReleasingHatchedPet()
    {
        for (var q = 0; q < PetData.EggGrindFood.Length; q++)
            Assert.True(PetData.EggGrindFood[q] < PetData.ReleaseFood[q]);
    }
}
