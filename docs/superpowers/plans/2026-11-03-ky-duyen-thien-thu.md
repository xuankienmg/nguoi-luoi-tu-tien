# Kỳ Duyên Thiên Thư Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Thêm sự kiện xuyên suốt game "Kỳ Duyên Thiên Thư": mỗi ngày 4 khung giờ cố định mở một kỳ duyên, có thông báo khi xuất hiện, người chơi chọn một trong hai lối để gom trang Thiên Thư; đủ một quyển thì nhận chỉ số cộng vĩnh viễn, đủ 10 quyển thì mở kết thúc theo Đạo Tâm.

**Architecture:** Luật thuần nằm trong `Game/KyDuyen.cs` (lịch khung giờ, chọn kỳ duyên, cộng trang, tổng thưởng) và `Game/KyDuyen.Data.cs` (10 quyển, 8 kỳ duyên, 3 kết thúc), không đụng DB nên test trực tiếp được. `Game/GameService.KyDuyen.cs` nối luật vào nhân vật: xử lý lựa chọn và trả state cho client. Giờ dùng `EventNow` (giờ máy chủ, giống sự kiện Thiên Cơ) để không chỉnh đồng hồ máy được. Client có tab mới "Kỳ duyên"; khi `state.kyDuyen.open` xuất hiện với khóa mới thì hiện toast trên dải game và báo host desktop hiện bóng thông báo ở khay hệ thống.

**Tech Stack:** C# / ASP.NET Core minimal API, EF Core (SQLite cục bộ + PostgreSQL online), xUnit, JavaScript thuần trên WebView2, WinForms (khay hệ thống).

**Quyết định đã chốt với người dùng:**
- Khung giờ (giờ Việt Nam): 08:00, 12:00, 19:00, 22:00; mỗi khung mở 60 phút.
- Thông báo: toast trong game + bóng thông báo ở khay hệ thống khi dải game đang ẩn.
- Bỏ lỡ khung nào thì mất kỳ duyên khung đó, không tích lại.
- Làm đủ 10 quyển ngay đợt đầu.

**Luật chi tiết:**
- Mỗi khung giờ, mỗi nhân vật gặp đúng 1 kỳ duyên (chọn theo hạt giống `Id nhân vật + giờ bắt đầu khung`, nên tải lại trang vẫn ra cùng kỳ duyên) và chỉ xử lý được 1 lần.
- Phải online (có `EventNow`), đã chọn khởi đầu, và không bị `EventBlocked` (save cũ / admin mode bị đánh dấu).
- Mỗi lối có: tỉ lệ thành công, giá linh thạch (có thể 0), kết quả thắng và (nếu tỉ lệ < 100%) kết quả thua. Kết quả gồm số trang, thay đổi Đạo Tâm, vật phẩm, linh thạch, phút suy yếu (dùng `DebuffUntil` có sẵn).
- Linh thạch tính theo đơn vị `StoneUnit(realm) = 50 × 5^realm` (hằng số chỉnh cân bằng sau).
- Trang vào quyển thấp nhất chưa đủ trong các quyển đã mở (quyển i mở khi cảnh giới ≥ i), dư thì sang quyển kế. Mỗi quyển 9 trang. Không còn quyển nào nhận được thì trang mất, vật phẩm và Đạo Tâm vẫn nhận.
- Đạo Tâm giới hạn −99…+99. Đủ 10 quyển: Đạo Tâm ≥ 10 → kết thúc Chính, ≤ −10 → Tà, còn lại → Trung. Kết thúc đi theo Đạo Tâm hiện tại nên vẫn đổi được về sau.
- "Tu lại từ đầu" xóa hết tiến độ Thiên Thư và Đạo Tâm.

---

## File Structure

| File | Việc | Trách nhiệm |
|---|---|---|
| `Data/Entities.cs` | Sửa | 3 cột mới trên `Player`: `KyDuyenKey`, `ThienThu`, `DaoTam` |
| `Data/GameDbContext.cs` | Sửa | Thêm cột cho save SQLite cũ |
| `Online/Migrations/*_OnlineKyDuyen.cs` | Tạo (sinh bằng `dotnet ef`) | Thêm cột cho DB online |
| `Game/KyDuyen.cs` | Tạo | Lịch khung giờ, chọn kỳ duyên, đọc/ghi trang, tổng thưởng |
| `Game/KyDuyen.Data.cs` | Tạo | Dữ liệu 10 quyển, 8 kỳ duyên, 3 kết thúc |
| `Game/GameService.KyDuyen.cs` | Tạo | Xử lý lựa chọn, state cho client |
| `Game/Dtos.cs` | Sửa | DTO kỳ duyên, thêm `KyDuyen` vào `StateDto` |
| `Game/GameService.cs` | Sửa | Gắn state, cộng chỉ số Thiên Thư, nguồn chỉ số, reset, admin |
| `Program.cs` | Sửa | Endpoint `POST /players/{id}/kyduyen?c=` |
| `wwwroot/js/kyduyen.js` | Tạo | Vẽ tab, gửi lựa chọn, thông báo khi xuất hiện |
| `wwwroot/css/kyduyen.css` | Tạo | Giao diện tab và chấm báo |
| `wwwroot/index.html`, `wwwroot/js/main.js`, `wwwroot/js/skill-preview.js`, `wwwroot/js/core.js` | Sửa | Nối tab, pane, render, gọi thông báo |
| `wwwroot/js/i18n-en.js` | Sửa | Chuỗi tiếng Anh |
| `Desktop/GameWindow.cs`, `Desktop/PanelWindow.cs` | Sửa | Bóng thông báo khay hệ thống, tên tab |
| `test/GemSystemTests/KyDuyenTests.cs` | Tạo | Test luật thuần và luồng service |

---

### Task 1: Cột lưu trên Player

**Files:**
- Modify: `Data/Entities.cs:99-101`
- Modify: `Data/GameDbContext.cs:170`
- Modify: `Game/GameService.cs:759` (khối tu lại từ đầu)
- Create: `Online/Migrations/<timestamp>_OnlineKyDuyen.cs` (sinh tự động)

- [ ] **Step 1: Thêm thuộc tính vào `Player`** ngay sau `EventClaimed` trong `Data/Entities.cs`:

```csharp
    public string EventClaimed { get; set; } = "";
    /// <summary>Kỳ duyên Thiên Thư: khóa khung giờ đã xử lý (yyyy-MM-dd:HH), số trang từng quyển (10 số, phẩy), Đạo Tâm (+ Chính / − Tà, −99…99).</summary>
    public string KyDuyenKey { get; set; } = "";
    public string ThienThu { get; set; } = "";
    public int DaoTam { get; set; }
```

- [ ] **Step 2: Thêm cột cho save SQLite cũ** trong `Data/GameDbContext.cs`, nối vào dòng 170 đang có `("EventKey", ...)`:

```csharp
            ("EventKey", "TEXT NOT NULL DEFAULT ''"), ("EventPoints", "INTEGER NOT NULL DEFAULT 0"), ("EventClaimed", "TEXT NOT NULL DEFAULT ''"),
            ("KyDuyenKey", "TEXT NOT NULL DEFAULT ''"), ("ThienThu", "TEXT NOT NULL DEFAULT ''"), ("DaoTam", "INTEGER NOT NULL DEFAULT 0"),
```

- [ ] **Step 3: Xóa tiến độ khi tu lại từ đầu** trong `Game/GameService.cs`, sau dòng `p.GemVault = ""; p.GemSlots = "";`:

```csharp
                p.GemVault = ""; p.GemSlots = "";
                p.KyDuyenKey = ""; p.ThienThu = ""; p.DaoTam = 0;
```

- [ ] **Step 4: Sinh migration online**

Run: `dotnet ef migrations add OnlineKyDuyen --project tools/online_host --output-dir ../../Online/Migrations --namespace VanDao.Online.Migrations`
Expected: tạo `Online/Migrations/<timestamp>_OnlineKyDuyen.cs` có 3 lệnh `AddColumn` (`KyDuyenKey` text, `ThienThu` text, `DaoTam` integer) trên bảng `game.Players`, và cập nhật `GameDbContextModelSnapshot.cs`. Mở file kiểm tra đúng 3 cột, không có thay đổi lạ khác.

- [ ] **Step 5: Build**

Run: `dotnet build`
Expected: Build succeeded, 0 lỗi.

- [ ] **Step 6: Commit**

```bash
git add Data/Entities.cs Data/GameDbContext.cs Game/GameService.cs Online/Migrations
git commit -m "feat: save fields for Kỳ Duyên Thiên Thư"
```

