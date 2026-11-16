# Tông Môn bản 2 — Boss tông hằng ngày + giao diện sơn môn: Kế hoạch triển khai

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mỗi tông có một Boss chung mỗi ngày (máu chung, mỗi người 2 lượt mô phỏng bằng `PvpCombat`), thưởng cống hiến / rương / top / buff tu vi 24h; tab Tông Môn đổi thành ảnh sơn môn có điểm bấm.

**Architecture:** Dữ liệu và luật trong `SectBossData` (hàm thuần) + `ServerSectBoss` (DB, mọi thay đổi dùng UPDATE có điều kiện như `ServerSect`); `GameService.SectBoss.cs` chạy mô phỏng trận và phát thưởng vào hành trang; UI thêm vào `sect.js`/`sect.css`, nền là ảnh sơn môn.

**Tech Stack:** C# / ASP.NET Core 8, EF Core (SQLite offline + Postgres online), xUnit (`test/GemSystemTests`), JS thuần + `node --test`.

**Spec:** `docs/superpowers/specs/2026-11-11-tong-mon-boss-design.md`

**Quy ước dự án áp dụng cho plan này**
- Không commit / push trừ khi người dùng yêu cầu (nên plan **không có bước commit**).
- Chỉ chạy test liên quan. Lệnh C#: `dotnet test test/GemSystemTests --filter "FullyQualifiedName~SectBoss" --nologo -v q -clp:ErrorsOnly --logger "console;verbosity=minimal"`. Lệnh JS: `node --test --test-reporter=dot test/sect.test.cjs`.
- Migration mới phải nối vào `supabase/migrations/20261026_online_authority.sql` trước khi phát hành.
- Chữ hiển thị mới phải có bản tiếng Anh trong `wwwroot/js/i18n-en.js`.

## Cấu trúc file

| File | Việc |
|---|---|
| `Game/PvpCombat.cs` (sửa) | Thêm tham số `maxRounds` cho `Run` |
| `Game/SectBossData.cs` (mới) | Hằng số, danh sách Boss, hàm thuần (máu, cống hiến, hạng, ngày) |
| `Data/Entities.cs` (sửa) | `SectBoss`, `SectBossHit`, `SectMember.BossRef`, `Sect.BuffDay` |
| `Data/GameDbContext.cs` (sửa) | DbSet, cấu hình, `UpgradeSect` cho SQLite |
| `Online/Migrations/20261111110000_OnlineSectBoss.cs` (mới) + snapshot + SQL | Postgres |
| `Game/SectBuff.cs` (sửa) | Cộng +3% khi `BuffDay` = hôm nay |
| `Game/ServerSect.cs` (sửa nhỏ) | `AwardAsync` công khai, `SystemAsync` internal, `PageAsync` truyền `BuffDay` |
| `Game/ServerSectBoss.cs` (mới) | Chốt máu, giữ lượt, ghi sát thương, hạ Boss, nhận rương |
| `Game/GameService.SectBoss.cs` (mới) | Mô phỏng lượt đánh, tấn công, nhận rương |
| `Game/Dtos.cs` (sửa) | `SectBossDto`, `SectBossRankDto` |
| `Program.cs` (sửa) | 3 API Boss tông |
| `wwwroot/js/sect.js`, `wwwroot/css/sect.css` (sửa) | Sơn môn, view Boss, Đan Phòng |
| `wwwroot/img/sect/son_mon.jpg` (mới) | Ảnh nền |
| `wwwroot/js/i18n-en.js`, `docs/releases/upcoming.md` (sửa) | Chữ tiếng Anh, ghi chú phát hành |
| `test/GemSystemTests/SectBossTests.cs`, `test/sect.test.cjs` | Test |

---

### Task 1: `PvpCombat.Run` nhận số hiệp tối đa

**Files:**
- Modify: `Game/PvpCombat.cs:120,130`
- Test: `test/GemSystemTests/SectBossTests.cs` (tạo mới ở bước này, các task sau thêm vào)

- [ ] **Step 1: Tạo file test với test đầu tiên**

```csharp
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class SectBossTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    private readonly DateTime now = DateTime.UtcNow;
    private ServerSect Sect => new(db);
    private ServerSectBoss Boss => new(db);
    private string Day => SectData.DayOf(now);

    public SectBossTests() { db.Database.OpenConnection(); db.Database.EnsureCreated(); SectBuff.Clear(); ServerChat.ResetOnlineCacheForTests(); }
    public void Dispose() => db.Dispose();

    private async Task<Guid> NewPlayer(string name, int realm = 1, long stones = 0)
    {
        var p = await new GameService(db).CreateAsync(name);
        p.Realm = realm; p.LastTickAt = now;
        if (stones > 0) p.Add("linh_thach", stones);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return p.Id;
    }

    /// <summary>Tông có chủ là leader; members vào bằng đơn + duyệt.</summary>
    private async Task<(Guid Leader, long SectId)> NewSect(string name = "Thanh Vân", params Guid[] members)
    {
        var leader = await NewPlayer("Chủ " + name);
        await Sect.CreateAsync(leader, name, now);
        var id = (await db.Sects.AsNoTracking().SingleAsync(s => s.Name == name)).Id;
        foreach (var m in members)
        {
            await Sect.ApplyAsync(m, id, now);
            var app = await db.SectApplications.SingleAsync(a => a.PlayerId == m);
            await Sect.DecideAsync(leader, app.Id, true, now);
        }
        return (leader, id);
    }

    private Task<SectMember> Row(Guid id) => db.SectMembers.AsNoTracking().SingleAsync(m => m.PlayerId == id);
    private Task SetRef(Guid id, double v) => db.SectMembers.Where(x => x.PlayerId == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.BossRef, v));

    [Fact]
    public void PvpRunStopsAtMaxRounds()
    {
        var a = PvpFighter.FromStats("A", 1, new() { ["atk"] = 100, ["hp"] = 1000, ["def"] = 0 });
        var d = PvpFighter.FromStats("D", 1, new() { ["atk"] = 1, ["hp"] = 1e12, ["def"] = 0 });
        Assert.True(PvpCombat.Run(a, d, 1, 3).Actions.Count <= 6);
        Assert.True(PvpCombat.Run(a, d, 1).Actions.Count > 6);
    }
}
```

- [ ] **Step 2: Chạy test, thấy lỗi biên dịch**

Run: `dotnet test test/GemSystemTests --filter "FullyQualifiedName~SectBossTests" --nologo -v q -clp:ErrorsOnly --logger "console;verbosity=minimal"`
Expected: lỗi biên dịch (`Run` không nhận 4 tham số, `ServerSectBoss`, `SectMember.BossRef` chưa có). Các lỗi này sẽ hết dần qua các task; để test chạy được ngay ở task này, tạm **chưa** thêm các helper dùng `Boss`/`SetRef`: xóa 2 dòng `ServerSectBoss Boss`, `SetRef` khỏi file và thêm lại ở Task 6.

- [ ] **Step 3: Sửa `PvpCombat.Run`**

Trong `Game/PvpCombat.cs`:

```csharp
    public static PvpResult Run(PvpFighter a, PvpFighter d, int seed, int maxRounds = MaxRounds)
```
và đổi vòng lặp:
```csharp
        for (var r = 0; r < Math.Min(maxRounds, MaxRounds) && !ko; r++)
```

- [ ] **Step 4: Chạy lại test, phải PASS**

Run: lệnh ở Step 2, thêm `--filter "FullyQualifiedName~PvpRunStopsAtMaxRounds"`.
Expected: 1 test pass. Chạy thêm `--filter "FullyQualifiedName~PvpPet|FullyQualifiedName~ElementCounter"` để chắc các test PK cũ vẫn xanh.

---

### Task 2: `SectBossData` (hằng số và hàm thuần)

**Files:**
- Create: `Game/SectBossData.cs`
- Test: `test/GemSystemTests/SectBossTests.cs`

- [ ] **Step 1: Viết test (thêm vào class `SectBossTests`)**

```csharp
    [Fact]
    public void BossRotatesDailyAndCycles()
    {
        var d1 = SectBossData.BossOf("2026-11-11");
        var d2 = SectBossData.BossOf("2026-11-12");
        Assert.NotEqual(d1.Id, d2.Id);
        Assert.Equal(d1.Id, SectBossData.BossOf(SectBossData.NextDay(SectBossData.NextDay("2026-11-11"))).Id);   // 2 Boss luân phiên
        Assert.Equal("2026-12-01", SectBossData.NextDay("2026-11-30"));
    }

    [Fact]
    public void MaxHpIsShareOfTwoHitsPerMember()
    {
        Assert.Equal(960, SectBossData.MaxHp([100, 300, 200, 200]));   // 0,6 × 2 lượt × 800
        Assert.Equal(1, SectBossData.MaxHp([]));
    }

    [Fact]
    public void ContributionScalesWithShareOfHpAndClamps()
    {
        Assert.Equal(5, SectBossData.Contribution(0, 1000));
        Assert.Equal(15, SectBossData.Contribution(50, 1000));      // 300 × 5%
        Assert.Equal(30, SectBossData.Contribution(900, 1000));
    }

    [Fact]
    public void RankOrdersByDamageThenId()
    {
        var a = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var b = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var c = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var rows = new (Guid, double)[] { (a, 10), (b, 50), (c, 50) };
        Assert.Equal(0, SectBossData.Rank(rows, b));
        Assert.Equal(1, SectBossData.Rank(rows, c));
        Assert.Equal(2, SectBossData.Rank(rows, a));
        Assert.Equal(int.MaxValue, SectBossData.Rank([(a, 0)], a));   // không gây sát thương: không vào top
    }
```

- [ ] **Step 2: Chạy, thấy lỗi biên dịch** (`SectBossData` chưa có).

- [ ] **Step 3: Tạo `Game/SectBossData.cs`**

