using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class ChatRulesTests
{
    [Theory]
    [InlineData("  chào   đạo   hữu  ", "chào đạo hữu")]
    [InlineData("a\tb\nc", "a b c")]
    [InlineData("x\u0000y​z", "xyz")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void CleanTrimsCollapsesAndStripsControl(string? input, string expected) =>
        Assert.Equal(expected, ChatRules.Clean(input));

    [Fact]
    public void CleanCutsAtMaxLenWithoutSplittingSurrogates()
    {
        Assert.Equal(ChatRules.MaxLen, ChatRules.Clean(new string('a', 300)).Length);
        var emoji = new string('a', ChatRules.MaxLen - 1) + "😀";   // cặp surrogate vắt qua mốc 120
        Assert.Equal(new string('a', ChatRules.MaxLen - 1), ChatRules.Clean(emoji));
    }

    [Fact]
    public void LockReasonBlocksLowRealmAndFlagged()
    {
        Assert.NotNull(ChatRules.LockReason(0, false));
        Assert.Null(ChatRules.LockReason(ChatRules.MinRealm, false));
        Assert.NotNull(ChatRules.LockReason(5, true));
    }

    [Fact]
    public void CooldownCountsUpWholeSeconds()
    {
        var now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0, ChatRules.CooldownLeft(null, now));
        Assert.Equal(3, ChatRules.CooldownLeft(now, now));
        Assert.Equal(1, ChatRules.CooldownLeft(now.AddSeconds(-2.5), now));
        Assert.Equal(0, ChatRules.CooldownLeft(now.AddSeconds(-3), now));
    }
}

