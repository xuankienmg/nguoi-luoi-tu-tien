# Donate codes + Tạp Hóa VIP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admin-made single-use donate codes that grant Linh Thạch Thượng Phẩm (online and offline), a Tạp Hóa VIP paid in Thượng Phẩm with weekly limits, herb exchange, and a one-time Gói Tốc Hành that unlocks a ×2 exploration toggle.

**Architecture:** Codes live in a new `game.DonateCodes` table owned by the online backend. Online characters redeem inside their request transaction; offline games call a public, rate-limited backend endpoint and add the stones locally. The VIP shop is a fixed catalog in `GameService.Vip.cs` mirroring `GameService.Shop.cs`, with per-player weekly counters on `Player`. ×2 exploration reuses `FastMult` in `ExploreSeconds`.

**Tech Stack:** .NET 8 minimal API, EF Core 8 (SQLite offline, Npgsql online, EF migrations in `Online/Migrations`), xUnit (`test/GemSystemTests`, SQLite in-memory), vanilla JS (`wwwroot/js`).

Spec: `docs/superpowers/specs/2026-11-03-donate-vip-shop-design.md`.

**Change 2026-11-03 (user):** no weekly limits in Tạp Hóa VIP. Drop `Player.VipWeek` / `VipBought`, all weekly-limit code, tests and UI below; only Gói Tốc Hành stays once per character.

**Repo rules:** work on the current branch (no worktree); commit only when the user asks, so each task ends with a checkpoint instead of a commit. The user's game may lock `bin\Debug\...\NguoiLuoiTuTien.exe`: build and test with `-p:BaseOutputPath=<scratchpad>/testbin/`.

Test command used below:

```bash
dotnet test test/GemSystemTests/GemSystemTests.csproj -nologo -v q "-p:BaseOutputPath=C:/Users/KienBui/AppData/Local/Temp/claude/D--projects-van-dao-VanDao/418ee4eb-3974-4bff-a120-71d94d729023/scratchpad/testbin/" --filter "FullyQualifiedName~<TestClass>"
```

---

### Task 1: Data model and online migration

**Files:**
- Modify: `Data/Entities.cs` (Player fields; new `DonateCode` class at the end of the file)
- Modify: `Data/GameDbContext.cs` (DbSet, entity config, `SchemaUpgrade` columns)
- Create: `Online/Migrations/<timestamp>_OnlineDonateVip.cs` (+ Designer, snapshot update) via `dotnet ef`
- Modify: `supabase/migrations/20261026_online_authority.sql` (regenerated)

- [ ] **Step 1: Add Player fields** in `Data/Entities.cs`, right after `public int DaoTam { get; set; }`:

```csharp
    /// <summary>Tạp Hóa VIP: tuần mua (thứ Hai "yyyy-MM-dd", giờ Việt Nam) và số đã mua trong tuần ("món:số,…").</summary>
    public string VipWeek { get; set; } = "";
    public string VipBought { get; set; } = "";
    /// <summary>Gói Tốc Hành: đã mở thám hiểm ×2 / người chơi đang bật.</summary>
    public bool Speed2 { get; set; }
    public bool Speed2On { get; set; }
```

- [ ] **Step 2: Add the `DonateCode` entity** at the end of `Data/Entities.cs`:

```csharp
/// <summary>Code donate dùng một lần (chế độ Server, schema game): admin tạo theo số tiền, người chơi online hoặc offline nhập để nhận Thượng Phẩm.</summary>
public class DonateCode
{
    public string Code { get; set; } = "";
    public int Amount { get; set; }
    public long Vnd { get; set; }
    public string Note { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }
    public Guid? UsedByPlayer { get; set; }
    public Guid? UsedByOwner { get; set; }
    public bool Offline { get; set; }
}
```

- [ ] **Step 3: Map it** in `Data/GameDbContext.cs`. Add next to the other DbSets:

```csharp
    public DbSet<DonateCode> DonateCodes => Set<DonateCode>();
```

and inside `OnModelCreating`, before the Npgsql `utc` converter block:

```csharp
        b.Entity<DonateCode>(e =>
        {
            e.HasKey(c => c.Code);
            e.Property(c => c.Code).HasMaxLength(20);
            e.Property(c => c.Note).HasMaxLength(100);
            e.HasIndex(c => c.CreatedAt);
        });
```

- [ ] **Step 4: Offline columns.** In `SchemaUpgrade.Apply`, append to the `Players` column list (after `("GemVault", …), ("GemSlots", …),`):

```csharp
            ("VipWeek", "TEXT NOT NULL DEFAULT ''"), ("VipBought", "TEXT NOT NULL DEFAULT ''"),
            ("Speed2", "INTEGER NOT NULL DEFAULT 0"), ("Speed2On", "INTEGER NOT NULL DEFAULT 0"),
```

(The offline game never reads `DonateCodes`; new SQLite files get the table from `EnsureCreated`, old ones never touch it.)

- [ ] **Step 5: Run the migration test to see it fail**

Run the test command with `--filter "FullyQualifiedName~OnlineMigrationsMatchTheEntityModel"`.
Expected: FAIL "Data/Entities.cs đã đổi mà chưa có migration online".

- [ ] **Step 6: Create the migration**

```bash
dotnet ef migrations add OnlineDonateVip --configuration Release --project tools/online_host --startup-project tools/online_host --output-dir ../../Online/Migrations --namespace VanDao.Online.Migrations
```

If `GameDbContextModelSnapshot.cs` lands in `tools/online_host/VanDao/Online/Migrations`, move it to `Online/Migrations` and delete `tools/online_host/VanDao`. Then at the end of `Up` in the new migration add (new table needs the backend grants and RLS):

```csharp
            migrationBuilder.Sql(OnlineSchema.Grants);
```

with `using VanDao.Online;` at the top if missing.

- [ ] **Step 7: Regenerate the Supabase script and strip the BOM**

```bash
dotnet ef migrations script --idempotent --configuration Release --project tools/online_host --startup-project tools/online_host -o supabase/migrations/20261026_online_authority.sql
node -e "const fs=require('fs');const p='supabase/migrations/20261026_online_authority.sql';let b=fs.readFileSync(p);if(b[0]===0xef)fs.writeFileSync(p,b.subarray(3))"
git diff --stat -- supabase/migrations/20261026_online_authority.sql
```

