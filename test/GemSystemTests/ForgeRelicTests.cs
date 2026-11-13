using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;
using static VanDao.Game.ForgeData;

namespace GemSystemTests;

public class ForgeRelicTests
{
    private static ForgeRelicData.Mods M(params string[] ids) => ForgeRelicData.ModsOf(ids);

    [Fact]
    public void Catalog_has_ten_unique_relics_with_prices() =>
        Assert.Equal(10, ForgeRelicData.All.Select(r => r.Id).Distinct().Count(r => ForgeRelicData.ById[r].Price > 0));

    [Fact]
    public void No_relic_keeps_old_odds() =>
        Assert.Equal(OddsAt(30, false), OddsAt(30, false, ForgeRelicData.Mods.None));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    public void Odds_stay_valid_with_every_relic(int level)
    {
        var o = OddsAt(level, false, M(ForgeRelicData.All.Select(r => r.Id).ToArray()));
        Assert.Equal(1.0, o.Great + o.Success + o.Hold + o.Damage + o.Break, 9);
        Assert.All(o.ToArray(), x => Assert.InRange(x, 0, 1));
    }

    [Fact]
    public void Odds_relics_move_probability_as_described()
    {
        var b = OddsAt(40, false);
        Assert.Equal(b.Break * 0.8, OddsAt(40, false, M("an_ho_binh")).Break, 9);
        Assert.Equal(b.Great + 0.04, OddsAt(40, false, M("kim_cuong_chuy")).Great, 9);
        Assert.Equal(b.Success + 0.08, OddsAt(40, false, M("thien_co_do")).Success, 9);
        var stone = OddsAt(40, false, M("ban_thach_de"));
        Assert.Equal(b.Damage - 0.06, stone.Damage, 9);
        Assert.Equal(b.Hold + 0.06, stone.Hold, 9);
        Assert.Equal(0, OddsAt(0, false, M("ban_thach_de")).Damage);
        Assert.Equal(b.Hold, OddsAt(0, false, M("ban_thach_de")).Hold, 9);   // +0 không có Tổn hại để chuyển
    }

    [Fact]
    public void Fire_relics_change_charge_and_fire_length()
    {
        var two = Apply(new SlotState { L = 3, C = 8 }, Outcome.Hold, M("hoa_tinh_thach"));
        Assert.Equal(0, two.C);
        Assert.Equal(FrenzyStrikes, two.F);
        var five = Apply(new SlotState { L = 3, C = 9 }, Outcome.Hold, M("phuong_vu"));
        Assert.Equal(5, five.F);
    }

    [Fact]
    public void Charge_overflow_carries_over()
    {
        var n = Apply(new SlotState { L = 3, C = 9 }, Outcome.Hold, M("hoa_tinh_thach"));
        Assert.Equal(1, n.C);
        Assert.Equal(FrenzyStrikes, n.F);
    }

    [Fact]
    public void Load_tolerates_null_lists()
    {
        var p = new Player { ForgeState = "{\"Relics\":null,\"Bought\":null,\"ShopDay\":null}" };
        var s = Load(p);
        Assert.Empty(s.Relics);
        _ = ForgeRelicData.ModsOf(s.Relics);
        Assert.Empty(s.Bought);
        Assert.Equal("", s.ShopDay);
    }

    [Theory]
    [InlineData(40, 20)]
    [InlineData(41, 20)]
    [InlineData(1, 0)]
    public void Undying_soul_halves_level_on_break(int from, int to) =>
        Assert.Equal(to, Apply(new SlotState { L = from }, Outcome.Break, M("bat_diet_hon")).L);

    [Fact]
    public void Salvage_points_double_per_rarity()
    {
        var gear = GameData.AllItems.Where(i => i.Slot != null && i.Base != null).ToList();
        foreach (var (rarity, q) in GameData.RarityOrder.Select((r, i) => (r, i)))
            if (gear.FirstOrDefault(i => i.Rarity == rarity) is { } d) Assert.Equal(1L << q, ForgeRelicData.SalvagePoints(d));
        Assert.Equal(0, ForgeRelicData.SalvagePoints(GameData.ItemById["linh_thach"]));
    }

    [Fact]
    public void Shelf_is_stable_and_skips_owned_except_bought_today()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var a = ForgeRelicData.Shelf(id, "2026-11-13", 0, [], []);
        Assert.Equal(3, a.Count);
        Assert.Equal(a, ForgeRelicData.Shelf(id, "2026-11-13", 0, [], []));
        Assert.NotEqual(a, ForgeRelicData.Shelf(id, "2026-11-13", 1, [], []));   // làm mới đổi kệ (khả năng trùng hệt rất nhỏ với seed cố định này)
        var owned = new[] { a[0] };
        Assert.DoesNotContain(a[0], ForgeRelicData.Shelf(id, "2026-11-13", 0, owned, []));
        Assert.Equal(a, ForgeRelicData.Shelf(id, "2026-11-13", 0, owned, owned));
    }

    [Fact]
    public void Linh_van_marker_multiplies_bonus_and_is_ignored_as_slot()
    {
        var p = new Player();
        var s = new ForgeSave { Relics = ["linh_van_khac"] };
        s.Slots["weapon"] = new SlotState { L = 10 };
        Store(p, s);
        Assert.Contains("linhvan:1", p.ForgeLevels);
        Assert.Equal(1.1, BonusMult(Levels(p)), 9);
        p.ForgeState = "{hỏng";
        Assert.DoesNotContain("linhvan", Load(p).Slots.Keys);
    }
}

