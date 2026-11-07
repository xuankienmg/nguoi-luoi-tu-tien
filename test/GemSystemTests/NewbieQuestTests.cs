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

    /// <summary>Đưa Player vào trạng thái thỏa nhiệm vụ q (giả lập, không chạy trận thật).</summary>
    private static void Satisfy(Player p, NewbieQuestData.QuestDef q)
    {
        if (q.CountEvent is not null) { GameService.RecordQuestEvent(p, q.CountEvent); return; }
        switch (q.Id)
        {
            case "starter": p.StarterChosen = true; break;
            case "first_area": p.AreaMax = Math.Max(p.AreaMax, 2); break;
            case "area3": p.AreaMax = Math.Max(p.AreaMax, 3); break;
            case "equip": p.Equipment.Add(new EquippedItem { PlayerId = p.Id, Slot = "test", ItemId = "test" }); break;
            case "skill": p.SetSkillLevels(new Dictionary<string, int> { [SkillData.StartSkill] = 1, ["liet_diem"] = 1 }); break;
            case "pet": p.Pets.Add(new Pet { PlayerId = p.Id, DefId = "test" }); break;
            case "lk3": p.Stage = Math.Max(p.Stage, 2); break;
            case "lk6": p.Stage = Math.Max(p.Stage, 5); break;
            case "lk9": p.Stage = Math.Max(p.Stage, 8); break;
            case "boss": p.BossKills = 1; break;
            case "trucco": p.Realm = 1; p.Stage = 0; break;
            default: throw new InvalidOperationException($"Chưa có cách thỏa nhiệm vụ {q.Id}");
        }
    }

    [Fact]
    public async Task NewbieQuestWalksWholeChain()
    {
        var (game, p) = await Setup();
        for (var i = 0; i < NewbieQuestData.Total; i++)
        {
            var q = NewbieQuestData.Quests[i];
            var s = game.NewbieQuestStateFor(p)!;
            Assert.Equal(i, s.Step);
            Assert.Equal(q.Id, s.Id);
            if (q.Id != "starter") Assert.False(s.CanClaim, $"{q.Id} không được tự đạt");
            Satisfy(p, q);
            Assert.True(game.NewbieQuestStateFor(p)!.CanClaim, q.Id);
            var before = q.Rewards.ToDictionary(r => r.Item, r => p.Count(r.Item));
            game.ClaimNewbieQuest(p);
            foreach (var r in q.Rewards) Assert.Equal(before[r.Item] + r.Qty, p.Count(r.Item));
        }
        Assert.Null(game.NewbieQuestStateFor(p));
        Assert.Throws<GameException>(() => game.ClaimNewbieQuest(p));
    }

    [Fact]
    public async Task NewbieQuestRejectsClaimBeforeDone()
    {
        var (game, p) = await Setup();
        game.ClaimNewbieQuest(p);   // nhiệm vụ 1 (đã chọn khởi đầu)
        var stones = p.Count("linh_thach");
        Assert.Throws<GameException>(() => game.ClaimNewbieQuest(p));
        Assert.Equal(1, p.NewbieQuestStep);
        Assert.Equal(stones, p.Count("linh_thach"));
    }

    [Fact]
    public async Task NewbieQuestCountsOnlyMatchingEvent()
    {
        var (game, p) = await Setup();
        p.NewbieQuestStep = Array.FindIndex(NewbieQuestData.Quests, q => q.Id == "sell");
        GameService.RecordQuestEvent(p, NewbieQuestData.EvBrew);
        Assert.Equal(0, p.NewbieQuestCount);
        Assert.False(game.NewbieQuestStateFor(p)!.CanClaim);
        GameService.RecordQuestEvent(p, NewbieQuestData.EvSell);
        Assert.Equal(1, p.NewbieQuestCount);
        Assert.True(game.NewbieQuestStateFor(p)!.CanClaim);
        game.ClaimNewbieQuest(p);
        Assert.Equal(0, p.NewbieQuestCount);   // đặt lại khi chuyển nhiệm vụ
    }

    [Fact]
    public async Task NewbieQuestRealmAlreadyPassedIsClaimableAtOnce()
    {
        var (game, p) = await Setup();
        p.NewbieQuestStep = Array.FindIndex(NewbieQuestData.Quests, q => q.Id == "lk3");
        p.Stage = 7;
        Assert.True(game.NewbieQuestStateFor(p)!.CanClaim);
    }

    [Fact]
    public async Task NewbieQuestNegativeStepDoesNotRepeatFirstReward()
    {
        var (game, p) = await Setup();
        p.NewbieQuestStep = -1;   // dữ liệu hỏng
        Assert.Equal(0, game.NewbieQuestStateFor(p)!.Step);
        game.ClaimNewbieQuest(p);
        Assert.Equal(1, p.NewbieQuestStep);
    }

    [Fact]
    public async Task NewbieQuestHiddenWhenChainDone()
    {
        var (game, p) = await Setup();
        p.NewbieQuestStep = NewbieQuestData.Total;
        Assert.Null(game.NewbieQuestStateFor(p));
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