---

### Task 2: Lịch khung giờ cố định

**Files:**
- Create: `Game/KyDuyen.cs`
- Test: `test/GemSystemTests/KyDuyenTests.cs`

- [ ] **Step 1: Viết test lỗi** — tạo `test/GemSystemTests/KyDuyenTests.cs`:

```csharp
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class KyDuyenTests
{
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);
    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, Vn);

    [Fact]
    public void SlotsOpenAtFixedHoursForOneHour()
    {
        Assert.Equal("2026-11-03:08", KyDuyen.At(At(3, 8))!.Key);
        Assert.Equal("2026-11-03:08", KyDuyen.At(At(3, 8, 59))!.Key);
        Assert.Null(KyDuyen.At(At(3, 9)));
        Assert.Null(KyDuyen.At(At(3, 7, 59)));
        Assert.Equal("2026-11-03:12", KyDuyen.At(At(3, 12, 30))!.Key);
        Assert.Equal("2026-11-03:19", KyDuyen.At(At(3, 19, 1))!.Key);
        Assert.Equal("2026-11-03:22", KyDuyen.At(At(3, 22, 45))!.Key);
        Assert.Equal(At(3, 9), KyDuyen.At(At(3, 8, 10))!.End);
    }

    [Fact]
    public void SlotsFollowVietnamTimeWhateverTheOffset()
    {
        // 01:30 UTC = 08:30 giờ Việt Nam
        Assert.Equal("2026-11-03:08", KyDuyen.At(new DateTimeOffset(2026, 11, 3, 1, 30, 0, TimeSpan.Zero))!.Key);
    }

    [Fact]
    public void NextSlotIsTheFollowingFixedHour()
    {
        Assert.Equal(At(3, 12), KyDuyen.Next(At(3, 9)).Start);
        Assert.Equal(At(3, 12), KyDuyen.Next(At(3, 8, 30)).Start);
        Assert.Equal(At(4, 8), KyDuyen.Next(At(3, 22, 30)).Start);
        Assert.Equal(At(3, 8), KyDuyen.Next(At(3, 0)).Start);
    }
}
```

- [ ] **Step 2: Chạy test để thấy lỗi**

Run: `dotnet test test/GemSystemTests --filter KyDuyenTests`
Expected: FAIL khi build, lỗi "The name 'KyDuyen' does not exist".

- [ ] **Step 3: Viết lịch** — tạo `Game/KyDuyen.cs`:

```csharp
using System.Globalization;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>Kỳ duyên Thiên Thư: mỗi ngày 4 khung giờ cố định (giờ Việt Nam), mỗi khung mở 60 phút một kỳ duyên.
/// Bỏ lỡ khung nào thì mất kỳ duyên khung đó. Chọn một trong hai lối để nhận trang Thiên Thư, vật phẩm và Đạo Tâm.</summary>
public static partial class KyDuyen
{
    public static readonly TimeSpan Vn = TimeSpan.FromHours(7);
    public static readonly int[] Hours = [8, 12, 19, 22];
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public record Slot(string Key, DateTimeOffset Start, DateTimeOffset End);

    /// <summary>Khung bắt đầu lúc hour giờ ngày day (giờ Việt Nam).</summary>
    public static Slot Make(DateTime day, int hour)
    {
        var start = new DateTimeOffset(day.Year, day.Month, day.Day, hour, 0, 0, Vn);
        return new($"{start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}:{hour:00}", start, start + Window);
    }

    /// <summary>Khung đang mở lúc now (null = ngoài khung).</summary>
    public static Slot? At(DateTimeOffset now)
    {
        var day = now.ToOffset(Vn).Date;
        foreach (var h in Hours)
        {
            var s = Make(day, h);
            if (now >= s.Start && now < s.End) return s;
        }
        return null;
    }

    /// <summary>Khung kế tiếp bắt đầu sau now.</summary>
    public static Slot Next(DateTimeOffset now)
    {
        var day = now.ToOffset(Vn).Date;
        foreach (var h in Hours) { var s = Make(day, h); if (s.Start > now) return s; }
        return Make(day.AddDays(1), Hours[0]);
    }
}
```

- [ ] **Step 4: Chạy test để thấy qua**

Run: `dotnet test test/GemSystemTests --filter KyDuyenTests`
Expected: PASS 3 test.

- [ ] **Step 5: Commit**

```bash
git add Game/KyDuyen.cs test/GemSystemTests/KyDuyenTests.cs
git commit -m "feat: fixed daily slots for Kỳ Duyên"
```

---

### Task 3: Dữ liệu 10 quyển, 8 kỳ duyên, 3 kết thúc

**Files:**
- Create: `Game/KyDuyen.Data.cs`
- Test: `test/GemSystemTests/KyDuyenTests.cs`

- [ ] **Step 1: Viết test lỗi** — thêm vào cuối class `KyDuyenTests`:

```csharp
    [Fact]
    public void DataHasTenBooksAndValidEncounters()
    {
        Assert.Equal(10, KyDuyen.Books.Length);
        Assert.Equal(GameData.Realms.Count, KyDuyen.Books.Length);
        Assert.Equal(8, KyDuyen.Encounters.Length);
        Assert.Equal(KyDuyen.Encounters.Length, KyDuyen.Encounters.Select(e => e.Id).Distinct().Count());
        foreach (var e in KyDuyen.Encounters)
        foreach (var c in new[] { e.A, e.B })
        {
            Assert.InRange(c.Chance, 0.01, 1);
            Assert.Equal(c.Chance < 1, c.Lose is not null);
            foreach (var o in new[] { c.Win, c.Lose }.OfType<KyDuyen.Outcome>())
            foreach (var (item, qty) in o.Items)
            {
                Assert.True(GameData.ItemById.ContainsKey(item), $"{e.Id}: thiếu vật phẩm {item}");
                Assert.True(qty > 0);
            }
        }
    }
```

- [ ] **Step 2: Chạy test để thấy lỗi**

Run: `dotnet test test/GemSystemTests --filter KyDuyenTests`
Expected: FAIL khi build, lỗi "'KyDuyen' does not contain a definition for 'Books'".

- [ ] **Step 3: Viết dữ liệu** — tạo `Game/KyDuyen.Data.cs`:

