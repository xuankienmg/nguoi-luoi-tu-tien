using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class CultivationPillBatchTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    private async Task<(GameService Game, Player Player, StateDto State)> Setup()
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Dùng đan");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Realm = 5; p.Stage = 0;
        p.ExploreReadyAt = DateTime.UtcNow.AddHours(1);
        return (game, p, await game.BuildStateAsync(p));
    }

    [Theory]
    [InlineData("bo_nguyen_dan_ha")]
    [InlineData("bo_nguyen_dan_trung")]
    [InlineData("bo_nguyen_dan_thuong")]
    public async Task BatchStopsAtCapAndRetainsUnusedPills(string item)
    {
        var (game, p, state) = await Setup();
        var gain = state.Rate * GameData.ItemById[item].UseQiSeconds;
        p.Qi = state.QiReq - gain * 2.5;
        p.Add(item, 10);
        game.UseItem(p, item, 0);
        Assert.Equal(state.QiReq, p.Qi);
        Assert.Equal(7, p.Count(item));
        Assert.Throws<GameException>(() => game.UseItem(p, item, 0));
        Assert.Equal(7, p.Count(item));
    }

    [Fact]
    public async Task ExactBoundaryDoesNotConsumeAnExtraPill()
    {
        var (game, p, state) = await Setup();
        const string item = "bo_nguyen_dan_ha";
        p.Qi = state.QiReq - state.Rate * GameData.ItemById[item].UseQiSeconds * 2;
        p.Add(item, 10);
        game.UseItem(p, item, 0);
        Assert.Equal(state.QiReq, p.Qi);
        Assert.Equal(8, p.Count(item));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(0, 3)]
    public async Task RespectsRequestedCountAndAvailableInventory(int requested, int expected)
    {
        var (game, p, state) = await Setup();
        const string item = "bo_nguyen_dan_ha";
        p.Qi = 0; p.Add(item, 3);
        game.UseItem(p, item, requested);
        Assert.Equal(state.Rate * GameData.ItemById[item].UseQiSeconds * expected, p.Qi);
        Assert.Equal(3 - expected, p.Count(item));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task FullCultivationPreservesEveryPill(int requested)
    {
        var (game, p, state) = await Setup();
        const string item = "bo_nguyen_dan_ha";
        p.Qi = state.QiReq; p.Add(item, 10);
        Assert.Throws<GameException>(() => game.UseItem(p, item, requested));
        Assert.Equal(10, p.Count(item));
        Assert.Equal(state.QiReq, p.Qi);
    }

    public void Dispose() => db.Dispose();
}