Expected: only an added `OnlineDonateVip` block (4 player columns, `DonateCodes` table + index, grants).

- [ ] **Step 8: Run the whole suite** (no filter). Expected: all pass (`MonsterRageTests.KillingWavesBeforeCounterNeverChargesOrTransfersRage` is a known flaky test; rerun if it alone fails).

- [ ] **Checkpoint** (commit only when the user asks): `feat: donate code table and VIP/×2 player fields`.

---

### Task 2: Tạp Hóa VIP catalog and purchase

**Files:**
- Create: `Game/GameService.Vip.cs`
- Modify: `Game/Market.cs` (`MarketDto` gets `VipShopDto? Vip = null`)
- Modify: `Game/GameService.Market.cs:52-53` (fill `Vip:`)
- Test: `test/GemSystemTests/VipShopTests.cs`

- [ ] **Step 1: Write the failing tests** `test/GemSystemTests/VipShopTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class VipShopTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public void Dispose() => db.Dispose();
    private const string Tp = GameService.VipCurrency;
    private static readonly DateTimeOffset Mon = new(2026, 11, 2, 9, 0, 0, TimeSpan.FromHours(7));   // thứ Hai

    private async Task<(GameService Game, Player P)> Setup(int premium = 1000)
    {
        db.Database.OpenConnection(); db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("VIP");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Add(Tp, premium);
        return (game, p);
    }

    [Fact]
    public async Task SpinTicketCostsOnePremiumStone()
    {
        var (game, p) = await Setup(5);
        game.VipBuy(p, GameData.SpinTicket, 3, Mon);
        Assert.Equal(2, p.Count(Tp));
        Assert.Equal(3, p.Count(GameData.SpinTicket));
    }

    [Fact]
    public async Task GemPricesFollowLevelAndWeeklyLimit()
    {
        var (game, p) = await Setup();
        var lv7 = GemData.GemsAtLevel(6)[0];
        Assert.Equal(10, GameService.VipOffersFor(p).Single(o => o.Item == lv7).Price);
        Assert.Equal(50, GameService.VipOffersFor(p).Single(o => o.Item == GemData.GemsAtLevel(9)[0]).Price);
        game.VipBuy(p, lv7, 3, Mon);
        Assert.Throws<GameException>(() => game.VipBuy(p, lv7, 1, Mon));
        game.VipBuy(p, lv7, 1, Mon.AddDays(7));                     // tuần sau mua lại được
        Assert.Equal(4, GemData.Count(p, lv7));
    }

    [Fact]
    public async Task NotEnoughPremiumIsRejected()
    {
        var (game, p) = await Setup(0);
        Assert.Throws<GameException>(() => game.VipBuy(p, "thu_dan_thuong_co", 1, Mon));
    }

    [Fact]
    public async Task ExchangeGivesStonesByRealm()
    {
        var (game, p) = await Setup(2);
        var before = p.Count("linh_thach");
        game.VipBuy(p, GameService.VipStoneOffer, 2, Mon);
        Assert.Equal(before + 2 * 20 * KyDuyen.StoneUnit(p.Realm), p.Count("linh_thach"));
    }

    [Fact]
    public async Task HerbTierCappedByRealm()
    {
        var (game, p) = await Setup();
        var allowed = GameService.VipHerbs(p);
        Assert.Contains("tu_linh_thao", allowed);                    // bậc 1
        Assert.DoesNotContain(allowed, h => GameService.HerbTier(h) > 2);   // Luyện Khí: tối đa bậc 2
        var high = GameData.AllItems.First(d => d.Type == "herb" && GameService.HerbTier(d.Id) >= 3).Id;
        Assert.Throws<GameException>(() => game.VipBuy(p, high, 1, Mon));
        game.VipBuy(p, "tu_linh_thao", 4, Mon);
        Assert.Equal(4, p.Count("tu_linh_thao"));
    }

    [Fact]
    public async Task PackUnlocksSpeedOnceAndGivesPills()
    {
        var (game, p) = await Setup(250);
        game.VipBuy(p, GameService.VipPackOffer, 1, Mon);
        Assert.True(p.Speed2);
        Assert.Equal(10, p.Count("bo_nguyen_dan_trung"));
        Assert.Equal(10, p.Count("bo_nguyen_dan_thuong"));
        Assert.Throws<GameException>(() => game.VipBuy(p, GameService.VipPackOffer, 1, Mon.AddDays(30)));
        Assert.Equal(150, p.Count(Tp));
    }

    [Fact]
    public async Task UnknownItemIsRejected()
    {
        var (game, p) = await Setup();
        Assert.Throws<GameException>(() => game.VipBuy(p, "linh_thu_dan_x", 1, Mon));
    }
}
```

Check before running: `GemData.Count(p, id)` must exist for gems stored in the vault. If the helper has another name, use the one `GameService.Shop.cs`'s callers use (`grep -n "static .* Count(Player" Game/GemData.cs`) and adjust the assertion.

- [ ] **Step 2: Run** with `--filter "FullyQualifiedName~VipShopTests"`. Expected: compile errors (`VipBuy`, `VipCurrency` … not defined).

