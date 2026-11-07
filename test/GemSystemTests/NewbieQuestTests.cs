using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>Nhiệm vụ tân thủ: chuỗi tuần tự đến Trúc Cơ, nhận quà từng bước.</summary>
public class NewbieQuestTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public void Dispose() => db.Dispose();

    private async Task<(GameService, Player)> Setup(bool chooseStarter = true)
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db, version: new VersionService(new LeaderboardOptions(), NullLogger<VersionService>.Instance) { UseOwnClock = true });
        var p = await game.CreateAsync("Đạo hữu");
        if (chooseStarter) game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        return (game, p);
    }

    [Fact]
    public async Task SqliteUpgradeMarksFoundationPlayersDone()
    {
        // SchemaUpgrade.Apply đóng kết nối nên giữ DB in-memory sống bằng kết nối riêng (như StarterChoiceTests).
        using var keeper = new SqliteConnection("Data Source=newbie-quest-schema;Mode=Memory;Cache=Shared"); keeper.Open();
        using var connection = new SqliteConnection(keeper.ConnectionString); connection.Open();
        using var db2 = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db2.Database.EnsureCreated();
        var foundation = new Player { Realm = 1 };
        var fresh = new Player { Realm = 0 };
        db2.Players.AddRange(foundation, fresh);
        await db2.SaveChangesAsync();
        var fid = foundation.Id; var nid = fresh.Id;
        db2.ChangeTracker.Clear();
        db2.Database.ExecuteSqlRaw("ALTER TABLE Players DROP COLUMN NewbieQuestStep");
        db2.Database.ExecuteSqlRaw("ALTER TABLE Players DROP COLUMN NewbieQuestCount");
        SchemaUpgrade.Apply(db2);
        Assert.Equal(14, (await db2.Players.SingleAsync(p => p.Id == fid)).NewbieQuestStep);
        Assert.Equal(0, (await db2.Players.SingleAsync(p => p.Id == nid)).NewbieQuestStep);
    }

    [Fact]
    public void NewbieQuestDataIsValid()
    {
        Assert.Equal(14, NewbieQuestData.Total);   // khớp UPDATE ... = 14 trong SchemaUpgrade và migration online
        Assert.Equal(NewbieQuestData.Total, NewbieQuestData.Quests.Select(q => q.Id).Distinct().Count());
        foreach (var q in NewbieQuestData.Quests)
        {
            Assert.True((q.Check is null) != (q.CountEvent is null), $"{q.Id}: phải có đúng một cách đo");
            if (q.CountEvent is not null) Assert.True(q.CountNeed >= 1, q.Id);
            Assert.NotEmpty(q.Rewards);
            foreach (var r in q.Rewards)
            {
                Assert.True(GameData.ItemById.ContainsKey(r.Item), $"{q.Id}: thiếu item {r.Item}");
                Assert.True(r.Qty > 0, q.Id);
            }
        }
        Assert.Equal("starter", NewbieQuestData.Quests[0].Id);
        Assert.Equal("trucco", NewbieQuestData.Quests[^1].Id);
    }
}
