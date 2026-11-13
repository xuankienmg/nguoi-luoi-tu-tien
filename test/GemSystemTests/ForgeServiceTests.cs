using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class ForgeServiceTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public void Dispose() => db.Dispose();

    /// <summary>Random trả về đúng một giá trị NextDouble để chọn kết quả.</summary>
    private sealed class FixedRandom(double v) : Random { public override double NextDouble() => v; }

    private async Task<(GameService Game, Player P, ItemDef Weapon)> Setup()
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Thợ rèn");
        var weapon = GameData.AllItems.First(i => i.Slot == "weapon" && i.MinRealm == 0);
        p.Equipment.RemoveAll(e => e.Slot == "weapon");
        p.Equipment.Add(new EquippedItem { PlayerId = p.Id, Slot = "weapon", ItemId = weapon.Id });
        p.Add("linh_thach", 1_000_000);
        p.Add(ForgeData.Iron, 100);
        return (game, p, weapon);
    }

    [Fact]
    public async Task Success_raises_level_and_pays()
    {
        var (game, p, weapon) = await Setup();
        var stone = p.Count("linh_thach");
        game.ForgeStrike(p, "weapon", new FixedRandom(0.5));   // +0: great 8% → success
        Assert.Equal(1, ForgeData.Levels(p)["weapon"]);
        Assert.Equal(stone - ForgeData.StoneCost(weapon, 0), p.Count("linh_thach"));
        Assert.Equal(99, p.Count(ForgeData.Iron));
        Assert.Equal("success", ForgeData.Load(p).Log[0].Kind);
    }

    [Fact]
    public async Task Break_resets_slot_and_undo_restores()
    {
        var (game, p, _) = await Setup();
        var s = new ForgeData.ForgeSave();
        s.Slots["weapon"] = new ForgeData.SlotState { L = 9, C = 4 };
        ForgeData.Store(p, s);
        game.ForgeStrike(p, "weapon", new FixedRandom(0.9999));
        Assert.False(ForgeData.Levels(p).ContainsKey("weapon"));   // về +0
        p.Add(ForgeData.Rewind, 1);
        game.ForgeUndo(p);
        Assert.Equal(9, ForgeData.Levels(p)["weapon"]);
        Assert.Equal(4, ForgeData.Load(p).Slots["weapon"].C);
        Assert.Equal(0, p.Count(ForgeData.Rewind));
        Assert.Throws<GameException>(() => game.ForgeUndo(p));   // chỉ hoàn 1 nhát
    }

    [Fact]
    public async Task Empty_slot_and_missing_iron_rejected()
    {
        var (game, p, _) = await Setup();
        Assert.Throws<GameException>(() => game.ForgeStrike(p, "head"));
        p.Add(ForgeData.Iron, -100);
        Assert.Throws<GameException>(() => game.ForgeStrike(p, "weapon"));
        Assert.Throws<GameException>(() => game.ForgeStrike(p, "khong_co"));
    }

    [Fact]
    public async Task Undo_needs_talisman()
    {
        var (game, p, _) = await Setup();
        game.ForgeStrike(p, "weapon", new FixedRandom(0.5));
        Assert.Throws<GameException>(() => game.ForgeUndo(p));
    }

    [Fact]
    public async Task State_lists_open_slots_with_odds()
    {
        var (game, p, weapon) = await Setup();
        var st = game.ForgeStateFor(p);
        var w = Assert.Single(st.Slots, s => s.Slot == "weapon");
        Assert.Equal(weapon.Id, w.ItemId);
        Assert.Equal(5, w.Odds.Length);
        Assert.Equal(0, w.Odds[4]);   // +0 không vỡ
        Assert.Equal(GameData.Realms[p.Realm].EquipSlots.Length, st.Slots.Count);
    }

    [Fact]
    public async Task Build_state_includes_forge()
    {
        var (game, p, _) = await Setup();
        var state = await game.BuildStateAsync(p);
        Assert.NotNull(state.Forge);
        Assert.Contains(state.Forge!.Slots, s => s.Slot == "weapon");
    }

    private static void SetSlot(Player p, int l, int c = 0, int f = 0)
    {
        var s = ForgeData.Load(p);
        s.Slots["weapon"] = new ForgeData.SlotState { L = l, C = c, F = f };
        ForgeData.Store(p, s);
    }

    [Fact]
    public async Task Max_level_rejected()
    {
        var (game, p, _) = await Setup();
        SetSlot(p, ForgeData.MaxLevel);
        Assert.Throws<GameException>(() => game.ForgeStrike(p, "weapon", new FixedRandom(0.5)));
    }

    [Fact]
    public async Task Frenzy_blocks_break_and_burns_charge()
    {
        var (game, p, _) = await Setup();
        SetSlot(p, 50, 0, 1);
        game.ForgeStrike(p, "weapon", new FixedRandom(0.9999));
        var s = ForgeData.Load(p).Slots["weapon"];
        Assert.NotEqual("break", ForgeData.Load(p).Log[0].Kind);
        Assert.True(s.L >= 49);
        Assert.Equal(0, s.F);
    }

    [Fact]
    public async Task Undo_restores_level_and_frenzy_without_refund()
    {
        var (game, p, _) = await Setup();
        SetSlot(p, 20, 3, 2);
        var stone = p.Count("linh_thach"); var iron = p.Count(ForgeData.Iron);
        game.ForgeStrike(p, "weapon", new FixedRandom(0.5));
        var stoneAfter = p.Count("linh_thach"); var ironAfter = p.Count(ForgeData.Iron);
        Assert.True(stoneAfter < stone && ironAfter < iron);
        p.Add(ForgeData.Rewind, 1);
        game.ForgeUndo(p);
        var s = ForgeData.Load(p).Slots["weapon"];
        Assert.Equal((20, 3, 2), (s.L, s.C, s.F));
        Assert.Equal(stoneAfter, p.Count("linh_thach"));
        Assert.Equal(ironAfter, p.Count(ForgeData.Iron));
    }

    [Fact]
    public async Task Rejected_strike_charges_nothing_and_messages_name_cause()
    {
        var (game, p, _) = await Setup();
        var ex = Assert.Throws<GameException>(() => game.ForgeStrike(p, "head"));
        Assert.Contains("chưa mặc", ex.Message);
        p.Add(ForgeData.Iron, -100);
        var stone = p.Count("linh_thach");
        ex = Assert.Throws<GameException>(() => game.ForgeStrike(p, "weapon"));
        Assert.Contains("Tinh Thiết", ex.Message);
        Assert.Equal(stone, p.Count("linh_thach"));
        Assert.Equal(0, ForgeData.Load(p).N);
    }

    [Fact]
    public async Task Only_milestones_are_journaled()
    {
        var (game, p, _) = await Setup();
        SetSlot(p, 3);
        var before = p.Logs.Count;
        game.ForgeStrike(p, "weapon", new FixedRandom(0.5));   // +3 → +4
        Assert.Equal(before, p.Logs.Count);
        SetSlot(p, 9);
        game.ForgeStrike(p, "weapon", new FixedRandom(0.5));   // +9 → +10
        Assert.Equal(before + 1, p.Logs.Count);
    }

    [Fact]
    public async Task Load_seeds_levels_when_state_corrupt()
    {
        var (_, p, _) = await Setup();
        p.ForgeLevels = "weapon:7"; p.ForgeState = "{bad";
        Assert.Equal(7, ForgeData.Load(p).Slots["weapon"].L);
    }

    [Fact]
    public async Task Leaderboard_snapshot_carries_forge_levels_of_equipped_slots()
    {
        var (_, p, _) = await Setup();
        var before = LeaderboardSnapshot.Of(p, dev: false);
        p.ForgeLevels = "weapon:12;head:5;linhvan:1";   // head trống, linhvan là dấu bảo vật: không gửi
        var snap = LeaderboardSnapshot.Of(p, dev: false);
        Assert.Equal(12, snap.Stats["forge:weapon"]);
        Assert.Equal(ForgeData.BonusPct(12) * 1.1, snap.Stats["forge_bonus:weapon"], 6);   // dấu linhvan:1 (Linh Văn Khắc) → chỉ số rèn ×1,1
        Assert.DoesNotContain("forge:head", snap.Stats.Keys);
        Assert.DoesNotContain("forge:linhvan", snap.Stats.Keys);
        Assert.NotEqual(before.Key, snap.Key);   // đổi cấp rèn → gửi lại bảng xếp hạng
    }
}