- [ ] **Step 3: Implement** `Game/GameService.Vip.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>Limit 0 = không giới hạn tuần. Qty: số nhận mỗi lần mua (đổi linh thạch: theo cảnh giới). Pick: chọn một trong Choices (đổi linh thảo).</summary>
public record VipOfferDto(string Item, int Price, int Qty, int Limit, int Bought, string Group, List<string>? Choices = null, bool Owned = false);
public record VipShopDto(string Name, List<VipOfferDto> Offers, long ResetAt, decimal Balance);

/// <summary>Tạp Hóa VIP: trả bằng Linh Thạch Thượng Phẩm, danh sách cố định, món mạnh giới hạn mỗi tuần (thứ Hai 0h giờ Việt Nam).</summary>
public partial class GameService
{
    public const string VipCurrency = "thuong_pham_linh_thach";
    public const string VipStoneOffer = "linh_thach";
    public const string VipHerbOffer = "vip_herb";
    public const string VipPackOffer = "goi_toc_hanh";
    public const string VipShopName = "Tạp Hóa VIP";

    private static readonly int[] VipGemPrice = { 10, 20, 30, 50 }, VipGemLimit = { 3, 2, 1, 1 };   // cấp 7–10

    /// <summary>Danh sách cố định (giá Thượng Phẩm, số nhận mỗi lần, giới hạn tuần, nhóm).</summary>
    private static IEnumerable<(string Item, int Price, int Qty, int Limit, string Group)> VipCatalog(Player p)
    {
        yield return (GameData.SpinTicket, 1, 1, 0, "Vé quay");
        yield return (GameData.PetSpinTicket, 1, 1, 0, "Vé quay");
        foreach (var skin in GameData.AllItems.Where(d => d.Type == "skin")) yield return (skin.Id, 10, 1, 0, "Trang phục");
        for (var i = 0; i < VipGemPrice.Length; i++)
            foreach (var gem in GemData.GemsAtLevel(6 + i)) yield return (gem, VipGemPrice[i], 1, VipGemLimit[i], "Tinh Thạch");
        yield return ("thu_dan_thien", 30, 1, 3, "Linh thú");
        yield return ("thu_dan_thuong_co", 50, 1, 1, "Linh thú");
        yield return ("linh_thu_dan", 5, 10, 0, "Linh thú");
        yield return (VipHerbOffer, 1, 1, 0, "Linh thảo");
        yield return (GameData.RenameCard, 20, 1, 0, "Tiện ích");
        yield return (SupportPills.TuLinh + "_thuong", 3, 1, 10, "Tiện ích");
        yield return (SupportPills.BeQuan + "_thuong", 3, 1, 10, "Tiện ích");
        yield return (VipStoneOffer, 1, (int)Math.Min(int.MaxValue, 20 * KyDuyen.StoneUnit(p.Realm)), 0, "Tiện ích");
        yield return (VipPackOffer, 100, 1, 0, "Gói");
    }

    /// <summary>Bậc linh thảo = N trong mô tả "Linh thảo bậc N"; 0 = không phải linh thảo có bậc.</summary>
    public static int HerbTier(string id) =>
        GameData.ItemById.TryGetValue(id, out var d) && d.Type == "herb" && d.Use is { } use
        && Regex.Match(use, @"bậc (\d+)") is { Success: true } m ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;

    private static readonly int MaxHerbTier = GameData.AllItems.Max(d => HerbTier(d.Id));

    /// <summary>Linh thảo đổi được: bậc ≤ bậc của cảnh giới + 1 (Luyện Khí, Trúc Cơ = 1; Kim Đan = 2 …).</summary>
    public static List<string> VipHerbs(Player p)
    {
        var tier = Math.Min(Math.Max(1, p.Realm), MaxHerbTier) + 1;
        return GameData.AllItems.Where(d => HerbTier(d.Id) is > 0 and var t && t <= tier).Select(d => d.Id).ToList();
    }

    private static DateTimeOffset VipWeekStart(DateTimeOffset now)
    {
        var local = now.ToOffset(SpinDayOffset);
        var monday = local.Date.AddDays(-(((int)local.DayOfWeek + 6) % 7));
        return new(monday, SpinDayOffset);
    }
    private static string VipWeekOf(DateTimeOffset now) => VipWeekStart(now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Dictionary<string, int> VipBoughtThisWeek(Player p, DateTimeOffset now) =>
        p.VipWeek != VipWeekOf(now) ? new() : p.VipBought.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(':'))
            .Where(x => x.Length == 2 && int.TryParse(x[1], out _)).ToDictionary(x => x[0], x => int.Parse(x[1], CultureInfo.InvariantCulture));

    public static List<VipOfferDto> VipOffersFor(Player p, DateTimeOffset? at = null)
    {
        var bought = VipBoughtThisWeek(p, at ?? DateTimeOffset.UtcNow);
        return VipCatalog(p).Select(o => new VipOfferDto(o.Item, o.Price, o.Qty, o.Limit, bought.GetValueOrDefault(o.Item), o.Group,
            o.Item == VipHerbOffer ? VipHerbs(p) : null, o.Item == VipPackOffer && p.Speed2)).ToList();
    }

    public static VipShopDto VipFor(Player p, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        return new(VipShopName, VipOffersFor(p, now), VipWeekStart(now).AddDays(7).ToUnixTimeMilliseconds(), p.Count(VipCurrency));
    }

    /// <summary>Mua ở Tạp Hóa VIP. itemId: mã món, hoặc mã linh thảo muốn đổi (nhóm Linh thảo).</summary>
    public (string msg, string kind) VipBuy(Player p, string itemId, int n = 1, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        if (n is < 1 or > 99) throw new GameException("Số lượng không hợp lệ.");
        var herb = VipHerbs(p).Contains(itemId);
        var offerId = herb ? VipHerbOffer : itemId;
        var offer = VipCatalog(p).Where(o => o.Item == offerId).Select(o => ((string Item, int Price, int Qty, int Limit, string Group)?)o).FirstOrDefault()
            ?? throw new GameException(HerbTier(itemId) > 0 ? "Linh thảo này vượt bậc được đổi ở cảnh giới hiện tại." : "Tạp Hóa VIP không bán món này.");
        if (offerId == VipPackOffer)
        {
            if (p.Speed2) throw new GameException("Đạo hữu đã mở Gói Tốc Hành rồi.");
            n = 1;
        }
        var bought = VipBoughtThisWeek(p, now);
        if (offer.Value.Limit > 0)
        {
            var left = offer.Value.Limit - bought.GetValueOrDefault(offerId);
            if (left <= 0) throw new GameException("Tuần này đã mua hết phần món này.");
            if (n > left) throw new GameException($"Tuần này chỉ còn mua được {left} món này.");
        }
        var cost = (long)offer.Value.Price * n;
        if (p.Count(VipCurrency) < cost) throw new GameException($"Cần {cost:N0} Linh Thạch Thượng Phẩm.");
        var get = offerId switch { VipPackOffer => "bo_nguyen_dan_trung", VipStoneOffer => "linh_thach", _ => itemId };
        var qty = (decimal)offer.Value.Qty * n;
        if (offerId == VipPackOffer)
        {
            var newSlots = new[] { "bo_nguyen_dan_trung", "bo_nguyen_dan_thuong" }.Count(id => p.Count(id) == 0);
            if (BagUsed(p) + newSlots > BagCap(p)) throw new GameException("Hành trang không đủ chỗ trống.");
        }
        else if (!CanFit(p, get, (int)Math.Min(qty, int.MaxValue))) throw new GameException("Hành trang không đủ chỗ trống.");

        p.Add(VipCurrency, -cost);
        string got;
        if (offerId == VipPackOffer)
        {
            p.Speed2 = true; p.Speed2On = true;
            p.Add("bo_nguyen_dan_trung", 10); p.Add("bo_nguyen_dan_thuong", 10);
            got = "Gói Tốc Hành: mở thám hiểm ×2, +10 Bổ Nguyên Đan · Trung phẩm, +10 Bổ Nguyên Đan · Thượng phẩm";
        }
        else
        {
            if (GemData.IsGemOrStone(get)) GemData.Add(p, get, (int)qty); else p.Add(get, qty);
            got = $"+{qty:N0} {GameData.ItemById[get].Name}";
        }
        if (offer.Value.Limit > 0)
        {
            bought[offerId] = bought.GetValueOrDefault(offerId) + n;
            p.VipWeek = VipWeekOf(now);
            p.VipBought = string.Join(',', bought.Select(kv => $"{kv.Key}:{kv.Value}"));
        }
        return Log(p, $"{VipShopName}: {got} (−{cost:N0} Thượng Phẩm).", "good");
    }
}
```

