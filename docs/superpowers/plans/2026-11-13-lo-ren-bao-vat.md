# Bảo Vật Lò Rèn (relic) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> Spec: `docs/superpowers/specs/2026-11-13-lo-ren-bao-vat-design.md` (luật, 10 bảo vật, giá — **nguồn sự thật**).
> Quy ước repo: sửa thẳng nhánh `feat/lo-ren`, **không commit / stash / reset / tạo worktree**; "Checkpoint" chỉ là `git diff --stat`. Cây thư mục có thay đổi chưa commit của việc khác (VIP…): chỉ thêm dòng của mình.
> Game đang mở khóa `bin/` → mọi lệnh test/build dùng `"-p:OutDir=<scratchpad>\out\"`.

**Goal:** Máy đổi trang bị → Điểm Luyện Khí; kệ hôm nay 3 bảo vật mua bằng điểm (làm mới 20.000 linh thạch); kệ bảo vật 4 ô vĩnh viễn chỉnh tỉ lệ / Hỏa Khí / Thần Hỏa / Tinh Thiết / Hồi Thiên Phù / chỉ số / vỡ nát.

**Architecture:** Luật thuần trong `Game/ForgeRelicData.cs` (danh mục, `Mods`, điểm đổi, kệ hôm nay). `ForgeData.OddsAt/Apply` nhận thêm `Mods` (mặc định `Mods.None`, giữ hành vi cũ). Trạng thái thêm vào JSON `ForgeSave` (không migration). Hành động trong `Game/GameService.ForgeRelic.cs`. Danh mục gửi qua `/data`; trạng thái người chơi qua `ForgeDto`.

**Tech Stack:** C# / ASP.NET Core 8, xUnit; JS thuần.

## Bản đồ file

| File | Việc |
|---|---|
| `Game/ForgeRelicData.cs` (mới) | danh mục 10 bảo vật, `Mods`, `ModsOf`, `SalvagePoints`, `Shelf` |
| `Game/ForgeData.cs` | `OddsAt`/`Apply` nhận `Mods`; `ForgeSave` thêm trường; `Store` ghi dấu `linhvan:1`; `Seeded` bỏ khóa lạ; `BonusMult` |
| `Game/GameService.cs:2131-2132` | `Stats`: % chỉ số rèn × `ForgeData.BonusMult(forge)` |
| `Game/GameService.Forge.cs` | `ForgeStrike` dùng `Mods` (tỉ lệ, Hỏa Khí, Thần Hỏa, Tinh Thiết miễn phí, vỡ nửa cấp); `ForgeUndo` (giữ phù); `ForgeStateFor` thêm điểm, kệ |
| `Game/GameService.ForgeRelic.cs` (mới) | `ForgeSalvage`, `ForgeRelicBuy`, `ForgeRelicDiscard`, `ForgeShelfReroll` |
| `Game/Dtos.cs` | `ForgeShelfDto`; `ForgeDto` thêm trường cuối; `GameDataDto` thêm `ForgeRelics`, `ForgeSalvagePoints` |
| `Program.cs` | 4 API; `/data` truyền danh mục |
| `wwwroot/js/forge.js`, `wwwroot/css/forge.css` | kệ bảo vật, cửa sổ máy đổi |
| `wwwroot/js/i18n-en.js` | bản tiếng Anh |
| `test/GemSystemTests/ForgeRelicTests.cs` (mới), `test/forge.test.cjs` | test |
| `docs/releases/upcoming.md` | ghi chú phát hành |

---

### Task 1: Luật thuần bảo vật

**Files:** Create `Game/ForgeRelicData.cs`; Modify `Game/ForgeData.cs`; Test `test/GemSystemTests/ForgeRelicTests.cs`

- [ ] **Step 1: Test thất bại** — `test/GemSystemTests/ForgeRelicTests.cs`:

```csharp
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
        var p = new VanDao.Data.Player();
        var s = new ForgeSave { Relics = ["linh_van_khac"] };
        s.Slots["weapon"] = new SlotState { L = 10 };
        Store(p, s);
        Assert.Contains("linhvan:1", p.ForgeLevels);
        Assert.Equal(1.1, BonusMult(Levels(p)), 9);
        p.ForgeState = "{hỏng";
        Assert.DoesNotContain("linhvan", Load(p).Slots.Keys);
    }
}
```

- [ ] **Step 2: Chạy, xác nhận FAIL** (biên dịch: `ForgeRelicData` chưa có).

`dotnet test test/GemSystemTests --filter "FullyQualifiedName~ForgeRelicTests" --nologo -v q -clp:ErrorsOnly "-p:OutDir=<scratchpad>\out\" --logger "console;verbosity=minimal"`

- [ ] **Step 3: `Game/ForgeRelicData.cs`**