```csharp
namespace VanDao.Game;

public static partial class KyDuyen
{
    public const int PagesPerBook = 9;
    public const int DaoTamMax = 99, PathAt = 10;

    /// <summary>Chỉ số cộng vĩnh viễn (%; Crit là điểm % bạo kích).</summary>
    public record Bonus(double AtkPct = 0, double DefPct = 0, double HpPct = 0, double QiPct = 0, double Crit = 0)
    {
        public static readonly Bonus None = new();
        public static Bonus operator +(Bonus a, Bonus b) => new(a.AtkPct + b.AtkPct, a.DefPct + b.DefPct, a.HpPct + b.HpPct, a.QiPct + b.QiPct, a.Crit + b.Crit);
        public string Text() => string.Join(", ", new[]
        {
            AtkPct > 0 ? $"+{AtkPct:0.#}% công" : "", DefPct > 0 ? $"+{DefPct:0.#}% thủ" : "", HpPct > 0 ? $"+{HpPct:0.#}% máu" : "",
            QiPct > 0 ? $"+{QiPct:0.#}% tốc độ tu luyện" : "", Crit > 0 ? $"+{Crit:0.#}% bạo kích" : "",
        }.Where(s => s.Length > 0));
    }
    public record Book(string Name, string Lore, Bonus Bonus);
    /// <summary>Kết quả một lối: trang Thiên Thư, Đạo Tâm, vật phẩm, linh thạch (× StoneUnit), phút suy yếu, lời kể.</summary>
    public record Outcome(int Pages, int DaoTam, (string Item, int Qty)[] Items, int Stones, int DebuffMinutes, string Text);
    /// <summary>Một lối chọn: Cost linh thạch (× StoneUnit) trả trước, rồi Chance ra Win, không thì Lose.</summary>
    public record Choice(string Label, double Chance, int Cost, Outcome Win, Outcome? Lose = null);
    public record Encounter(string Id, string Title, string Text, Choice A, Choice B);
    public record Ending(string Name, string Text, Bonus Bonus);

    private static Outcome O(int pages, int dao, string text, int stones = 0, int debuff = 0, params (string, int)[] items) => new(pages, dao, items, stones, debuff, text);

    /// <summary>Quyển i mở khi cảnh giới ≥ i.</summary>
    public static readonly Book[] Books =
    [
        new("Quyển Nhất · Khí Hải", "Trang đầu Thiên Thư kể chuyện thuở hỗn độn, linh khí tụ thành biển, người đầu tiên học cách hít thở cùng trời đất.", new(QiPct: 3)),
        new("Quyển Nhị · Đạo Cơ", "Thiên Thư chép rằng nền móng của đạo không xây bằng đá mà bằng ý chí; ai giữ được tâm thì cơ không sụp.", new(HpPct: 2)),
        new("Quyển Tam · Kim Đan", "Một vị chân nhân vô danh ngưng tụ kim đan giữa sấm sét, để lại dòng chữ: kiên nhẫn là lửa luyện đan tốt nhất.", new(AtkPct: 2)),
        new("Quyển Tứ · Nguyên Anh", "Nguyên anh rời thân dạo chơi tam giới; Thiên Thư cảnh báo kẻ quên đường về sẽ hóa cô hồn.", new(DefPct: 2)),
        new("Quyển Ngũ · Hóa Thần", "Ở cảnh giới này, Thiên Thư lần đầu nhắc đến kẻ đã xé nó thành mười quyển: một người từng đứng trên đỉnh tiên đạo.", new(Crit: 1)),
        new("Quyển Lục · Luyện Hư", "Hư không không phải trống rỗng; Thiên Thư vẽ lại bản đồ những vết nứt mà kẻ xé sách đã đi qua.", new(QiPct: 3)),
        new("Quyển Thất · Hợp Thể", "Kẻ xé sách muốn hợp nhất với Thiên Đạo nhưng bị chính đạo tâm phản phệ; mười quyển văng khắp hạ giới.", new(HpPct: 3)),
        new("Quyển Bát · Đại Thừa", "Thiên Thư hé lộ: mỗi người gom đủ các quyển đều phải trả lời một câu hỏi, đạo của ngươi vì ai mà tu?", new(AtkPct: 3)),
        new("Quyển Cửu · Độ Kiếp", "Chín đạo lôi kiếp được ghi tên từng đạo; người thuộc tên lôi thì lôi không đánh nhầm.", new(DefPct: 3)),
        new("Quyển Thập · Chân Tiên", "Trang cuối để trống. Thiên Thư chờ người gom đủ mười quyển tự viết lên đó con đường của mình.", new(Crit: 2)),
    ];

    public static readonly Ending[] Endings =
    [
        new("Chính Đạo · Thiên Thư Hộ Thế", "Ngươi viết lên trang cuối chữ \"Hộ\". Thiên Thư hóa thành lá chắn che chở chúng sinh.", new(DefPct: 5, HpPct: 5)),
        new("Ma Đạo · Thiên Thư Nghịch Mệnh", "Ngươi viết lên trang cuối chữ \"Nghịch\". Thiên Thư bốc cháy thành ngọn lửa đen trong lòng bàn tay.", new(AtkPct: 5, Crit: 2)),
        new("Vô Vi · Thiên Thư Quy Nhất", "Ngươi để trang cuối tiếp tục trống. Thiên Thư khép lại, hòa vào đạo tâm không thiện không ác.", new(AtkPct: 3, DefPct: 3, HpPct: 3)),
    ];

    public static readonly Encounter[] Encounters =
    [
        new("lao_nhan", "Lão nhân bị thương", "Bên đường, một lão nhân ôm vết thương rỉ máu, trong ngực áo lộ ra mép một trang giấy vàng cổ.",
            new("Tặng linh thạch chữa thương", 1, 2, O(2, 3, "Lão nhân cảm tạ, tặng lại trang giấy cổ rồi biến mất trong gió.")),
            new("Đoạt túi trữ vật", .7, 0, O(1, -3, "Ngươi giật được túi trữ vật, bên trong có một trang Thiên Thư và ít linh thạch.", stones: 4),
                O(0, -3, "Lão nhân phất tay áo, một luồng kình lực hất ngươi văng xa.", debuff: 5))),
        new("dong_phu", "Động phủ cổ", "Vách núi sụp lộ ra cửa động phủ phủ đầy rêu, trận pháp bên trong vẫn còn le lói.",
            new("Liều vào sâu", .5, 0, O(2, 0, "Ngươi phá trận vào được mật thất, trên án thư còn hai trang Thiên Thư.", 0, 0, ("ve_tai_xiu", 2)),
                O(0, 0, "Trận pháp phản kích, ngươi phải lết ra ngoài điều tức.", debuff: 5)),
            new("Chép bia đá ngoài cửa", 1, 0, O(1, 0, "Chữ trên bia đá chính là một trang Thiên Thư bị khắc lại."))),
        new("thu_non", "Yêu thú non lạc mẹ", "Một con yêu thú non kêu thảm thiết giữa bụi gai, quanh cổ đeo một cuộn giấy nhỏ.",
            new("Nuôi dưỡng", 1, 0, O(1, 2, "Ngươi chữa thương cho nó; nó dụi đầu vào tay ngươi, nhả ra cuộn giấy.", 0, 0, ("linh_thu_dan", 10))),
            new("Bán cho thương nhân", 1, 0, O(0, -2, "Thương nhân trả giá hời, nhưng cuộn giấy theo con thú đi mất.", stones: 8))),
        new("tan_tu", "Tán tu cầu cứu", "Một tán tu bị ma tu truy đuổi chạy tới, van ngươi ra tay.",
            new("Ra tay cứu", .75, 0, O(2, 3, "Ngươi đánh lui ma tu; tán tu tặng ngươi hai trang giấy cổ giữ mạng."),
                O(0, 1, "Ma tu quá mạnh, hai người cùng trọng thương nhưng tán tu thoát được.", debuff: 5)),
            new("Làm ngơ", 1, 0, O(1, -1, "Ngươi lặng lẽ rời đi, nhặt được một trang giấy rơi trên đường tán tu chạy qua."))),
        new("ma_dao", "Ma đạo truyền thừa", "Trong hang tối, một khối ngọc đen tỏa ma khí, thì thầm hứa trao truyền thừa.",
            new("Tiếp nhận", .8, 0, O(2, -4, "Ma khí nhập thể, trong thức hải hiện lên hai trang Thiên Thư.", 0, 0, ("hoan_cot_dan", 1)),
                O(0, -4, "Ma khí phản phệ, kinh mạch rối loạn.", debuff: 10)),
            new("Hủy truyền thừa", 1, 0, O(1, 4, "Ngọc đen vỡ nát, để lại một trang Thiên Thư bị phong ấn bên trong."))),
        new("linh_tuyen", "Linh tuyền", "Giữa rừng sâu có một mạch linh tuyền sủi bọt, đáy suối lấp lánh ánh vàng.",
            new("Ngồi thiền hấp thu", 1, 0, O(1, 0, "Linh khí thấm vào kinh mạch; dưới đáy suối có một trang Thiên Thư.", stones: 5)),
            new("Đóng bình mang về", 1, 0, O(1, 0, "Ngươi đóng đầy bình linh tuyền, tiện tay vớt được trang giấy dưới đáy.", 0, 0, ("hoa_hinh_dan", 2)))),
        new("thuong_nhan", "Thương nhân bí ẩn", "Một thương nhân che mặt bày bán vài trang giấy vàng, nói là Thiên Thư thất lạc.",
            new("Mua tàn trang", 1, 3, O(2, 0, "Hàng thật. Thương nhân cười khẽ rồi biến mất cùng sạp hàng.")),
            new("Mặc cả", .5, 0, O(2, 0, "Thương nhân chịu thua tài ăn nói của ngươi, tặng luôn hai trang."),
                O(0, 0, "Thương nhân phật ý, cuốn sạp bỏ đi."))),
        new("loi_kiep", "Thiên lôi giáng đỉnh núi", "Mây đen kéo tới, thiên lôi đánh liên hồi lên đỉnh núi gần đó, giữa tia sét thấp thoáng trang giấy cháy sáng.",
            new("Lên đỉnh hứng lôi", .6, 0, O(3, 0, "Ngươi chịu ba đạo lôi, ba trang Thiên Thư rơi vào tay."),
                O(0, 0, "Lôi đánh trúng, toàn thân tê dại.", debuff: 10)),
            new("Chờ lôi tan rồi nhặt", 1, 0, O(1, 0, "Lôi tan, ngươi nhặt được một trang còn nguyên vẹn trong đống tro."))),
    ];
}
```

- [ ] **Step 4: Chạy test để thấy qua**