```csharp
using System.Globalization;

namespace VanDao.Game;

/// <summary>
/// Boss Tông Môn hằng ngày: luật và con số. Mỗi tông một Boss mỗi ngày (0h giờ Việt Nam), máu chung.
/// Mỗi người đánh HitsPerDay lượt; một lượt = trận PvpCombat tối đa MaxRounds hiệp với Boss bản sao của chính mình.
/// Con số khởi điểm: chỉnh khi cân bằng (skill can-bang-chi-so).
/// </summary>
public static class SectBossData
{
    public const int HitsPerDay = 2, MaxRounds = 15;
    public const int ActiveDays = 3;                  // thành viên vắng quá số ngày này không tính vào máu Boss
    public const double HpShare = .6;                 // máu Boss = 60% × tổng sát thương tham chiếu × số lượt
    public const double AtkMult = 1.6, DefMult = 1.2; // Boss bản sao: công/thủ nhân hệ số này
    public const double BuffQiPct = 3;                // buff tu vi toàn tông ngày hôm sau khi hạ Boss
    public const double ContribScale = 300;           // cống hiến/lượt = clamp(300 × sát thương / máu Boss, Min, Max)
    public const int ContribMin = 5, ContribMax = 30;
    public static readonly int[] TopBonus = [50, 30, 20];   // cống hiến thêm cho top 1/2/3 khi nhận rương

    public const int ChestStoneMult = 3;              // linh thạch rương = 3 × StoneBase(cảnh giới)
    public const string ChestPill = "bo_nguyen_dan_trung";
    public const int ChestPetPills = 50;              // Linh Thú Đan

    public record Def(string Id, string Name, string Image, string Desc);
    public static readonly Def[] All =
    [
        new("boss_co_thu_yeu_vuong", "Cổ Thụ Yêu Vương", "img/boss_co_thu_yeu_vuong.png",
            "Cây cổ vạn năm thành yêu, rễ bám khắp rừng sâu, bị phong ấn dưới Bệ Phong Ấn của tông."),
        new("boss_huyen_nham_cu_nhan", "Huyền Nham Cự Nhân", "img/boss_huyen_nham_cu_nhan.png",
            "Người đá khổng lồ ngủ dưới linh mạch, mỗi đêm lại phá phong ấn thức dậy."),
    ];

    private static DateOnly Parse(string day) => DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static Def BossOf(string day) => All[Parse(day).DayNumber % All.Length];
    public static string NextDay(string day) => Parse(day).AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Máu Boss từ sát thương tham chiếu của từng thành viên hoạt động; luôn ≥ 1.</summary>
    public static double MaxHp(IEnumerable<double> refs) => Math.Max(1, Math.Round(HpShare * HitsPerDay * refs.Sum()));

    public static int Contribution(double damage, double maxHp) =>
        (int)Math.Clamp(Math.Round(ContribScale * damage / Math.Max(1, maxHp)), ContribMin, ContribMax);

    /// <summary>Hạng 0-based theo sát thương giảm dần, hòa thì theo Id; không gây sát thương thì int.MaxValue.</summary>
    public static int Rank(IEnumerable<(Guid Id, double Damage)> rows, Guid me)
    {
        var list = rows.Where(r => r.Damage > 0).OrderByDescending(r => r.Damage).ThenBy(r => r.Id).ToList();
        var i = list.FindIndex(r => r.Id == me);
        return i < 0 ? int.MaxValue : i;
    }
}
```

- [ ] **Step 4: Chạy test**

Run: lệnh test với `--filter "FullyQualifiedName~SectBossTests"` (các test thuộc task này).
Expected: pass. (Test hòa trong `RankOrdersByDamageThenId`: b và c cùng 50, b có Id nhỏ hơn nên hạng 0.)

---

### Task 3: Thực thể, DbContext, nâng cấp SQLite, `SectBuff`

**Files:**
- Modify: `Data/Entities.cs` (cuối file), `Data/GameDbContext.cs`, `Game/SectBuff.cs`, `Game/ServerSect.cs:97`
- Test: `test/GemSystemTests/SectBossTests.cs`

- [ ] **Step 1: Viết test buff (thêm vào `SectBossTests`)**

```csharp
    [Fact]
    public async Task BuffDayAddsQiPctOnlyOnThatDay()
    {
        var (leader, sid) = await NewSect();
        var tomorrow = SectBossData.NextDay(Day);
        await db.Sects.Where(s => s.Id == sid).ExecuteUpdateAsync(u => u.SetProperty(s => s.BuffDay, tomorrow));
        SectBuff.Clear();
        await SectBuff.EnsureAsync(db, leader, now);
        Assert.Equal(SectData.QiPct(1), SectBuff.QiPct(leader, now));
        Assert.Equal(SectData.QiPct(1) + SectBossData.BuffQiPct, SectBuff.QiPct(leader, now.AddDays(1)));
        Assert.Equal(SectData.QiPct(1), SectBuff.QiPct(leader, now.AddDays(2)));
    }
```

- [ ] **Step 2: Chạy, thấy lỗi biên dịch** (`Sect.BuffDay`, `QiPct(id, now)` chưa có).

- [ ] **Step 3: Thêm vào `Data/Entities.cs`**

Trong class `Sect` thêm sau `CreatedAt`:
```csharp
    /// <summary>Ngày (yyyy-MM-dd giờ VN) buff tu vi do hạ Boss tông hôm trước còn hiệu lực; rỗng = không có.</summary>
    public string BuffDay { get; set; } = "";
```
Trong class `SectMember` thêm sau `ShopBought`:
```csharp
    /// <summary>Sát thương của lượt đánh Boss tông gần nhất (dùng tính máu Boss); 0 = chưa từng đánh.</summary>
    public double BossRef { get; set; }
```
Cuối file thêm (dùng `double` vì số trận có thể vượt `long`, và `ExecuteUpdate` cộng dồn được trên cả SQLite lẫn Postgres):
```csharp
/// <summary>Boss tông của một tông trong một ngày (giờ VN). MaxHp chốt lần đầu mở trang Boss trong ngày.</summary>
public class SectBoss
{
    public long SectId { get; set; }
    public string Day { get; set; } = "";
    public string BossId { get; set; } = "";
    public double MaxHp { get; set; }
    public double Damage { get; set; }
    public DateTime? DefeatedAt { get; set; }
}

/// <summary>Số lượt đã đánh, sát thương và trạng thái nhận rương của một người với Boss tông một ngày.</summary>
public class SectBossHit
{
    public long SectId { get; set; }
    public string Day { get; set; } = "";
    public Guid PlayerId { get; set; }
    public int Hits { get; set; }
    public double Damage { get; set; }
    public bool ChestClaimed { get; set; }
}
```

- [ ] **Step 4: `Data/GameDbContext.cs`**

Thêm DbSet cạnh `SectApplications` (dòng 25):
```csharp
    public DbSet<SectBoss> SectBosses => Set<SectBoss>();
    public DbSet<SectBossHit> SectBossHits => Set<SectBossHit>();
```
Trong `b.Entity<Sect>` thêm: `e.Property(s => s.BuffDay).HasMaxLength(10);`. Sau khối `b.Entity<SectApplication>` thêm:
```csharp
        b.Entity<SectBoss>(e =>
        {
            e.HasKey(x => new { x.SectId, x.Day });
            e.Property(x => x.Day).HasMaxLength(10);
            e.Property(x => x.BossId).HasMaxLength(40);
            e.HasOne<Sect>().WithMany().HasForeignKey(x => x.SectId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SectBossHit>(e =>
        {
            e.HasKey(x => new { x.SectId, x.Day, x.PlayerId });
            e.Property(x => x.Day).HasMaxLength(10);
            e.HasOne<Sect>().WithMany().HasForeignKey(x => x.SectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Player>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.PlayerId);
        });
```
Trong `UpgradeSect` (file SQLite cũ), thêm vào cuối hàm, sau lệnh tạo index `IX_ChatMessages_SectId_Id`:
```csharp
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS "SectBosses" (
                "SectId" INTEGER NOT NULL, "Day" TEXT NOT NULL, "BossId" TEXT NOT NULL,
                "MaxHp" REAL NOT NULL, "Damage" REAL NOT NULL, "DefeatedAt" TEXT NULL,
                CONSTRAINT "PK_SectBosses" PRIMARY KEY ("SectId", "Day"),
                CONSTRAINT "FK_SectBosses_Sects_SectId" FOREIGN KEY ("SectId") REFERENCES "Sects" ("Id") ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS "SectBossHits" (
                "SectId" INTEGER NOT NULL, "Day" TEXT NOT NULL, "PlayerId" TEXT NOT NULL,
                "Hits" INTEGER NOT NULL, "Damage" REAL NOT NULL, "ChestClaimed" INTEGER NOT NULL,
                CONSTRAINT "PK_SectBossHits" PRIMARY KEY ("SectId", "Day", "PlayerId"),
                CONSTRAINT "FK_SectBossHits_Sects_SectId" FOREIGN KEY ("SectId") REFERENCES "Sects" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_SectBossHits_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE CASCADE);
            CREATE INDEX IF NOT EXISTS "IX_SectBossHits_PlayerId" ON "SectBossHits" ("PlayerId");
            """;
        cmd.ExecuteNonQuery();
        void AddColumn(string table, string column, string ddl)
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'";
            if (Convert.ToInt64(cmd.ExecuteScalar()) != 0) return;
            cmd.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {ddl}";
            cmd.ExecuteNonQuery();
        }
        AddColumn("Sects", "BuffDay", "TEXT NOT NULL DEFAULT ''");
        AddColumn("SectMembers", "BossRef", "REAL NOT NULL DEFAULT 0");
```

- [ ] **Step 5: Viết lại `Game/SectBuff.cs`**

```csharp
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>
/// Cấp tông của từng nhân vật cho buff tu luyện trong GameService.Rate (hàm tĩnh, không đọc DB).
/// Cộng thêm SectBossData.BuffQiPct khi tông vừa hạ Boss hôm trước (Sect.BuffDay = hôm nay).
/// LoadAsync gọi EnsureAsync: đọc lại DB tối đa mỗi 60 giây một lần cho mỗi nhân vật. Hành động tông làm mới ngay.
/// </summary>
public static class SectBuff
{
    private static readonly ConcurrentDictionary<Guid, (int Level, string BuffDay, DateTime At)> cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public static int LevelOf(Guid id) => cache.TryGetValue(id, out var c) ? c.Level : 0;
    public static double QiPct(Guid id) => QiPct(id, DateTime.UtcNow);
    public static double QiPct(Guid id, DateTime now)
    {
        if (!cache.TryGetValue(id, out var c) || c.Level <= 0) return 0;
        return SectData.QiPct(c.Level) + (c.BuffDay == SectData.DayOf(now) ? SectBossData.BuffQiPct : 0);
    }

    public static async Task EnsureAsync(GameDbContext db, Guid id, DateTime now)
    {
        if (cache.TryGetValue(id, out var c) && now >= c.At && now - c.At < Ttl) return;
        int level = 0; string buffDay = "";
        try
        {
            var row = await db.SectMembers.AsNoTracking().Where(m => m.PlayerId == id && m.SectId != null)
                .Join(db.Sects, m => m.SectId, s => (long?)s.Id, (m, s) => new { s.Level, s.BuffDay }).FirstOrDefaultAsync();
            if (row is not null) { level = row.Level; buffDay = row.BuffDay; }
        }
        catch (Exception) when (db.Database.IsSqlite()) { level = 0; }   // file SQLite chưa nâng cấp bảng: không buff
        cache[id] = (level, buffDay, now);
    }

    public static void Set(Guid id, int level, DateTime now, string buffDay = "") => cache[id] = (level, buffDay, now);
    public static void Forget(Guid id) => cache.TryRemove(id, out _);
    /// <summary>Tông lên cấp / giải tán / hạ Boss: mọi thành viên đọc lại ở lần nạp kế.</summary>
    public static void Clear() => cache.Clear();
}
```
Trong `Game/ServerSect.cs:97` đổi:
```csharp
        SectBuff.Set(me, sect?.Level ?? 0, now, sect?.BuffDay ?? "");
```

- [ ] **Step 6: Chạy test**

Run: `--filter "FullyQualifiedName~SectBossTests|FullyQualifiedName~SectTests"`
Expected: pass (kể cả `SectTests` cũ, kiểm tra `SectBuff.QiPct(leader)` vẫn bằng `SectData.QiPct(2)`).

---

### Task 4: Migration Postgres (online)

