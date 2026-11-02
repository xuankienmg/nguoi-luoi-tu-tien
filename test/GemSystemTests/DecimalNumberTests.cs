using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>Linh thạch, tu vi, máu / công / thủ / sát thương / lực chiến là decimal: không còn chặn ở int (2.147.483.647) hay long.</summary>
public class DecimalNumberTests
{
    [Fact]
    public void SellingPushesSpiritStonesPastIntMax()
    {
        var p = new Player();
        var service = new GameService(null!);
        var price = GameData.ItemById["gem_hoa_lv10"].Sell;
        p.Add("linh_thach", 2_130_000_000);
        p.Add("gem_hoa_lv10", 9);
        var unit = GameData.ItemById["thuong_pham_linh_thach"].Sell;
        var n = (int)Math.Ceiling((int.MaxValue - 2_130_000_000m) / unit) + 1;
        p.Add("thuong_pham_linh_thach", n);

        service.Sell(p, "thuong_pham_linh_thach", n);
        Assert.Equal(2_130_000_000m + (decimal)unit * n, p.Count("linh_thach"));
        Assert.True(p.Count("linh_thach") > int.MaxValue);

        var before = p.Count("linh_thach");
        service.SellBulk(p, 9, items: "gem_hoa_lv10:9", pick: true);
        Assert.Equal(before + 9m * price, p.Count("linh_thach"));
        Assert.Equal(p.Count("linh_thach"), p.Inventory.Single(i => i.ItemId == "linh_thach").Count);
    }

    [Fact]
    public void HpAndDamageGoPastIntAndLongMax()
    {
        Assert.Equal(5_100_000_000m, MonsterRageCombat.ScaleCounter(3_000_000_000m, MonsterRageCombat.BossMultiplier));
        var hit = GemCombat.Mitigate(20_000_000_000_000_000_000m, new GemBonuses(), new Random(1));
        Assert.Equal(20_000_000_000_000_000_000m, hit.Damage);
        Assert.True(CombatRatings.Power(3_000_000_000, 3_000_000_000, 100_000_000_000_000_000_000m, new CombatRatings()) > long.MaxValue);

        var p = new Player { Id = Guid.Parse("50000000-0000-0000-0000-000000000001"), Realm = 0, Chapter = 0, Diff = 0, MapVer = 1,
            BodyHp = 100_000_000_000_000_000_000m, BodyDef = 3_000_000_000m };
        var st = GameService.Stats(p);
        Assert.True(st.Hp > long.MaxValue);
        Assert.True(st.Def > int.MaxValue);
        var battle = GameService.BattleAt(p, 1, new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
        Assert.True(battle.Win);
        Assert.Equal(st.Hp, battle.HeroHp);
        Assert.All(battle.HeroHpLeft, hp => Assert.True(hp > long.MaxValue));
        Assert.True(GameService.Power(p) > long.MaxValue);
    }

    [Fact]
    public void QiAccumulatesPastIntMaxWithoutLosingTheFraction()
    {
        var p = new Player { Qi = 3_000_000_000.25m };
        p.Qi += 0.5m;
        Assert.Equal(3_000_000_000.75m, p.Qi);
    }

    /// <summary>Save SQLite của bản cũ: Count / BodyHp là INTEGER, Qi là REAL (EnsureCreated bản mới tạo TEXT).
    /// Bản mới vẫn đọc được, ghi số vượt int và đọc lại đúng.</summary>
    [Fact]
    public async Task OldSqliteSaveLoadsAndStoresLargeNumbers()
    {
        var file = Path.Combine(Path.GetTempPath(), $"vandao-old-{Guid.NewGuid():N}.db");
        var id = Guid.NewGuid();
        try
        {
            using (var c = new SqliteConnection($"Data Source={file};Pooling=False"))
            {
                c.Open();
                using (var db = Context(c)) db.Database.EnsureCreated();
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                    PRAGMA writable_schema = ON;
                    UPDATE sqlite_master SET sql = replace(sql, '"Count" TEXT NOT NULL', '"Count" INTEGER NOT NULL') WHERE name = 'Inventory';
                    UPDATE sqlite_master SET sql = replace(replace(replace(sql, '"Qi" TEXT NOT NULL', '"Qi" REAL NOT NULL'),
                        '"BodyHp" TEXT NOT NULL', '"BodyHp" INTEGER NOT NULL'), '"BodyDef" TEXT NOT NULL', '"BodyDef" INTEGER NOT NULL') WHERE name = 'Players';
                    PRAGMA writable_schema = OFF;
                    """;
                cmd.ExecuteNonQuery();
            }
            using (var c = new SqliteConnection($"Data Source={file};Pooling=False"))
            {
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = "SELECT sql FROM sqlite_master WHERE name = 'Inventory'";
                Assert.Contains("\"Count\" INTEGER", (string)cmd.ExecuteScalar()!);
                using (var db = Context(c))
                {
                    var p = new Player { Id = id, Qi = 1234.5m, BodyHp = 7 };
                    p.Add("linh_thach", 2_000_000_000);
                    db.Players.Add(p);
                    db.SaveChanges();
                }
                using (var db = Context(c))
                {
                    SchemaUpgrade.Apply(db);
                    var p = await Load(db, id);
                    Assert.Equal(2_000_000_000m, p.Count("linh_thach"));
                    Assert.Equal(1234.5m, p.Qi);
                    p.Add("linh_thach", 1_000_000_000);
                    p.Qi = 123_456_789_012.5m;
                    p.BodyHp = 5_000_000_000_000m;
                    db.SaveChanges();
                }
                if (c.State != System.Data.ConnectionState.Open) c.Open();   // SchemaUpgrade.Apply đóng kết nối
                cmd.CommandText = "SELECT typeof(\"Count\") FROM \"Inventory\" WHERE \"ItemId\" = 'linh_thach'";
                Assert.Equal("integer", cmd.ExecuteScalar());   // cột INTEGER của save cũ vẫn lưu số nguyên
                using (var db = Context(c))
                {
                    var p = await Load(db, id);
                    Assert.Equal(3_000_000_000m, p.Count("linh_thach"));
                    Assert.Equal(123_456_789_012.5m, p.Qi);
                    Assert.Equal(5_000_000_000_000m, p.BodyHp);
                }
            }
        }
        finally { File.Delete(file); }
    }

    /// <summary>Nạp thẳng từ DB (LoadAsync còn cộng tu vi / thu hoạch theo thời gian).</summary>
    private static Task<Player> Load(GameDbContext db, Guid id) => db.Players.Include(x => x.Inventory).SingleAsync(x => x.Id == id);

    private static GameDbContext Context(SqliteConnection c) => new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(c).Options);
}