```csharp
namespace VanDao.Game;

/// <summary>Bảo vật Lò Rèn: kệ bảo vật 4 ô vĩnh viễn; mua bằng Điểm Luyện Khí từ máy đổi trang bị; kệ hôm nay 3 món (giờ VN).
/// Trạng thái nằm trong ForgeData.ForgeSave (JSON Player.ForgeState).</summary>
public static class ForgeRelicData
{
    public const int Slots = 4, ShelfSize = 3;
    public const long RerollStone = 20_000;   // cân bằng 11/2026
    /// <summary>Dấu trong Player.ForgeLevels khi có Linh Văn Khắc: Stats nhân % chỉ số rèn mà không đọc JSON.</summary>
    public const string LinhVanKey = "linhvan";

    public sealed record RelicDef(string Id, string Name, string Desc, int Price);
    // cân bằng 11/2026: giá theo spec Bảo Vật Lò Rèn
    public static readonly RelicDef[] All =
    [
        new("an_ho_binh", "Ấn Hộ Binh", "Tỉ lệ Vỡ nát ×0,8.", 120),
        new("kim_cuong_chuy", "Kim Cương Chùy", "Đại thành +4% (bớt từ Bất biến).", 100),
        new("thien_co_do", "Thiên Cơ Đồ", "Thành công +8% (bớt từ Bất biến).", 110),
        new("ban_thach_de", "Bàn Thạch Đe", "Tổn hại −6% (chuyển sang Bất biến).", 60),
        new("hoa_tinh_thach", "Hỏa Tinh Thạch", "Mỗi nhát thường tích +2 Hỏa Khí.", 90),
        new("phuong_vu", "Phượng Vũ", "Thần Hỏa che chở 5 nhát (thay vì 3).", 90),
        new("tiet_thiet_lo", "Tiết Thiết Lô", "25% nhát rèn không tốn Tinh Thiết.", 70),
        new("hoi_quang_kinh", "Hồi Quang Kính", "Hồi Thiên Phù: 20% không bị tiêu.", 60),
        new("linh_van_khac", "Linh Văn Khắc", "Chỉ số rèn ×1,1.", 150),
        new("bat_diet_hon", "Bất Diệt Hồn", "Vỡ nát chỉ mất nửa cấp.", 200),
    ];
    public static readonly IReadOnlyDictionary<string, RelicDef> ById = All.ToDictionary(r => r.Id);

    public sealed record Mods(double BreakMult = 1, double GreatAdd = 0, double SuccessAdd = 0, double DamageSub = 0,
        int ChargePerStrike = 1, int FrenzyStrikes = ForgeData.FrenzyStrikes, double IronFreeChance = 0,
        double RewindKeepChance = 0, double BonusMult = 1, bool HalfOnBreak = false)
    {
        public static readonly Mods None = new();
    }

    public static Mods ModsOf(IEnumerable<string> owned)
    {
        var s = owned.ToHashSet();
        return new(
            BreakMult: s.Contains("an_ho_binh") ? 0.8 : 1,
            GreatAdd: s.Contains("kim_cuong_chuy") ? 0.04 : 0,
            SuccessAdd: s.Contains("thien_co_do") ? 0.08 : 0,
            DamageSub: s.Contains("ban_thach_de") ? 0.06 : 0,
            ChargePerStrike: s.Contains("hoa_tinh_thach") ? 2 : 1,
            FrenzyStrikes: s.Contains("phuong_vu") ? 5 : ForgeData.FrenzyStrikes,
            IronFreeChance: s.Contains("tiet_thiet_lo") ? 0.25 : 0,
            RewindKeepChance: s.Contains("hoi_quang_kinh") ? 0.2 : 0,
            BonusMult: s.Contains("linh_van_khac") ? 1.1 : 1,
            HalfOnBreak: s.Contains("bat_diet_hon"));
    }

    /// <summary>Điểm Luyện Khí khi đổi 1 trang bị: 2^phẩm (Phàm 1 … Kim Tiên 1024). 0 = không phải trang bị.</summary>
    public static long SalvagePoints(ItemDef d)
    {
        if (d.Slot is null || d.Base is null) return 0;
        var q = Array.IndexOf(GameData.RarityOrder, d.Rarity);
        return q < 0 ? 0 : 1L << q;
    }

    /// <summary>Điểm theo chỉ số phẩm (GameData.RarityOrder), gửi client qua /data.</summary>
    public static long[] SalvagePointsByRarity => GameData.RarityOrder.Select((_, q) => 1L << q).ToArray();

    /// <summary>Kệ hôm nay: xáo 10 bảo vật theo (nhân vật, ngày VN, số lần làm mới) — cố định, tải lại vẫn ra cùng kệ.
    /// Lấy 3 món đầu chưa có trên kệ bảo vật; món mua hôm nay vẫn giữ chỗ (client hiện "Đã mua").</summary>
    public static List<string> Shelf(Guid playerId, string day, int rerolls, ICollection<string> owned, ICollection<string> boughtToday)
    {
        var rng = new Random(StableSeed($"{playerId:N}|{day}|{rerolls}"));
        return All.Select(r => (r.Id, Key: rng.Next())).OrderBy(x => x.Key).Select(x => x.Id)
            .Where(id => !owned.Contains(id) || boughtToday.Contains(id)).Take(ShelfSize).ToList();
    }

    /// <summary>FNV-1a: string.GetHashCode đổi theo tiến trình nên không dùng làm hạt giống.</summary>
    private static int StableSeed(string s)
    {
        unchecked
        {
            var h = (int)2166136261;
            foreach (var c in s) h = (h ^ c) * 16777619;
            return h;
        }
    }
}
```