**Files:**
- Create: `Online/Migrations/20261111110000_OnlineSectBoss.cs`
- Modify: `Online/Migrations/GameDbContextModelSnapshot.cs`, `supabase/migrations/20261026_online_authority.sql` (nối cuối, trước dòng `COMMIT;` của khối mới nhất là **không được**: thêm khối `START TRANSACTION; … COMMIT;` mới ở cuối file)

- [ ] **Step 1: Tạo migration**

```csharp
using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VanDao.Data;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <summary>Boss Tông Môn hằng ngày: bảng SectBosses, SectBossHits; Sects.BuffDay; SectMembers.BossRef.</summary>
    [DbContext(typeof(GameDbContext))]
    [Migration("20261111110000_OnlineSectBoss")]
    public partial class OnlineSectBoss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "BuffDay", schema: "game", table: "Sects", type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<double>(name: "BossRef", schema: "game", table: "SectMembers", type: "double precision", nullable: false, defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "SectBosses",
                schema: "game",
                columns: table => new
                {
                    SectId = table.Column<long>(type: "bigint", nullable: false),
                    Day = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BossId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MaxHp = table.Column<double>(type: "double precision", nullable: false),
                    Damage = table.Column<double>(type: "double precision", nullable: false),
                    DefeatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectBosses", x => new { x.SectId, x.Day });
                    table.ForeignKey(name: "FK_SectBosses_Sects_SectId", column: x => x.SectId,
                        principalSchema: "game", principalTable: "Sects", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SectBossHits",
                schema: "game",
                columns: table => new
                {
                    SectId = table.Column<long>(type: "bigint", nullable: false),
                    Day = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hits = table.Column<int>(type: "integer", nullable: false),
                    Damage = table.Column<double>(type: "double precision", nullable: false),
                    ChestClaimed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectBossHits", x => new { x.SectId, x.Day, x.PlayerId });
                    table.ForeignKey(name: "FK_SectBossHits_Players_PlayerId", column: x => x.PlayerId,
                        principalSchema: "game", principalTable: "Players", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(name: "FK_SectBossHits_Sects_SectId", column: x => x.SectId,
                        principalSchema: "game", principalTable: "Sects", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_SectBossHits_PlayerId", schema: "game", table: "SectBossHits", column: "PlayerId");

            // bảng mới cần cùng quyền game_backend + RLS như các bảng khác
            migrationBuilder.Sql(OnlineSchema.Grants);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SectBossHits", schema: "game");
            migrationBuilder.DropTable(name: "SectBosses", schema: "game");
            migrationBuilder.DropColumn(name: "BossRef", schema: "game", table: "SectMembers");
            migrationBuilder.DropColumn(name: "BuffDay", schema: "game", table: "Sects");
        }
    }
}
```

- [ ] **Step 2: Cập nhật snapshot `GameDbContextModelSnapshot.cs`**

Làm theo đúng mẫu các khối `Sect` / `SectApplication` đã có (dòng 156–230 và 961–988):
1. Trong khối `modelBuilder.Entity("VanDao.Data.Sect", …)` thêm, giữa `AutoAccept` và `CreatedAt` (theo thứ tự chữ cái):
```csharp
                    b.Property<string>("BuffDay")
                        .IsRequired()
                        .HasMaxLength(10)
                        .HasColumnType("character varying(10)");
```
2. Trong khối `VanDao.Data.SectMember` thêm property (đúng thứ tự chữ cái, trước `CheckinDay`):
```csharp
                    b.Property<double>("BossRef")
                        .HasColumnType("double precision");
```
3. Ngay sau khối `SectApplication` (kết thúc dòng 230) thêm hai khối thực thể mới:
```csharp
            modelBuilder.Entity("VanDao.Data.SectBoss", b =>
                {
                    b.Property<long>("SectId")
                        .HasColumnType("bigint");

                    b.Property<string>("Day")
                        .HasMaxLength(10)
                        .HasColumnType("character varying(10)");

                    b.Property<string>("BossId")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<double>("Damage")
                        .HasColumnType("double precision");

                    b.Property<DateTime?>("DefeatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<double>("MaxHp")
                        .HasColumnType("double precision");

                    b.HasKey("SectId", "Day");

                    b.ToTable("SectBosses", "game");
                });

            modelBuilder.Entity("VanDao.Data.SectBossHit", b =>
                {
                    b.Property<long>("SectId")
                        .HasColumnType("bigint");

                    b.Property<string>("Day")
                        .HasMaxLength(10)
                        .HasColumnType("character varying(10)");

                    b.Property<Guid>("PlayerId")
                        .HasColumnType("uuid");

                    b.Property<bool>("ChestClaimed")
                        .HasColumnType("boolean");

                    b.Property<double>("Damage")
                        .HasColumnType("double precision");

                    b.Property<int>("Hits")
                        .HasColumnType("integer");

                    b.HasKey("SectId", "Day", "PlayerId");

                    b.HasIndex("PlayerId");

                    b.ToTable("SectBossHits", "game");
                });
```
4. Trong phần quan hệ (sau khối `modelBuilder.Entity("VanDao.Data.SectApplication", …)` ở khoảng dòng 961–974) thêm:
```csharp
            modelBuilder.Entity("VanDao.Data.SectBoss", b =>
                {
                    b.HasOne("VanDao.Data.Sect", null)
                        .WithMany()
                        .HasForeignKey("SectId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("VanDao.Data.SectBossHit", b =>
                {
                    b.HasOne("VanDao.Data.Player", null)
                        .WithMany()
                        .HasForeignKey("PlayerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("VanDao.Data.Sect", null)
                        .WithMany()
                        .HasForeignKey("SectId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
```

- [ ] **Step 3: Nối SQL vào `supabase/migrations/20261026_online_authority.sql`** (thêm vào cuối file, sau `COMMIT;` của khối `OnlineSect`)

```sql

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261111110000_OnlineSectBoss') THEN
    ALTER TABLE game."Sects" ADD "BuffDay" character varying(10) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261111110000_OnlineSectBoss') THEN
    ALTER TABLE game."SectMembers" ADD "BossRef" double precision NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261111110000_OnlineSectBoss') THEN
    CREATE TABLE game."SectBosses" (
        "SectId" bigint NOT NULL,
        "Day" character varying(10) NOT NULL,
        "BossId" character varying(40) NOT NULL,
        "MaxHp" double precision NOT NULL,
        "Damage" double precision NOT NULL,
        "DefeatedAt" timestamp with time zone,
        CONSTRAINT "PK_SectBosses" PRIMARY KEY ("SectId", "Day"),
        CONSTRAINT "FK_SectBosses_Sects_SectId" FOREIGN KEY ("SectId") REFERENCES game."Sects" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261111110000_OnlineSectBoss') THEN
    CREATE TABLE game."SectBossHits" (
        "SectId" bigint NOT NULL,
        "Day" character varying(10) NOT NULL,
        "PlayerId" uuid NOT NULL,
        "Hits" integer NOT NULL,
        "Damage" double precision NOT NULL,
        "ChestClaimed" boolean NOT NULL,
        CONSTRAINT "PK_SectBossHits" PRIMARY KEY ("SectId", "Day", "PlayerId"),
        CONSTRAINT "FK_SectBossHits_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES game."Players" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_SectBossHits_Sects_SectId" FOREIGN KEY ("SectId") REFERENCES game."Sects" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261111110000_OnlineSectBoss') THEN
    CREATE INDEX "IX_SectBossHits_PlayerId" ON game."SectBossHits" ("PlayerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261111110000_OnlineSectBoss') THEN
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA game TO game_backend;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA game TO game_backend;
    DO $$ DECLARE t record; BEGIN FOR t IN SELECT tablename FROM pg_tables WHERE schemaname='game' LOOP
      EXECUTE format('ALTER TABLE game.%I ENABLE ROW LEVEL SECURITY',t.tablename);
      EXECUTE format('DROP POLICY IF EXISTS backend_only ON game.%I',t.tablename);
      EXECUTE format('CREATE POLICY backend_only ON game.%I TO game_backend USING (true) WITH CHECK (true)',t.tablename);
    END LOOP; END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261111110000_OnlineSectBoss') THEN
    INSERT INTO game."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111110000_OnlineSectBoss', '8.0.11');
    END IF;
END $EF$;
COMMIT;
```

- [ ] **Step 4: Kiểm tra biên dịch toàn dự án**

Run: `dotnet build --nologo -v q -clp:ErrorsOnly`
Expected: không lỗi. (Nếu repo có test kiểm tra snapshot khớp model, chạy `dotnet test test/GemSystemTests --filter "FullyQualifiedName~Migration|FullyQualifiedName~Schema"`.)

---

### Task 5: `ServerSectBoss` (chốt máu, lượt, ghi sát thương, hạ Boss, nhận rương) + DTO

**Files:**
- Create: `Game/ServerSectBoss.cs`
- Modify: `Game/ServerSect.cs` (hai chỗ nhỏ), `Game/Dtos.cs` (sau dòng 287)
- Test: `test/GemSystemTests/SectBossTests.cs`

- [ ] **Step 1: Viết test (thêm vào `SectBossTests`; thêm lại `ServerSectBoss Boss => new(db);` và `SetRef` nếu đã tạm xóa ở Task 1)**