Notes for the implementer: confirm `GemData.Add(p, id, n)` is how gems enter the vault (`grep -n "public static void Add(Player" Game/GemData.cs`, RedeemCode uses `p.Add` for everything because `Player.Add` may already route gems; if `p.Add` handles gems, replace the `IsGemOrStone` branch with `p.Add(get, qty)`). `GameData.SpinTicket` / `PetSpinTicket` / `RenameCard` exist (`GameData.cs`).

- [ ] **Step 4: Expose it in the market DTO.** In `Game/Market.cs` change the last parameter line of `MarketDto` to:

```csharp
    ShopDto? Shop = null, VipShopDto? Vip = null);
```

In `Game/GameService.Market.cs` line 53, append `, Vip: VipFor(p)` after `Shop: ShopFor(p)`. Search the same file for every other `new MarketDto(` / `empty with` that sets `Shop =` and set `Vip = VipFor(p)` next to it.

- [ ] **Step 5: Run** `VipShopTests`. Expected: PASS (7 tests). Fix assertions only if a helper name differs (see Step 1/3 notes), not the rules.

- [ ] **Step 6: Endpoint.** In `Program.cs` after the shop endpoint (line ~352):

```csharp
        api.MapPost("/players/{id:guid}/vip/{itemId}", (Guid id, string itemId, int? n, GameService game) =>
            Run(id, game, p => game.VipBuy(p, itemId, n ?? 1)));
```

- [ ] **Checkpoint:** `feat: Tạp Hóa VIP with weekly limits, herb exchange and Gói Tốc Hành`.

---

### Task 3: ×2 exploration toggle

**Files:**
- Modify: `Game/GameService.cs:658` (`FastMult`), add `SetSpeed2`
- Modify: `Game/Dtos.cs:124` + `Game/GameService.cs:184` (state)
- Modify: `Program.cs` (endpoint)
- Test: `test/GemSystemTests/VipShopTests.cs` (add)

- [ ] **Step 1: Failing tests** (append to `VipShopTests`):

```csharp
    [Fact]
    public async Task SpeedToggleHalvesTripsOnlyWhenOn()
    {
        var (game, p) = await Setup(200);
        var normal = (await game.BuildStateAsync(p)).ExploreCooldownMs;
        Assert.Throws<GameException>(() => game.SetSpeed2(p, true));          // chưa mua gói
        game.VipBuy(p, GameService.VipPackOffer, 1, Mon);
        var fast = (await game.BuildStateAsync(p)).ExploreCooldownMs;
        Assert.InRange(fast, normal / 2 - 1000, normal / 2 + 1000);
        game.SetSpeed2(p, false);
        Assert.Equal(normal, (await game.BuildStateAsync(p)).ExploreCooldownMs);
        Assert.True((await game.BuildStateAsync(p)).Speed2);
        Assert.False((await game.BuildStateAsync(p)).Speed2On);
    }
```

Check: the StateDto property for trip length — `grep -n "ExploreCooldownMs\|(long)(ExploreSeconds(p) \* 1000)" Game/Dtos.cs Game/GameService.cs`; use that property name.

- [ ] **Step 2: Run** → FAIL (`SetSpeed2`, `Speed2` missing).

- [ ] **Step 3: Implement.** Replace `FastMult` (`Game/GameService.cs:658`):

```csharp
    /// <summary>Tốc độ thám hiểm: cờ admin (×0.5 / ×2 / ×3 / ×5; "fast" cũ = ×3) thắng; không có cờ thì Gói Tốc Hành đang bật = ×2.</summary>
    private static double FastMult(Player p) => p.Cheat("fast5") ? 5 : p.Cheat("fast3") || p.Cheat("fast") ? 3 : p.Cheat("fast2") ? 2 : p.Cheat("fast05") ? .5
        : p.Speed2 && p.Speed2On ? 2 : 1;

    /// <summary>Bật / tắt thám hiểm ×2 của Gói Tốc Hành.</summary>
    public (string msg, string kind) SetSpeed2(Player p, bool on)
    {
        if (!p.Speed2) throw new GameException("Cần mở Gói Tốc Hành ở Tạp Hóa VIP.");
        p.Speed2On = on;
        p.ExploreReadyAt = DateTime.UtcNow.AddSeconds(ExploreSeconds(p));   // chuyến mới theo tốc độ mới
        return Log(p, on ? "Thám hiểm ×2: bật." : "Thám hiểm ×2: tắt.", "good");
    }
```

In `Game/Dtos.cs` change the last StateDto parameter (line 124):

```csharp
    KyDuyenDto? KyDuyen = null,   // kỳ duyên Thiên Thư: khung đang mở, tiến độ 10 quyển, Đạo Tâm
    bool Speed2 = false, bool Speed2On = false);   // Gói Tốc Hành: đã mở / đang bật thám hiểm ×2
```