- [ ] **Step 4: Sửa `Game/ForgeData.cs`**

`OddsAt` thay bằng:

```csharp
    /// <summary>Tỉ lệ nhát kế tiếp. shielded = Thần Hỏa đang cháy (không thể vỡ). mods = bảo vật trên kệ.</summary>
    public static Odds OddsAt(int level, bool shielded, ForgeRelicData.Mods? mods = null)
    {
        var m = mods ?? ForgeRelicData.Mods.None;
        var brk = shielded || level <= 0 ? 0 : Math.Min(BreakMax, BreakBase + BreakPerLevel * level) * m.BreakMult;
        var dmg = level <= 0 ? 0 : Math.Max(0, Damage - m.DamageSub);
        var moved = level <= 0 ? 0 : Damage - dmg;   // Bàn Thạch Đe: phần Tổn hại bớt đi chuyển sang Bất biến
        var great = Great + m.GreatAdd;
        var hold = Math.Max(0, Hold - m.GreatAdd - m.SuccessAdd + moved);
        return new(great, Math.Max(0, 1 - brk - great - dmg - hold), hold, dmg, brk);
    }
```

`Apply` thay bằng:

```csharp
    public static SlotState Apply(SlotState cur, Outcome o, ForgeRelicData.Mods? mods = null)
    {
        var m = mods ?? ForgeRelicData.Mods.None;
        var n = new SlotState
        {
            L = o switch
            {
                Outcome.Great => Math.Min(MaxLevel, cur.L + 2),
                Outcome.Success => Math.Min(MaxLevel, cur.L + 1),
                Outcome.Damage => Math.Max(0, cur.L - 1),
                Outcome.Break => m.HalfOnBreak ? cur.L / 2 : 0,   // Bất Diệt Hồn: chỉ mất nửa cấp
                _ => cur.L,
            },
            C = cur.C, F = cur.F,
        };
        if (cur.F > 0) n.F = cur.F - 1;
        else if ((n.C += m.ChargePerStrike) >= FrenzyCharge) { n.C = 0; n.F = m.FrenzyStrikes; }
        if (o == Outcome.Break) { n.C = 0; n.F = 0; }
        return n;
    }

    /// <summary>Hệ số % chỉ số rèn từ dấu Linh Văn Khắc trong ForgeLevels (đọc trong Stats, không parse JSON).</summary>
    public static double BonusMult(IReadOnlyDictionary<string, int> levels) =>
        levels.ContainsKey(ForgeRelicData.LinhVanKey) ? ForgeRelicData.ModsOf(["linh_van_khac"]).BonusMult : 1;
```

`ForgeSave` thêm trường (sau `Log`):

```csharp
        /// <summary>Bảo vật trên kệ (tối đa ForgeRelicData.Slots), Điểm Luyện Khí, kệ hôm nay: ngày VN, số lần làm mới, món đã mua.</summary>
        public List<string> Relics { get; set; } = new();
        public long Points { get; set; }
        public string ShopDay { get; set; } = "";
        public int Rerolls { get; set; }
        public List<string> Bought { get; set; } = new();
```

`Seeded`: đổi điều kiện thành `if (l > 0 && GemData.EquipSlots.Contains(slot))`.

`Store`: thay dòng gán `p.ForgeLevels` bằng:

```csharp
        var parts = s.Slots.Where(kv => kv.Value.L > 0).Select(kv => $"{kv.Key}:{kv.Value.L.ToString(CultureInfo.InvariantCulture)}");
        if (s.Relics.Contains("linh_van_khac")) parts = parts.Append($"{ForgeRelicData.LinhVanKey}:1");
        p.ForgeLevels = string.Join(';', parts);
```

- [ ] **Step 5: Chạy `~ForgeRelicTests` và `~Forge`** → PASS (test Lò Rèn cũ không đổi vì `Mods.None` giữ hành vi cũ).

- [ ] **Step 6: Checkpoint.**

---

### Task 2: Chỉ số + nhát rèn + Hồi Thiên dùng bảo vật

**Files:** Modify `Game/GameService.cs:2131-2132`, `Game/GameService.Forge.cs`; Test `test/GemSystemTests/ForgeRelicTests.cs` (thêm lớp `ForgeRelicServiceTests` cùng file)

- [ ] **Step 1: Test thất bại** — thêm cuối file `ForgeRelicTests.cs`:

```csharp
public class ForgeRelicServiceTests : IDisposable
{
    private readonly VanDao.Data.GameDbContext db = new(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<VanDao.Data.GameDbContext>()
        .UseSqlite("Data Source=:memory:").Options);
    public void Dispose() => db.Dispose();
    private sealed class FixedRandom(double v) : Random { public override double NextDouble() => v; }

    private async Task<(GameService Game, VanDao.Data.Player P)> Setup(params string[] relics)
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Thợ rèn");
        var weapon = GameData.AllItems.First(i => i.Slot == "weapon" && i.MinRealm == 0);
        p.Equipment.RemoveAll(e => e.Slot == "weapon");
        p.Equipment.Add(new VanDao.Data.EquippedItem { PlayerId = p.Id, Slot = "weapon", ItemId = weapon.Id });
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

    [Fact]
    public async Task Linh_van_raises_forged_stats()
    {
        var (_, p) = await Setup();
        var strong = GameData.AllItems.Where(i => i.Slot == "weapon" && i.MinRealm == 0).MaxBy(i => i.Atk)!;   // Công đủ lớn để ×1,1 không bị làm tròn mất
        p.Equipment.RemoveAll(e => e.Slot == "weapon");
        p.Equipment.Add(new VanDao.Data.EquippedItem { PlayerId = p.Id, Slot = "weapon", ItemId = strong.Id });
        var s = ForgeData.Load(p); s.Slots["weapon"] = new ForgeData.SlotState { L = 99 }; ForgeData.Store(p, s);
        var plain = GameService.Stats(p, buffs: false).Atk;
        s.Relics = ["linh_van_khac"]; ForgeData.Store(p, s);
        Assert.True(GameService.Stats(p, buffs: false).Atk > plain);
    }
}
```

Thêm `using Microsoft.EntityFrameworkCore;` ở đầu file nếu `UseSqlite`/`OpenConnection` không nhận.

- [ ] **Step 2: Chạy, xác nhận FAIL** (`ForgeUndo` chưa nhận `Random`).

- [ ] **Step 3: `Stats`** — `GameService.cs:2132` đổi thành:

```csharp
        var forgeMult = ForgeData.BonusMult(forge);                              // Linh Văn Khắc: % chỉ số rèn ×1,1
        decimal SumF(Func<ItemDef, int> f) => items.Sum(i => f(i) * (1 + (decimal)(ForgeData.BonusPct(forge.GetValueOrDefault(i.Slot!)) * forgeMult) / 100));
```

- [ ] **Step 4: `GameService.Forge.cs`**

Trong `ForgeStrike`, sau `var cur = …` thêm `var mods = ForgeRelicData.ModsOf(save.Relics); var rng = random ?? Random.Shared;`. Thay khối trả Tinh Thiết và roll:

```csharp
        p.Add("linh_thach", -stone);
        var outcome = ForgeData.Roll(ForgeData.OddsAt(cur.L, cur.F > 0, mods), rng.NextDouble());
        var ironFree = mods.IronFreeChance > 0 && rng.NextDouble() < mods.IronFreeChance;   // Tiết Thiết Lô
        if (!ironFree) p.Add(ForgeData.Iron, -iron);
        var next = ForgeData.Apply(cur, outcome, mods);
```

(xóa dòng `p.Add(ForgeData.Iron, -iron);` cũ và dòng `var outcome = …` / `var next = …` cũ). Dòng `lit` đổi `3 nhát` thành `{mods.FrenzyStrikes} nhát`. Nhánh Break: thông điệp `về +0` đổi thành `về +{next.L}` (cả 2 nhánh Break).

`ForgeUndo(Player p)` → `ForgeUndo(Player p, Random? random = null)`; thay `p.Add(ForgeData.Rewind, -1);` bằng:

```csharp
        var kept = ForgeRelicData.ModsOf(save.Relics).RewindKeepChance is > 0 and var k && (random ?? Random.Shared).NextDouble() < k;   // Hồi Quang Kính
        if (!kept) p.Add(ForgeData.Rewind, -1);
```

và thông điệp cuối thêm `{(kept ? " Hồi Quang Kính giữ lại phù." : "")}`.

`ForgeStateFor`: `var mods = ForgeRelicData.ModsOf(save.Relics);` — `BonusPct(...)` nhân `mods.BonusMult` (cả 2 chỗ), `OddsAt(s.L, s.F > 0, mods)`.

- [ ] **Step 5: Chạy `~Forge`** (gồm ForgeTests, ForgeServiceTests, ForgeRelic*) → PASS.

- [ ] **Step 6: Checkpoint.**

---

### Task 3: Máy đổi, kệ hôm nay, mua / bỏ / làm mới + DTO + API

**Files:** Create `Game/GameService.ForgeRelic.cs`; Modify `Game/Dtos.cs`, `Game/GameService.Forge.cs` (`ForgeStateFor`), `Program.cs`; Test thêm vào `ForgeRelicServiceTests`

- [ ] **Step 1: Test thất bại** — thêm vào `ForgeRelicServiceTests`:

```csharp
    private static string ShelfFirst(GameService g, VanDao.Data.Player p) => g.ForgeStateFor(p).Shelf!.First(x => !x.Bought).Id;

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
        var before = game.ForgeStateFor(p).Shelf!.Select(x => x.Id).ToList();
        var stone = p.Count("linh_thach");
        game.ForgeShelfReroll(p);
        Assert.Equal(stone - ForgeRelicData.RerollStone, p.Count("linh_thach"));
        Assert.Equal(1, ForgeData.Load(p).Rerolls);
        Assert.Equal(3, game.ForgeStateFor(p).Shelf!.Count);
        _ = before;   // kệ mới có thể trùng một phần kệ cũ; chỉ kiểm số lần làm mới
    }
```

- [ ] **Step 2: Chạy, xác nhận FAIL.**