```csharp
    [Fact]
    public async Task MaxHpScalesWithActiveMembersAndFillsMissingRefs()
    {
        var m1 = await NewPlayer("Một"); var m2 = await NewPlayer("Hai"); var m3 = await NewPlayer("Ba");
        var (leader, _) = await NewSect("Thanh Vân", m1, m2, m3);
        await SetRef(leader, 100); await SetRef(m1, 300);   // m2, m3 chưa có: lấy trung bình của người có (200)
        var dto = await Boss.StateAsync(leader, now, () => 999);
        Assert.Equal(960, dto.MaxHp);                       // 0,6 × 2 × (100 + 300 + 200 + 200)
    }

    [Fact]
    public async Task SoloSectUsesSelfReference()
    {
        var (leader, _) = await NewSect();
        Assert.Equal(120, (await Boss.StateAsync(leader, now, () => 100)).MaxHp);   // 0,6 × 2 × 100
    }

    [Fact]
    public async Task InactiveMembersAreNotCounted()
    {
        var m1 = await NewPlayer("Vắng");
        var (leader, _) = await NewSect("Thanh Vân", m1);
        await SetRef(leader, 100); await SetRef(m1, 100);
        await db.Players.IgnoreQueryFilters().Where(p => p.Id == m1).ExecuteUpdateAsync(u => u.SetProperty(p => p.LastTickAt, now.AddDays(-5)));
        Assert.Equal(120, (await Boss.StateAsync(leader, now, () => 999)).MaxHp);
    }

    [Fact]
    public async Task MaxHpIsLockedForTheDay()
    {
        var (leader, _) = await NewSect();
        var first = (await Boss.StateAsync(leader, now, () => 100)).MaxHp;
        await SetRef(leader, 10_000);
        Assert.Equal(first, (await Boss.StateAsync(leader, now, () => 100)).MaxHp);
    }

    [Fact]
    public async Task TwoHitsPerDayThenRefusedAndNewDayResets()
    {
        var (leader, _) = await NewSect();
        Assert.Equal(1, (await Boss.ReserveHitAsync(leader, now, () => 100)).HitNo);
        Assert.Equal(2, (await Boss.ReserveHitAsync(leader, now, () => 100)).HitNo);
        var ex = await Assert.ThrowsAsync<GameException>(() => Boss.ReserveHitAsync(leader, now, () => 100));
        Assert.Contains("hết lượt", ex.Message);
        Assert.Equal(1, (await Boss.ReserveHitAsync(leader, now.AddDays(1), () => 100)).HitNo);
    }

    [Fact]
    public async Task OutsidersCannotHit()
    {
        var loner = await NewPlayer("Lẻ Loi");
        await Assert.ThrowsAsync<GameException>(() => Boss.ReserveHitAsync(loner, now, () => 100));
    }

    [Fact]
    public async Task RecordHitAddsDamageContributionRefAndFund()
    {
        var (leader, sid) = await NewSect();
        var r = await Boss.ReserveHitAsync(leader, now, () => 100);          // máu 120
        var (pts, defeated) = await Boss.RecordHitAsync(leader, r.Boss, r.Sect, 50, now);
        Assert.Equal(SectBossData.Contribution(50, 120), pts);
        Assert.False(defeated);
        var row = await Row(leader);
        Assert.Equal(pts, row.Contribution); Assert.Equal(pts, row.TotalContribution); Assert.Equal(50, row.BossRef);
        Assert.Equal(pts, (await db.Sects.AsNoTracking().SingleAsync(s => s.Id == sid)).Fund);
        Assert.Equal(50, (await db.SectBosses.AsNoTracking().SingleAsync()).Damage);
    }

    [Fact]
    public async Task DefeatSetsBuffOnceAnnouncesAndBlocksFurtherHits()
    {
        var m = await NewPlayer("Đệ Tử");
        var (leader, sid) = await NewSect("Thanh Vân", m);                   // 2 người hoạt động, máu 240
        var r1 = await Boss.ReserveHitAsync(leader, now, () => 100);
        Assert.False((await Boss.RecordHitAsync(leader, r1.Boss, r1.Sect, 130, now)).Defeated);
        var r2 = await Boss.ReserveHitAsync(leader, now, () => 100);
        Assert.True((await Boss.RecordHitAsync(leader, r2.Boss, r2.Sect, 130, now)).Defeated);   // 260 ≥ 240
        Assert.Equal(SectBossData.NextDay(Day), (await db.Sects.AsNoTracking().SingleAsync(s => s.Id == sid)).BuffDay);
        Assert.Contains(await db.ChatMessages.Where(c => c.SectId == sid).Select(c => c.Text).ToListAsync(), t => t.Contains("bị hạ"));
        var ex = await Assert.ThrowsAsync<GameException>(() => Boss.ReserveHitAsync(m, now, () => 100));
        Assert.Contains("đã bị hạ", ex.Message);
        Assert.False((await Boss.RecordHitAsync(leader, r2.Boss, r2.Sect, 10, now)).Defeated);   // chỉ kích hoạt một lần
    }

    [Fact]
    public async Task ChestOncePerPlayerWithTopBonus()
    {
        var m = await NewPlayer("Đệ Tử"); var idle = await NewPlayer("Lười");
        var (leader, _) = await NewSect("Thanh Vân", m, idle);               // 3 người hoạt động, máu 360
        await Assert.ThrowsAsync<GameException>(() => Boss.ClaimAsync(leader, now));    // chưa hạ Boss
        var rm = await Boss.ReserveHitAsync(m, now, () => 100);
        await Boss.RecordHitAsync(m, rm.Boss, rm.Sect, 100, now);
        for (var i = 0; i < 2; i++)
        {
            var r = await Boss.ReserveHitAsync(leader, now, () => 100);
            await Boss.RecordHitAsync(leader, r.Boss, r.Sect, 130, now);
        }                                                                    // 360 ≥ 360: hạ Boss
        Assert.Equal(SectBossData.TopBonus[0], await Boss.ClaimAsync(leader, now));
        Assert.Equal(SectBossData.TopBonus[1], await Boss.ClaimAsync(m, now));
        Assert.Equal(2 * SectBossData.Contribution(130, 360) + SectBossData.TopBonus[0], (await Row(leader)).Contribution);
        var again = await Assert.ThrowsAsync<GameException>(() => Boss.ClaimAsync(leader, now));
        Assert.Contains("Đã nhận", again.Message);
        var none = await Assert.ThrowsAsync<GameException>(() => Boss.ClaimAsync(idle, now));
        Assert.Contains("chưa đánh", none.Message);
    }

    [Fact]
    public async Task PageBuffReachesQiRateOnTheNextDay()
    {
        var (leader, _) = await NewSect();
        var r1 = await Boss.ReserveHitAsync(leader, now, () => 100);         // máu 120
        await Boss.RecordHitAsync(leader, r1.Boss, r1.Sect, 130, now);       // hạ ngay
        await Sect.PageAsync(leader, now);
        Assert.Equal(SectData.QiPct(1) + SectBossData.BuffQiPct, SectBuff.QiPct(leader, now.AddDays(1)));
    }

    [Fact]
    public async Task StateShowsRankingAndFlags()
    {
        var m = await NewPlayer("Đệ Tử");
        var (leader, _) = await NewSect("Thanh Vân", m);
        var r = await Boss.ReserveHitAsync(m, now, () => 100);
        await Boss.RecordHitAsync(m, r.Boss, r.Sect, 70, now);
        var dto = await Boss.StateAsync(leader, now, () => 100);
        Assert.Equal(SectBossData.HitsPerDay, dto.HitsLeft);
        Assert.Single(dto.Ranking);
        Assert.Equal(70, dto.Ranking[0].Damage);
        Assert.False(dto.Ranking[0].Me);
        Assert.False(dto.Defeated); Assert.False(dto.CanClaim);
    }
```

- [ ] **Step 2: Chạy, thấy lỗi biên dịch** (`ServerSectBoss`, `SectBossDto` chưa có).

- [ ] **Step 3: Sửa `Game/ServerSect.cs`**

Đổi `private async Task SystemAsync(...)` thành `internal async Task SystemAsync(...)`, và thêm ngay sau `AddPointsAsync` (dòng 84):
```csharp
    /// <summary>Cộng điểm cống hiến cho người chơi và cùng số điểm vào quỹ tông (dùng cho Boss tông).</summary>
    public async Task AwardAsync(Guid me, long sectId, int points)
    {
        await db.SectMembers.Where(x => x.PlayerId == me && x.SectId == sectId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Contribution, x => x.Contribution + points)
                .SetProperty(x => x.TotalContribution, x => x.TotalContribution + points));
        await AddPointsAsync(me, sectId, points);
    }
```

- [ ] **Step 4: Thêm DTO vào `Game/Dtos.cs` (sau dòng `SectActionResponse`)**

```csharp
public record SectBossRankDto(Guid PlayerId, string Name, double Damage, int Hits, bool Me);
public record SectBossDto(string Day, string BossId, string Name, string Image, string Desc, double MaxHp, double Damage, bool Defeated,
    int HitsPerDay, int HitsLeft, int MyHits, double MyDamage, bool CanClaim, bool Claimed, bool BuffToday, bool BuffTomorrow,
    int[] TopBonus, List<SectBossRankDto> Ranking);
```