Run: `dotnet test test/GemSystemTests --filter KyDuyenTests`
Expected: PASS 4 test. Nếu báo "thiếu vật phẩm", kiểm tra id trong `Game/SpinEvent.cs` (các id `ve_tai_xiu`, `linh_thu_dan`, `hoa_hinh_dan`, `hoan_cot_dan` đều đang dùng ở đó).

- [ ] **Step 5: Commit**

```bash
git add Game/KyDuyen.Data.cs test/GemSystemTests/KyDuyenTests.cs
git commit -m "feat: Thiên Thư books, encounters and endings"
```

---

### Task 4: Luật trang, chọn kỳ duyên, tổng thưởng

**Files:**
- Modify: `Game/KyDuyen.cs`
- Test: `test/GemSystemTests/KyDuyenTests.cs`

- [ ] **Step 1: Viết test lỗi** — thêm vào class `KyDuyenTests`:

```csharp
    [Fact]
    public void EncounterIsStablePerPlayerAndSlot()
    {
        var id = Guid.Parse("60000000-0000-0000-0000-000000000001");
        var slot = KyDuyen.At(At(3, 8))!;
        Assert.Same(KyDuyen.Pick(id, slot), KyDuyen.Pick(id, slot));
        var seen = Enumerable.Range(1, 40).Select(d => KyDuyen.Pick(id, KyDuyen.Make(new DateTime(2026, 1, 1).AddDays(d), 12)).Id).Distinct().Count();
        Assert.True(seen >= 5, "40 ngày phải gặp nhiều loại kỳ duyên");
    }

    [Fact]
    public void PagesFillTheLowestOpenBookThenOverflow()
    {
        var pages = new int[KyDuyen.Books.Length];
        pages[0] = 8;
        Assert.Equal(1, KyDuyen.AddPages(pages, realm: 0, 3));   // chỉ quyển 1 mở: 1 trang vào, 2 trang mất
        Assert.Equal(9, pages[0]);
        Assert.Equal(-1, KyDuyen.Target(pages, 0));
        Assert.Equal(3, KyDuyen.AddPages(pages, realm: 2, 3));
        Assert.Equal(3, pages[1]);
        pages[1] = 8;
        Assert.Equal(2, KyDuyen.AddPages(pages, realm: 2, 2));   // 1 trang đủ quyển 2, 1 trang sang quyển 3
        Assert.Equal(9, pages[1]); Assert.Equal(1, pages[2]);
    }

    [Fact]
    public void PagesRoundTripThroughThePlayerField()
    {
        var p = new Player { ThienThu = "9,4,x" };
        var pages = KyDuyen.Pages(p);
        Assert.Equal(new[] { 9, 4, 0, 0, 0, 0, 0, 0, 0, 0 }, pages);
        KyDuyen.SetPages(p, pages);
        Assert.Equal("9,4,0,0,0,0,0,0,0,0", p.ThienThu);
    }

    [Fact]
    public void TotalSumsCompletedBooksAndEndingFollowsDaoTam()
    {
        var p = new Player { ThienThu = "9,9,3" };
        Assert.Equal(KyDuyen.Books[0].Bonus + KyDuyen.Books[1].Bonus, KyDuyen.Total(p));
        Assert.Null(KyDuyen.EndingFor(p));
        var full = new Player { ThienThu = string.Join(',', Enumerable.Repeat(9, 10)), DaoTam = 12 };
        Assert.Same(KyDuyen.Endings[0], KyDuyen.EndingFor(full));
        full.DaoTam = -10; Assert.Same(KyDuyen.Endings[1], KyDuyen.EndingFor(full));
        full.DaoTam = 9; Assert.Same(KyDuyen.Endings[2], KyDuyen.EndingFor(full));
        var books = KyDuyen.Books.Aggregate(KyDuyen.Bonus.None, (a, b) => a + b.Bonus);
        Assert.Equal(books + KyDuyen.Endings[2].Bonus, KyDuyen.Total(full));
    }
```

- [ ] **Step 2: Chạy test để thấy lỗi**

Run: `dotnet test test/GemSystemTests --filter KyDuyenTests`
Expected: FAIL khi build, lỗi "'KyDuyen' does not contain a definition for 'Pick'".

- [ ] **Step 3: Viết luật** — thêm vào class `KyDuyen` trong `Game/KyDuyen.cs` (sau `Next`):

```csharp
    /// <summary>Đơn vị linh thạch theo cảnh giới (giá và thưởng của kỳ duyên nhân với số này).</summary>
    public static int StoneUnit(int realm) => 50 * (int)Math.Pow(5, Math.Clamp(realm, 0, Books.Length - 1));

    /// <summary>Kỳ duyên của nhân vật ở khung này: cố định theo Id + giờ bắt đầu khung, tải lại vẫn ra cùng kỳ duyên.</summary>
    public static Encounter Pick(Guid id, Slot s) =>
        Encounters[new Random(unchecked(id.GetHashCode() * 31 + (int)(s.Start.ToUnixTimeSeconds() / 3600))).Next(Encounters.Length)];

    /// <summary>Lối chọn có thành công không: cố định theo Id + khung + lối.</summary>
    public static bool Roll(Guid id, Slot s, int choice, double chance) =>
        chance >= 1 || new Random(unchecked(id.GetHashCode() * 17 + (int)(s.Start.ToUnixTimeSeconds() / 60) + choice)).NextDouble() < chance;

    public static int[] Pages(Player p)
    {
        var a = new int[Books.Length];
        var parts = p.ThienThu.Split(',');
        for (var i = 0; i < a.Length && i < parts.Length; i++)
            a[i] = int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? Math.Clamp(n, 0, PagesPerBook) : 0;
        return a;
    }
    public static void SetPages(Player p, int[] pages) => p.ThienThu = string.Join(',', pages);

    /// <summary>Quyển nhận trang: quyển thấp nhất chưa đủ trong các quyển đã mở (quyển i mở khi cảnh giới ≥ i). -1 = không còn quyển nào nhận.</summary>
    public static int Target(int[] pages, int realm)
    {
        for (var i = 0; i <= Math.Min(realm, pages.Length - 1); i++) if (pages[i] < PagesPerBook) return i;
        return -1;
    }

    /// <summary>Cộng n trang, dư sang quyển kế. Trả về số trang đã vào sách.</summary>
    public static int AddPages(int[] pages, int realm, int n)
    {
        var added = 0;
        while (n > 0 && Target(pages, realm) is var i and >= 0)
        {
            var take = Math.Min(n, PagesPerBook - pages[i]);
            pages[i] += take; n -= take; added += take;
        }
        return added;
    }

    public static bool Complete(int[] pages) => pages.All(n => n >= PagesPerBook);

    /// <summary>Kết thúc khi đủ 10 quyển, theo Đạo Tâm hiện tại (null = chưa đủ).</summary>
    public static Ending? EndingFor(Player p) =>
        !Complete(Pages(p)) ? null : p.DaoTam >= PathAt ? Endings[0] : p.DaoTam <= -PathAt ? Endings[1] : Endings[2];

    /// <summary>Tổng chỉ số vĩnh viễn: mọi quyển đã đủ + kết thúc (nếu có).</summary>
    public static Bonus Total(Player p)
    {
        var pages = Pages(p);
        var sum = Books.Where((_, i) => pages[i] >= PagesPerBook).Aggregate(Bonus.None, (a, b) => a + b.Bonus);
        return EndingFor(p) is { } e ? sum + e.Bonus : sum;
    }
```

- [ ] **Step 4: Chạy test để thấy qua**

Run: `dotnet test test/GemSystemTests --filter KyDuyenTests`
Expected: PASS 8 test.

- [ ] **Step 5: Commit**

```bash
git add Game/KyDuyen.cs test/GemSystemTests/KyDuyenTests.cs
git commit -m "feat: Thiên Thư page, pick and bonus rules"
```

---

### Task 5: Service xử lý lựa chọn và state

**Files:**
- Create: `Game/GameService.KyDuyen.cs`
- Modify: `Game/Dtos.cs:124` (cuối `StateDto`) và cuối file
- Modify: `Game/GameService.cs:176` (dựng `StateDto`)
- Test: `test/GemSystemTests/KyDuyenTests.cs`

- [ ] **Step 1: Viết test lỗi** — thêm vào đầu file test `using Microsoft.EntityFrameworkCore;`, đổi khai báo class thành `public class KyDuyenTests : IDisposable` và thêm vào class:

