using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class BoNguyenDailyLimitTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public void Dispose() => db.Dispose();

    [Fact]
    public async Task LimitsPerQualityAndStopsWhenExhausted()
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Bổ nguyên");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Qi = 0;
        p.Add("bo_nguyen_dan_thuong", 100);
        Assert.Equal(10, GameService.BoNguyenRemaining(p, "bo_nguyen_dan_thuong"));
        Assert.Null(GameService.BoNguyenRemaining(p, "ngo_dao_dan"));
        game.Use(p, "bo_nguyen_dan_thuong", 0);
        var left = GameService.BoNguyenRemaining(p, "bo_nguyen_dan_thuong")!.Value;
        Assert.InRange(left, 0, 9);
        Assert.Equal(100 - (10 - left), p.CountInt("bo_nguyen_dan_thuong"));
        Assert.Equal(50, GameService.BoNguyenRemaining(p, "bo_nguyen_dan_ha"));   // phẩm khác tính riêng
    }
}