- [ ] **Step 5: Tạo `Game/ServerSectBoss.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>
/// Boss Tông Môn hằng ngày: đọc/ghi SectBosses, SectBossHits. Như ServerSect, mọi số dùng chung chỉ đổi bằng UPDATE có điều kiện
/// (ExecuteUpdate): hai người đánh cùng lúc không ghi đè nhau, và chỉ một người kích hoạt được "hạ Boss".
/// Không chạy trận và không đụng vật phẩm: phần đó ở GameService.SectBoss.cs.
/// </summary>
public sealed class ServerSectBoss(GameDbContext db)
{
    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    private async Task<(SectMember M, Sect S)> MineAsync(Guid me)
    {
        var m = await db.SectMembers.AsNoTracking().FirstOrDefaultAsync(x => x.PlayerId == me);
        var s = m?.SectId is { } sid ? await db.Sects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sid) : null;
        return s is null ? throw new GameException("Chưa gia nhập Tông Môn.") : (m!, s);
    }

    /// <summary>Boss hôm nay của tông; chưa có thì chốt máu: 60% × 2 lượt × tổng sát thương tham chiếu của thành viên hoạt động.
    /// Người chưa từng đánh lấy trung bình của những người đã có; chưa ai có thì lấy selfRef (mô phỏng của người đang mở).</summary>
    private async Task<SectBoss> EnsureBossAsync(Sect s, string day, DateTime now, Func<double> selfRef)
    {
        var b = await db.SectBosses.AsNoTracking().FirstOrDefaultAsync(x => x.SectId == s.Id && x.Day == day);
        if (b is not null) return b;
        var cutoff = now.AddDays(-SectBossData.ActiveDays);
        var rows = await db.SectMembers.AsNoTracking().Where(x => x.SectId == s.Id)
            .Join(db.Players.IgnoreQueryFilters(), x => x.PlayerId, p => p.Id, (x, p) => new { x.BossRef, p.LastTickAt })
            .Where(x => x.LastTickAt >= cutoff).ToListAsync();
        var known = rows.Where(r => r.BossRef > 0).Select(r => r.BossRef).ToList();
        var fill = known.Count > 0 ? known.Average() : selfRef();
        var refs = rows.Select(r => r.BossRef > 0 ? r.BossRef : fill).DefaultIfEmpty(fill).ToList();
        var nb = new SectBoss { SectId = s.Id, Day = day, BossId = SectBossData.BossOf(day).Id, MaxHp = SectBossData.MaxHp(refs) };
        db.SectBosses.Add(nb);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)   // người khác vừa chốt cùng lúc: dùng bản của họ
        {
            db.Entry(nb).State = EntityState.Detached;
            return await db.SectBosses.AsNoTracking().FirstAsync(x => x.SectId == s.Id && x.Day == day);
        }
        db.Entry(nb).State = EntityState.Detached;
        return nb;
    }

    public async Task<SectBossDto> StateAsync(Guid me, DateTime now, Func<double> selfRef)
    {
        var (_, s) = await MineAsync(me);
        var day = SectData.DayOf(now);
        var b = await EnsureBossAsync(s, day, now, selfRef);
        var hits = await db.SectBossHits.AsNoTracking().Where(x => x.SectId == s.Id && x.Day == day)
            .Join(db.Players.IgnoreQueryFilters(), x => x.PlayerId, p => p.Id, (x, p) => new { x.PlayerId, p.Name, x.Hits, x.Damage, x.ChestClaimed })
            .ToListAsync();
        var mine = hits.FirstOrDefault(x => x.PlayerId == me);
        var def = SectBossData.BossOf(day);
        var defeated = b.DefeatedAt is not null;
        var ranking = hits.Where(x => x.Hits > 0).OrderByDescending(x => x.Damage).ThenBy(x => x.PlayerId)
            .Select(x => new SectBossRankDto(x.PlayerId, x.Name, x.Damage, x.Hits, x.PlayerId == me)).ToList();
        return new SectBossDto(day, def.Id, def.Name, def.Image, def.Desc, b.MaxHp, b.Damage, defeated,
            SectBossData.HitsPerDay, Math.Max(0, SectBossData.HitsPerDay - (mine?.Hits ?? 0)), mine?.Hits ?? 0, mine?.Damage ?? 0,
            defeated && (mine?.Hits ?? 0) > 0 && !(mine?.ChestClaimed ?? false), mine?.ChestClaimed ?? false,
            s.BuffDay == day, defeated && s.BuffDay == SectBossData.NextDay(day), SectBossData.TopBonus, ranking);
    }

    private async Task EnsureHitRowAsync(long sectId, string day, Guid me)
    {
        if (await db.SectBossHits.AnyAsync(x => x.SectId == sectId && x.Day == day && x.PlayerId == me)) return;
        var row = new SectBossHit { SectId = sectId, Day = day, PlayerId = me };
        db.SectBossHits.Add(row);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { /* hàng vừa được tạo bởi yêu cầu song song */ }
        finally { db.Entry(row).State = EntityState.Detached; }
    }

    /// <summary>Giữ một lượt đánh (UPDATE có điều kiện Hits &lt; tối đa). Trả số thứ tự lượt (1..), Boss hôm nay và tông.</summary>
    public async Task<(int HitNo, SectBoss Boss, Sect Sect)> ReserveHitAsync(Guid me, DateTime now, Func<double> selfRef)
    {
        var (_, s) = await MineAsync(me);
        var day = SectData.DayOf(now);
        var boss = await EnsureBossAsync(s, day, now, selfRef);
        if (boss.DefeatedAt is not null) throw new GameException("Boss tông hôm nay đã bị hạ.");
        await EnsureHitRowAsync(s.Id, day, me);
        var max = SectBossData.HitsPerDay;
        if (await db.SectBossHits.Where(x => x.SectId == s.Id && x.Day == day && x.PlayerId == me && x.Hits < max)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Hits, x => x.Hits + 1)) == 0)
            throw new GameException("Hôm nay đã hết lượt đánh Boss tông.");
        var hitNo = await db.SectBossHits.AsNoTracking().Where(x => x.SectId == s.Id && x.Day == day && x.PlayerId == me).Select(x => x.Hits).FirstAsync();
        return (hitNo, boss, s);
    }

    /// <summary>Ghi sát thương của lượt vừa đánh: cộng vào Boss, người đánh, BossRef, cống hiến + quỹ; nếu vừa đủ máu thì hạ Boss
    /// (một UPDATE có điều kiện DefeatedAt IS NULL nên chỉ một lần) và đặt buff cho ngày mai. Trả (cống hiến nhận được, vừa hạ Boss).</summary>
    public async Task<(int Contribution, bool Defeated)> RecordHitAsync(Guid me, SectBoss boss, Sect sect, double damage, DateTime now)
    {
        var (sid, day) = (sect.Id, boss.Day);
        await db.SectBossHits.Where(x => x.SectId == sid && x.Day == day && x.PlayerId == me)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Damage, x => x.Damage + damage));
        await db.SectBosses.Where(x => x.SectId == sid && x.Day == day)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Damage, x => x.Damage + damage));
        await db.SectMembers.Where(x => x.PlayerId == me).ExecuteUpdateAsync(u => u.SetProperty(x => x.BossRef, damage));
        var pts = SectBossData.Contribution(damage, boss.MaxHp);
        var server = new ServerSect(db);
        await server.AwardAsync(me, sid, pts);
        var defeated = await db.SectBosses.Where(x => x.SectId == sid && x.Day == day && x.DefeatedAt == null && x.Damage >= x.MaxHp)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.DefeatedAt, (DateTime?)now)) > 0;
        if (defeated)
        {
            var next = SectBossData.NextDay(day);
            await db.Sects.Where(x => x.Id == sid).ExecuteUpdateAsync(u => u.SetProperty(x => x.BuffDay, next));
            SectBuff.Clear();
            await server.SystemAsync(sid, $"{SectBossData.BossOf(day).Name} đã bị hạ! Cả tông nhận +{SectBossData.BuffQiPct}% tu vi ngày mai, đồng môn nhớ vào Bệ Phong Ấn nhận rương.", now);
        }
        return (pts, defeated);
    }

    /// <summary>Đánh dấu đã nhận rương (UPDATE có điều kiện) và cộng thưởng top. Trả cống hiến thưởng top (0 nếu ngoài top 3).
    /// Vật phẩm trong rương do GameService phát.</summary>
    public async Task<int> ClaimAsync(Guid me, DateTime now)
    {
        var (_, s) = await MineAsync(me);
        var day = SectData.DayOf(now);
        var boss = await db.SectBosses.AsNoTracking().FirstOrDefaultAsync(x => x.SectId == s.Id && x.Day == day);
        if (boss?.DefeatedAt is null) throw new GameException("Boss tông hôm nay chưa bị hạ.");
        var rows = await db.SectBossHits.AsNoTracking().Where(x => x.SectId == s.Id && x.Day == day).ToListAsync();
        var mine = rows.FirstOrDefault(x => x.PlayerId == me);
        if (mine is null || mine.Hits < 1) throw new GameException("Hôm nay bạn chưa đánh Boss tông nên không có rương.");
        if (await db.SectBossHits.Where(x => x.SectId == s.Id && x.Day == day && x.PlayerId == me && !x.ChestClaimed)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.ChestClaimed, true)) == 0)
            throw new GameException("Đã nhận rương hôm nay rồi.");
        var rank = SectBossData.Rank(rows.Select(r => (r.PlayerId, r.Damage)), me);
        var bonus = rank < SectBossData.TopBonus.Length ? SectBossData.TopBonus[rank] : 0;
        if (bonus > 0) await new ServerSect(db).AwardAsync(me, s.Id, bonus);
        return bonus;
    }
}
```

- [ ] **Step 6: Chạy test**

Run: `--filter "FullyQualifiedName~SectBossTests"`
Expected: toàn bộ pass. Nếu `ChestOncePerPlayerWithTopBonus` lệch số cống hiến, kiểm tra `Contribution(130, 360)` = 30 (đã chặn trần 30) — công thức kỳ vọng trong test dùng chính hàm đó nên không phụ thuộc.

---

### Task 6: `GameService.SectBoss` (mô phỏng, tấn công, nhận rương) + API

**Files:**
- Create: `Game/GameService.SectBoss.cs`
- Modify: `Program.cs` (sau dòng 460 `sect/buy`, và hàm mới cạnh `SectRun`)
- Test: `test/GemSystemTests/SectBossTests.cs`

- [ ] **Step 1: Viết test (thêm vào `SectBossTests`)**

```csharp
    [Fact]
    public async Task SimulationIsDeterministicAndBounded()
    {
        var game = new GameService(db);
        var id = await NewPlayer("Thử Chiêu");
        var (_, a) = await game.ActAsync(id, p => Task.FromResult(GameService.SectBossSim(p, Day, 1)));
        var (_, b) = await game.ActAsync(id, p => Task.FromResult(GameService.SectBossSim(p, Day, 1)));
        Assert.True(a.Damage > 0);
        Assert.Equal(a, b);
        Assert.InRange(a.Rounds, 1, SectBossData.MaxRounds);
    }

    [Fact]
    public void HugeStatsDoNotOverflow()
    {
        var me = PvpFighter.FromStats("Cự", 5, new() { ["atk"] = 1e18, ["hp"] = 1e18, ["def"] = 1e17, ["speed"] = 100 });
        var r = GameService.SectBossSimulate(me, SectBossData.BossOf(Day), 7);
        Assert.True(r.Damage > 0 && double.IsFinite(r.Damage));
    }

    [Fact]
    public async Task AttackEndToEndGivesDamageContributionAndFund()
    {
        var game = new GameService(db);
        var (leader, sid) = await NewSect();
        var (_, msg) = await game.ActAsync(leader, p => game.SectBossAttackAsync(p));
        Assert.Contains("sát thương", msg);
        Assert.True((await db.Sects.AsNoTracking().SingleAsync(s => s.Id == sid)).Fund > 0);
        Assert.Equal(1, (await db.SectBossHits.AsNoTracking().SingleAsync()).Hits);
    }

    [Fact]
    public async Task ChestPaysItemsAfterDefeat()
    {
        var game = new GameService(db);
        var (leader, _) = await NewSect();
        await game.ActAsync(leader, p => game.SectBossAttackAsync(p));
        await db.SectBosses.ExecuteUpdateAsync(u => u.SetProperty(x => x.MaxHp, 1.0));   // ép Boss yếu để lượt sau chắc chắn hạ
        var (before, _) = await game.ActAsync(leader, p => Task.FromResult(""));
        var stones0 = before.Count("linh_thach"); var pills0 = before.Count("linh_thu_dan");
        var (_, msg) = await game.ActAsync(leader, p => game.SectBossAttackAsync(p));
        Assert.Contains("bị hạ", msg);
        var (after, _) = await game.ActAsync(leader, p => game.SectBossClaimAsync(p));
        Assert.True(after.Count("linh_thach") - stones0 >= SectBossData.ChestStoneMult * SectData.StoneBase(1));
        Assert.True(after.Count("linh_thu_dan") - pills0 >= SectBossData.ChestPetPills);
        await Assert.ThrowsAsync<GameException>(() => game.ActAsync(leader, p => game.SectBossClaimAsync(p)));
    }
```

- [ ] **Step 2: Chạy, thấy lỗi biên dịch** (`SectBossSim`, `SectBossAttackAsync`… chưa có).

- [ ] **Step 3: Tạo `Game/GameService.SectBoss.cs`**