```csharp
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public void Dispose() => db.Dispose();

    private async Task<(GameService Game, Player Player)> Setup()
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Kỳ duyên");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Add("linh_thach", 1_000_000);
        return (game, p);
    }

    [Fact]
    public async Task ResolveOncePerSlotAndGivePages()
    {
        var (game, p) = await Setup();
        var now = At(3, 8, 5);
        var slot = KyDuyen.At(now)!;
        var enc = KyDuyen.Pick(p.Id, slot);
        var sure = enc.A.Chance >= 1 ? 0 : enc.B.Chance >= 1 ? 1 : 0;
        var state = game.KyDuyenStateAt(p, now);
        Assert.NotNull(state.Open);
        Assert.Equal(enc.Title, state.Open!.Title);

        game.ResolveKyDuyenAt(p, sure, now);
        Assert.Equal(slot.Key, p.KyDuyenKey);
        Assert.Null(game.KyDuyenStateAt(p, now).Open);
        Assert.Throws<GameException>(() => game.ResolveKyDuyenAt(p, sure, now.AddMinutes(10)));
        Assert.NotNull(game.KyDuyenStateAt(p, At(3, 12, 1)).Open);   // khung kế tiếp mở kỳ duyên mới
    }

    [Fact]
    public async Task NoEncounterOutsideSlotsOrOffline()
    {
        var (game, p) = await Setup();
        Assert.Null(game.KyDuyenStateAt(p, At(3, 10)).Open);
        Assert.Throws<GameException>(() => game.ResolveKyDuyenAt(p, 0, At(3, 10)));
        Assert.Throws<GameException>(() => game.ResolveKyDuyenAt(p, 0, null));
        Assert.Throws<GameException>(() => game.ResolveKyDuyenAt(p, 2, At(3, 8)));
    }

    [Fact]
    public async Task CompletingABookRaisesStats()
    {
        var (game, p) = await Setup();
        var before = GameService.Stats(p);
        p.ThienThu = "9";
        p.Realm = 0;
        var after = GameService.Stats(p);
        Assert.Equal(KyDuyen.Books[0].Bonus.QiPct, KyDuyen.Total(p).QiPct);
        Assert.True(after.Hp >= before.Hp);
        p.ThienThu = "9,9";
        Assert.True(GameService.Stats(p).Hp > before.Hp, "Quyển Nhị cộng % máu");
    }
```

- [ ] **Step 2: Chạy test để thấy lỗi**

Run: `dotnet test test/GemSystemTests --filter KyDuyenTests`
Expected: FAIL khi build, lỗi "'GameService' does not contain a definition for 'KyDuyenStateAt'".

- [ ] **Step 3: Thêm DTO** — trong `Game/Dtos.cs`, sửa dòng cuối của `StateDto`:

```csharp
    List<BuffDto>? Buffs = null,   // hiệu lực đan hỗ trợ đang có
    KyDuyenDto? KyDuyen = null);   // kỳ duyên Thiên Thư: khung đang mở, tiến độ 10 quyển, Đạo Tâm
```

và thêm vào cuối file:

```csharp
/// <summary>Kỳ duyên Thiên Thư. Open = kỳ duyên khung đang mở chưa xử lý (null = không có). NextAt: giờ mở khung kế (Unix ms).</summary>
public record KyDuyenDto(bool Online, string? Blocked, KyDuyenOpenDto? Open, long NextAt, int DaoTam, List<ThienThuBookDto> Books, string? Ending, string? EndingText);
public record KyDuyenOpenDto(string Key, long EndsAt, string Title, string Text, List<KyDuyenChoiceDto> Choices);
/// <summary>Chance: % thành công · Cost: linh thạch trả trước · Hint: kết quả khi thành công.</summary>
public record KyDuyenChoiceDto(string Label, int Chance, long Cost, string Hint);
public record ThienThuBookDto(string Name, int Pages, int Need, bool Open, string Bonus, string? Lore);
```

- [ ] **Step 4: Viết service** — tạo `Game/GameService.KyDuyen.cs`:

```csharp
using VanDao.Data;
namespace VanDao.Game;

/// <summary>Kỳ duyên Thiên Thư: xử lý lựa chọn ở khung giờ đang mở, trả state cho client.</summary>
public partial class GameService
{
    /// <summary>Khung đang mở; admin bật cờ "kyduyen" thì khung giờ hiện tại luôn mở (để thử ngoài giờ).</summary>
    private static KyDuyen.Slot? KyDuyenSlot(Player p, DateTimeOffset now) =>
        KyDuyen.At(now) ?? (p.Cheat("kyduyen") ? KyDuyen.Make(now.ToOffset(KyDuyen.Vn).Date, now.ToOffset(KyDuyen.Vn).Hour) : null);

    /// <summary>Xử lý kỳ duyên khung đang mở: choice 0 = lối thứ nhất, 1 = lối thứ hai. Mỗi khung một lần, phải online.</summary>
    public (string, string) ResolveKyDuyen(Player p, int choice) => ResolveKyDuyenAt(p, choice, EventNow);

    public (string, string) ResolveKyDuyenAt(Player p, int choice, DateTimeOffset? at)
    {
        if (at is not { } now) throw new GameException("Cần kết nối mạng để gặp kỳ duyên.");
        if (EventBlocked(p) is { } why) throw new GameException(why);
        if (!p.StarterChosen) throw new GameException("Chọn khởi đầu trước đã.");
        if (KyDuyenSlot(p, now) is not { } slot) throw new GameException($"Hiện không có kỳ duyên. Khung kế tiếp lúc {KyDuyen.Next(now).Start.ToOffset(KyDuyen.Vn):HH:mm}.");
        if (p.KyDuyenKey == slot.Key) throw new GameException("Đã xử lý kỳ duyên khung giờ này.");
        if (choice is not (0 or 1)) throw new GameException("Lựa chọn không hợp lệ.");
        var enc = KyDuyen.Pick(p.Id, slot);
        var c = choice == 0 ? enc.A : enc.B;
        var unit = KyDuyen.StoneUnit(p.Realm);
        var cost = c.Cost * unit;
        if (p.Count("linh_thach") < cost) throw new GameException($"Cần {cost:N0} linh thạch.");
        var win = KyDuyen.Roll(p.Id, slot, choice, c.Chance);
        var o = win ? c.Win : c.Lose!;
        var need = o.Items.Select(x => x.Item).Append(o.Stones > 0 ? "linh_thach" : "").Where(i => i.Length > 0).Distinct().Count(i => p.Count(i) == 0);
        if (BagCap(p) - BagUsed(p) < need) throw new GameException($"Hành trang cần {need} ô trống.");

        p.Add("linh_thach", -cost);
        var pages = KyDuyen.Pages(p);
        var doneBefore = pages.Select(n => n >= KyDuyen.PagesPerBook).ToArray();
        var added = KyDuyen.AddPages(pages, p.Realm, o.Pages);
        KyDuyen.SetPages(p, pages);
        foreach (var (item, qty) in o.Items) p.Add(item, qty);
        if (o.Stones > 0) p.Add("linh_thach", o.Stones * unit);
        p.DaoTam = Math.Clamp(p.DaoTam + o.DaoTam, -KyDuyen.DaoTamMax, KyDuyen.DaoTamMax);
        if (o.DebuffMinutes > 0)
        {
            var until = DateTime.UtcNow.AddMinutes(o.DebuffMinutes);
            if (until > p.DebuffUntil) p.DebuffUntil = until;
        }
        p.KyDuyenKey = slot.Key;

        for (var i = 0; i < pages.Length; i++)
            if (!doneBefore[i] && pages[i] >= KyDuyen.PagesPerBook)
                AddLog(p, $"Ghép đủ {KyDuyen.Books[i].Name}: {KyDuyen.Books[i].Bonus.Text()} vĩnh viễn.", "gold");
        if (!doneBefore.All(d => d) && KyDuyen.EndingFor(p) is { } end)
            AddLog(p, $"Đủ mười quyển Thiên Thư · {end.Name}: {end.Bonus.Text()}.", "gold");

        var gains = new List<string>();
        if (added > 0) gains.Add($"+{added} trang Thiên Thư");
        if (o.Stones > 0) gains.Add($"+{o.Stones * unit:N0} linh thạch");
        gains.AddRange(o.Items.Select(x => $"+{x.Qty} {GameData.ItemById[x.Item].Name}"));
        if (o.DaoTam != 0) gains.Add($"Đạo Tâm {o.DaoTam:+0;-0}");
        if (o.DebuffMinutes > 0) gains.Add($"suy yếu {o.DebuffMinutes} phút");
        if (cost > 0) gains.Insert(0, $"−{cost:N0} linh thạch");
        return Log(p, $"Kỳ duyên · {enc.Title}: {o.Text}{(gains.Count > 0 ? $" ({string.Join(", ", gains)})" : "")}", win ? "gold" : "bad");
    }

    private static string Hint(KyDuyen.Outcome o, int unit) => string.Join(" · ", new[]
    {
        o.Pages > 0 ? $"+{o.Pages} trang" : "",
        o.Stones > 0 ? $"+{o.Stones * unit:N0} linh thạch" : "",
        string.Join(", ", o.Items.Select(x => $"+{x.Qty} {GameData.ItemById[x.Item].Name}")),
        o.DaoTam != 0 ? $"Đạo Tâm {o.DaoTam:+0;-0}" : "",
    }.Where(s => s.Length > 0));

    public KyDuyenDto KyDuyenStateFor(Player p) => KyDuyenStateAt(p, EventNow);

    public KyDuyenDto KyDuyenStateAt(Player p, DateTimeOffset? at)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        var pages = KyDuyen.Pages(p);
        var unit = KyDuyen.StoneUnit(p.Realm);
        KyDuyenOpenDto? open = null;
        if (at is not null && p.StarterChosen && KyDuyenSlot(p, now) is { } slot && p.KyDuyenKey != slot.Key)
        {
            var enc = KyDuyen.Pick(p.Id, slot);
            open = new(slot.Key, slot.End.ToUnixTimeMilliseconds(), enc.Title, enc.Text,
                new[] { enc.A, enc.B }.Select(c => new KyDuyenChoiceDto(c.Label, (int)Math.Round(c.Chance * 100), (long)c.Cost * unit, Hint(c.Win, unit))).ToList());
        }
        var books = KyDuyen.Books.Select((b, i) => new ThienThuBookDto(b.Name, pages[i], KyDuyen.PagesPerBook, p.Realm >= i, b.Bonus.Text(),
            pages[i] >= KyDuyen.PagesPerBook ? b.Lore : null)).ToList();
        var end = KyDuyen.EndingFor(p);
        return new(at is not null, EventBlocked(p), open, KyDuyen.Next(now).Start.ToUnixTimeMilliseconds(), p.DaoTam, books, end?.Name, end?.Text);
    }
}
```