In `Game/GameService.cs:184` change the end `KyDuyenStateFor(p));` to:

```csharp
KyDuyenStateFor(p), p.Speed2, p.Speed2On);
```

`Program.cs`, next to the VIP endpoint:

```csharp
        api.MapPost("/players/{id:guid}/speed2", (Guid id, bool on, GameService game) =>
            Run(id, game, p => game.SetSpeed2(p, on)));
```

- [ ] **Step 4: Run** `VipShopTests` → PASS.
- [ ] **Checkpoint:** `feat: ×2 exploration toggle from Gói Tốc Hành`.

---

### Task 4: Donate code store (server)

**Files:**
- Create: `Game/DonateCodes.cs`
- Test: `test/GemSystemTests/DonateCodeTests.cs`

- [ ] **Step 1: Failing tests** `test/GemSystemTests/DonateCodeTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class DonateCodeTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public DonateCodeTests() { db.Database.OpenConnection(); db.Database.EnsureCreated(); }
    public void Dispose() => db.Dispose();
    private static readonly Guid Admin = Guid.NewGuid(), Player1 = Guid.NewGuid(), Player2 = Guid.NewGuid();

    [Fact]
    public async Task CreateMakesAmountFromVnd()
    {
        var store = new DonateStore(db);
        var c = await store.CreateAsync(50_000, "Anh A", Admin);
        Assert.Matches(@"^NLTT-[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}$", c.Code);
        Assert.Equal(50, c.Amount);
        Assert.Single(await store.ListAsync());
    }

    [Theory]
    [InlineData(0)] [InlineData(1500)] [InlineData(-1000)] [InlineData(10_001_000)]
    public async Task CreateRejectsBadAmounts(long vnd) =>
        await Assert.ThrowsAsync<GameException>(() => new DonateStore(db).CreateAsync(vnd, "", Admin));

    [Fact]
    public async Task ClaimWorksOnceCaseInsensitive()
    {
        var store = new DonateStore(db);
        var c = await store.CreateAsync(20_000, "", Admin);
        Assert.Equal(20, await store.ClaimAsync(c.Code.ToLowerInvariant(), Player1, null, offline: true));
        Assert.Null(await store.ClaimAsync(c.Code, Player2, null, offline: false));
        var row = await db.DonateCodes.AsNoTracking().SingleAsync();
        Assert.Equal(Player1, row.UsedByPlayer);
        Assert.True(row.Offline);
        Assert.Null(await store.ClaimAsync("NLTT-XXXX-XXXX", Player1, null, false));
    }
}
```

- [ ] **Step 2: Run** `DonateCodeTests` → compile errors.

- [ ] **Step 3: Implement** `Game/DonateCodes.cs`:

```csharp
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Game;

public record DonateCodeDto(string Code, int Amount, long Vnd, string Note, DateTime CreatedAt, DateTime? UsedAt, Guid? UsedByPlayer, bool Offline);

/// <summary>Code donate (chế độ Server, bảng game.DonateCodes): 1.000đ = 1 Linh Thạch Thượng Phẩm, mỗi code dùng một lần.</summary>
public sealed class DonateStore(GameDbContext db)
{
    public const string Prefix = "NLTT-";
    public const long VndPerStone = 1000, MaxVnd = 10_000_000;
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";   // bỏ 0/O/1/I dễ nhầm

    public static bool IsDonateCode(string? code) => (code ?? "").Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    private static string NewCode()
    {
        Span<char> s = stackalloc char[8];
        for (var i = 0; i < s.Length; i++) s[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return $"{Prefix}{s[..4]}-{s[4..]}";
    }

    public async Task<DonateCodeDto> CreateAsync(long vnd, string? note, Guid admin)
    {
        if (vnd <= 0 || vnd % VndPerStone != 0 || vnd > MaxVnd) throw new GameException($"Số tiền phải là bội của 1.000đ, tối đa {MaxVnd:N0}đ.");
        var row = new DonateCode { Code = NewCode(), Amount = (int)(vnd / VndPerStone), Vnd = vnd, Note = (note ?? "").Trim()[..Math.Min(100, (note ?? "").Trim().Length)], CreatedBy = admin };
        db.DonateCodes.Add(row);
        await db.SaveChangesAsync();
        return Dto(row);
    }

    public async Task<List<DonateCodeDto>> ListAsync(int take = 50) =>
        (await db.DonateCodes.AsNoTracking().OrderByDescending(c => c.CreatedAt).Take(take).ToListAsync()).Select(Dto).ToList();

    /// <summary>Đánh dấu đã dùng (một câu UPDATE có điều kiện: hai người nhập cùng lúc chỉ một người được). Trả về số Thượng Phẩm, null = sai hoặc đã dùng.</summary>
    public async Task<int?> ClaimAsync(string code, Guid player, Guid? owner, bool offline)
    {
        var c = code.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow;
        var n = await db.DonateCodes.Where(x => x.Code == c && x.UsedAt == null).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.UsedAt, now).SetProperty(x => x.UsedByPlayer, player).SetProperty(x => x.UsedByOwner, owner).SetProperty(x => x.Offline, offline));
        if (n == 0) return null;
        return await db.DonateCodes.Where(x => x.Code == c).Select(x => x.Amount).SingleAsync();
    }

    private static DonateCodeDto Dto(DonateCode c) => new(c.Code, c.Amount, c.Vnd, c.Note, c.CreatedAt, c.UsedAt, c.UsedByPlayer, c.Offline);
}
```

- [ ] **Step 4: Run** `DonateCodeTests` → PASS.
- [ ] **Checkpoint:** `feat: donate code store`.

---

### Task 5: Redeem donate codes online and offline

**Files:**
- Create: `Game/DonateClient.cs`
- Modify: `Game/GameService.cs` (constructor params + `RedeemCodeAsync`), `Program.cs:248` (redeem endpoint uses `RunAsync`)
- Test: `test/GemSystemTests/DonateCodeTests.cs` (add)

- [ ] **Step 1: Failing tests** (append to `DonateCodeTests`):