- [ ] **Step 3: DTO** — `Game/Dtos.cs`:
  - Cuối file: `public record ForgeShelfDto(string Id, bool Bought, bool Owned);`
  - `ForgeDto(...)` thêm tham số cuối: `, long Points = 0, List<string>? Relics = null, List<ForgeShelfDto>? Shelf = null, long RerollStone = 0, int RelicSlots = 0`
  - `GameDataDto`: thay `double[]? GemUpgradeChances = null);` bằng `double[]? GemUpgradeChances = null, IEnumerable<ForgeRelicData.RelicDef>? ForgeRelics = null, long[]? ForgeSalvagePoints = null);`

- [ ] **Step 4: `Program.cs`** — `/data` (dòng có `GemData.UpgradeChances));`) đổi đuôi thành `GemData.UpgradeChances, ForgeRelicData.All, ForgeRelicData.SalvagePointsByRarity));`. Sau 2 route `forge/strike`, `forge/undo` thêm:

```csharp
        // Bảo vật Lò Rèn: đổi trang bị trong túi lấy Điểm Luyện Khí · mua / bỏ bảo vật · làm mới kệ hôm nay
        api.MapPost("/players/{id:guid}/forge/salvage", (Guid id, string? items, GameService game) => Run(id, game, p => game.ForgeSalvage(p, items)));
        api.MapPost("/players/{id:guid}/forge/relic/buy/{relicId}", (Guid id, string relicId, GameService game) => Run(id, game, p => game.ForgeRelicBuy(p, relicId)));
        api.MapPost("/players/{id:guid}/forge/relic/discard/{relicId}", (Guid id, string relicId, GameService game) => Run(id, game, p => game.ForgeRelicDiscard(p, relicId)));
        api.MapPost("/players/{id:guid}/forge/shelf/reroll", (Guid id, GameService game) => Run(id, game, p => game.ForgeShelfReroll(p)));
```

- [ ] **Step 5: `Game/GameService.ForgeRelic.cs`**

```csharp
using VanDao.Data;
namespace VanDao.Game;

/// <summary>Bảo vật Lò Rèn: máy đổi trang bị → Điểm Luyện Khí, kệ hôm nay (giờ VN), kệ bảo vật 4 ô.</summary>
public partial class GameService
{
    /// <summary>Sang ngày mới (giờ VN): số lần làm mới và món đã mua về 0.</summary>
    private static string ForgeShopRoll(ForgeData.ForgeSave s)
    {
        var day = LoginDayOf(DateTimeOffset.UtcNow);
        if (s.ShopDay != day) { s.ShopDay = day; s.Rerolls = 0; s.Bought.Clear(); }
        return day;
    }

    private static List<string> ForgeShelfOf(Player p, ForgeData.ForgeSave s) =>
        ForgeRelicData.Shelf(p.Id, s.ShopDay, s.Rerolls, s.Relics, s.Bought);

    /// <summary>items: id trang bị cách nhau dấu phẩy, lặp lại để đổi nhiều cái cùng loại.</summary>
    public (string msg, string kind) ForgeSalvage(Player p, string? items)
    {
        var ids = (items ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (ids.Count == 0) throw new GameException("Chưa chọn trang bị để đổi.");
        long points = 0;
        foreach (var g in ids.GroupBy(x => x))
        {
            var d = GameData.ItemById.GetValueOrDefault(g.Key);
            var each = d is null ? 0 : ForgeRelicData.SalvagePoints(d);
            if (each <= 0) throw new GameException("Máy đổi chỉ nhận trang bị.");
            if (p.Count(g.Key) < g.Count()) throw new GameException($"Không đủ {d!.Name} trong hành trang.");
            points += each * g.Count();
        }
        foreach (var g in ids.GroupBy(x => x)) p.Add(g.Key, -g.Count());
        var s = ForgeData.Load(p);
        s.Points += points;
        ForgeData.Store(p, s);
        return ($"Máy đổi: {ids.Count} trang bị → +{points:N0} Điểm Luyện Khí (còn {s.Points:N0}).", "good");
    }

    public (string msg, string kind) ForgeRelicBuy(Player p, string relicId)
    {
        var def = ForgeRelicData.ById.GetValueOrDefault(relicId) ?? throw new GameException("Bảo vật không tồn tại.");
        var s = ForgeData.Load(p);
        ForgeShopRoll(s);
        if (!ForgeShelfOf(p, s).Contains(relicId)) throw new GameException("Kệ hôm nay không có bảo vật này.");
        if (s.Bought.Contains(relicId)) throw new GameException("Hôm nay đã mua bảo vật này.");
        if (s.Relics.Contains(relicId)) throw new GameException($"Kệ bảo vật đã có {def.Name}.");
        if (s.Relics.Count >= ForgeRelicData.Slots) throw new GameException($"Kệ bảo vật đã đủ {ForgeRelicData.Slots} món: bỏ bớt 1 món trước.");
        if (s.Points < def.Price) throw new GameException($"Cần {def.Price:N0} Điểm Luyện Khí (đang có {s.Points:N0}).");
        s.Points -= def.Price;
        s.Relics.Add(relicId);
        s.Bought.Add(relicId);
        ForgeData.Store(p, s);
        return Log(p, $"Lò Rèn: đặt {def.Name} lên kệ bảo vật ({def.Desc})", "gold");
    }

    public (string msg, string kind) ForgeRelicDiscard(Player p, string relicId)
    {
        var s = ForgeData.Load(p);
        if (!s.Relics.Remove(relicId)) throw new GameException("Kệ bảo vật không có món này.");
        ForgeData.Store(p, s);
        return ($"Đã bỏ {ForgeRelicData.ById.GetValueOrDefault(relicId)?.Name ?? relicId} khỏi kệ bảo vật.", "");
    }

    public (string msg, string kind) ForgeShelfReroll(Player p)
    {
        if (p.Count("linh_thach") < ForgeRelicData.RerollStone)
            throw new GameException($"Cần {ForgeRelicData.RerollStone:N0} linh thạch để làm mới kệ.");
        var s = ForgeData.Load(p);
        ForgeShopRoll(s);
        p.Add("linh_thach", -ForgeRelicData.RerollStone);
        s.Rerolls++;
        s.Bought.Clear();
        ForgeData.Store(p, s);
        return ($"Làm mới kệ bảo vật (−{ForgeRelicData.RerollStone:N0} linh thạch).", "");
    }
}
```