public class ForgeRelicServiceTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>()
        .UseSqlite("Data Source=:memory:").Options);
    public void Dispose() => db.Dispose();
    private sealed class FixedRandom(double v) : Random { public override double NextDouble() => v; }

    private async Task<(GameService Game, Player P)> Setup(params string[] relics)
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
        var s = ForgeData.Load(p); s.Relics = relics.ToList(); ForgeData.Store(p, s);
        return (game, p);
    }

    [Fact]
    public async Task Iron_saver_skips_iron_on_lucky_roll()
    {
        var (game, p) = await Setup("tiet_thiet_lo");
        game.ForgeStrike(p, "weapon", new FixedRandom(0.1));   // 0,1 < 25%
        Assert.Equal(100, p.Count(ForgeData.Iron));
    }

    [Fact]
    public async Task Undying_soul_keeps_half_on_break()
    {
        var (game, p) = await Setup("bat_diet_hon");
        var s = ForgeData.Load(p); s.Slots["weapon"] = new ForgeData.SlotState { L = 40 }; ForgeData.Store(p, s);
        game.ForgeStrike(p, "weapon", new FixedRandom(0.9999));
        Assert.Equal(20, ForgeData.Levels(p)["weapon"]);
    }

    [Fact]
    public async Task Rewind_mirror_can_keep_talisman()
    {
        var (game, p) = await Setup("hoi_quang_kinh");
        game.ForgeStrike(p, "weapon", new FixedRandom(0.5));
        p.Add(ForgeData.Rewind, 1);
        game.ForgeUndo(p, new FixedRandom(0.1));   // 0,1 < 20%: không tiêu phù
        Assert.Equal(1, p.Count(ForgeData.Rewind));
    }

    [Fact]
    public async Task State_odds_include_relics()
    {
        var (game, p) = await Setup("kim_cuong_chuy");
        Assert.Equal(0.12, game.ForgeStateFor(p).Slots.Single(x => x.Slot == "weapon").Odds[0], 9);
    }

    private static string ShelfFirst(GameService g, Player p) => g.ForgeStateFor(p).Shelf!.First(x => !x.Bought).Id;

    [Fact]
    public async Task Salvage_turns_bag_gear_into_points()
    {
        var (game, p) = await Setup();
        var gear = GameData.AllItems.First(i => i.Slot != null && i.Base != null && i.Rarity == "rare");   // Huyền: 4 điểm
        p.Add(gear.Id, 3);
        game.ForgeSalvage(p, $"{gear.Id},{gear.Id}");
        Assert.Equal(8, ForgeData.Load(p).Points);
        Assert.Equal(1, p.Count(gear.Id));
        Assert.Throws<GameException>(() => game.ForgeSalvage(p, $"{gear.Id},{gear.Id}"));   // chỉ còn 1
        Assert.Throws<GameException>(() => game.ForgeSalvage(p, "linh_thach"));
    }

    private static ItemDef RareGear() => GameData.AllItems.First(i => i.Slot != null && i.Base != null && i.Rarity == "rare");

    [Fact]
    public async Task Salvage_refuses_gear_with_socketed_gem()
    {
        var (game, p) = await Setup();
        var gear = RareGear();
        p.Add(gear.Id, 2);
        GemData.StoreBagCopy(p, gear.Id, 0, new GemSlotData { Sockets = ["gem_x"] });
        Assert.Throws<GameException>(() => game.ForgeSalvage(p, gear.Id));   // bản 0 có gem
        Assert.Equal(2, p.Count(gear.Id));
    }

    [Fact]
    public async Task Salvage_accepts_id_count_and_rejects_bad_counts()
    {
        var (game, p) = await Setup();
        var gear = RareGear();
        p.Add(gear.Id, 5);
        game.ForgeSalvage(p, $"{gear.Id}:2");
        Assert.Equal(8, ForgeData.Load(p).Points);
        game.ForgeSalvage(p, $"{gear.Id}:1,{gear.Id}");
        Assert.Equal(16, ForgeData.Load(p).Points);
        Assert.Equal(1, p.Count(gear.Id));
        foreach (var bad in new[] { $"{gear.Id}:0", $"{gear.Id}:x", $"{gear.Id}:-1" })
            Assert.Throws<GameException>(() => game.ForgeSalvage(p, bad));
    }

    [Fact]
    public async Task Salvage_is_atomic()
    {
        var (game, p) = await Setup();
        var gear = RareGear();
        p.Add(gear.Id, 2);
        Assert.Throws<GameException>(() => game.ForgeSalvage(p, $"{gear.Id},linh_thach"));
        Assert.Equal(2, p.Count(gear.Id));
        Assert.Equal(0, ForgeData.Load(p).Points);
    }

    [Fact]
    public async Task Reroll_clears_bought_and_needs_stone()
    {
        var (game, p) = await Setup();
        var s = ForgeData.Load(p); s.Points = 1000; ForgeData.Store(p, s);
        game.ForgeRelicBuy(p, ShelfFirst(game, p));
        game.ForgeShelfReroll(p);
        Assert.Empty(ForgeData.Load(p).Bought);
        p.Add("linh_thach", -p.Count("linh_thach"));
        Assert.Throws<GameException>(() => game.ForgeShelfReroll(p));
    }

    [Fact]
    public async Task Buying_owned_relic_throws()
    {
        var (game, p) = await Setup("an_ho_binh");
        var s = ForgeData.Load(p); s.Points = 1000; ForgeData.Store(p, s);
        Assert.Throws<GameException>(() => game.ForgeRelicBuy(p, "an_ho_binh"));
    }

    [Fact]
    public async Task Day_rollover_resets_on_write_not_on_read()
    {
        var (game, p) = await Setup();
        var s = ForgeData.Load(p); s.ShopDay = "2000-01-01"; s.Rerolls = 3; s.Bought = [ForgeRelicData.All[0].Id]; ForgeData.Store(p, s);
        var before = p.ForgeState;
        game.ForgeStateFor(p);
        Assert.Equal(before, p.ForgeState);
        game.ForgeShelfReroll(p);
        Assert.Equal(1, ForgeData.Load(p).Rerolls);
    }

    [Fact]
    public async Task Buy_needs_points_and_free_shelf_slot()
    {
        var (game, p) = await Setup();
        var id = ShelfFirst(game, p);
        Assert.Throws<GameException>(() => game.ForgeRelicBuy(p, id));   // 0 điểm
        var s = ForgeData.Load(p); s.Points = 1000; ForgeData.Store(p, s);
        game.ForgeRelicBuy(p, id);
        Assert.Contains(id, ForgeData.Load(p).Relics);
        Assert.Equal(1000 - ForgeRelicData.ById[id].Price, ForgeData.Load(p).Points);
        Assert.True(game.ForgeStateFor(p).Shelf!.Single(x => x.Id == id).Bought);
        Assert.Throws<GameException>(() => game.ForgeRelicBuy(p, id));   // đã mua
        Assert.Throws<GameException>(() => game.ForgeRelicBuy(p, "khong_co"));
    }

    [Fact]
    public async Task Full_shelf_blocks_buy_until_discard()
    {
        var (game, p) = await Setup("an_ho_binh", "kim_cuong_chuy", "thien_co_do", "ban_thach_de");
        var s = ForgeData.Load(p); s.Points = 1000; ForgeData.Store(p, s);
        var id = ShelfFirst(game, p);
        Assert.Throws<GameException>(() => game.ForgeRelicBuy(p, id));
        game.ForgeRelicDiscard(p, "an_ho_binh");
        Assert.DoesNotContain("an_ho_binh", ForgeData.Load(p).Relics);
        game.ForgeRelicBuy(p, ShelfFirst(game, p));
        Assert.Equal(4, ForgeData.Load(p).Relics.Count);
    }

    [Fact]
    public async Task Reroll_costs_stone_and_changes_shelf()
    {
        var (game, p) = await Setup();
        var stone = p.Count("linh_thach");
        game.ForgeShelfReroll(p);
        Assert.Equal(stone - ForgeRelicData.RerollStone, p.Count("linh_thach"));
        Assert.Equal(1, ForgeData.Load(p).Rerolls);
        Assert.Equal(3, game.ForgeStateFor(p).Shelf!.Count);
    }

    [Fact]
    public async Task Linh_van_raises_forged_stats()
    {
        var (_, p) = await Setup();
        var strong = GameData.AllItems.Where(i => i.Slot == "weapon" && i.MinRealm == 0).MaxBy(i => i.Atk)!;   // Công đủ lớn để ×1,1 không bị làm tròn mất
        p.Equipment.RemoveAll(e => e.Slot == "weapon");
        p.Equipment.Add(new EquippedItem { PlayerId = p.Id, Slot = "weapon", ItemId = strong.Id });
        var s = ForgeData.Load(p); s.Slots["weapon"] = new ForgeData.SlotState { L = 99 }; ForgeData.Store(p, s);
        var plain = GameService.Stats(p, buffs: false).Atk;
        s.Relics = ["linh_van_khac"]; ForgeData.Store(p, s);
        Assert.True(GameService.Stats(p, buffs: false).Atk > plain);
    }
}
