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

    private sealed class AlwaysZero : Random { public override double NextDouble() => 0; }

    [Fact]
    public async Task NewbieQuestBrewCountsEvenWhenFurnaceFails()
    {
        var (game, p) = await Setup();
        p.NewbieQuestStep = Array.FindIndex(NewbieQuestData.Quests, q => q.Id == "brew");
        foreach (var r in NewbieQuestData.Quests.Single(q => q.Id == "sell").Rewards) p.Add(r.Item, r.Qty);   // đúng quà nhiệm vụ bán đồ
        var (_, kind, _) = game.Brew(p, "linh_thu_dan", 1, new AlwaysZero());   // NextDouble = 0 < tỉ lệ hỏng → hỏng lò
        Assert.Equal("bad", kind);
        Assert.True(game.NewbieQuestStateFor(p)!.CanClaim);
    }

    [Fact]
    public async Task NewbieQuestSellRewardCoversOneBrew()
    {
        var sell = NewbieQuestData.Quests.Single(q => q.Id == "sell").Rewards.ToDictionary(r => r.Item, r => r.Qty);
        var recipe = GameData.Brews.Single(b => b.Out == "linh_thu_dan").In;
        foreach (var (item, need) in recipe) Assert.True(sell.GetValueOrDefault(item) >= need, $"quà nhiệm vụ bán đồ thiếu {item}");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task NewbieQuestStarterRewardLetsNewPlayerFinishEquipQuest()
    {
        var (game, p) = await Setup();
        Assert.Empty(p.Equipment);
        game.ClaimNewbieQuest(p);   // NV1 «Bước vào tiên đồ»: tặng trang bị khởi đầu
        Assert.Contains(NewbieQuestData.Quests[0].Rewards, r => GameData.ItemById[r.Item].Slot is not null);
        Satisfy(p, NewbieQuestData.Quests[1]);
        game.ClaimNewbieQuest(p);   // NV2
        Assert.Equal("equip", game.NewbieQuestStateFor(p)!.Id);
        game.EquipBest(p);          // nút Mặc nhanh
        Assert.True(game.NewbieQuestStateFor(p)!.CanClaim);
    }

    [Fact]
    public async Task NewbieQuestAreaCountsCurrentAreaWhenAreaMaxLags()
    {
        // tái hiện: cheat admin "map" nhảy tới ải 4 nhưng AreaMax vẫn 1
        var (game, p) = await Setup();
        p.NewbieQuestStep = Array.FindIndex(NewbieQuestData.Quests, q => q.Id == "area3");
        p.AreaMax = 1; p.Area = 2;
        Assert.False(game.NewbieQuestStateFor(p)!.CanClaim);
        p.Area = 4;
        Assert.True(game.NewbieQuestStateFor(p)!.CanClaim);
        p.Area = 1; p.Cleared = "0.0";   // đã hạ Boss chương 1
        Assert.True(game.NewbieQuestStateFor(p)!.CanClaim);
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
    public async Task NewbieQuestSellAdvancesCounterAndStateIsSent()
    {
        var (game, p) = await Setup();
        p.NewbieQuestStep = Array.FindIndex(NewbieQuestData.Quests, q => q.Id == "sell");
        var item = GameData.AllItems.First(i => i.Sell > 0 && i.Slot is null).Id;
        p.Add(item, 1);
        game.Sell(p, item, 1);
        Assert.Equal(1, p.NewbieQuestCount);
        var state = await game.BuildStateAsync(p);
        Assert.NotNull(state.NewbieQuest);
        Assert.True(state.NewbieQuest!.CanClaim);
        Assert.Equal("sell", state.NewbieQuest.Id);
    }

    /// <summary>Gốc repo theo vị trí file test lúc biên dịch: chạy đúng cả khi build ra thư mục khác (-p:BaseOutputPath).</summary>
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string file = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", ".."));

    [Fact]
    public void NewbieQuestEndpointAndHooksExist()
    {
        var root = RepoRoot();
        var program = File.ReadAllText(Path.Combine(root, "Program.cs"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(program, @"MapPost\(""/players/\{id:guid\}/newbie-quest/claim"""));
        var service = File.ReadAllText(Path.Combine(root, "Game", "GameService.cs"));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(service, @"RecordQuestEvent\(p, NewbieQuestData\.EvSell\)").Count);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(service, @"RecordQuestEvent\(p, NewbieQuestData\.EvBrew\)"));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(service, @"RecordQuestEvent\(p, NewbieQuestData\.EvFuse\)").Count);
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