- [ ] **Step 6: `ForgeStateFor`** — trước `return`, thêm (không ghi ngày vào save khi chỉ đọc):

```csharp
        var day = LoginDayOf(DateTimeOffset.UtcNow);
        var sameDay = save.ShopDay == day;
        var bought = sameDay ? save.Bought : new List<string>();
        var shelf = ForgeRelicData.Shelf(p.Id, day, sameDay ? save.Rerolls : 0, save.Relics, bought)
            .Select(id => new ForgeShelfDto(id, bought.Contains(id), save.Relics.Contains(id))).ToList();
```

và `return new ForgeDto(slots, save.Undo is not null, log, ForgeData.MaxLevel, ForgeData.FrenzyCharge, save.Points, save.Relics.ToList(), shelf, ForgeRelicData.RerollStone, ForgeRelicData.Slots);`

- [ ] **Step 7: Chạy `~Forge`** → PASS. Build `VanDao.csproj` với OutDir → 0 lỗi.

- [ ] **Step 8: Checkpoint.**

---

### Task 4: Giao diện

**Files:** Modify `wwwroot/js/forge.js`, `wwwroot/css/forge.css`, `wwwroot/js/i18n-en.js`; Test `test/forge.test.cjs`

- [ ] **Step 1: Test JS thất bại** — thêm vào `test/forge.test.cjs`:

```js
test('salvage list keeps only bag gear with points per rarity', () => {
  const ctx = vm.createContext({});
  vm.runInContext(pure + `;globalThis.r = forgeSalvageRows({a:2,b:1,herb:9,c:0},
    {a:{name:'Kiếm',slot:'weapon',base:'k',rarity:'rare'},b:{name:'Giáp',slot:'body',base:'g',rarity:'common'},herb:{name:'Cỏ'},c:{name:'Nón',slot:'head',base:'n',rarity:'rare'}},
    ['common','uncommon','rare'], [1,2,4])`, ctx);
  const rows = JSON.parse(JSON.stringify(ctx.r));
  assert.deepEqual(rows.map(x => [x.id, x.n, x.each]), [['a',2,4],['b',1,1]]);
});
```

Run `node --test --test-reporter=dot test/forge.test.cjs` → FAIL.

- [ ] **Step 2: `forge.js`** — thêm vào khối hàm thuần (trước `function renderForge(`):

```js
/** Dòng máy đổi: trang bị trong túi (có slot + base), số lượng > 0, điểm mỗi món theo phẩm; phẩm cao trước. */
function forgeSalvageRows(inv, items, rarities, points){
  return Object.entries(inv || {}).filter(([k, n]) => n > 0 && items[k]?.slot && items[k]?.base)
    .map(([k, n]) => ({id: k, name: items[k].name, n, each: points[rarities.indexOf(items[k].rarity)] || 0}))
    .filter(r => r.each > 0).sort((a, b) => b.each - a.each || a.name.localeCompare(b.name));
}
```

Hằng và trạng thái (cạnh `let forgeSlot…`):

```js
let forgeDiscardArm = null;   // bảo vật đang chờ bấm "Bỏ" lần 2
const forgeRelicDef = id => (DATA.forgeRelics || []).find(r => r.id === id) || {id, name: id, desc: '', price: 0};
```

Trong `renderForge()`, ngay sau `${forgeAnvil(...)}` thêm dải kệ:

```js
      <div class="fg-relics" aria-label="Kệ bảo vật">${Array.from({length: F.relicSlots || 4}, (_, i) => {
        const id = (F.relics || [])[i]; if (!id) return '<div class="fg-relic empty">Ô trống</div>';
        const r = forgeRelicDef(id);
        return `<div class="fg-relic"><b>${r.name}</b><small>${r.desc}</small>
          <button type="button" data-fg-discard="${id}">${forgeDiscardArm === id ? 'Bỏ thật?' : 'Bỏ'}</button></div>`;
      }).join('')}</div>
      <button type="button" class="fg-shop-open" data-fg="shop">Máy đổi bảo vật · ${fmt(F.points || 0)} điểm</button>
```

