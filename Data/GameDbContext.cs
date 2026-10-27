using Microsoft.EntityFrameworkCore;

namespace VanDao.Data;

public class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<InventoryItem> Inventory => Set<InventoryItem>();
    public DbSet<LogEntry> Logs => Set<LogEntry>();
    public DbSet<EquippedItem> Equipment => Set<EquippedItem>();

    /// <summary>Sau mỗi lần lưu: ghi Id nhật ký lớn nhất vào Meta (SaveGuard). Bản game cũ ghi nhật ký mà không cập nhật mốc này
    /// → lần mở sau bản mới phát hiện được (SaveGuard.Check đánh dấu nhân vật).</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var n = await base.SaveChangesAsync(ct);
        try { await Database.ExecuteSqlRawAsync("""INSERT INTO "Meta" ("Key", "Value") SELECT 'log_mark', CAST(COALESCE(MAX("Id"), 0) AS TEXT) FROM "Logs" WHERE 1 ON CONFLICT("Key") DO UPDATE SET "Value" = excluded."Value";""", ct); }
        catch (Exception) { }   // chưa có bảng Meta (công cụ / test): bỏ qua
        return n;
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Player>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasMany(p => p.Pets).WithOne().HasForeignKey(p => p.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.Property(p => p.Name).HasMaxLength(64);
            e.HasMany(p => p.Inventory).WithOne().HasForeignKey(i => i.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Logs).WithOne().HasForeignKey(l => l.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Equipment).WithOne().HasForeignKey(q => q.PlayerId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<InventoryItem>(e =>
        {
            e.HasKey(i => new { i.PlayerId, i.ItemId });
            e.Property(i => i.ItemId).HasMaxLength(40);
        });

        b.Entity<EquippedItem>(e =>
        {
            e.HasKey(q => new { q.PlayerId, q.Slot });
            e.Property(q => q.Slot).HasMaxLength(20);
            e.Property(q => q.ItemId).HasMaxLength(40);
        });

        b.Entity<LogEntry>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => new { l.PlayerId, l.Id });
            e.Property(l => l.Text).HasMaxLength(500);
            e.Property(l => l.Kind).HasMaxLength(10);
        });
    }
}