public class ServerChatTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    private readonly DateTime now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
    public ServerChatTests() { db.Database.OpenConnection(); db.Database.EnsureCreated(); ServerChat.ResetOnlineCacheForTests(); }
    public void Dispose() => db.Dispose();

    private async Task<Player> NewPlayer(string name, int realm = 1)
    {
        var p = await new GameService(db).CreateAsync(name);
        p.Realm = realm; p.LastTickAt = now;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return p;
    }

    [Fact]
    public async Task PostThenPageReturnsMessageMarkedMine()
    {
        var a = await NewPlayer("Lãnh Phong");
        var page = await new ServerChat(db).PostAsync(a.Id, "  chào   chư vị ", 0, now);
        var m = Assert.Single(page.Messages);
        Assert.Equal("chào chư vị", m.Text);
        Assert.Equal("Lãnh Phong", m.Name);
        Assert.Equal(GameData.Realms[1].Name, m.RealmName);
        Assert.True(m.Mine);
        Assert.True(page.Enabled);
        Assert.Equal(3, page.CooldownLeft);
        Assert.False(page.CanPost);
    }

    [Fact]
    public async Task PostRejectsEmptyLowRealmFlaggedAndCooldown()
    {
        var low = await NewPlayer("Tiểu Bạch", realm: 0);
        var ok = await NewPlayer("Hàn Vũ");
        var chat = new ServerChat(db);
        await Assert.ThrowsAsync<GameException>(() => chat.PostAsync(low.Id, "hi", 0, now));
        await Assert.ThrowsAsync<GameException>(() => chat.PostAsync(ok.Id, "   ", 0, now));
        await chat.PostAsync(ok.Id, "một", 0, now);
        await Assert.ThrowsAsync<GameException>(() => chat.PostAsync(ok.Id, "hai", 0, now.AddSeconds(2)));
        await chat.PostAsync(ok.Id, "ba", 0, now.AddSeconds(3));

        var bad = await NewPlayer("Cờ");
        await db.Players.Where(x => x.Id == bad.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Tainted, true));
        await Assert.ThrowsAsync<GameException>(() => chat.PostAsync(bad.Id, "hi", 0, now));
    }

    [Fact]
    public async Task PageAfterReturnsOnlyNewerInOrderAndFirstPageIsLatestFifty()
    {
        var a = await NewPlayer("A");
        for (var i = 0; i < 60; i++) ServerChat.AddSystem(db, $"tin {i}", now);
        await db.SaveChangesAsync();
        var chat = new ServerChat(db);
        var first = await chat.PageAsync(a.Id, 0, now);
        Assert.Equal(ChatRules.PageSize, first.Messages.Count);
        Assert.Equal("tin 10", first.Messages[0].Text);
        Assert.Equal("tin 59", first.Messages[^1].Text);
        var lastId = first.Messages[^1].Id;
        Assert.Empty((await chat.PageAsync(a.Id, lastId, now)).Messages);
        ServerChat.AddSystem(db, "mới", now); await db.SaveChangesAsync();
        Assert.Equal("mới", Assert.Single((await chat.PageAsync(a.Id, lastId, now)).Messages).Text);
    }

    [Fact]
    public async Task FlaggedLeaderboardSenderIsHidden()
    {
        var a = await NewPlayer("A");
        var cheat = await NewPlayer("Gian");
        var chat = new ServerChat(db);
        await chat.PostAsync(cheat.Id, "rao bán", 0, now);
        db.LeaderboardEntries.Add(new LeaderboardEntry { PlayerId = cheat.Id, Name = "Gian", Flagged = true });
        await db.SaveChangesAsync();
        Assert.Empty((await chat.PageAsync(a.Id, 0, now)).Messages);
    }

    [Fact]
    public async Task OnlineCountsRecentPlayersAndOldMessagesAreDeleted()
    {
        var a = await NewPlayer("A");
        var b = await NewPlayer("B");
        await db.Players.Where(x => x.Id == b.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.LastTickAt, now.AddHours(-1)));
        Assert.Equal(1, (await new ServerChat(db).PageAsync(a.Id, 0, now)).Online);

        db.ChatMessages.Add(new ChatMessage { At = now.AddDays(-8), Kind = ChatRules.KindSystem, Text = "cũ" });
        ServerChat.AddSystem(db, "mới", now);
        await db.SaveChangesAsync();
        Assert.Equal(1, await ServerChat.DeleteOldAsync(db, now, default));
        Assert.Equal("mới", (await db.ChatMessages.SingleAsync()).Text);
    }

    [Fact]
    public async Task UnknownPlayerThrowsKeyNotFound() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new ServerChat(db).PageAsync(Guid.NewGuid(), 0, now));
}

public class ChatSystemTests : IDisposable
{
    private sealed class Roll(double value) : Random { public override double NextDouble() => value; }
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public ChatSystemTests() { db.Database.OpenConnection(); db.Database.EnsureCreated(); }
    public void Dispose() => db.Dispose();

    private async Task<(GameService, Player)> PeakPlayer(bool online)
    {
        var game = online ? new GameService(db, serverChat: new ServerChat(db)) : new GameService(db);
        var p = await game.CreateAsync("Mộ Dung Tuyết");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Stage = GameData.Realms[0].Stages.Length - 1;
        p.Qi = GameData.Realms[0].QiReq(p.Stage);
        foreach (var (id, qty) in GameData.Realms[0].BreakCost!) p.Add(id, qty);
        return (game, p);
    }

    [Fact]
    public async Task MajorBreakthroughPostsSystemMessageOnline()
    {
        var (game, p) = await PeakPlayer(online: true);
        game.Advance(p, new Roll(0));
        await db.SaveChangesAsync();
        var m = await db.ChatMessages.SingleAsync();
        Assert.Equal(ChatRules.KindSystem, m.Kind);
        Assert.Contains("Mộ Dung Tuyết", m.Text);
        Assert.Contains(GameData.Realms[1].Name, m.Text);
    }

    [Fact]
    public async Task OfflineGamePostsNothing()
    {
        var (game, p) = await PeakPlayer(online: false);
        game.Advance(p, new Roll(0));
        await db.SaveChangesAsync();
        Assert.Empty(db.ChatMessages);
    }
}