Hàm cửa sổ (cạnh `openForgeLog`):

```js
function openForgeShop(){
  let dlg = $('forgeShop');
  if (!dlg){ dlg = document.createElement('dialog'); dlg.id = 'forgeShop'; dlg.className = 'fg-dialog fg-shop'; document.body.appendChild(dlg); }
  const F = S.forge, rows = forgeSalvageRows(S.inventory, ITEM, FUSE_R, DATA.forgeSalvagePoints || []);
  const full = (F.relics || []).length >= (F.relicSlots || 4);
  dlg.innerHTML = `<header><div><h3>Máy đổi bảo vật</h3><p>Điểm Luyện Khí: <b>${fmt(F.points || 0)}</b></p></div>
      <button type="button" data-fg="shop-close" aria-label="Đóng máy đổi">×</button></header>
    <div class="fg-shop-body">
      <section aria-label="Đổi trang bị"><h4>Trang bị trong túi</h4>${rows.map(r => `<div class="fg-salv">
        <span>${r.name} <em>×${fmt(r.n)}</em></span><small>${fmt(r.each)} điểm/món</small>
        <button type="button" data-fg-salv="${r.id}" data-n="1">Đổi 1</button>
        <button type="button" data-fg-salv="${r.id}" data-n="${r.n}">Đổi hết</button></div>`).join('') || '<p>Không có trang bị nào trong túi để đổi.</p>'}</section>
      <section aria-label="Kệ hôm nay"><h4>Kệ hôm nay</h4>${(F.shelf || []).map(o => { const r = forgeRelicDef(o.id);
        return `<div class="fg-offer"><b>${r.name}</b><small>${r.desc}</small><span>${fmt(r.price)} điểm</span>
          ${o.bought ? '<em>Đã mua</em>' : `<button type="button" data-fg-buy="${o.id}" ${full || (F.points || 0) < r.price ? 'disabled' : ''}>Mua</button>`}</div>`; }).join('')}
        ${full ? '<p class="fg-warn">Kệ bảo vật đã đủ: bỏ bớt 1 món trước khi mua.</p>' : ''}
        <button type="button" class="fg-reroll" data-fg="reroll">Làm mới · ${fmt(F.rerollStone || 0)} linh thạch</button></section>
    </div>`;
  if (!dlg.open) dlg.showModal();
}
```

Cuối `renderForge()`, cạnh dòng `if ($('forgeLog')?.open) openForgeLog();` thêm `if ($('forgeShop')?.open) openForgeShop();`.

Trong trình xử lý click, trước dòng `const b = e.target.closest('[data-fg]'); if (!b) return;` thêm:

```js
  const salv = e.target.closest('[data-fg-salv]');
  if (salv){ await act(`forge/salvage?items=${encodeURIComponent(Array(+salv.dataset.n).fill(salv.dataset.fgSalv).join(','))}`); renderForge(); return; }
  const buy = e.target.closest('[data-fg-buy]');
  if (buy){ await act(`forge/relic/buy/${buy.dataset.fgBuy}`); renderForge(); return; }
  const dis = e.target.closest('[data-fg-discard]');
  if (dis){
    const id = dis.dataset.fgDiscard;
    if (forgeDiscardArm !== id){ forgeDiscardArm = id; renderForge(); return; }
    forgeDiscardArm = null; await act(`forge/relic/discard/${id}`); renderForge(); return;
  }
```

và sau dòng `if (b.dataset.fg === 'log-close') …` thêm:

```js
  if (b.dataset.fg === 'shop') return openForgeShop();
  if (b.dataset.fg === 'shop-close') return $('forgeShop')?.close();
  if (b.dataset.fg === 'reroll'){ await act('forge/shelf/reroll'); renderForge(); return; }
```

Lưu ý: "Đổi hết" với số lượng rất lớn tạo URL dài — chặn tối đa 500 món mỗi lần: `data-n="${Math.min(r.n, 500)}"`.

- [ ] **Step 3: `forge.css`** — nối cuối file:

