using VanDao.Game;
using static VanDao.Game.ForgeData;

namespace GemSystemTests;

public class ForgeTests
{
    [Fact]
    public void Level_zero_never_breaks_or_damages()
    {
        var o = OddsAt(0, shielded: false);
        Assert.Equal(0, o.Break);
        Assert.Equal(0, o.Damage);
        Assert.Equal(1.0, o.Great + o.Success + o.Hold + o.Damage + o.Break, 9);
    }

    [Theory]
    [InlineData(5, 0.045)]
    [InlineData(10, 0.07)]
    [InlineData(50, 0.27)]
    [InlineData(99, 0.45)]   // chặn trần
    public void Break_chance_grows_with_level(int level, double expected) =>
        Assert.Equal(expected, OddsAt(level, false).Break, 3);

    [Fact]
    public void Divine_fire_removes_break_chance() =>
        Assert.Equal(0, OddsAt(40, shielded: true).Break);

    [Theory]
    [InlineData(0.00, Outcome.Great)]
    [InlineData(0.079, Outcome.Great)]
    [InlineData(0.08, Outcome.Success)]
    [InlineData(0.9999999, Outcome.Break)]
    public void Roll_walks_outcomes_in_order(double r, Outcome expected) =>
        Assert.Equal(expected, Roll(OddsAt(5, false), r));

    [Fact]
    public void Roll_at_level_zero_never_returns_break() =>
        Assert.NotEqual(Outcome.Break, Roll(OddsAt(0, false), 0.9999999));

    [Theory]
    [InlineData(4, Outcome.Great, 6)]
    [InlineData(99, Outcome.Success, 100)]
    [InlineData(199, Outcome.Great, 200)]
    [InlineData(200, Outcome.Success, 200)]
    [InlineData(4, Outcome.Success, 5)]
    [InlineData(4, Outcome.Hold, 4)]
    [InlineData(4, Outcome.Damage, 3)]
    [InlineData(0, Outcome.Damage, 0)]
    [InlineData(9, Outcome.Break, 0)]
    public void Apply_changes_level(int from, Outcome o, int to) =>
        Assert.Equal(to, Apply(new SlotState { L = from }, o).L);

    [Fact]
    public void Tenth_charge_lights_divine_fire()
    {
        var n = Apply(new SlotState { L = 3, C = 9 }, Outcome.Hold);
        Assert.Equal(0, n.C);
        Assert.Equal(FrenzyStrikes, n.F);
    }

    [Fact]
    public void Divine_fire_strike_consumes_fire_not_charge()
    {
        var n = Apply(new SlotState { L = 3, C = 0, F = 3 }, Outcome.Success);
        Assert.Equal(0, n.C);
        Assert.Equal(2, n.F);
    }

    [Fact]
    public void Break_clears_charge() =>
        Assert.Equal(0, Apply(new SlotState { L = 9, C = 7 }, Outcome.Break).C);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 21)]
    [InlineData(50, 125)]
    [InlineData(99, 296.01)]
    [InlineData(200, 800)]
    public void Bonus_percent_curve(int level, double pct) => Assert.Equal(pct, BonusPct(level), 6);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(95, 10)]
    public void Iron_cost_steps(int level, int iron) => Assert.Equal(iron, IronCost(level));

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(98)]
    public void Stone_cost_is_fixed_10k_for_any_item_and_level(int level)
    {
        var cheap = GameData.AllItems.Where(i => i.Slot != null).MinBy(GameData.SellPrice)!;
        var dear = GameData.AllItems.Where(i => i.Slot != null).MaxBy(GameData.SellPrice)!;
        Assert.Equal(10_000, StoneCost(cheap, level));
        Assert.Equal(10_000, StoneCost(dear, level));
    }

    [Fact]
    public void Store_writes_compact_levels_and_round_trips()
    {
        var p = new VanDao.Data.Player();
        var s = new ForgeSave { N = 3 };
        s.Slots["weapon"] = new SlotState { L = 7, C = 2 };
        s.Slots["feet"] = new SlotState { L = 0, C = 5 };
        for (var i = 0; i < HistoryKeep + 5; i++) s.Log.Add(new LogLine(i, "weapon", "x", 0, 1, "success", 0));
        s.Undo = new UndoState("weapon", new SlotState { L = 6, C = 1 });
        Store(p, s);
        Assert.Equal("weapon:7", p.ForgeLevels);
        Assert.Equal(7, Levels(p)["weapon"]);
        var back = Load(p);
        Assert.Equal(3, back.N);
        Assert.Equal(5, back.Slots["feet"].C);
        Assert.Equal(HistoryKeep, back.Log.Count);
        Assert.Equal(0, back.Log[0].N);   // giữ các dòng đầu (mới nhất), cắt phần cuối
        Assert.Equal("weapon", back.Undo!.Slot);
        Assert.Equal(6, back.Undo.Before.L);
    }

    [Fact]
    public void Levels_tolerates_duplicates_and_garbage()
    {
        var p = new VanDao.Data.Player { ForgeLevels = "weapon:3;weapon:5;garbage;head:x" };
        var l = Levels(p);
        Assert.Equal(5, l["weapon"]);
        Assert.False(l.ContainsKey("head"));
    }

    [Fact]
    public void Corrupt_state_loads_empty()
    {
        var p = new VanDao.Data.Player { ForgeState = "{không phải json" };
        Assert.Empty(Load(p).Slots);
    }

    [Fact]
    public void Forge_items_registered_and_sold_at_shop()
    {
        Assert.True(GameData.ItemById.ContainsKey(Iron));
        Assert.True(GameData.ItemById.ContainsKey(Rewind));
        Assert.Contains(GameData.Shop.Fixed, x => x.Item == Iron);
        Assert.DoesNotContain(GameData.Shop.Fixed, x => x.Item == Rewind);   // Hồi Thiên Phù bán ở Tạp Hóa VIP
        Assert.Equal(5, GameService.VipOffersFor(new VanDao.Data.Player()).Single(o => o.Item == Rewind).Price);
    }

    [Fact]
    public void Forge_level_multiplies_equipped_item_stats()
    {
        var weapon = GameData.AllItems.Where(i => i.Slot == "weapon" && i.MinRealm == 0 && i.Atk > 0).OrderByDescending(i => i.Atk).First();   // công đủ lớn để làm tròn không lệch
        var p = new VanDao.Data.Player { Realm = 0, Stage = 0 };
        p.Equipment.Add(new VanDao.Data.EquippedItem { Slot = "weapon", ItemId = weapon.Id });
        var before = GameService.Stats(p, buffs: false).Atk;
        p.ForgeLevels = "weapon:10";   // +21%
        var after = GameService.Stats(p, buffs: false).Atk;
        Assert.True(after > before, $"{after} > {before}");
        // Phần tăng xấp xỉ 21% công của món sau hệ số toàn cục và làm tròn.
        Assert.InRange((double)(after - before), weapon.Atk * 0.15, weapon.Atk * 0.30);
    }

    [Fact]
    public void Forge_level_on_empty_slot_does_nothing()
    {
        var p = new VanDao.Data.Player { Realm = 0, Stage = 0 };
        var before = GameService.Stats(p, buffs: false).Atk;
        p.ForgeLevels = "weapon:15";
        Assert.Equal(before, GameService.Stats(p, buffs: false).Atk);
    }
}