```csharp
using VanDao.Data;

namespace VanDao.Game;

/// <summary>Boss Tông Môn hằng ngày: chạy trận mô phỏng và phát thưởng. Luật và DB nằm ở SectBossData / ServerSectBoss.</summary>
public partial class GameService
{
    public ServerSectBoss SectBoss => new(db);

    /// <summary>Boss bản sao của chính người đánh: cùng chỉ số, công/thủ nhân hệ số, máu vô hạn, không linh thú / thần thông.</summary>
    public static PvpFighter SectBossFighter(PvpFighter me, SectBossData.Def def) =>
        me with { Name = def.Name, Atk = CombatNumber.Scale(me.Atk, SectBossData.AtkMult), Def = CombatNumber.Scale(me.Def, SectBossData.DefMult),
            Hp = CombatNumber.Max, Pet = null, Divine = 0 };

    /// <summary>Một lượt đánh: sát thương trực tiếp người đánh gây ra trong tối đa MaxRounds hiệp, số hiệp đã diễn ra, có bị đánh gục không.</summary>
    public static (double Damage, int Rounds, bool Died) SectBossSimulate(PvpFighter me, SectBossData.Def def, int seed)
    {
        var r = PvpCombat.Run(me, SectBossFighter(me, def), seed, SectBossData.MaxRounds);
        var dmg = r.Actions.Where(a => a.ByA).Aggregate(0m, (t, a) => CombatNumber.Add(t, a.Direct));
        return ((double)dmg, (r.Actions.Count + 1) / 2, r.Actions.Count > 0 && r.Actions[^1].HpA <= 0);
    }

    /// <summary>Mô phỏng theo (người, ngày, số lượt): cùng đầu vào cho cùng kết quả, nên không thể "reroll" bằng cách tải lại.</summary>
    public static (double Damage, int Rounds, bool Died) SectBossSim(Player p, string day, int salt)
    {
        var seed = unchecked(BitConverter.ToInt32(p.Id.ToByteArray(), 0) * 31 + int.Parse(day.Replace("-", "")) * 7 + salt);
        return SectBossSimulate(PvpFighterOf(p), SectBossData.BossOf(day), seed);
    }

    /// <summary>Trang Boss tông. Chốt máu lần đầu trong ngày cần mô phỏng của người mở, nên nạp nhân vật qua ActAsync.</summary>
    public async Task<SectBossDto> SectBossPageAsync(Guid id)
    {
        var now = DateTime.UtcNow; var day = SectData.DayOf(now);
        var (_, dto) = await ActAsync(id, p => SectBoss.StateAsync(p.Id, now, () => SectBossSim(p, day, 0).Damage));
        return dto;
    }

    public async Task<string> SectBossAttackAsync(Player p)
    {
        var now = DateTime.UtcNow; var day = SectData.DayOf(now);
        var (hitNo, boss, sect) = await SectBoss.ReserveHitAsync(p.Id, now, () => SectBossSim(p, day, 0).Damage);
        var sim = SectBossSim(p, day, hitNo);
        var (pts, defeated) = await SectBoss.RecordHitAsync(p.Id, boss, sect, sim.Damage, now);
        var name = SectBossData.BossOf(day).Name;
        var msg = $"Đánh {name}: gây {sim.Damage:N0} sát thương trong {sim.Rounds} hiệp{(sim.Died ? " (bị đánh gục)" : "")}, nhận {pts} cống hiến.";
        if (defeated) msg += $" Boss đã bị hạ! Nhận rương và cả tông được +{SectBossData.BuffQiPct}% tu vi ngày mai.";
        AddLog(p, msg, defeated ? "gold" : "");
        return msg;
    }

    public async Task<string> SectBossClaimAsync(Player p)
    {
        var bonus = await SectBoss.ClaimAsync(p.Id, DateTime.UtcNow);
        var stones = SectBossData.ChestStoneMult * SectData.StoneBase(p.Realm);
        p.Add("linh_thach", stones);
        p.Add(SectBossData.ChestPill, 1);
        p.Add("linh_thu_dan", SectBossData.ChestPetPills);
        var msg = $"Mở rương Boss tông: {stones:N0} linh thạch, 1 Bổ Nguyên Đan Trung phẩm, {SectBossData.ChestPetPills} Linh Thú Đan{(bonus > 0 ? $", thưởng top +{bonus} cống hiến" : "")}.";
        AddLog(p, msg, "gold");
        return msg;
    }
}
```

- [ ] **Step 4: Thêm API vào `Program.cs`**

Sau dòng `api.MapPost("/players/{id:guid}/sect/buy/{itemId}", …)` (khoảng dòng 459–460) thêm:
```csharp
        // Boss tông hằng ngày: GET trang Boss; POST đánh một lượt / nhận rương
        api.MapGet("/players/{id:guid}/sect/boss", (Guid id, GameService game) => SectBossGet(id, game));
        api.MapPost("/players/{id:guid}/sect/boss/attack", (Guid id, GameService game) =>
            SectRun(id, game, p => game.SectBossAttackAsync(p)));
        api.MapPost("/players/{id:guid}/sect/boss/chest", (Guid id, GameService game) =>
            SectRun(id, game, p => game.SectBossClaimAsync(p)));
```
Và cạnh `SectRun` (sau dòng 545) thêm:
```csharp
    // Trang Boss tông: có thể chốt máu Boss (ghi DB) nên đi qua MutationGate như các hành động tông
    private static async Task<IResult> SectBossGet(Guid id, GameService game)
    {
        await MutationGate.WaitAsync();
        try { return Results.Ok(await game.SectBossPageAsync(id)); }
        catch (KeyNotFoundException) { return Results.NotFound(new { error = "Không tìm thấy tu sĩ." }); }
        catch (GameException ex) { return Results.BadRequest(new { error = ex.Message }); }
        finally { MutationGate.Release(); }
    }
```

- [ ] **Step 5: Chạy test**

Run: `--filter "FullyQualifiedName~SectBossTests"` rồi `dotnet build --nologo -v q -clp:ErrorsOnly`.
Expected: pass, build sạch. Nếu `HugeStatsDoNotOverflow` ném `OverflowException`, xem lại `CombatNumber` trong chỗ ném và hạ `Hp` Boss xuống `1e21m` (vẫn đủ lớn); ghi lý do vào comment của `SectBossFighter`.

---

### Task 7: Hàm thuần của giao diện + test JS

**Files:**
- Modify: `wwwroot/js/sect.js` (khối `sect-pure`, dòng 4–29), `test/sect.test.cjs:8`
- Test: `test/sect.test.cjs`

- [ ] **Step 1: Viết test (thêm cuối `test/sect.test.cjs`) và mở rộng `F`**

Đổi dòng 8 thành:
```js
vm.runInContext(pure + ';globalThis.F={sectRoleName,sectIsOfficer,sectCanKick,sectFundProgress,sectShopState,sectWaitText,sectSeen,SECT_SPOTS,sectBossHpPct,sectBossStatus,sectSpotAlert}', ctx);
```
Thêm cuối file:
```js
test('boss hp percent clamps and handles zero max', () => {
  assert.equal(F.sectBossHpPct({maxHp: 200, damage: 50}), 75);
  assert.equal(F.sectBossHpPct({maxHp: 200, damage: 999}), 0);
  assert.equal(F.sectBossHpPct({maxHp: 0, damage: 0}), 0);
});

test('boss status: defeated before spent before open', () => {
  assert.equal(F.sectBossStatus({defeated: true, hitsLeft: 2}), 'defeated');
  assert.equal(F.sectBossStatus({defeated: false, hitsLeft: 0}), 'spent');
  assert.equal(F.sectBossStatus({defeated: false, hitsLeft: 1}), 'open');
});

test('spot alerts: boss, alchemy, tower', () => {
  const d = {me: {checkedIn: false}, applications: []};
  assert.equal(F.sectSpotAlert('alchemy', d, null), true);
  assert.equal(F.sectSpotAlert('alchemy', {...d, me: {checkedIn: true}}, null), false);
  assert.equal(F.sectSpotAlert('tower', {...d, applications: [1]}, null), true);
  assert.equal(F.sectSpotAlert('boss', d, null), false);
  assert.equal(F.sectSpotAlert('boss', d, {defeated: false, hitsLeft: 2, canClaim: false}), true);
  assert.equal(F.sectSpotAlert('boss', d, {defeated: true, hitsLeft: 0, canClaim: true}), true);
  assert.equal(F.sectSpotAlert('boss', d, {defeated: true, hitsLeft: 0, canClaim: false}), false);
  assert.equal(F.sectSpotAlert('hall', d, null), false);
});

test('every hotspot opens a distinct view and sits inside the scene', () => {
  const views = F.SECT_SPOTS.map(s => s.view);
  assert.equal(new Set(views).size, views.length);
  assert.deepEqual([...views].sort(), ['boss', 'daily', 'info', 'members', 'shop']);
  for (const s of F.SECT_SPOTS) { assert.ok(s.x > 0 && s.x < 100 && s.y > 0 && s.y < 100, s.id); assert.ok(s.tip && s.name); }
});
```

- [ ] **Step 2: Chạy test, thấy lỗi** (`node --test --test-reporter=dot test/sect.test.cjs` → `sectBossHpPct is not defined`).

- [ ] **Step 3: Thêm vào khối `sect-pure` của `wwwroot/js/sect.js` (ngay trước `/* sect-pure:end */`)**

```js
/** Công trình trên ảnh sơn môn (x, y là % theo ảnh 3:2) và view mà nó mở. */
const SECT_SPOTS = [
  {id: 'hall', view: 'info', name: 'Chính Điện', x: 50, y: 20, tip: 'Chính Điện&#10;Cấp tông, nâng cấp, thông báo, nhận đệ tử, rời tông'},
  {id: 'tower', view: 'members', name: 'Tháp Đệ Tử', x: 13, y: 24, tip: 'Tháp Đệ Tử&#10;Thành viên, chức vị, duyệt đơn xin vào'},
  {id: 'vault', view: 'shop', name: 'Tàng Bảo Các', x: 27, y: 50, tip: 'Tàng Bảo Các&#10;Đổi cống hiến lấy đan dược và vé quay'},
  {id: 'alchemy', view: 'daily', name: 'Đan Phòng', x: 71, y: 46, tip: 'Đan Phòng&#10;Điểm danh và quyên linh thạch lấy cống hiến'},
  {id: 'boss', view: 'boss', name: 'Bệ Phong Ấn', x: 87, y: 17, tip: 'Bệ Phong Ấn&#10;Boss tông mỗi ngày: cả tông cùng đánh, hạ Boss nhận rương và buff tu vi'},
];
/** % máu Boss còn lại, 0–100. */
const sectBossHpPct = b => b.maxHp > 0 ? Math.max(0, Math.min(100, (1 - b.damage / b.maxHp) * 100)) : 0;
/** defeated · spent (hết lượt) · open. */
function sectBossStatus(b){ return b.defeated ? 'defeated' : b.hitsLeft <= 0 ? 'spent' : 'open'; }
/** Chấm đỏ trên công trình khi còn việc cần làm. */
function sectSpotAlert(id, d, boss){
  if (id === 'boss') return !!boss && (boss.canClaim || (!boss.defeated && boss.hitsLeft > 0));
  if (id === 'alchemy') return !d.me.checkedIn;
  if (id === 'tower') return d.applications.length > 0;
  return false;
}
```

- [ ] **Step 4: Chạy lại, phải PASS**

Run: `node --test --test-reporter=dot test/sect.test.cjs`
Expected: tất cả test (cũ và mới) pass.

---

### Task 8: Giao diện sơn môn, view Boss, Đan Phòng

**Files:**
- Create: `wwwroot/img/sect/son_mon.jpg`
- Modify: `wwwroot/js/sect.js`, `wwwroot/css/sect.css`, `wwwroot/js/i18n-en.js`

- [ ] **Step 1: Nén ảnh nền thành JPEG (PowerShell, không cần công cụ ngoài)**

```powershell
New-Item -ItemType Directory -Force D:\projects\van-dao\VanDao\wwwroot\img\sect | Out-Null
Add-Type -AssemblyName System.Drawing
$src = 'C:\Users\KienBui\.codex\generated_images\01a1c9eb-37be-7dd3-8e9f-74f59c63522f\exec-1a0e2894-c64d-4404-9542-3824acdda3d7.png'
$img = [System.Drawing.Image]::FromFile($src)
$enc = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
$ep = New-Object System.Drawing.Imaging.EncoderParameters(1)
$ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::Quality, [long]85)
$img.Save('D:\projects\van-dao\VanDao\wwwroot\img\sect\son_mon.jpg', $enc, $ep)
$img.Dispose()
(Get-Item D:\projects\van-dao\VanDao\wwwroot\img\sect\son_mon.jpg).Length
```
Expected: in ra kích thước file (mong ~300–700 KB). Nếu file gốc không còn ở đường dẫn trên, hỏi người dùng vị trí ảnh.