```css
/* Kệ bảo vật + máy đổi */
.fg-relics{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:8px}
.fg-relic{display:flex;flex-direction:column;gap:4px;min-height:78px;padding:8px 10px;border:1px solid #6b5a2c;border-radius:10px;background:var(--ink-2)}
.fg-relic b{color:var(--gold);font-size:13px}.fg-relic small{color:var(--muted);font-size:11px;line-height:1.3;flex:1}
.fg-relic button{align-self:flex-start;min-height:28px;padding:0 10px;border-radius:6px;border:1px solid var(--line);background:transparent;color:var(--bad);cursor:pointer;font-size:12px}
.fg-relic.empty{align-items:center;justify-content:center;border-style:dashed;border-color:var(--line);color:var(--muted);font-size:12px}
.fg-shop-open{min-height:44px;border-radius:10px;border:1px solid #6b5a2c;background:#21190d;color:var(--gold);font-weight:600;cursor:pointer}
.fg-shop{width:min(860px,calc(100vw - 32px))}
.fg-shop-body{display:flex;flex-wrap:wrap;gap:16px;padding:14px 18px 18px;max-height:66vh;overflow-y:auto}
.fg-shop-body>section{flex:1 1 300px;min-width:0;display:flex;flex-direction:column;gap:8px}
.fg-salv{display:grid;grid-template-columns:minmax(0,1fr) auto auto;gap:4px 8px;align-items:center;padding:8px 0;border-bottom:1px solid #1f2c30}
.fg-salv small{grid-column:1;color:var(--muted)}.fg-salv em{font-style:normal;color:var(--muted)}
.fg-salv button,.fg-offer button,.fg-reroll{min-height:36px;padding:0 12px;border-radius:8px;border:1px solid var(--line);background:var(--ink);color:var(--jade);font-weight:600;cursor:pointer}
.fg-salv button:nth-of-type(1){grid-row:1/3;grid-column:2}.fg-salv button:nth-of-type(2){grid-row:1/3;grid-column:3}
.fg-offer{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:2px 10px;padding:10px 12px;border:1px solid #6b5a2c;border-radius:10px;background:var(--ink)}
.fg-offer b{color:var(--gold)}.fg-offer small{grid-column:1;color:var(--muted)}.fg-offer span{grid-column:1;font-weight:600}
.fg-offer button,.fg-offer em{grid-row:1/4;grid-column:2;align-self:center}.fg-offer em{font-style:normal;color:var(--muted)}
.fg-warn{margin:0;color:var(--bad)}
.fg-shop button:disabled{opacity:.42;cursor:not-allowed}
@media (max-width:560px){.fg-relics{grid-template-columns:repeat(2,minmax(0,1fr))}}
```

- [ ] **Step 4: i18n** — Grep từng khóa trước khi thêm (khóa đã có thì bỏ qua): `"Kệ bảo vật"`, `"Máy đổi bảo vật"`, `"Điểm Luyện Khí"`, `"Trang bị trong túi"`, `"Kệ hôm nay"`, `"Đã mua"`, `"Mua"`, `"Bỏ"`, `"Bỏ thật?"`, `"Đổi 1"`, `"Đổi hết"`, `"Không có trang bị nào trong túi để đổi."`, `"Kệ bảo vật đã đủ: bỏ bớt 1 món trước khi mua."`, và 10 tên bảo vật (Ấn Hộ Binh → "Weapon Guard Seal", Kim Cương Chùy → "Diamond Hammer", Thiên Cơ Đồ → "Heavenly Chart", Bàn Thạch Đe → "Bedrock Anvil", Hỏa Tinh Thạch → "Fire Crystal", Phượng Vũ → "Phoenix Feather", Tiết Thiết Lô → "Iron-Saving Furnace", Hồi Quang Kính → "Rewind Mirror", Linh Văn Khắc → "Spirit Rune Carving", Bất Diệt Hồn → "Undying Soul"). Theo đúng định dạng dòng có sẵn trong file; `node --check wwwroot/js/i18n-en.js`.

- [ ] **Step 5: Chạy** `node --test --test-reporter=dot test/forge.test.cjs test/quick-actions.test.cjs` → PASS.

- [ ] **Step 6: Kiểm tra trình duyệt** (controller làm, cổng 5191, save thật — **không bấm Mua / Đổi / Làm mới thật**; chỉ dùng dữ liệu giả gán vào `S.forge` trong trình duyệt): kệ 4 ô hiện đúng, nút Bỏ 2 lần, cửa sổ máy đổi hiện danh sách trang bị trong túi và kệ hôm nay, console không lỗi.

- [ ] **Step 7: Checkpoint.**

---

### Task 5: Ghi chú phát hành

- [ ] Thêm vào mục **🔨 Lò Rèn** trong `docs/releases/upcoming.md` (skill `release-notes`): máy đổi (2^phẩm điểm mỗi món), kệ hôm nay 3 món, làm mới 20.000 linh thạch, kệ bảo vật 4 ô vĩnh viễn, bảng 10 bảo vật (tên · hiệu ứng · điểm) lấy từ `ForgeRelicData.All`.
- [ ] Chạy cuối: `dotnet test test/GemSystemTests --nologo -v q -clp:ErrorsOnly "-p:OutDir=<scratchpad>\out-final\" --logger "console;verbosity=minimal"` và test JS ở Task 4.

## Tự rà soát
- Spec → task: máy đổi (T3), kệ hôm nay + làm mới (T3), kệ 4 ô + bỏ (T3, T4), 10 hiệu ứng (T1 tỉ lệ / Hỏa Khí / Thần Hỏa / vỡ nửa; T2 Tinh Thiết, Hồi Thiên, chỉ số; T1 Linh Văn qua dấu), lưu JSON không migration (T1), giao diện (T4), ghi chú (T5).
- Tên thống nhất: `ForgeRelicData.{All,ById,Mods,ModsOf,SalvagePoints,SalvagePointsByRarity,Shelf,Slots,ShelfSize,RerollStone,LinhVanKey}`, `ForgeData.{OddsAt,Apply,BonusMult}`, `GameService.{ForgeSalvage,ForgeRelicBuy,ForgeRelicDiscard,ForgeShelfReroll,ForgeUndo(p, random)}`, `ForgeShelfDto(Id,Bought,Owned)`.
