using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class GemCombatRatingAggregationTests
{
    private static Player Equipped()
    {
        var p = new Player { Realm = 9 };
        foreach (var slot in GemData.EquipSlots)
            p.Equipment.Add(new() { Slot = slot, ItemId = GameData.AllItems.First(i => i.Slot == slot && i.MinRealm == 9 && i.Rarity == "supreme").Id });
        return p;
    }

    private static void Socket(Player p, string theme, int count)
    {
        var id = $"gem_{theme}_lv10";
        var slots = new Dictionary<string, GemSlotData>();
        foreach (var slot in GemData.EquipSlots.Where(s => GemData.CanSocket(id, s)).Take(count / 3))
            slots[slot] = new() { Sockets = Enumerable.Repeat(id, 3).ToList() };
        Assert.Equal(count, slots.Values.Sum(s => s.Sockets.Count));
        GemData.SetSlots(p, slots);
    }

    [Theory]
    [InlineData("quang", 3, "Accuracy", 90)]
    [InlineData("tho", 15, "ComboResist", 450)]
    [InlineData("vu_tru", 15, "ReflectResist", 450)]
    [InlineData("loi", 15, "Combo", 450)]
    public void LegalBuildAddsRatingsExactlyOnceAndSurvivesReload(string theme, int count, string field, double expected)
    {
        var p = Equipped();
        double Read(CombatStats s) => (double)typeof(CombatStats).GetProperty(field)!.GetValue(s)!;
        var before = Read(GameService.Stats(p));
        Socket(p, theme, count);
        var after = GameService.Stats(p);
        Assert.Equal(expected, Read(after) - before);
        Assert.Equal(after, GameService.Stats(p));
        Assert.Equal(after, GameService.Stats(JsonSerializer.Deserialize<Player>(JsonSerializer.Serialize(p))!));
        GemData.SetSlots(p, new());
        Assert.Equal(before, Read(GameService.Stats(p)));
    }

    [Theory]
    [InlineData("tho", "ComboResist")]
    [InlineData("vu_tru", "ReflectResist")]
    public void LockedAndUnequippedSocketsDoNotContribute(string theme, string field)
    {
        var p = Equipped();
        Socket(p, theme, 3);
        double Read() => (double)typeof(GemBonuses).GetProperty(field)!.GetValue(GemData.GetSocketedBonuses(p))!;
        Assert.Equal(90, Read());
        var slot = Assert.Single(GemData.GetSlots(p)).Key;
        var gear = p.Equipment.Single(e => e.Slot == slot);
        gear.ItemId = GameData.AllItems.First(i => i.Slot == slot && i.MinRealm == 9 && i.Rarity == "common").Id;
        Assert.Equal(30, Read());
        p.Add("linh_thach", 10000);
        new GameService(null!).UnsocketGem(p, slot, 2);
        Assert.Equal(30, Read());
        Assert.Equal(1, p.Count($"gem_{theme}_lv10"));
        p.Equipment.Remove(gear);
        Assert.Equal(0, Read());
    }

    [Theory]
    [InlineData("tho")]
    [InlineData("vu_tru")]
    [InlineData("loi")]
    public async Task SourceRowMatchesUncappedGemBonusesAndTotal(string theme)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var p = Equipped();
        Socket(p, theme, 15);
        db.Players.Add(p);
        await db.SaveChangesAsync();
        var state = await new GameService(db).BuildStateAsync(p);
        var gem = GemData.GetSocketedBonuses(p);
        var row = Assert.Single(state.StatSources!, s => s.Name == "Khảm Tinh Thạch");
        Assert.Equal(gem.ComboResist, row.ComboResist);
        Assert.Equal(gem.ReflectResist, row.ReflectResist);
        Assert.Equal(gem.ExtraHit, row.Combo);
        Assert.Equal(state.Stats.ComboResist, state.StatSources!.Sum(s => s.ComboResist), 6);
        Assert.Equal(state.Stats.ReflectResist, state.StatSources!.Sum(s => s.ReflectResist), 6);
        Assert.Equal(state.Stats.Combo, state.StatSources!.Sum(s => s.Combo), 6);
    }
}
