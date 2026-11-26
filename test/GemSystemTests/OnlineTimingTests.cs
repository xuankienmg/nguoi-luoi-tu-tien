using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Online;
using Xunit;

namespace GemSystemTests;

public class OnlineTimingTests
{
    [Fact]
    public async Task Interceptors_count_sql_commands_and_connection_opens()
    {
        await using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:")
            .AddInterceptors(new DbTimingCommandInterceptor(), new DbTimingConnectionInterceptor()).Options);
        db.Timing = new DbTiming();
        await db.Database.ExecuteSqlRawAsync("SELECT 1");
        await db.Database.ExecuteSqlRawAsync("SELECT 2");
        Assert.Equal(2, db.Timing.Commands);
        Assert.True(db.Timing.Opens >= 1);
        Assert.True(db.Timing.CommandMs >= 0);
    }

    [Fact]
    public void Header_uses_server_timing_format()
    {
        var t = new DbTiming { Commands = 7, CommandMs = 12.345, Opens = 1, OpenMs = 3 };
        Assert.Equal("auth;dur=1.5, total;dur=40.0, db;dur=12.3;desc=\"7 sql\", conn;dur=3.0;desc=\"1 open\"",
            t.Header(("auth", 1.5), ("total", 40)));
    }

    [Fact]
    public void Context_without_timing_is_ignored()
    {
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:")
            .AddInterceptors(new DbTimingCommandInterceptor(), new DbTimingConnectionInterceptor()).Options);
        db.Database.OpenConnection();
        db.Database.ExecuteSqlRaw("SELECT 1");   // Timing null: không lỗi
        Assert.Null(db.Timing);
    }
}
