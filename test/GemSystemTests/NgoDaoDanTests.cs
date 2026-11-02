using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class NgoDaoDanTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    private async Task<(GameService Game, Player Player)> Setup()
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Ngộ đạo");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Realm = 3;
        p.Add("ngo_dao_dan", 5);
        return (game, p);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(9, 5)]
    [InlineData(0, 5)]
    public async Task UsesRequestedCountCappedByStock(int n, int used)
    {
        var (game, p) = await Setup();
        var before = p.ElixirPoints;
        game.UseItem(p, "ngo_dao_dan", n);
        Assert.Equal(before + used, p.ElixirPoints);
        Assert.Equal(5 - used, p.CountInt("ngo_dao_dan"));
    }

    [Fact]
    public async Task BelowNascentSoulKeepsAllPills()
    {
        var (game, p) = await Setup();
        p.Realm = 2;
        Assert.Throws<GameException>(() => game.UseItem(p, "ngo_dao_dan", 0));
        Assert.Equal(5, p.CountInt("ngo_dao_dan"));
    }

    public void Dispose() => db.Dispose();
}