- [ ] **Step 5: Gắn state** — trong `Game/GameService.cs` dòng 176, thêm tham số cuối khi dựng `StateDto`:

```csharp
            UnlockedSkins(p).ToArray(), CurrentGender(p), EventStateFor(p), ArenaReplay(p), CultivationManuals.ProgressAll(p), GemData.BuildState(p), StarterChoiceFor(p), Alchemy.State(p), SupportPills.State(p, DateTime.UtcNow), KyDuyenStateFor(p));
```

- [ ] **Step 6: Chạy test** (test `CompletingABookRaisesStats` vẫn lỗi tới Task 6)

Run: `dotnet test test/GemSystemTests --filter KyDuyenTests`
Expected: `ResolveOncePerSlotAndGivePages` và `NoEncounterOutsideSlotsOrOffline` PASS; `CompletingABookRaisesStats` FAIL ở assert "Quyển Nhị cộng % máu".

- [ ] **Step 7: Commit**

```bash
git add Game/GameService.KyDuyen.cs Game/Dtos.cs Game/GameService.cs test/GemSystemTests/KyDuyenTests.cs
git commit -m "feat: resolve Kỳ Duyên choices and send its state"
```

---

### Task 6: Cộng chỉ số Thiên Thư, nguồn chỉ số, admin, endpoint

**Files:**
- Modify: `Game/GameService.cs` (hàm chỉ số ~1877-1883, tốc độ tu luyện ~1719, nguồn chỉ số ~1843, `AdminFlags` 637, lệnh admin ~693)
- Modify: `Program.cs:257`
- Modify: `wwwroot/js/main.js:224` (nút cờ admin)

- [ ] **Step 1: Cộng vào chỉ số chiến đấu** — trong hàm trả `new CombatStats(` (~dòng 1877), thêm trước `return new CombatStats(`:

```csharp
        var tt = KyDuyen.Total(p);                                               // Thiên Thư: quyển đã đủ + kết thúc
```

và sửa 3 dòng công / máu / thủ cùng dòng bạo kích:

```csharp
            CombatNumber.Clamp((b.Atk + Sum(i => i.Atk) + PetBonus(p).Atk + gems.Atk) * CombatNumber.Factor(weak * (1 + (sk.atk + fx.SetPct + PetTalent(p,"atk") + tt.AtkPct) / 100))),
            CombatNumber.Clamp((b.Hp + Sum(i => i.Hp) + p.BodyHp + PetBonus(p).Hp + gems.Hp) * CombatNumber.Factor(1 + (sk.hp + fx.HpPct + fx.SetPct + PetTalent(p,"hp") + tt.HpPct) / 100)),
            CombatNumber.Clamp((b.Def + Sum(i => i.Def) + p.BodyDef + PetBonus(p).Def + gems.Def) * CombatNumber.Factor(weak * (1 + (sk.def + fx.SetPct + PetTalent(p,"def") + tt.DefPct) / 100))),
            legacyDodge,
            Math.Max(0, 5 + SumD(i => i.CritPct) + sk.crit + fx.SetCrit + fx.CritBonus + PetTalent(p,"crit") + gems.Crit + tt.Crit),
```

- [ ] **Step 2: Cộng vào tốc độ tu luyện** — dòng ~1719, thêm `+ KyDuyen.Total(p).QiPct` vào tổng phần trăm:

```csharp
        (p.StarterChosen ? GameData.QiRate(Realm(p), p.Stage) : 0) * (1 + (Bonus(p, i => i.QiPct) + SkillBonus(p).qi + GearEffects(p).Qi + PetTalent(p,"qi") + KyDuyen.Total(p).QiPct) / 100) * (DateTime.UtcNow < p.DebuffUntil ? GameData.DebuffRate : 1);
```

Sau đó chạy `grep -n "QiPct\|qiPct" wwwroot/js/*.js` để xem client có tự tính lại tốc độ tu luyện không. Nếu client chỉ đọc số từ server (`S.bonus`, `S.stats`) thì không cần sửa thêm; nếu client tự cộng các nguồn, thêm nguồn "Thiên Thư" ở cùng chỗ đó.

- [ ] **Step 3: Thêm nguồn chỉ số** — trong mảng `StatSourceDto` (~dòng 1843), ngay sau dòng "Luyện thể":

```csharp
            new StatSourceDto("Luyện thể", Def: p.BodyDef, Hp: p.BodyHp),
            new StatSourceDto("Thiên Thư", AtkPct: KyDuyen.Total(p).AtkPct, DefPct: KyDuyen.Total(p).DefPct, HpPct: KyDuyen.Total(p).HpPct, Crit: KyDuyen.Total(p).Crit, Qi: KyDuyen.Total(p).QiPct),
```

- [ ] **Step 4: Cờ và lệnh admin để thử ngoài giờ** — dòng 637:

```csharp
    public static readonly string[] AdminFlags = { "god", "break", "fast", "fast05", "fast2", "fast3", "fast5", "boss", "map", "petevo", "kyduyen" };
```

và thêm lệnh ngay trước `case "stones":`:

```csharp
            case "kyduyen":
                p.KyDuyenKey = "";
                return Log(p, "[Admin] Mở lại kỳ duyên khung giờ hiện tại.", "good");
```

Trong `wwwroot/js/main.js` dòng 224, nối sau nút `F('map', ...)` (trước `</div>` của hàng "Chế độ"):

```js
${F('kyduyen', 'Kỳ duyên luôn mở', 'Khung giờ hiện tại luôn có kỳ duyên, kể cả ngoài 08/12/19/22 giờ')}<button data-adm="kyduyen">Mở lại kỳ duyên</button>
```

Kiểm tra handler `data-adm` trong `main.js` gửi `admin/<lệnh>` chung cho mọi nút (như nút `qi`, `clearcd`); nếu có danh sách lệnh cho phép thì thêm `kyduyen` vào đó.

- [ ] **Step 5: Endpoint** — `Program.cs`, sau dòng `event/claim` (257):

```csharp
        // kỳ duyên Thiên Thư: c = 0 (lối thứ nhất) / 1 (lối thứ hai), mỗi khung giờ một lần
        api.MapPost("/players/{id:guid}/kyduyen", (Guid id, int? c, GameService game) => Run(id, game, p => game.ResolveKyDuyen(p, c ?? -1)));
```

Chạy `grep -rn "event/claim" Online tools/online_host --include=*.cs`: nếu server online map endpoint riêng thì thêm dòng tương tự ở đó.

- [ ] **Step 6: Chạy toàn bộ test**