public static class SchemaUpgrade
{
    /// <summary>
    /// EnsureCreated không sửa DB đã có, nên tự thêm cột/bảng mới cho file vandao.db của bản cũ.
    /// </summary>
    public static void Apply(GameDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Players') WHERE name = 'AutoExplore'";
        if (Convert.ToInt64(cmd.ExecuteScalar()) == 0)
        {
            cmd.CommandText = "ALTER TABLE \"Players\" ADD COLUMN \"AutoExplore\" INTEGER NOT NULL DEFAULT 1";
            cmd.ExecuteNonQuery();
        }
        // cột luyện đan / luyện thể thêm sau
        foreach (var (col, def) in new[]
        {
            ("Hatch", "TEXT NOT NULL DEFAULT ''"),
            ("AlchemyExp", "INTEGER NOT NULL DEFAULT 0"), ("RefineRecipe", "INTEGER NOT NULL DEFAULT -1"),
            ("RefineStartAt", "TEXT NOT NULL DEFAULT '2000-01-01 00:00:00'"), ("RefineEndAt", "TEXT NOT NULL DEFAULT '2000-01-01 00:00:00'"),
            ("RefineLeft", "INTEGER NOT NULL DEFAULT 0"), ("RefineCatalyst", "INTEGER NOT NULL DEFAULT 0"),
            ("LastRefine", "TEXT NOT NULL DEFAULT ''"), ("LastRefineAt", "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'"),
            ("BodyHp", "INTEGER NOT NULL DEFAULT 0"), ("BodyDef", "INTEGER NOT NULL DEFAULT 0"),
            ("Area", "INTEGER NOT NULL DEFAULT 1"), ("AreaMax", "INTEGER NOT NULL DEFAULT 1"),
            ("Skills", "TEXT NOT NULL DEFAULT ''"), ("BossKills", "INTEGER NOT NULL DEFAULT 0"), ("SkillSlots", "TEXT NOT NULL DEFAULT ''"),
            ("LearnedManuals", "TEXT NOT NULL DEFAULT ''"), ("ManualVersion", "INTEGER NOT NULL DEFAULT 0"), ("ActiveManual", "TEXT NOT NULL DEFAULT ''"), ("ManualProgress", "TEXT NOT NULL DEFAULT ''"),
            ("BreakCooldownUntil", "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'"), ("DebuffUntil", "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'"),
            ("PillBonus", "REAL NOT NULL DEFAULT 0"), ("Skin", "TEXT NOT NULL DEFAULT ''"), ("SkinsUnlocked", "TEXT NOT NULL DEFAULT ''"),
            ("Gender", "TEXT NOT NULL DEFAULT 'male'"),
            ("Admin", "TEXT NOT NULL DEFAULT ''"), ("AdminPoints", "INTEGER NOT NULL DEFAULT 0"),
            ("ElixirPoints", "INTEGER NOT NULL DEFAULT 0"),
            ("MapRealm", "INTEGER NOT NULL DEFAULT -1"), ("Redeemed", "TEXT NOT NULL DEFAULT ''"), ("SpinHistory", "TEXT NOT NULL DEFAULT ''"),
            ("SpinDay", "TEXT NOT NULL DEFAULT ''"), ("SpinDailyUsed", "INTEGER NOT NULL DEFAULT 0"),
            ("PetSpinDay", "TEXT NOT NULL DEFAULT ''"), ("PetSpinDailyUsed", "INTEGER NOT NULL DEFAULT 0"),
            ("SpinPityUsed", "INTEGER NOT NULL DEFAULT 0"), ("PetSpinPityUsed", "INTEGER NOT NULL DEFAULT 0"),
            ("Chapter", "INTEGER NOT NULL DEFAULT 0"), ("ChapterMax", "INTEGER NOT NULL DEFAULT 0"), ("MapVer", "INTEGER NOT NULL DEFAULT 0"),
            ("Diff", "INTEGER NOT NULL DEFAULT 0"), ("DiffProg", "TEXT NOT NULL DEFAULT ''"), ("Cleared", "TEXT NOT NULL DEFAULT ''"),
            ("EventKey", "TEXT NOT NULL DEFAULT ''"), ("EventPoints", "INTEGER NOT NULL DEFAULT 0"), ("EventClaimed", "TEXT NOT NULL DEFAULT ''"),
            ("LbSecret", "TEXT NOT NULL DEFAULT ''"), ("Tainted", "INTEGER NOT NULL DEFAULT 0"), ("Flagged", "INTEGER NOT NULL DEFAULT 0"),
            ("GemVault", "TEXT NOT NULL DEFAULT ''"), ("GemSlots", "TEXT NOT NULL DEFAULT ''"),
        })
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('Players') WHERE name = '{col}'";
            if (Convert.ToInt64(cmd.ExecuteScalar()) > 0) continue;
            cmd.CommandText = $"ALTER TABLE \"Players\" ADD COLUMN \"{col}\" {def}";
            cmd.ExecuteNonQuery();
        }
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS "Equipment" (
                "PlayerId" TEXT NOT NULL,
                "Slot" TEXT NOT NULL,
                "ItemId" TEXT NOT NULL,
                CONSTRAINT "PK_Equipment" PRIMARY KEY ("PlayerId", "Slot"),
                CONSTRAINT "FK_Equipment_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE CASCADE
            )
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS "Pets" (
                "Id" TEXT NOT NULL PRIMARY KEY, "PlayerId" TEXT NOT NULL, "DefId" TEXT NOT NULL,
                "Level" INTEGER NOT NULL DEFAULT 1, "Exp" INTEGER NOT NULL DEFAULT 0,
                "Form" INTEGER NOT NULL DEFAULT 0, "AptAtk" INTEGER NOT NULL DEFAULT 100,
                "AptDef" INTEGER NOT NULL DEFAULT 100, "AptHp" INTEGER NOT NULL DEFAULT 100,
                "Slot" INTEGER NOT NULL DEFAULT 0, "FoodSpent" INTEGER NOT NULL DEFAULT 0,
                "CreatedAt" TEXT NOT NULL,
                FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE CASCADE);
            CREATE INDEX IF NOT EXISTS "IX_Pets_PlayerId" ON "Pets" ("PlayerId");
            """;
        cmd.ExecuteNonQuery();
        ReplaceSpears(cmd);
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Pets') WHERE name = 'AppearanceForm'";
        if (Convert.ToInt64(cmd.ExecuteScalar()) == 0)
        {
            cmd.CommandText = "ALTER TABLE \"Pets\" ADD COLUMN \"AppearanceForm\" INTEGER NOT NULL DEFAULT -1";
            cmd.ExecuteNonQuery();
        }
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Pets') WHERE name = 'Quality'";
        if (Convert.ToInt64(cmd.ExecuteScalar()) == 0)
        {
            cmd.CommandText = "ALTER TABLE \"Pets\" ADD COLUMN \"Quality\" INTEGER NOT NULL DEFAULT -1";   // -1: linh thú cũ giữ phẩm theo loài
            cmd.ExecuteNonQuery();
        }
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Pets') WHERE name = 'Stars'";
        if (Convert.ToInt64(cmd.ExecuteScalar()) == 0)
        {
            cmd.CommandText = "ALTER TABLE \"Pets\" ADD COLUMN \"Stars\" INTEGER NOT NULL DEFAULT 0";
            cmd.ExecuteNonQuery();
        }
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Pets') WHERE name = 'StarPts'";
        if (Convert.ToInt64(cmd.ExecuteScalar()) == 0)
        {
            cmd.CommandText = "ALTER TABLE \"Pets\" ADD COLUMN \"StarPts\" INTEGER NOT NULL DEFAULT 0";   // con đã có sao (luật cũ 1 con/sao): quy đổi trong PetData.Pts
            cmd.ExecuteNonQuery();
        }
        // bồi dưỡng linh thú: thức tỉnh huyết mạch + 3 ô trang bị linh thú
        foreach (var (col, def) in new[] { ("Awaken", "INTEGER NOT NULL DEFAULT 0"), ("GearCollar", "TEXT NOT NULL DEFAULT ''"), ("GearArmor", "TEXT NOT NULL DEFAULT ''"), ("GearCharm", "TEXT NOT NULL DEFAULT ''") })
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('Pets') WHERE name = '{col}'";
            if (Convert.ToInt64(cmd.ExecuteScalar()) != 0) continue;
            cmd.CommandText = $"ALTER TABLE \"Pets\" ADD COLUMN \"{col}\" {def}";
            cmd.ExecuteNonQuery();
        }
        // vật phẩm đã bị gỡ khỏi game (vd. trang bị Trúc Cơ / Kim Đan bản cũ): xóa khỏi túi và ô trang bị để save cũ không lỗi
        var known = string.Join(",", VanDao.Game.GameData.AllItems.Select(i => $"'{i.Id.Replace("'", "''")}'"));
        cmd.CommandText = $"DELETE FROM \"Inventory\" WHERE \"ItemId\" NOT IN ({known}); DELETE FROM \"Equipment\" WHERE \"ItemId\" NOT IN ({known});";
        cmd.ExecuteNonQuery();
        conn.Close();
    }

    /// <summary>Vũ khí dạng Thương đã bỏ: đổi sang Kiếm cùng bộ, cùng phẩm (trong túi thì cộng dồn vào chồng kiếm sẵn có; đang mặc thì thay tại chỗ).
    /// Chạy trước bước xóa vật phẩm lạ để người chơi không mất đồ.</summary>
    private static void ReplaceSpears(System.Data.Common.DbCommand cmd)
    {
        void Param(string name, object value) { var p = cmd.CreateParameter(); p.ParameterName = name; p.Value = value; cmd.Parameters.Add(p); }
        foreach (var table in new[] { "Inventory", "Equipment" })
        {
            cmd.CommandText = $"SELECT DISTINCT \"ItemId\" FROM \"{table}\" WHERE \"ItemId\" LIKE '%thuong%'";
            var ids = new List<string>();
            using (var r = cmd.ExecuteReader()) while (r.Read()) ids.Add(r.GetString(0));
            foreach (var from in ids)
            {
                if (VanDao.Game.GameData.ReplaceSpear(from) is not { } to) continue;
                cmd.Parameters.Clear(); Param("$from", from); Param("$to", to);
                cmd.CommandText = table == "Equipment"
                    ? "UPDATE \"Equipment\" SET \"ItemId\" = $to WHERE \"ItemId\" = $from"
                    : """
                      INSERT INTO "Inventory" ("PlayerId", "ItemId", "Count")
                        SELECT "PlayerId", $to, "Count" FROM "Inventory" WHERE "ItemId" = $from
                        ON CONFLICT ("PlayerId", "ItemId") DO UPDATE SET "Count" = "Count" + excluded."Count";
                      DELETE FROM "Inventory" WHERE "ItemId" = $from;
                      """;
                cmd.ExecuteNonQuery();
            }
        }
        cmd.Parameters.Clear();
    }
}