```csharp
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public HttpRequestMessage? Last;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { Last = r; return Task.FromResult(reply(r)); }
    }

    [Fact]
    public async Task OnlineRedeemAddsPremiumOnce()
    {
        var store = new DonateStore(db);
        var code = (await store.CreateAsync(30_000, "", Admin)).Code;
        var game = new GameService(db, donateStore: store);
        var p = await game.CreateAsync("Online");
        await game.RedeemCodeAsync(p, code);
        Assert.Equal(30, p.Count(GameService.VipCurrency));
        await Assert.ThrowsAsync<GameException>(() => game.RedeemCodeAsync(p, code));
    }

    [Fact]
    public async Task OfflineRedeemAsksTheBackend()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{\"amount\":15}") });
        var client = new DonateClient(new HttpClient(handler) { BaseAddress = new Uri("https://backend.test/") });
        var game = new GameService(db, donateClient: client);
        var p = await game.CreateAsync("Offline");
        await game.RedeemCodeAsync(p, "nltt-abcd-efgh");
        Assert.Equal(15, p.Count(GameService.VipCurrency));
        Assert.Equal("/api/donate/redeem", handler.Last!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task OfflineRedeemUsedCodeShowsError()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"x\"}") });
        var game = new GameService(db, donateClient: new DonateClient(new HttpClient(handler) { BaseAddress = new Uri("https://backend.test/") }));
        var p = await game.CreateAsync("Offline2");
        var ex = await Assert.ThrowsAsync<GameException>(() => game.RedeemCodeAsync(p, "NLTT-AAAA-BBBB"));
        Assert.Contains("đã được dùng", ex.Message);
    }

    [Fact]
    public async Task GiftCodesStillWork()
    {
        var game = new GameService(db);
        var p = await game.CreateAsync("Gift");
        var gift = GameData.GiftCodes.Keys.First();
        await game.RedeemCodeAsync(p, gift);
        Assert.Contains(gift, p.Redeemed);
    }
```

- [ ] **Step 2: Run** → compile errors.

- [ ] **Step 3: Implement** `Game/DonateClient.cs`:

```csharp
using System.Net.Http.Json;

namespace VanDao.Game;

/// <summary>Bản offline: nhờ backend online "tiêu" code donate (POST /api/donate/redeem) rồi tự cộng vào save trên máy.</summary>
public sealed class DonateClient(HttpClient http)
{
    private sealed record Reply(int Amount);

    public async Task<int> RedeemAsync(string code, Guid player)
    {
        HttpResponseMessage r;
        try { r = await http.PostAsJsonAsync("api/donate/redeem", new { code, player }); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { throw new GameException("Không kết nối được máy chủ để kiểm tra code."); }
        if ((int)r.StatusCode == 429) throw new GameException("Nhập code quá nhanh. Hãy chờ một phút rồi thử lại.");
        if (!r.IsSuccessStatusCode) throw new GameException("Code không đúng hoặc đã được dùng.");
        return (await r.Content.ReadFromJsonAsync<Reply>())?.Amount is > 0 and var n ? n : throw new GameException("Code không đúng hoặc đã được dùng.");
    }
}
```

In `Game/GameService.cs` extend the primary constructor (line 10), keeping existing params, appending:

```csharp
, DonateStore? donateStore = null, DonateClient? donateClient = null)
```

(after `ServerLogCache? logCache = null`). Then add next to `RedeemCode`:

```csharp
    /// <summary>Nhập code: code donate (NLTT-…) cộng Linh Thạch Thượng Phẩm (online: đánh dấu trong DB; offline: hỏi backend); code khác như RedeemCode.</summary>
    public async Task<(string msg, string kind)> RedeemCodeAsync(Player p, string? code)
    {
        if (!DonateStore.IsDonateCode(code)) return RedeemCode(p, code);
        var c = code!.Trim().ToUpperInvariant();
        if (p.Count(VipCurrency) == 0 && BagUsed(p) >= BagCap(p)) throw new GameException("Hành trang đầy, dọn bớt một ô rồi nhập lại.");
        int amount;
        if (donateStore is not null)
            amount = await donateStore.ClaimAsync(c, p.Id, db.RequestOwnerId, offline: false) ?? throw new GameException("Code không đúng hoặc đã được dùng.");
        else if (donateClient is not null)
            amount = await donateClient.RedeemAsync(c, p.Id);
        else throw new GameException("Bản này chưa nhập được code donate.");
        p.Add(VipCurrency, amount);
        return Log(p, $"Nhập code donate {c}: +{amount:N0} Linh Thạch Thượng Phẩm. Cảm ơn đạo hữu đã ủng hộ!", "gold");
    }
```

`Program.cs:248` redeem endpoint becomes:

```csharp
        api.MapPost("/players/{id:guid}/redeem", (Guid id, string? code, GameService game) =>
            RunAsync(id, game, p => game.RedeemCodeAsync(p, code)));
```

- [ ] **Step 4: Run** `DonateCodeTests` → PASS (all 8).
- [ ] **Checkpoint:** `feat: redeem donate codes online and offline`.

---

### Task 6: Wiring, admin endpoints and the public redeem endpoint

**Files:**
- Modify: `Program.cs` (DI + endpoints), `Online/OnlineGateway.cs` (rate-limit policy)

- [ ] **Step 1: DI.** In `Program.cs`, inside `if (online.IsServer) { … }` (line ~104):

```csharp
            builder.Services.AddScoped<DonateStore>();     // code donate: bảng game.DonateCodes
```

and after that block (offline/desktop only):

```csharp
        if (!online.IsServer && online.DefaultBackendUrl.Length > 0)   // bản offline nhập code donate qua backend online
            builder.Services.AddSingleton(new DonateClient(new HttpClient { BaseAddress = new Uri(online.DefaultBackendUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(15) }));
```

- [ ] **Step 2: Rate limit.** In `Online/OnlineGateway.cs` add a constant and a policy inside `AddAuthRateLimit` (keep the existing one):

```csharp
    public const string DonateLimit = "donate-redeem";
```

```csharp
        o.AddPolicy(DonateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
```

- [ ] **Step 3: Endpoints** in `Program.cs` near the admin endpoint (line ~323). Admin create/list (server only; `OnlineBoundary` already rejects `/admin/` paths for non-admin accounts):