- [ ] **Step 2: `wwwroot/js/sect.js` — trạng thái và tải Boss**

Đổi dòng khai báo `SECT` (dòng 31):
```js
const SECT = {data: null, loading: false, at: 0, view: 'hub', notice: '', bad: false, err: '', boss: null, bossAt: 0, bossErr: '', bossLoading: false};
```
Trong `loadSect`, thay dòng `finally` bằng:
```js
  finally { SECT.loading = false; if (!SECT.data?.sect) SECT.boss = null; renderSect(); sectChatRefresh(); if (SECT.data?.sect) loadSectBoss(); }
```
Thêm ngay sau `loadSect`:
```js
async function loadSectBoss(force){
  if (!S || SECT.bossLoading) return;
  if (!force && SECT.boss && performance.now() - SECT.bossAt < 10000) return;
  SECT.bossLoading = true;
  try { SECT.boss = await api('GET', `/api/players/${S.id}/sect/boss`); SECT.bossAt = performance.now(); SECT.bossErr = ''; }
  catch(e){ SECT.bossErr = e.message || 'Không tải được Boss tông.'; }
  finally { SECT.bossLoading = false; renderSect(); }
}
```
Trong `sectAct`, ngay sau `setState(res.state); notifyHost('changed');` thêm:
```js
    if (path.startsWith('boss/')) loadSectBoss(true);
```

- [ ] **Step 3: `sectHome` mới (thay hàm cũ, dòng 95–110)**

```js
function sectHome(d){
  const s = d.sect, me = d.me;
  const prog = sectFundProgress(s.fund, s.nextCost), apps = d.applications.length;
  const views = [['hub', 'Sơn môn'], ['info', 'Chính Điện'], ['members', `Tháp Đệ Tử (${s.members}${apps ? ` · ${apps} đơn` : ''})`],
    ['shop', 'Tàng Bảo Các'], ['daily', 'Đan Phòng'], ['boss', 'Bệ Phong Ấn']];
  if (!views.some(([k]) => k === SECT.view)) SECT.view = 'hub';
  const body = {hub: sectHub, info: sectInfo, members: sectMembersView, shop: sectShop, daily: sectDaily, boss: () => sectBossView(SECT.boss)}[SECT.view](d);
  return `<header class="sect-head">
      <h2><span translate="no">${sectEsc(s.name)}</span> <span class="sect-lv">Cấp ${s.level}</span></h2>
      <p>Tu luyện <b class="up">+${s.qiPct}%</b> · ${sectEsc(sectRoleName(me.role))} · Cống hiến <b>${fmt(me.contribution)}</b></p>
      <div class="sect-fund"><span>Quỹ tông <b>${fmt(s.fund)}</b>${s.nextCost != null ? ` / ${fmt(s.nextCost)} để lên cấp ${s.level + 1}` : ' · cấp tối đa'}</span>
        ${prog != null ? `<div class="sect-bar"><i style="width:${(prog * 100).toFixed(1)}%"></i></div>` : ''}</div>
      ${s.notice ? `<p class="sect-notice" translate="no">${sectEsc(s.notice)}</p>` : ''}
    </header>
    <nav class="sect-tabs" role="tablist">${views.map(([k, n]) => `<button type="button" role="tab" aria-selected="${k === SECT.view}" data-sect-view="${k}">${n}</button>`).join('')}</nav>
    ${body}`;
}

/** Ảnh sơn môn: mỗi công trình là một nút mở view tương ứng. */
function sectHub(d){
  const spots = SECT_SPOTS.map(sp => `<button type="button" class="sect-spot sp-${sp.id}${sectSpotAlert(sp.id, d, SECT.boss) ? ' alert' : ''}"
      style="left:${sp.x}%;top:${sp.y}%" data-sect-view="${sp.view}" data-tip="${sp.tip}" aria-label="${sp.name}"><b>${sp.name}</b></button>`).join('');
  return `<div class="sect-scene" role="group" aria-label="Sơn môn">${spots}</div>`;
}

/** Tháp Đệ Tử: đơn xin vào (chỉ Tông chủ / Trưởng lão, khi có đơn) rồi danh sách thành viên. */
function sectMembersView(d){
  const apps = sectIsOfficer(d.me.role) && d.applications.length
    ? `<h3 class="sect-h">Đơn xin vào (${d.applications.length})</h3>${sectApps(d)}<h3 class="sect-h">Thành viên</h3>` : '';
  return apps + sectMembers(d);
}
```

- [ ] **Step 4: Tách Đan Phòng khỏi `sectInfo`**

Trong `sectInfo` (dòng 112–134) **xóa** khai báo `donate` và khối `<section class="sect-card"><h3>Cống hiến hằng ngày</h3>…</section>` (dòng 115–116 và 119–123), giữ nguyên phần còn lại. Thêm hàm mới:
```js
/** Đan Phòng: điểm danh + quyên linh thạch. */
function sectDaily(d){
  const me = d.me, stones = S?.inventory?.linh_thach || 0;
  const donate = me.donateCost.map((c, i) => `<button type="button" data-sect="donate/${i}" ${!me.donatesLeft || stones < c ? 'disabled' : ''}>
      <b>${['Nhỏ', 'Vừa', 'Lớn'][i]}</b><small>${fmt(c)} linh thạch</small><small class="up">+${me.donatePoints[i]} cống hiến</small></button>`).join('');
  return `<section class="sect-card"><h3>Cống hiến hằng ngày</h3>
      <div class="sect-row"><button type="button" data-sect="checkin" ${me.checkedIn ? 'disabled' : ''}>${me.checkedIn ? 'Đã điểm danh' : 'Điểm danh +10'}</button>
        <span class="sect-sub">Quyên góp còn <b>${me.donatesLeft}</b> lần hôm nay</span></div>
      <div class="sect-donate">${donate}</div>
    </section>`;
}
```
Trong `sectInfo` dòng `const stones = …` không còn dùng thì xóa.

- [ ] **Step 5: View Boss (thêm sau `sectShop`)**

```js
/** Bệ Phong Ấn: Boss tông hôm nay. */
function sectBossView(b){
  if (!b) return `<section class="sect-card"><p class="sect-empty">${SECT.bossErr ? sectEsc(SECT.bossErr) : 'Đang tải Boss tông…'}</p></section>`;
  const st = sectBossStatus(b), pct = sectBossHpPct(b);
  const stText = st === 'defeated' ? 'Đã hạ' : st === 'spent' ? 'Hết lượt' : 'Đang chờ đồng môn';
  const slots = Array.from({length: b.hitsPerDay}, (_, i) => `<i class="sect-slot${i < b.myHits ? ' used' : ''}"></i>`).join('');
  const attack = b.defeated ? '' : `<button type="button" data-sect="boss/attack" ${b.hitsLeft > 0 ? '' : 'disabled'}>Tấn công · còn ${b.hitsLeft}/${b.hitsPerDay} lượt</button>`;
  const chest = !b.defeated ? '' : `<button type="button" data-sect="boss/chest" ${b.canClaim ? '' : 'disabled'}>${b.claimed ? 'Đã nhận rương' : b.myHits ? 'Nhận rương' : 'Chưa đánh lượt nào'}</button>`;
  const buff = b.buffToday ? `<p class="sect-sub up">Hôm nay cả tông được +${SECT_BUFF_PCT}% tu vi nhờ hạ Boss hôm qua.</p>`
    : b.buffTomorrow ? `<p class="sect-sub up">Ngày mai cả tông được +${SECT_BUFF_PCT}% tu vi.</p>` : '';
  const rank = b.ranking.map((r, i) => `<li class="sect-li${r.me ? ' me' : ''}${i < b.topBonus.length ? ' top' : ''}">
      <div><b>${i + 1}. <span translate="no">${sectEsc(r.name)}</span></b><small>${r.hits} lượt${i < b.topBonus.length ? ` · thưởng top +${b.topBonus[i]} cống hiến` : ''}</small></div>
      <b>${fmt(Math.round(r.damage))}</b></li>`).join('');
  return `<section class="sect-card sect-boss ${st}">
      <img class="sect-boss-img" src="${sectEsc(b.image)}" alt="">
      <div class="sect-boss-info"><h3 translate="no">${sectEsc(b.name)}</h3><p class="sect-sub">${sectEsc(b.desc)}</p>
        <div class="sect-bar hp"><i style="width:${pct.toFixed(1)}%"></i></div>
        <p class="sect-sub"><b>${fmt(Math.max(0, Math.round(b.maxHp - b.damage)))}</b> / ${fmt(Math.round(b.maxHp))} máu · ${stText}</p>
        <div class="sect-slots" aria-label="Lượt đánh hôm nay">${slots}<span class="sect-sub">Bạn đã gây <b>${fmt(Math.round(b.myDamage))}</b> sát thương</span></div>
        <div class="sect-row">${attack}${chest}</div>${buff}</div>
    </section>
    <section class="sect-card"><h3>Sát thương hôm nay</h3>${rank ? `<ul class="sect-list">${rank}</ul>` : '<p class="sect-empty">Chưa ai ra tay hôm nay.</p>'}
      <p class="sect-sub">Hạ Boss trước 0h: mọi người đã đánh nhận rương (nhận trong ngày), top 3 thưởng thêm cống hiến, cả tông +${SECT_BUFF_PCT}% tu vi ngày mai.</p></section>`;
}
```
Thêm hằng số `const SECT_BUFF_PCT = 3;   // khớp SectBossData.BuffQiPct` ngay trên khối `sect-pure:start`... đặt **ngoài** khối pure (dưới `const SECT = …`).

- [ ] **Step 6: `initSect` — mở view Boss thì tải lại**

Trong handler click, thay:
```js
    if (v){ SECT.view = v.dataset.sectView; SECT.notice = ''; renderSect(); return; }
```
bằng:
```js
    if (v){ SECT.view = v.dataset.sectView; SECT.notice = ''; renderSect(); if (SECT.view === 'boss') loadSectBoss(); return; }
```

- [ ] **Step 7: CSS (`wwwroot/css/sect.css`, thêm cuối file)**

