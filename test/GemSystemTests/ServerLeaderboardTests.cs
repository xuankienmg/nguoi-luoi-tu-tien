using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class ServerLeaderboardTests : IDisposable
{
    private readonly GameDbContext db;
    private readonly ServerLeaderboard board;

    public ServerLeaderboardTests()
    {
        db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        board = new ServerLeaderboard(db);
    }

    public void Dispose() => db.Dispose();

    private async Task<Player> AddPlayer(string name)
    {
        var p = new Player { Name = name };
        db.Players.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    /// <summary>Dòng bảng xếp hạng dựng tay để kiểm tra luật xếp hạng mà không phải nuôi nhân vật thật.</summary>
    private async Task<Player> AddEntry(string name, int realm = 0, int stage = 0, double qi = 0, long power = 0, long progress = 0, long petPower = 0, bool flagged = false)
    {
        var p = await AddPlayer(name);
        db.LeaderboardEntries.Add(new LeaderboardEntry { PlayerId = p.Id, Name = name, Realm = realm, Stage = stage, Qi = (decimal)qi, Power = power, Progress = progress, PetPower = petPower, Flagged = flagged });
        await db.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task AlchemyBoardRanksByAlchemyExpAndGivesTitle()
    {
        var low = await AddPlayer("Tiểu Đan"); low.AlchemyExp = Alchemy.ExpAt(2);
        var high = await AddPlayer("Đan Thánh"); high.AlchemyExp = Alchemy.ExpAt(9) + 5;
        await board.StageAsync(low); await board.StageAsync(high);
        await db.SaveChangesAsync();
        var lb = await board.GetAsync(low, "alchemy");
        Assert.Equal("alchemy", lb.By);
        Assert.Equal(new[] { "Đan Thánh", "Tiểu Đan" }, lb.Rows.Select(r => r.Name));
        Assert.Equal(1, lb.Rows[0].Rank);
        Assert.Equal(Alchemy.ExpAt(9) + 5, lb.Rows[0].AlchemyExp);
        Assert.Equal("Cửu Phẩm - Luyện Đan Đại Sư", lb.Rows[0].AlchemyName);
        Assert.Equal(2, lb.Me!.Rank);
        Assert.Contains(lb.Rows[0].Titles!, t => t.Id == "alchemy");
    }

    [Fact]
    public async Task SavingCreatesThenUpdatesOneRow()
    {
        var p = await AddPlayer("Lâm Phong");
        await board.StageAsync(p);
        await db.SaveChangesAsync();
        var row = Assert.Single(db.LeaderboardEntries.AsNoTracking());
        Assert.Equal("Lâm Phong", row.Name);
        Assert.Equal(GameService.Power(p), row.Power);

        p.Name = "Lâm Phong Vũ";
        await board.StageAsync(p);
        await db.SaveChangesAsync();
        Assert.Equal("Lâm Phong Vũ", Assert.Single(db.LeaderboardEntries.AsNoTracking()).Name);
    }

    [Fact]
    public async Task RowShowsCurrentSkinAndFollowsSkinChanges()
    {
        var p = await AddPlayer("Vân Y");
        p.Realm = 2;
        await board.StageAsync(p);
        await db.SaveChangesAsync();
        Assert.Null(Assert.Single((await board.GetAsync(p, "realm")).Rows).Hero);       // chưa mặc trang phục: client dùng ảnh cảnh giới

        p.Skin = "truc_co";
        await board.StageAsync(p);
        await db.SaveChangesAsync();
        Assert.Equal(GameData.SkinById["truc_co"].Hero, Assert.Single((await board.GetAsync(p, "realm")).Rows).Hero);
    }

    [Fact]
    public async Task UnchangedSnapshotIsNotRewrittenWithinRefresh()
    {
        var p = await AddPlayer("Tĩnh Tâm");
        await board.StageAsync(p);
        await db.SaveChangesAsync();
        var first = db.LeaderboardEntries.AsNoTracking().Single().UpdatedAt;

        await board.StageAsync(p);
        await db.SaveChangesAsync();
        Assert.Equal(first, db.LeaderboardEntries.AsNoTracking().Single().UpdatedAt);
    }

    [Fact]
    public async Task EachBoardUsesItsOwnOrder()
    {
        var a = await AddEntry("A", realm: 3, stage: 1, power: 100, progress: 5, petPower: 9);
        var b = await AddEntry("B", realm: 3, stage: 2, power: 300, progress: 1, petPower: 0);
        var c = await AddEntry("C", realm: 1, stage: 9, power: 200, progress: 7, petPower: 4);

        async Task<string[]> Names(string by) => (await board.GetAsync(a, by)).Rows.Select(r => r.Name).ToArray();
        Assert.Equal(["B", "A", "C"], await Names("realm"));
        Assert.Equal(["B", "C", "A"], await Names("power"));
        Assert.Equal(["C", "A", "B"], await Names("progress"));
        Assert.Equal(["A", "C", "B"], await Names("pet"));
        var power = await board.GetAsync(c, "power");
        Assert.Equal([1L, 2L, 3L], power.Rows.Select(r => r.Rank));
        Assert.Equal(2, power.Me!.Rank);
        Assert.True(power.Rows.Single(r => r.Name == "C").Me);
    }

    [Fact]
    public async Task MyRowIsReturnedOutsideTheTop()
    {
        for (var i = 0; i < ServerLeaderboard.Top + 2; i++) await AddEntry($"T{i}", power: 1000 + i);
        var me = await AddEntry("Cuối", power: 1);
        var dto = await board.GetAsync(me, "power");
        Assert.Equal(ServerLeaderboard.Top, dto.Rows.Count);
        Assert.DoesNotContain(dto.Rows, r => r.Me);
        Assert.Equal(ServerLeaderboard.Top + 3, dto.Me!.Rank);
    }

    [Fact]
    public async Task FlaggedPlayersStayOutOfTheTopTen()
    {
        var cheat = await AddEntry("Cờ", power: 99_999, flagged: true);
        for (var i = 0; i < 11; i++) await AddEntry($"S{i}", power: 100 + i);
        var dto = await board.GetAsync(cheat, "power");
        Assert.Equal(11, dto.Me!.Rank);
        Assert.True(dto.Me.Flagged);
        Assert.All(dto.Rows.Take(10), r => Assert.False(r.Flagged));
        // hạng 11 trở đi vẫn theo lực chiến: người sạch yếu nhất xếp sau người bị gắn cờ
        Assert.Equal("S0", dto.Rows[11].Name);
    }

    [Fact]
    public async Task FlagStaysOnceSet()
    {
        var p = await AddPlayer("Cũ");
        p.Flagged = true;
        await board.StageAsync(p);
        await db.SaveChangesAsync();
        p.Flagged = false;
        p.Name = "Mới";
        await board.StageAsync(p);
        await db.SaveChangesAsync();
        Assert.True(db.LeaderboardEntries.AsNoTracking().Single().Flagged);
    }

    [Fact]
    public async Task TitlesGoToTheCleanFirstPlace()
    {
        var a = await AddEntry("A", realm: 5, power: 10, progress: 0, petPower: 0);
        var b = await AddEntry("B", realm: 1, power: 50, progress: 3, petPower: 0);
        await AddEntry("Cờ", realm: 9, power: 999, progress: 99, petPower: 99, flagged: true);
        var titles = await board.TitlesAsync();
        Assert.Equal(["realm"], titles[a.Id].Select(t => t.Id));
        Assert.Equal(["power", "progress"], titles[b.Id].Select(t => t.Id));
        Assert.Equal(2, titles.Count);   // không ai có linh thú → không có danh hiệu pet
    }

    [Fact]
    public async Task ProfileComesFromTheSnapshotOnly()
    {
        var me = await AddEntry("Ta", power: 1);
        var other = await AddPlayer("Người");
        var sword = GameData.GearId(GameData.GearBaseById["lk_kiem"], 0);
        other.Equipment.Add(new EquippedItem { PlayerId = other.Id, Slot = "weapon", ItemId = sword });
        other.ForgeLevels = "weapon:10;hat:5;linhvan:1";
        await board.StageAsync(other);
        await db.SaveChangesAsync();

        var pf = await board.ProfileAsync(me, other.Id);
        Assert.NotNull(pf);
        Assert.Equal("Người", pf.Name);
        Assert.False(pf.Me);
        Assert.Equal(sword, pf.Equipment["weapon"]);
        Assert.True(pf.Stats.ContainsKey("atk"));
        Assert.Equal(10d, pf.Stats["forge:weapon"]);
        Assert.Equal(23.1, pf.Stats["forge_bonus:weapon"], 8);
        Assert.DoesNotContain("forge_bonus:hat", pf.Stats.Keys);
        Assert.Null(await board.ProfileAsync(me, Guid.NewGuid()));
    }

    [Fact]
    public async Task DeletingAPlayerRemovesTheRow()
    {
        var p = await AddEntry("Đi");
        db.Players.Remove(p);
        await db.SaveChangesAsync();
        Assert.Empty(db.LeaderboardEntries);
    }

    [Fact]
    public async Task CacheIsDroppedWhenSomeoneSaves()
    {
        var cache = new ServerLeaderboardCache();
        var cached = new ServerLeaderboard(db, cache);
        var a = await AddEntry("A", power: 10);
        Assert.Single((await cached.GetAsync(a, "power")).Rows);
        var b = await AddPlayer("B");
        await cached.StageAsync(b);
        await db.SaveChangesAsync();
        Assert.Equal(2, (await cached.GetAsync(a, "power")).Rows.Count);
    }
}