```csharp
        api.MapPost("/players/{id:guid}/admin/donate-codes", async (Guid id, long vnd, string? note, HttpContext ctx, IServiceProvider sp) =>
        {
            if (!Player.AdminEnabled || sp.GetService<DonateStore>() is not { } store || sp.GetRequiredService<GameDbContext>().RequestOwnerId is not { } admin)
                return Results.Json(new { error = "Chỉ tài khoản admin online mới tạo được code donate." }, statusCode: StatusCodes.Status403Forbidden);
            try { return Results.Ok(await store.CreateAsync(vnd, note, admin)); }
            catch (GameException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
        api.MapGet("/players/{id:guid}/admin/donate-codes", async (Guid id, IServiceProvider sp) =>
            Player.AdminEnabled && sp.GetService<DonateStore>() is { } store
                ? Results.Ok(await store.ListAsync())
                : Results.Json(new { error = "Chỉ tài khoản admin online mới xem được code donate." }, statusCode: StatusCodes.Status403Forbidden));
```

Public redeem for offline games (server only):

```csharp
        if (online.IsServer)
            app.MapPost("/api/donate/redeem", async (DonateRedeemRequest body, DonateStore store) =>
                body.Code is { Length: > 0 and <= 20 } && DonateStore.IsDonateCode(body.Code)
                && await store.ClaimAsync(body.Code, body.Player, null, offline: true) is { } amount
                    ? Results.Ok(new { amount })
                    : Results.NotFound(new { error = "Code không đúng hoặc đã được dùng." }))
               .RequireRateLimiting(OnlineGateway.DonateLimit);
```

with the request record at the bottom of `Program.cs` (outside the class) or in `Game/DonateCodes.cs`:

```csharp
public record DonateRedeemRequest(string? Code, Guid Player);
```

Check `app.UseRateLimiter()` is already called in server mode (`grep -n "UseRateLimiter" Program.cs Online/*.cs`); if not, add it after `app.UseMiddleware<OnlineBoundary>()`.

- [ ] **Step 4: Build** (`dotnet build VanDao.csproj -p:BaseOutputPath=…`) → 0 errors. Run the full suite → all pass.
- [ ] **Checkpoint:** `feat: donate admin and redeem endpoints`.

---

### Task 7: UI

**Files:**
- Modify: `wwwroot/js/market.js` (VIP tab), `wwwroot/css/market.css`
- Modify: `wwwroot/js/main.js` (`renderAdmin`: Donate section)
- Modify: `wwwroot/index.html:66` + `wwwroot/js/skill-preview.js` (×2 button), `wwwroot/css/window.css` or `concept-shell.css`
- Modify: `Game/GameData.cs:173-174` (item text)
- Modify: `wwwroot/js/i18n-en.js` (via `node tools/i18n_extract.cjs`)

- [ ] **Step 1: Item text** in `Game/GameData.cs:172-174`: drop the "chưa có nguồn" comment and set

```csharp
            Rarity: "epic", Kind: "Linh thạch", Use: "Nhận từ code donate (1.000đ = 1 viên). Tiêu ở Chợ → Tạp Hóa VIP."),
```

- [ ] **Step 2: VIP tab** in `wwwroot/js/market.js`. Add `['vip', 'Tạp Hóa VIP']` after `['shop', 'Tạp Hóa']` in the tab list, `if (mkView === 'vip') body = mkVipView(); else` before the `shop` branch, and the view after `mkShopView`:

```js
/** Tạp Hóa VIP (mkData.vip): trả bằng Thượng Phẩm, món mạnh giới hạn mỗi tuần; nhóm Linh thảo chọn loại trong danh sách. */
function mkVipView(){
  const vip = mkData?.vip;
  if (!vip) return `<p class="mk-empty">${mkLoading ? 'Đang tải…' : 'Tạp Hóa VIP chưa mở cửa.'}</p>`;
  const bal = vip.balance;
  const left = o => o.owned ? 0 : o.limit ? Math.max(0, o.limit - o.bought) : 99;
  const name = o => o.item === 'goi_toc_hanh' ? 'Gói Tốc Hành' : o.item === 'vip_herb' ? 'Linh thảo tự chọn' : ITEM[o.item]?.name || o.item;
  const desc = o => o.item === 'goi_toc_hanh' ? 'Mở thám hiểm ×2 (bật/tắt ở màn hình chính) · +10 Bổ Nguyên Đan Trung phẩm · +10 Thượng phẩm'
    : o.item === 'vip_herb' ? 'Mỗi Thượng Phẩm đổi 1 linh thảo, bậc tối đa = bậc cảnh giới + 1'
    : o.item === 'linh_thach' ? `Nhận ${fmt(o.qty)} linh thạch` : o.qty > 1 ? `×${o.qty} mỗi lần` : '';
  const icon = o => ITEM[o.item] ? mkItem(o.item, 0) : `<span class="mk-item"><b>${mkEsc(name(o))}</b></span>`;
  const herbPick = o => `<select data-vip-herb aria-label="Chọn linh thảo">${o.choices.map(h => `<option value="${h}">${mkEsc(ITEM[h]?.name || h)}</option>`).join('')}</select>`;
  const row = o => `<li class="mk-row${!left(o) ? ' bought' : ''}">
      ${o.item === 'vip_herb' ? `<span class="mk-item"><b>${name(o)}</b>${herbPick(o)}</span>` : icon(o)}
      <span class="mk-meta">${mkEsc(desc(o))}${o.limit ? ` · tuần này còn ${left(o)}/${o.limit}` : ''}</span>
      <span class="mk-price"><b>${fmt(o.price)}</b><small>Thượng Phẩm</small></span>
      ${o.owned ? '<span class="mk-tag">Đã mở</span>' : !left(o) ? '<span class="mk-tag">Hết phần</span>'
        : `<span class="mk-shop-buy">${o.item !== 'goi_toc_hanh' ? `<input type="number" min="1" max="${Math.max(1, Math.min(left(o), Math.floor(bal / o.price)))}" value="1" aria-label="Số lượng">` : ''}<button data-vip-buy="${o.item}" ${bal < o.price ? 'disabled' : ''}>Mua</button></span>`}
    </li>`;
  const groups = [...new Set(vip.offers.map(o => o.group))];
  return `<section class="mk-shop mk-vip">
    <header class="mk-shop-head"><div><h3>${mkEsc(vip.name)}</h3><p>Có <b>${fmt(bal)}</b> Linh Thạch Thượng Phẩm · giới hạn tuần đặt lại sau ${mkLeft(vip.resetAt)}</p></div></header>
    ${groups.map(g => `<div class="mk-list-head"><span>${mkEsc(g)}</span><span>Giá · Thượng Phẩm</span></div>
      <ul class="mk-list">${vip.offers.filter(o => o.group === g).map(row).join('')}</ul>`).join('')}</section>`;
}
```