```css
/* Sơn môn: ảnh nền + công trình bấm được */
.sect-scene{position:relative;aspect-ratio:3/2;border-radius:6px;border:1px solid var(--line);overflow:hidden;background:#1b1530 url(../img/sect/son_mon.jpg) center/cover no-repeat}
.sect-spot{position:absolute;transform:translate(-50%,-50%);display:flex;flex-direction:column;align-items:center;gap:4px;padding:0;border:0;background:none;cursor:pointer;color:#f3e9c8;font:600 12px var(--f-body,system-ui)}
.sect-spot::before{content:"";width:34px;height:34px;border-radius:50%;border:2px solid #e3c16b;background:rgba(27,21,48,.55);box-shadow:0 0 0 3px rgba(227,193,107,.2),0 0 14px rgba(150,110,255,.6);transition:background .15s}
.sect-spot b{padding:1px 8px;border-radius:10px;background:rgba(14,10,28,.78);border:1px solid rgba(227,193,107,.5);white-space:nowrap}
.sect-spot:hover::before,.sect-spot:focus-visible::before{background:rgba(227,193,107,.35)}
.sect-spot.alert::after{content:"";position:absolute;top:-2px;left:calc(50% + 10px);width:10px;height:10px;border-radius:50%;background:#e5484d;box-shadow:0 0 0 2px rgba(14,10,28,.9)}
@media (max-width:640px){.sect-spot::before{width:26px;height:26px}.sect-spot b{font-size:10px;padding:0 5px}}
.sect-h{margin:12px 0 6px;font-size:14px}
/* Bệ Phong Ấn: Boss tông */
.sect-boss{display:grid;grid-template-columns:140px minmax(0,1fr);gap:14px;align-items:start}
.sect-boss-img{width:140px;height:140px;object-fit:contain;border-radius:6px;background:rgba(14,10,28,.5)}
.sect-boss.defeated .sect-boss-img{filter:grayscale(.8) brightness(.7)}
.sect-boss-info h3{margin:0 0 4px}
.sect-bar.hp i{background:#c4524a}
.sect-slots{display:flex;align-items:center;gap:6px;margin:6px 0}
.sect-slot{width:14px;height:14px;border-radius:3px;border:1px solid var(--line);background:rgba(227,193,107,.8)}
.sect-slot.used{background:transparent}
.sect-li.top b:first-child{color:#e3c16b}
@media (max-width:640px){.sect-boss{grid-template-columns:1fr}.sect-boss-img{width:100%;height:160px}}
```

- [ ] **Step 8: Chữ tiếng Anh (`wwwroot/js/i18n-en.js`)**

Mở cuối từ điển trong file, thêm theo đúng kiểu dòng của các mục lân cận (`"Tiếng Việt": "English",`) các mục sau (bỏ mục đã có sẵn nếu trùng khóa):
```
"Sơn môn": "Sect grounds",
"Chính Điện": "Main Hall",
"Tháp Đệ Tử": "Disciple Tower",
"Tàng Bảo Các": "Treasure Pavilion",
"Đan Phòng": "Alchemy Room",
"Bệ Phong Ấn": "Sealing Platform",
"Chính Điện&#10;Cấp tông, nâng cấp, thông báo, nhận đệ tử, rời tông": "Main Hall&#10;Sect level, upgrades, notice, recruitment, leaving",
"Tháp Đệ Tử&#10;Thành viên, chức vị, duyệt đơn xin vào": "Disciple Tower&#10;Members, ranks, join requests",
"Tàng Bảo Các&#10;Đổi cống hiến lấy đan dược và vé quay": "Treasure Pavilion&#10;Trade contribution for pills and spin tickets",
"Đan Phòng&#10;Điểm danh và quyên linh thạch lấy cống hiến": "Alchemy Room&#10;Check in and donate spirit stones for contribution",
"Bệ Phong Ấn&#10;Boss tông mỗi ngày: cả tông cùng đánh, hạ Boss nhận rương và buff tu vi": "Sealing Platform&#10;Daily sect Boss: fight together, defeat it for a chest and a cultivation buff",
"Đơn xin vào": "Join requests",
"Thành viên": "Members",
"Đang tải Boss tông…": "Loading sect Boss…",
"Không tải được Boss tông.": "Could not load the sect Boss.",
"Đang chờ đồng môn": "Awaiting disciples",
"Đã hạ": "Defeated",
"Hết lượt": "Out of attempts",
"Nhận rương": "Claim chest",
"Đã nhận rương": "Chest claimed",
"Chưa đánh lượt nào": "No attacks yet",
"Sát thương hôm nay": "Damage today",
"Chưa ai ra tay hôm nay.": "Nobody has attacked today.",
"Lượt đánh hôm nay": "Today's attacks",
"Cổ Thụ Yêu Vương": "Ancient Tree Demon King",
"Huyền Nham Cự Nhân": "Dark Rock Giant",
"Cây cổ vạn năm thành yêu, rễ bám khắp rừng sâu, bị phong ấn dưới Bệ Phong Ấn của tông.": "A ten-thousand-year-old tree turned demon, its roots spread through the deep forest, sealed beneath the sect's Sealing Platform.",
"Người đá khổng lồ ngủ dưới linh mạch, mỗi đêm lại phá phong ấn thức dậy.": "A giant of stone sleeping beneath the spirit vein, breaking its seal to wake every night.",
```
Các chuỗi có số (vd. `Tấn công · còn 2/2 lượt`, `+3% tu vi`) là chuỗi ghép động: nếu hệ thống i18n của dự án có cơ chế mẫu (xem cách `i18n-en.js` xử lý chuỗi có số, tìm `RegExp`/`pattern` trong file), thêm mẫu tương ứng; nếu không thì để nguyên tiếng Việt cho các câu động này và ghi vào báo cáo cuối.

- [ ] **Step 9: Kiểm tra cú pháp và test**

Run: `node --check wwwroot/js/sect.js` rồi `node --test --test-reporter=dot test/sect.test.cjs`
Expected: không lỗi, test pass.

- [ ] **Step 10: Thử tay trên bản web**

Run: `dotnet run -- --web` (xem README/`Program.cs` nếu cờ khác), mở trang, tạo/vào một tông và kiểm:
1. Tab Tông Môn hiện ảnh sơn môn với 5 nút; rê chuột ra tooltip; chấm đỏ ở Đan Phòng khi chưa điểm danh.
2. Bấm từng công trình mở đúng view; tab "Sơn môn" quay lại ảnh.
3. Bệ Phong Ấn: thanh máu, bấm Tấn công hai lần, lần ba bị khóa; bảng sát thương cập nhật; cống hiến và quỹ tăng.
4. Ép hạ Boss (SQLite: `UPDATE SectBosses SET MaxHp = 1`), thấy nút Nhận rương, nhận một lần; dòng buff hiện cho "ngày mai".
5. Màn hẹp (thu nhỏ cửa sổ dưới 640px): nút vẫn bấm được, hàng tab dùng được.
Báo cáo trung thực những mục không kiểm tra được.

---

### Task 9: Ghi chú phát hành và đối chiếu spec

**Files:**
- Modify: `docs/releases/upcoming.md`, `docs/superpowers/specs/2026-11-11-tong-mon-boss-design.md`

- [ ] **Step 1: Ghi chú phát hành** — gọi skill `release-notes` (cập nhật `docs/releases/upcoming.md`) với nội dung: "Tông Môn: giao diện sơn môn mới, mỗi công trình mở một chức năng; Boss tông hằng ngày (Bệ Phong Ấn): cả tông cùng đánh, mỗi người 2 lượt/ngày, hạ Boss nhận rương, top 3 thưởng cống hiến, cả tông +3% tu vi ngày hôm sau."

- [ ] **Step 2: Đối chiếu spec với những chỗ plan đã chọn khác**

Sửa `docs/superpowers/specs/2026-11-11-tong-mon-boss-design.md`:
1. Dòng máu Boss: thay "`MaxHp = max(sàn theo cấp tông, 60% × Σ BossRef của thành viên hoạt động × 2 lượt)`" bằng "`MaxHp = 60% × Σ BossRef của thành viên hoạt động × 2 lượt`"; thay câu "người chưa từng đánh tính mức sàn theo cảnh giới" bằng "người chưa từng đánh lấy trung bình của những người đã có; chưa ai có thì lấy sát thương mô phỏng của người đầu tiên mở trang Boss (luôn có ít nhất một đơn vị, nên tông 1 người vẫn có Boss hợp lý)".
2. Mục Rương chung: thêm "Rương chỉ nhận được trong chính ngày hạ Boss (trước 0h)."
3. Mục Giao diện: thay "Nén thành `wwwroot/img/sect/son_mon.webp` (+ `.jpg` dự phòng)" bằng "Nén thành `wwwroot/img/sect/son_mon.jpg`"; thay câu "Bấm công trình: nội dung thay ảnh, có nút "‹ Về sơn môn"" bằng "Bấm công trình: nội dung thay ảnh; hàng tab (tab đầu là "Sơn môn") luôn hiện để quay lại và làm lối vào dự phòng"; xóa câu "Màn hẹp: … hàng tab cũ giữ làm lối vào dự phòng" thành "Màn hẹp: ảnh co theo khung".
4. Mục DB: ghi rõ cột sát thương/máu là `double`.

- [ ] **Step 3: Chạy lần cuối toàn bộ test liên quan**

Run: `dotnet test test/GemSystemTests --filter "FullyQualifiedName~SectBoss|FullyQualifiedName~SectTests" --nologo -v q -clp:ErrorsOnly --logger "console;verbosity=minimal"` và `node --test --test-reporter=dot test/sect.test.cjs`
Expected: tất cả pass. Báo kết quả thật cho người dùng, kèm những gì chưa thử được.

---

## Tự rà soát (đối chiếu spec)

- Máu chung, chốt trong ngày, co theo thành viên hoạt động: Task 2 (`MaxHp`), Task 5 (`EnsureBossAsync`, test `MaxHp*`, `MaxHpIsLockedForTheDay`).
- 2 lượt/ngày, reset 0h VN, chống đồng thời: Task 5 (`ReserveHitAsync`, test `TwoHitsPerDay…`).
- Trận mô phỏng 15 hiệp với Boss bản sao: Task 1 + Task 6 (`SectBossFighter`, `SectBossSimulate`, test `HugeStats…`).
- Cống hiến theo sát thương (+ quỹ), rương chung, top 3, buff +3% ngày sau: Task 5 (`RecordHitAsync`, `ClaimAsync`), Task 3 (`SectBuff`), Task 6 (`SectBossClaimAsync`).
- Boss hạ chỉ một lần, chặn đánh thêm, thông báo chat tông: test `DefeatSetsBuffOnceAnnouncesAndBlocksFurtherHits`.
- DB mọi chế độ + SQL online: Task 3 (SQLite `UpgradeSect`), Task 4 (Postgres migration, snapshot, SQL).
- API: Task 6. Giao diện sơn môn, Bệ Phong Ấn, Đan Phòng, tooltip, chấm đỏ: Task 7–8. Chữ tiếng Anh, ghi chú phát hành: Task 8–9.
- Tên dùng xuyên suốt: `SectBossData` (`BossOf`, `NextDay`, `MaxHp`, `Contribution`, `Rank`, `TopBonus`, `BuffQiPct`), `ServerSectBoss` (`StateAsync`, `ReserveHitAsync`, `RecordHitAsync`, `ClaimAsync`), `GameService` (`SectBossSim`, `SectBossSimulate`, `SectBossFighter`, `SectBossPageAsync`, `SectBossAttackAsync`, `SectBossClaimAsync`), `SectBuff.QiPct(id, now)`, DTO `SectBossDto`/`SectBossRankDto`; JS `SECT_SPOTS`, `sectBossHpPct`, `sectBossStatus`, `sectSpotAlert`, `loadSectBoss`, `sectBossView`, `sectDaily`, `sectHub`, `sectMembersView`.
- Rủi ro đã ghi nhận: `HugeStatsDoNotOverflow` có thể buộc hạ máu Boss trong trận (Task 6 Step 5); chuỗi i18n động (Task 8 Step 8); các test dùng `DateTime.UtcNow` thật nên có thể lệch nếu chạy đúng vào 0h giờ VN.
