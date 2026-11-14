using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class StageBreakthroughTests : IDisposable
{
    private sealed class Roll(double value) : Random { public override double NextDouble() => value; }
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    private async Task<(GameService Game, Player Player)> Setup(int realm = 0)
    {
        db.Database.OpenConnection(); db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Đột phá nhỏ");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Realm = realm; p.Stage = 0;
        p.Qi = GameData.Realms[realm].QiReq(0);
        foreach (var (id, qty) in GameData.Realms[realm].StageCost(0)) p.Add(id, qty * 3);
        return (game, p);
    }

    [Fact]
    public async Task EveryRealmHasStageChanceAndOptionalPillRecipe()
    {
        var (game, p) = await Setup();
        for (int realm = 0; realm < GameData.Realms.Count; realm++)
        {
            p.Realm = realm; p.Stage = 0;
            var state = await game.BuildStateAsync(p);
            Assert.Equal(Math.Max(.45, .90 - realm * .05), state.Chance, 8);   // sàn 45% (GameData.StageBreakChance)
            Assert.Equal(state.Chance, state.Break.BaseChance, 8);
            var pill = GameData.BreakPillId(realm);
            Assert.True(GameData.ItemById.ContainsKey(pill));
            Assert.Single(GameData.Brews.Where(b => b.Out == pill));
            Assert.DoesNotContain(state.TargetCost, c => c.Item == pill);
        }
    }

    [Fact]
    public async Task StageCanSucceedWithoutSupportPillAndClearsBonuses()
    {
        var (game, p) = await Setup(5);
        p.Bonus = .10; p.PillBonus = .10;
        var (message, kind) = game.Advance(p, new Roll(0));
        Assert.Equal("good", kind);
        Assert.Equal(1, p.Stage);
        Assert.Equal(5, p.Realm);
        Assert.Equal(0, p.Qi); Assert.Equal(0, p.Bonus); Assert.Equal(0, p.PillBonus);
        Assert.Equal(0, p.BodyHp); Assert.Equal(0, p.BodyDef);
        Assert.Equal(0, p.Count(GameData.BreakPillId(5)));
    }

    [Fact]
    public async Task FailedStageConsumesCostAddsPityAndCooldownWithoutMajorFailure()
    {
        var (game, p) = await Setup(5);
        p.PillBonus = .10;
        var cap = p.Qi;
        var cost = GameData.Realms[5].StageCost(0);
        var before = cost.ToDictionary(c => c.Key, c => p.Count(c.Key));
        var (_, kind) = game.Advance(p, new Roll(.999));
        Assert.Equal("bad", kind);
        Assert.Equal(0, p.Stage); Assert.Equal(5, p.Realm);
        Assert.Equal(cap * .90m, p.Qi);
        Assert.Equal(.05, p.Bonus); Assert.Equal(.10, p.PillBonus);
        Assert.True(p.BreakCooldownUntil > DateTime.UtcNow.AddSeconds(50));
        Assert.True(p.DebuffUntil <= DateTime.UtcNow);
        foreach (var (id, qty) in cost) Assert.Equal(before[id] - qty, p.Count(id));
        p.Qi = cap;
        Assert.Throws<GameException>(() => game.Advance(p, new Roll(0)));
    }

    [Fact]
    public async Task OptionalPillRaisesStageChanceAndDoesNotWasteAtCap()
    {
        var (game, p) = await Setup();
        var pill = GameData.BreakPillId(0); p.Add(pill, 3);
        game.UseItem(p, pill);
        var state = await game.BuildStateAsync(p);
        Assert.Equal(.95, state.Chance);
        Assert.Equal(2, p.Count(pill));
        Assert.Throws<GameException>(() => game.UseItem(p, pill));
        Assert.Equal(2, p.Count(pill));
    }

    [Fact]
    public async Task SupportPillAddsFivePercentAndDoublesCostUpToSixteen()
    {
        var (game, p) = await Setup(6);
        var pill = GameData.BreakPillId(6); p.Add(pill, 100);
        var costs = new List<int>();
        for (var i = 0; i < 6; i++)
        {
            var before = p.CountInt(pill);
            game.UseItem(p, pill);
            costs.Add(before - p.CountInt(pill));
            Assert.Equal(.05 * (i + 1), p.PillBonus, 8);
        }
        Assert.Equal(new[] { 1, 2, 4, 8, 16, 16 }, costs);
        Assert.Throws<GameException>(() => game.UseItem(p, pill));   // đã tối đa +30%
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public async Task DoKiepAndChanTienPillsAddTwoAndHalfPercent(int realm)
    {
        var (game, p) = await Setup(realm);
        var pill = GameData.BreakPillId(realm); p.Add(pill, 200);
        var base_ = (await game.BuildStateAsync(p)).Chance;
        var costs = new List<int>();
        for (var i = 0; i < 12; i++)
        {
            var before = p.CountInt(pill);
            game.UseItem(p, pill);
            costs.Add(before - p.CountInt(pill));
            Assert.Equal(.025 * (i + 1), p.PillBonus, 8);
        }
        Assert.Equal(new[] { 1, 2, 4, 8, 16, 16, 16, 16, 16, 16, 16, 16 }, costs);
        Assert.Equal(base_ + .30, (await game.BuildStateAsync(p)).Chance, 8);
        Assert.Throws<GameException>(() => game.UseItem(p, pill));
    }

    [Fact]
    public async Task SupportPillRequiresFullBatch()
    {
        var (game, p) = await Setup(6);
        var pill = GameData.BreakPillId(6); p.Add(pill, 3);
        game.UseItem(p, pill); game.UseItem(p, pill);
        Assert.Equal(0, p.Count(pill)); Assert.Equal(.10, p.PillBonus, 8);
        p.Add(pill, 3);
        Assert.Throws<GameException>(() => game.UseItem(p, pill));   // lần 3 cần 4 viên
        Assert.Equal(3, p.Count(pill)); Assert.Equal(.10, p.PillBonus, 8);
    }

    [Fact]
    public async Task FinalRealmUsesSupportPillForSmallStagesButNotAfterCompletion()
    {
        var (game, p) = await Setup(9);
        var pill = GameData.BreakPillId(9); p.Add(pill, 2);
        game.UseItem(p, pill);
        Assert.Equal(.475, (await game.BuildStateAsync(p)).Chance, 8);
        p.Stage = GameData.Realms[9].Stages.Length - 1;
        Assert.Throws<GameException>(() => game.UseItem(p, pill));
        Assert.Equal(1, p.Count(pill));
    }

    [Fact]
    public async Task MajorBreakthroughStillUsesOriginalBaseChanceAndConsequences()
    {
        var (game, p) = await Setup();
        p.Stage = GameData.Realms[0].Stages.Length - 1;
        p.Qi = GameData.Realms[0].QiReq(p.Stage);
        foreach (var (id, qty) in GameData.Realms[0].BreakCost!) p.Add(id, qty);
        // Thiên Kiếp cộng/trừ tỉ lệ gốc theo số đạo lôi chịu được
        var trib = GameService.Tribulation(p)!;
        Assert.Equal(Math.Clamp(GameData.Realms[0].BreakChance + trib.ChanceBonus, 0, 0.95), (await game.BuildStateAsync(p)).Chance, 6);
        var (_, kind) = game.Advance(p, new Roll(.999));
        Assert.Equal("major", kind);
        Assert.Equal(0, p.Realm);
        Assert.True(p.DebuffUntil > DateTime.UtcNow);
    }

    public void Dispose() => db.Dispose();
}