Click handling, inside the existing `pane.addEventListener('click', …)` in `initMarket` before the shop-buy branch:

```js
    const vb = e.target.closest('[data-vip-buy]');
    if (vb){
      const li = vb.closest('li'), n = Math.max(1, +(li.querySelector('input[type=number]')?.value || 1));
      const item = vb.dataset.vipBuy === 'vip_herb' ? li.querySelector('[data-vip-herb]').value : vb.dataset.vipBuy;
      vb.disabled = true; act(`vip/${item}?n=${n}`).then(() => loadMarket(true)); return;
    }
```

`mkLeft(ms)` already formats a countdown (used for listings); if it expects an absolute time, pass `vip.resetAt`. Add `.mk-vip select{…}` styling next to `.mk-shop` rules in `wwwroot/css/market.css` matching the existing input style (copy the `.mk-shop-buy input` rule's colours/border).

- [ ] **Step 3: Admin Donate section** in `wwwroot/js/main.js` `renderAdmin`, next to the Kỳ duyên section (line ~226). Online admins only (the endpoints 403 otherwise):

```js
    <section><h4>Donate</h4>
      <div class="adm-row"><input id="admDonateVnd" type="number" min="1000" step="1000" placeholder="Số tiền (đ)"><input id="admDonateNote" maxlength="100" placeholder="Ghi chú (tên người donate)"><button data-adm-donate>Tạo code</button></div>
      <small>1.000đ = 1 Linh Thạch Thượng Phẩm. Code dùng một lần, gửi cho người donate nhập ở “Nhập code quà tặng”.</small>
      <div id="admDonateList" class="adm-donate-list"></div></section>
```

and handlers (in the admin click handler; `api` already adds online auth headers):

```js
  const donateList = async () => {
    try { const rows = await api('GET', `/api/players/${S.id}/admin/donate-codes`);
      $('admDonateList').innerHTML = rows.map(r => `<div class="adm-donate${r.usedAt ? ' used' : ''}"><code>${r.code}</code> ${fmt(r.amount)} TP · ${fmt(r.vnd)}đ · ${r.note ? r.note.replace(/[<>&"]/g, '') + ' · ' : ''}${r.usedAt ? `đã dùng ${new Date(r.usedAt).toLocaleString('vi-VN')}${r.offline ? ' (offline)' : ''}` : 'chưa dùng'} <button data-adm-copy="${r.code}">Copy</button></div>`).join('');
    } catch(e){ $('admDonateList').textContent = e.message; }
  };
```

On `[data-adm-donate]`: `api('POST', \`/api/players/${S.id}/admin/donate-codes?vnd=${vnd}&note=${encodeURIComponent(note)}\`)` then `localLog('Đã tạo code ' + r.code, 'good')` and `donateList()`; on `[data-adm-copy]`: `navigator.clipboard.writeText(code)`. Call `donateList()` once after rendering the admin pane.

- [ ] **Step 4: ×2 button** on the main screen. In `wwwroot/index.html:66`, after the `#trip` div:

```html
        <button class="ib sm speed2" id="btnSpeed2" hidden aria-pressed="false" data-tip="Thám hiểm ×2&#10;Gói Tốc Hành · bấm để bật / tắt">×2</button>
```

In `wwwroot/js/skill-preview.js`, where the strip HUD is refreshed each `live()`/`renderAll` (next to the `btnBoss` block), add:

```js
  { const b = $('btnSpeed2'); if (b){ b.hidden = !S.speed2; b.classList.toggle('on', !!S.speed2On); b.setAttribute('aria-pressed', String(!!S.speed2On)); } }
```

and once at startup (in `main.js` where other buttons get `onclick`):

```js
  if ($('btnSpeed2')) $('btnSpeed2').onclick = () => act(`speed2?on=${!S.speed2On}`);
```

CSS (`wwwroot/css/window.css`, near `.ib.boss`): `.ib.speed2{font-weight:700;font-size:12px}.ib.speed2.on{color:var(--gold);box-shadow:0 0 0 1px var(--gold) inset}`.

- [ ] **Step 5: English strings.** Run `node tools/i18n_extract.cjs`, add every listed missing segment to `wwwroot/js/i18n-en.js` (keep the file's sorted order; "Tạp Hóa VIP" stays "VIP Shop", "Linh Thạch Thượng Phẩm" = "High-grade Spirit Stones", "Gói Tốc Hành" = "Swift Travel Pack"), rerun until `thiếu 0`, then `rm tools/i18n-missing.json`.

- [ ] **Step 6: Browser check** (`--web`, scratch DB, `--Leaderboard:Url=`): give Thượng Phẩm with the offline admin `give` action, open Chợ → Tạp Hóa VIP, buy a spin ticket, a herb, the pack; confirm the ×2 button appears and toggles the trip length shown in the trip bar; check English mode for leftover Vietnamese.
- [ ] **Checkpoint:** `feat: VIP shop, donate admin and ×2 toggle UI`.

---

### Task 8: Final verification

- [ ] Full test suite: all pass (apart from the known flaky `MonsterRageTests` case, rerun once).
- [ ] `node tools/i18n_extract.cjs` and `--launcher`: `thiếu 0`.
- [ ] `git diff --stat` reviewed; spec and plan unchanged except checkboxes.
- [ ] Release note bullet for `docs/releases/upcoming.md` drafted for the user (donate codes, Tạp Hóa VIP, Gói Tốc Hành).
- [ ] Remind the user: after merge, run the regenerated `supabase/migrations/20261026_online_authority.sql` on Supabase **before** deploying the server (the deploy workflow will skip until then).
