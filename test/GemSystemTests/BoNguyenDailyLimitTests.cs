using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class BoNguyenDailyLimitTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public void Dispose() => db.Dispose();

    private async Task<(GameService Game, Player P)> Setup()
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Bổ nguyên");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Realm = 5; p.Stage = 0; p.Qi = 0;
        p.ExploreReadyAt = DateTime.UtcNow.AddHours(1);
        return (game, p);
    }

    [Theory]
    [InlineData(0, 3600, 3600)]              // trong 12h đầu: 100%
    [InlineData(11 * 3600, 2 * 3600, 3600 + 1800)]   // tách mốc 12h: 1h × 100% + 1h × 50%
    [InlineData(24 * 3600, 3600, 1800)]      // sau 12h giữ 50%
    [InlineData(100 * 3600, 3600, 1800)]
    public void EffectiveSecondsSplitAcrossTiers(double from, double secs, double expected) =>
        Assert.Equal(expected, GameService.BoNguyenEffectiveSeconds(from, secs), 6);

    [Fact]
    public async Task NoDailyCapAndQualitiesShareEfficiency()
    {
        var (game, p) = await Setup();
        p.Add("bo_nguyen_dan_thuong", 100);
        var rate = (await game.BuildStateAsync(p)).Rate;
        var cap = (await game.BuildStateAsync(p)).QiReq;
        Assert.True((double)cap > rate * 5400 * 20, "cần trần tu vi đủ lớn để thử");

        game.UseItem(p, "bo_nguyen_dan_thuong", 20);   // vượt mức 10 viên cũ: 30h gốc
        Assert.Equal(80, p.Count("bo_nguyen_dan_thuong"));
        Assert.Equal(rate * (12 + 18 * 0.5) * 3600, (double)p.Qi, 0);
        var st = GameService.BoNguyenState(p);
        Assert.Equal(50, st.EfficiencyPct);
        Assert.Equal(30, st.HoursToday);
        Assert.Null(st.NextDropHours);
        Assert.Null(st.NextPct);

        var before = p.Qi;   // phẩm khác dùng chung hiệu quả
        p.Add("bo_nguyen_dan_ha", 1);
        game.UseItem(p, "bo_nguyen_dan_ha", 1);
        Assert.Equal(rate * 300 * 0.5, (double)(p.Qi - before), 0);
    }

    [Fact]
    public async Task ResetsOnNewDayAndIgnoresOldLimitFlag()
    {
        var (_, p) = await Setup();
        p.Admin = "bnlimit=2026-01-01|bo_nguyen_dan_thuong*10,bnqi=2026-01-01|200000";
        Assert.Equal(0, GameService.BoNguyenSecondsToday(p));
        Assert.Equal(100, GameService.BoNguyenState(p).EfficiencyPct);
    }
}