Run: `dotnet test test/GemSystemTests`
Expected: mọi test PASS, gồm cả `CompletingABookRaisesStats`.

- [ ] **Step 7: Commit**

```bash
git add Game/GameService.cs Program.cs wwwroot/js/main.js
git commit -m "feat: Thiên Thư stats, admin switch and Kỳ Duyên endpoint"
```

---

### Task 7: Tab "Kỳ duyên" trên client

**Files:**
- Create: `wwwroot/js/kyduyen.js`
- Create: `wwwroot/css/kyduyen.css`
- Modify: `wwwroot/index.html` (dòng 16, 90, 106, 131)
- Modify: `wwwroot/js/main.js:293`
- Modify: `wwwroot/js/skill-preview.js:308`
- Modify: `Desktop/PanelWindow.cs:13,18`

- [ ] **Step 1: Nối tab vào HTML** — `wwwroot/index.html`:

Sau `<link rel="stylesheet" href="css/craft.css">`:
```html
<link rel="stylesheet" href="css/kyduyen.css">
```
Sau nút tab Linh thú (trước nút Chợ, dòng 90):
```html
        <button class="tab" role="tab" id="tab-kyduyen" aria-selected="false" data-tab="kyduyen">Kỳ duyên</button>
```
Sau `<div class="pane" id="pane-spin" hidden></div>`:
```html
      <div class="pane" id="pane-kyduyen" hidden></div>
```
Trước `<script src="js/notify.js"></script>`:
```html
<script src="js/kyduyen.js"></script>
```

- [ ] **Step 2: Nối tab vào JS** — `wwwroot/js/main.js:293`, thêm `'kyduyen'` vào danh sách pane:

```js
  ['req','inv','craft','fuse','petfuse','sell','spin','map','skill','pet','market','rank','admin','vault','kyduyen'].forEach(id => { const p = $('pane-'+id); if (p) p.hidden = id !== name; });
```

`wwwroot/js/skill-preview.js:308`, thêm vào `PANE_RENDER`:

```js
  spin: () => renderSpin(), map: () => renderMap(), skill: () => renderSkill(), admin: () => renderAdmin(), pet: () => renderPet(), kyduyen: () => renderKyDuyen(),
```

Chạy `grep -n "paneDirty.add\|renderPane(" wwwroot/js/*.js` để xem chỗ đánh dấu pane bẩn sau mỗi lần đồng bộ; nếu có danh sách id pane cố định thì thêm `'kyduyen'`.

- [ ] **Step 3: Tên tab trên cửa sổ desktop** — `Desktop/PanelWindow.cs`, thêm vào `TabNames` và `TabNamesEn`:

```csharp
        ..., ["market"] = "Chợ", ["rank"] = "Bảng xếp hạng", ["kyduyen"] = "Kỳ duyên",
```
```csharp
        ..., ["market"] = "Market", ["rank"] = "Leaderboard", ["kyduyen"] = "Fortune",
```

- [ ] **Step 4: Viết `wwwroot/js/kyduyen.js`**:

```js
/* Vấn Đạo · js/kyduyen.js: Kỳ duyên Thiên Thư: 4 khung giờ mỗi ngày, chọn một trong hai lối, gom 10 quyển Thiên Thư. */
const KD_SEEN = 'van-dao-kd-seen';
const kdTime = ms => new Date(ms).toLocaleTimeString('vi-VN', {hour: '2-digit', minute: '2-digit'});
function kdPath(d){ return d >= 10 ? 'Chính' : d <= -10 ? 'Tà' : 'Trung'; }

function renderKyDuyen(){
  const pane = $('pane-kyduyen'), k = S?.kyDuyen; if (!pane || !k) return;
  const warn = !k.online ? '<p class="sp-ev-warn">Mất kết nối máy chủ: kỳ duyên chỉ xuất hiện khi online.</p>'
    : k.blocked ? `<p class="sp-ev-warn">${k.blocked}</p>` : '';
  const open = k.open ? `<section class="kd-open">
      <h4>${k.open.title}<small>Còn tới ${kdTime(k.open.endsAt)}</small></h4>
      <p>${k.open.text}</p>
      <div class="kd-choices">${k.open.choices.map((c, i) => `<button type="button" data-kd="${i}" ${busy || k.blocked ? 'disabled' : ''}>
        <b>${c.label}</b><small>${c.chance < 100 ? `${c.chance}% thành công · ` : ''}${c.cost > 0 ? `tốn ${fmt(c.cost)} linh thạch · ` : ''}${c.hint || 'không có gì'}</small></button>`).join('')}</div>
    </section>`
    : `<p class="kd-wait">Chưa có kỳ duyên. Khung kế tiếp lúc <b>${kdTime(k.nextAt)}</b> (08:00 · 12:00 · 19:00 · 22:00, mỗi khung 1 giờ).</p>`;
  const books = k.books.map(b => `<li class="${b.pages >= b.need ? 'done' : b.open ? '' : 'lock'}" ${b.lore ? `data-tip="${b.lore}"` : ''}>
      <b>${b.name}</b><span class="kd-bar"><i style="width:${b.pages / b.need * 100}%"></i></span>
      <small>${b.open ? `${b.pages}/${b.need} trang` : 'Mở ở cảnh giới kế'} · ${b.bonus}</small></li>`).join('');
  const end = k.ending ? `<p class="kd-end"><b>${k.ending}</b> ${k.endingText}</p>` : '';
  pane.innerHTML = `${warn}${open}
    <h4 class="dd-sec">Thiên Thư <small>Đạo Tâm ${k.daoTam > 0 ? '+' : ''}${k.daoTam} · ${kdPath(k.daoTam)}</small></h4>
    ${end}<ol class="kd-books">${books}</ol>`;
}

/** Gọi mỗi lần nhận state: kỳ duyên mới xuất hiện → toast, chấm đỏ trên tab, báo host desktop (chỉ dải game báo để không lặp). */
function kyDuyenNotice(){
  const o = S?.kyDuyen?.open;
  $('tab-kyduyen')?.classList.toggle('kd-dot', !!o);
  if (!o) return;
  let seen = ''; try { seen = localStorage.getItem(KD_SEEN) || ''; } catch(e){}
  if (seen === o.key) return;
  try { localStorage.setItem(KD_SEEN, o.key); } catch(e){}
  if (MODE === 'strip') notifyHost('kyduyen', {title: o.title});
  const el = $('craftToast'); if (!el) return;
  el.classList.remove('brief'); el.classList.add('fast');
  el.innerHTML = `<span class="ct-ic" style="display:grid;place-items:center;font-size:24px;color:#ffd36a">✦</span><span class="ct-t">Kỳ duyên xuất hiện</span>
    <span class="ct-o">${o.title}</span><span class="ct-u"><span>Bấm để xem · còn tới ${kdTime(o.endsAt)}</span></span>`;
  const done = () => { el.hidden = true; el.onclick = null; el.classList.remove('kd-click'); };
  el.classList.add('kd-click');   // toast mặc định pointer-events:none (admin.css), riêng toast kỳ duyên bấm được
  el.onclick = () => { done(); if (MODE === 'strip') notifyHost('open-panel', {tab: 'kyduyen'}); else { if (MODE === 'full') setBookOpen(true); openTab('kyduyen'); } };
  el.hidden = true; void el.offsetWidth; el.hidden = false;
  clearTimeout(craftTimer); craftTimer = setTimeout(done, 6000);
}

document.addEventListener('click', async e => {
  const b = e.target.closest('#pane-kyduyen [data-kd]'); if (!b || b.disabled) return;
  await act(`kyduyen?c=${b.dataset.kd}`);
  renderKyDuyen();
});
```

Kiểm tra trước khi viết: `act(path)` ở `wwwroot/js/core.js:130` trả `true` khi server nhận lệnh; xem thân hàm để chắc nó gửi `POST /api/players/{id}/<path>` và tự hiện lỗi từ server. `setBookOpen` (`main.js`) và `craftTimer` là biến dùng chung, `notify.js` đang dùng cùng cách.

- [ ] **Step 5: Viết `wwwroot/css/kyduyen.css`**:

```css
/* Kỳ duyên Thiên Thư: thẻ kỳ duyên đang mở, hai lối chọn, 10 quyển, chấm báo trên tab. */
.tab.kd-dot { position: relative; }
.tab.kd-dot::after { content: ""; position: absolute; top: 4px; right: 4px; width: 7px; height: 7px; border-radius: 50%; background: #ff5a4a; box-shadow: 0 0 6px #ff5a4a; }
.craft-toast.kd-click { pointer-events: auto; cursor: pointer; }
.kd-open { border: 1px solid #ffd36a66; border-radius: 8px; padding: 10px 12px; margin-bottom: 12px; background: #ffd36a12; }
.kd-open h4 { display: flex; justify-content: space-between; align-items: baseline; margin: 0 0 6px; color: #ffd36a; }
.kd-open h4 small, .dd-sec small { font-weight: 400; opacity: .7; }
.kd-open p { margin: 0 0 10px; line-height: 1.45; }
.kd-choices { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
.kd-choices button { display: grid; gap: 3px; text-align: left; padding: 8px 10px; }
.kd-choices small { opacity: .75; }
.kd-wait { opacity: .8; }
.kd-end { border-left: 3px solid #ffd36a; padding-left: 8px; }
.kd-books { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
.kd-books li { display: grid; grid-template-columns: 1fr 90px; gap: 2px 10px; align-items: center; }
.kd-books li small { grid-column: 1 / -1; opacity: .7; }
.kd-books li.done b { color: #ffd36a; }
.kd-books li.lock { opacity: .45; }
.kd-bar { height: 6px; border-radius: 3px; background: #ffffff1a; overflow: hidden; }
.kd-bar i { display: block; height: 100%; background: linear-gradient(90deg, #c9a24a, #ffd36a); }
@media (max-width: 420px) { .kd-choices { grid-template-columns: 1fr; } }
```

`.craft-toast` trong `wwwroot/css/admin.css:52` đặt `pointer-events:none`; class `kd-click` chỉ bật click cho toast kỳ duyên và được gỡ khi toast ẩn. Các toast khác (`showCraft`, `showChestDrop`…) ghi đè `innerHTML` nhưng không gỡ class, nên thêm `el.classList.remove('kd-click'); el.onclick = null;` vào đầu `showCraft`, `showKnockback`, `showChestDrop` trong `wwwroot/js/notify.js` và đầu toast nhận quà trong `wwwroot/js/spin.js:156`.

- [ ] **Step 6: Commit**

```bash
git add wwwroot/js/kyduyen.js wwwroot/css/kyduyen.css wwwroot/index.html wwwroot/js/main.js wwwroot/js/skill-preview.js wwwroot/js/notify.js wwwroot/js/spin.js Desktop/PanelWindow.cs
git commit -m "feat: Kỳ duyên tab with choices and Thiên Thư books"
```

---

### Task 8: Thông báo khi xuất hiện (toast + khay hệ thống)

**Files:**
- Modify: `wwwroot/js/core.js:100-112` (`setState`)
- Modify: `Desktop/GameWindow.cs` (constructor gần dòng 56 và `OnMessage` ~448)

- [ ] **Step 1: Gọi thông báo mỗi lần nhận state** — `wwwroot/js/core.js`, trong `setState`, ngay sau khối `if (prev && prev.id === S.id){ ... }` (kết thúc dòng 112):

```js
  if (typeof kyDuyenNotice === 'function') kyDuyenNotice();   // kỳ duyên mới xuất hiện → toast + báo khay hệ thống
```

Đồng bộ đã chạy mỗi 4 giây (`setInterval(sync, 4000)` trong `main.js:445`) nên kỳ duyên hiện trong vòng vài giây sau 08:00 / 12:00 / 19:00 / 22:00.

- [ ] **Step 2: Bóng thông báo khay hệ thống** — `Desktop/GameWindow.cs`:

Thêm trường cạnh các trường khác của class:
```csharp
    private bool _kyDuyenTip;   // bóng thông báo đang mở là của kỳ duyên → bấm vào mở tab Kỳ duyên
```
Trong constructor, sau dòng `_tray.MouseClick += ...` (56):
```csharp
        _tray.BalloonTipClicked += (_, _) => { if (!_kyDuyenTip) return; _kyDuyenTip = false; ShowStrip(); OpenPanel("kyduyen"); };
```
Trong `OnMessage`, thêm case cạnh `case "open-panel"`:
```csharp
            case "kyduyen":
                if (!Visible)
                {
                    _kyDuyenTip = true;
                    _tray.ShowBalloonTip(8000, _s.L("Kỳ duyên xuất hiện", "A fortune appears"),
                        (msg.TryGetProperty("title", out var kt) ? kt.GetString() : null) ?? _s.L("Mở Tu luyện · Kỳ duyên để xem.", "Open Cultivation · Fortune to see it."),
                        ToolTipIcon.Info);
                }
                break;
```

Dải đang hiện thì toast trong game đã đủ, không bật bóng thông báo để tránh làm phiền hai lần.

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: Build succeeded, 0 lỗi.

- [ ] **Step 4: Commit**

```bash
git add wwwroot/js/core.js Desktop/GameWindow.cs
git commit -m "feat: notify when a Kỳ duyên appears, in game and in the tray"
```

---

### Task 9: Chuỗi tiếng Anh

**Files:**
- Modify: `wwwroot/js/i18n-en.js`

- [ ] **Step 1: Tìm chuỗi còn thiếu**

Run: `node tools/i18n_extract.cjs`
Expected: liệt kê các chuỗi tiếng Việt mới trong `kyduyen.js` và `index.html` chưa có bản dịch.

- [ ] **Step 2: Thêm bản dịch** vào `window.I18N_EN` trong `wwwroot/js/i18n-en.js`, ít nhất:

```js
"Kỳ duyên":"Fortune",
"Kỳ duyên xuất hiện":"A fortune appears",
"Thiên Thư":"Heavenly Tome",
"Chưa có kỳ duyên. Khung kế tiếp lúc":"No fortune yet. Next window at",
"Mất kết nối máy chủ: kỳ duyên chỉ xuất hiện khi online.":"Lost connection to the server: fortunes only appear while online.",
"Mở ở cảnh giới kế":"Opens at a later realm",
"Kỳ duyên luôn mở":"Fortune always open",
"Mở lại kỳ duyên":"Reopen fortune",
```

Thêm tiếp mọi chuỗi mà `node tools/i18n_extract.cjs` còn báo thiếu (tiêu đề, lời kể, lối chọn của 8 kỳ duyên, tên 10 quyển, 3 kết thúc nếu công cụ có quét chuỗi từ server).

- [ ] **Step 3: Kiểm tra lại**

Run: `node tools/i18n_extract.cjs`
Expected: không còn chuỗi thiếu thuộc phần Kỳ duyên.

- [ ] **Step 4: Commit**

```bash
git add wwwroot/js/i18n-en.js
git commit -m "feat: English strings for Kỳ Duyên"
```

---

### Task 10: Kiểm tra trong game

- [ ] **Step 1: Toàn bộ test**

Run: `dotnet test test/GemSystemTests`
Expected: mọi test PASS.

- [ ] **Step 2: Chạy game và mở bản web** (dùng preview của app hoặc `dotnet run`), vào Admin, bật cờ "Kỳ duyên luôn mở".
Expected: trong ≤ 4 giây hiện toast "Kỳ duyên xuất hiện", tab "Kỳ duyên" có chấm đỏ.

- [ ] **Step 3: Mở tab Kỳ duyên**, kiểm tra thẻ kỳ duyên có tiêu đề, lời kể, hai lối với tỉ lệ / giá / kết quả; bấm một lối.
Expected: nhật ký có dòng "Kỳ duyên · …", thẻ biến mất, thanh trang của Quyển Nhất tăng, Đạo Tâm đổi đúng.

- [ ] **Step 4: Bấm "Mở lại kỳ duyên"** vài lần cho tới khi Quyển Nhất đủ 9 trang.
Expected: log "Ghép đủ Quyển Nhất · Khí Hải: +3% tốc độ tu luyện vĩnh viễn", trong bảng nguồn chỉ số có dòng "Thiên Thư", rê chuột lên quyển hiện lời kể.

- [ ] **Step 5: Bản desktop** (`dotnet run`, cửa sổ dải): ẩn dải xuống khay, rồi bấm "Mở lại kỳ duyên" từ bảng Tu luyện (hoặc chờ khung giờ).
Expected: bóng thông báo Windows "Kỳ duyên xuất hiện"; bấm vào thì dải hiện lại và mở tab Kỳ duyên.

- [ ] **Step 6: Tắt cờ admin**, kiểm tra ngoài khung giờ tab ghi "Khung kế tiếp lúc …" đúng giờ.

- [ ] **Step 7: Commit nếu có sửa trong lúc kiểm tra**

```bash
git add -A
git commit -m "fix: Kỳ Duyên polish from in-game check"
```
