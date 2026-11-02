# Vấn Đạo Online Phase 4: Bảng xếp hạng, Chợ, Lôi đài trên backend — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mở Bảng xếp hạng, Chợ và Lôi đài ở chế độ `Server`. Hiện `OnlineBoundary` trả 503 cho cả ba vì chúng ghi thẳng lên Supabase cũ bằng anon key, ngoài transaction nhân vật.

**Architecture:** Mỗi tính năng có bảng riêng trong schema `game` (EF Core migration là nguồn sự thật). Backend ghi các bảng này **trong cùng transaction** của `OnlineBoundary` với thay đổi nhân vật, nên một hành động hoặc ghi đủ cả hai, hoặc không ghi gì. Offline (SQLite) và Client (proxy) giữ nguyên: offline vẫn dùng Supabase anon cũ; Client chỉ chuyển tiếp `/api` sang backend. Luật đang nằm trong SQL (`docs/*/supabase.sql`) được chuyển sang C# và có unit test.

**Thứ tự:** 4a Bảng xếp hạng → 4b Chợ → 4c Lôi đài. Lôi đài cần chỉ số và danh hiệu của bảng xếp hạng, Chợ thì độc lập. Cả ba phần đều có task chi tiết.

**Tech Stack:** .NET 8 minimal API, EF Core 8 + Npgsql, xUnit + SQLite in-memory cho test, Supabase local (`tools/online_local`) để thử thật.

---

## Hiện trạng

| Thành phần | Offline / Client | Server |
|---|---|---|
| `LeaderboardService` | `rpc/submit_score` nền sau mỗi lần lưu; đọc view `leaderboard_public` (hạng tính bằng window function) | `LeaderboardOptions` rỗng → `Enabled=false` |
| `MarketService` | `rpc/market_*` giữ đồ ký gửi trên Supabase | tắt |
| `ArenaService` | `rpc/arena_begin/finish` giữ bậc thang hạng, seed, lượt | tắt |
| `OnlineBoundary` | — | (trước Phase 4) trả 503 mọi đường dẫn chứa `/market`, `/arena`, `/leaderboard`; sau 4c không còn chặn |

Quyền sở hữu ở Server: query filter `Player.OwnerId == RequestOwnerId` và kiểm tra trong `SaveChangesAsync`. RLS của schema `game` chỉ là `backend_only`, nên bảng mới không có `OwnerId` vẫn được backend đọc chéo người chơi. Đó đúng là điều bảng xếp hạng cần.

---

## Phase 4a — Bảng xếp hạng

### Thiết kế

Không đọc thẳng `game."Players"`: lực chiến, tu vi vượt ải, linh thú mạnh nhất phải dựng từ cả cây Player (trang bị, linh thú, túi đồ), quá nặng để tính cho mọi người mỗi lần mở bảng. Thay vào đó lưu **ảnh chụp** mỗi nhân vật vào bảng mới `game."LeaderboardEntries"`, ghi trong cùng transaction lúc lưu nhân vật. Cách này giống `submit_score`, nhưng đồng bộ và không cần secret.

`LeaderboardEntry` (PK `PlayerId`, FK → `Players` cascade):

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `PlayerId` | uuid | PK, FK cascade |
| `Name` | varchar(64) | như `Player.Name` |
| `Realm`, `Stage`, `Level` | int | |
| `Qi` | double | khóa phụ của bảng Cảnh giới |
| `RealmName`, `StageName` | varchar(32) | |
| `Power`, `Progress`, `PetPower` | bigint | |
| `Equipment`, `Stats` | text (JSON) | text để chạy cả SQLite lẫn Postgres; giữ đúng khóa của `stats` hiện tại (`progress`, `pet_id:*`, `pet_stars`, `pet_form`, …) |
| `Flagged` | bool | `SaveGuard.DataFlagged(p, false)`; đã gắn thì giữ |
| `UpdatedAt` | timestamptz | |

Không thêm index: hạng tính bằng C# trên toàn bảng nên index không giúp gì; thêm khi chuyển sang view.

**Xếp hạng:** đọc mọi dòng (chỉ các cột xếp hạng), tính hạng bằng C#, cache 20 giây cho mỗi tiến trình. Giữ đúng luật của view hiện tại: 10 hạng đầu chỉ dành cho nhân vật không gắn cờ, và cùng thứ tự sắp xếp/khóa phụ cho bốn bảng `realm`/`power`/`progress`/`pet`. Mức vài chục nghìn dòng vẫn ổn. Khi lớn hơn thì chuyển sang view có `row_number()` trong migration; đổi chỗ này không ảnh hưởng API.

**Danh hiệu:** người hạng 1 không gắn cờ của mỗi bảng (`progress`/`pet` cần giá trị > 0), như `titles_public`. Danh hiệu `arena` để trống tới 4c.

**Ghi:** `GameService.SaveAsync` gọi `board.Stage(p)` **trước** `db.SaveChangesAsync()`, nên dòng bảng xếp hạng nằm trong cùng transaction. Không cần giới hạn tần suất như bản Supabase: chỉ đánh dấu Modified khi khóa so sánh (giống `Score().key`) đổi, hoặc khi tu vi đã cũ hơn 3 phút.

**Đọc chéo người chơi:** `GET /leaderboard` và `GET /leaderboard/{other}` chỉ đọc `LeaderboardEntries`, không bao giờ nạp `Player` của người khác. Hồ sơ người khác chỉ gồm tên, cảnh giới, trang bị và chỉ số đã chụp, giống bản Supabase.

### File Structure

| File | Trách nhiệm |
|---|---|
| `Data/Entities.cs` | thêm `LeaderboardEntry` |
| `Data/GameDbContext.cs` | `DbSet<LeaderboardEntry>`, khóa, độ dài, index, FK cascade |
| `Online/Migrations/*_OnlineLeaderboard.cs` (mới) | `dotnet ef migrations add OnlineLeaderboard`, cuối `Up` chạy lại `OnlineSchema.Grants` |
| `Game/LeaderboardSnapshot.cs` (mới) | dựng ảnh chụp từ `Player` (tách từ `LeaderboardService.Score`), dùng chung cho Supabase và backend |
| `Game/ServerLeaderboard.cs` (mới) | `Stage(p)`, `GetAsync(p, by)`, `ProfileAsync(me, other)`, `TitlesAsync()`; trả đúng `LeaderboardDto` / `LbProfileDto` hiện có |
| `Game/GameService.cs` | nhận `ServerLeaderboard?`; `SaveAsync`, `LeaderboardAsync`, `PushLeaderboard` dùng nó khi có |
| `Program.cs` | đăng ký `ServerLeaderboard` (scoped) khi `IsServer`; endpoint hồ sơ dùng `GameService` thay vì `LeaderboardService` trực tiếp |
| `Online/OnlineBoundary.cs` | bỏ `/leaderboard` khỏi danh sách 503 |
| `test/GemSystemTests/ServerLeaderboardTests.cs` (mới) | xếp hạng, cờ, danh hiệu, hồ sơ, cập nhật trong transaction |
| `tools/online_checks/Program.cs` | thêm check: hai tài khoản thấy nhau trên bảng, hồ sơ không lộ dữ liệu ngoài ảnh chụp |

### Task 4a.1: Ảnh chụp dùng chung

- [x] Tách phần dựng `body`/`stats`/`equipment`/`key` trong `LeaderboardService.Score` thành `LeaderboardSnapshot.Of(Player p, bool dev)` trả record `(Name, Realm, Stage, Level, Qi, RealmName, StageName, Power, Progress, PetPower, Equipment, Stats, Flagged, Key)`.
- [x] `LeaderboardService.Score` dùng lại record này. Nội dung gửi lên Supabase không đổi.
- [x] Chạy `dotnet test test/GemSystemTests`: phải giữ nguyên số test qua.

### Task 4a.2: Entity + migration

- [x] Thêm `LeaderboardEntry` và cấu hình trong `GameDbContext` như bảng trên.
- [x] Xác nhận check lệch schema (`OnlineMigrationTests`) **FAIL** khi chưa có migration.
- [x] `dotnet ef migrations add OnlineLeaderboard -o Online/Migrations`, thêm `migrationBuilder.Sql(OnlineSchema.Grants)` cuối `Up`.
- [x] Check lệch schema PASS. Áp lên Supabase local bằng `online_checks prepare` và xác nhận `game."LeaderboardEntries"` có policy `backend_only`.
- [ ] Offline: `EnsureCreated` chỉ tạo bảng cho file save mới; offline không bao giờ đọc/ghi bảng này. Kiểm tra mở một save SQLite cũ vẫn chạy.

### Task 4a.3: `ServerLeaderboard` (TDD)

Viết test trước trong `ServerLeaderboardTests` (SQLite in-memory, nhiều `Player` với `OwnerId` khác nhau):

- [x] `Stage` tạo dòng mới, rồi cập nhật đúng dòng đó khi lưu lần sau; tên đổi thì bảng đổi ngay.
- [x] Bốn bảng sắp đúng thứ tự và khóa phụ như view SQL, `Me` trỏ đúng dòng của mình kể cả khi ngoài top 50.
- [x] Luật cờ: nhân vật gắn cờ không bao giờ nằm trong 10 hạng đầu, nhưng vẫn xếp đúng thứ tự từ hạng 11; cờ đã gắn thì không tự gỡ.
- [x] Danh hiệu: hạng 1 không gắn cờ của từng bảng; `progress`/`pet` bằng 0 thì không có danh hiệu.
- [x] Hồ sơ người khác: trả `LbProfileDto` từ ảnh chụp, quy đổi mã trang bị cũ như `ProfileAsync` hiện tại; không có dòng thì `null`.
- [x] Xóa nhân vật thì dòng bảng xếp hạng biến mất (cascade).

Sau đó viết `ServerLeaderboard` cho tới khi các test qua.

### Task 4a.4: Nối vào game và mở endpoint

- [x] `GameService.SaveAsync`: `board?.Stage(p)` trước `SaveChangesAsync`; `LeaderboardAsync` / `PushLeaderboard` / hồ sơ ưu tiên `board`.
- [x] `Program.cs`: `if (online.IsServer) builder.Services.AddScoped<ServerLeaderboard>();`; endpoint hồ sơ đi qua `GameService`.
- [x] `OnlineBoundary`: chỉ còn chặn `/market` và `/arena`, đổi câu báo thành "Chợ và Lôi đài online đang chờ chuyển sang giao dịch server thống nhất."
- [x] `online_checks`: tạo hai tài khoản, mỗi người một nhân vật, gọi `/leaderboard?by=power` từ cả hai và thấy nhau; gọi hồ sơ người kia và chỉ nhận các trường của `LbProfileDto`.
- [x] `dotnet test` toàn bộ + JS tests.

### Task 4a.5: Thử trên desktop

- [x] `Start-Online -Mode Server`, đăng nhập hai tài khoản thử (launcher), mở tab Bảng xếp hạng: bốn tab hiện đúng, bấm vào người kia ra hồ sơ, đổi đạo hiệu thấy ngay.
- [ ] Tắt backend giữa chừng: tab báo lỗi rõ, không trắng.

**Xong 4a khi:** bảng xếp hạng chạy ở Server, test và `online_checks` qua, không còn đường nào ghi Supabase cũ ở Server.

---

## Phase 4b — Chợ

### Thiết kế

- **Một interface, hai nơi lưu.** `IMarketStore` (Open, Mine, List, Buy, Cancel, Claim, Unlimited) do `MarketService` (Supabase, offline) và `ServerMarket` (EF, chế độ Server) triển khai. Phần kiểm tra vật phẩm, giá, chỗ trống trong túi và cộng/trừ hành trang vẫn nằm một chỗ trong `GameService.Market.cs`, nên hai chế độ không lệch luật.
- Bảng `game."MarketListings"`: `Id` (identity), `SellerId` (FK → `Players`, cascade), `SellerName`, `ItemId`, `Qty`, `Price`, `Status` (`open`/`sold`/`cancelled`/`expired`), `BuyerId`, `BuyerName`, `Payout`, `SellerClaimed`, `CreatedAt`, `ExpiresAt`, `ClosedAt`. Index `(Status, CreatedAt)`, `(SellerId, Status)`, `(BuyerId, ClosedAt)`.
- **Luật giữ như `market_rules()`:** Trúc Cơ trở lên, thuế 5% (làm tròn lên), tối đa 5 tin đang treo, 10 giao dịch / 24 giờ (tin rao + lần mua), tin sống 3 ngày, giá 1…2 tỷ. Ở Server không có quyền treo bán không giới hạn (không có admin).
- **Rao bán:** thêm tin và trừ đồ trong cùng transaction.
- **Mua:** `ExecuteUpdate … WHERE Id=@id AND Status='open' AND BuyerId IS NULL AND ExpiresAt>now AND SellerId<>@me`. Ở READ COMMITTED, người mua thứ hai phải chờ khóa dòng rồi thấy `Status='sold'`, nên nhận 0 dòng và báo "đã có người mua"; transaction của họ rollback. Người bán **không** bị sửa trong transaction của người mua; tiền đến qua `claim`. Mỗi transaction chỉ khóa một dòng tin của người khác, nên không có vòng deadlock.
- **Hủy / hết hạn / nhận:** chỉ chạm tin của chính mình. Tin đã đóng thì nội dung không đổi nữa; `SellerClaimed` chỉ người bán bật, dưới khóa tài khoản của họ.
- Tin của người bán bị gắn cờ trên bảng xếp hạng (`LeaderboardEntries.Flagged`) bị ẩn và không mua được, như view `market_open`.
- Không chuyển tin rao từ Supabase cũ sang (online là thế giới mới).

### Task 4b.1: Interface và bản Supabase

- [x] Thêm `IMarketStore` trong `Game/Market.cs`; `MarketService` triển khai bằng các RPC hiện có (chuyển phần dựng tham số `p_id/p_secret/...` từ `GameService.Market.cs` vào đây).
- [x] `GameService` giữ `IMarketStore? _market`; `GameService.Market.cs` gọi interface thay cho `Rpc`.
- [x] `tools/market_checks` (mua đồng thời qua Supabase giả) và toàn bộ test vẫn qua.

### Task 4b.2: Entity + migration

- [x] `MarketListing` + cấu hình trong `GameDbContext`; check lệch schema FAIL trước khi có migration.
- [x] `dotnet ef migrations add OnlineMarket …`, cuối `Up` chạy `OnlineSchema.Grants`; áp lên Supabase local.

### Task 4b.3: `ServerMarket` (TDD, SQLite in-memory)

- [x] Rao bán: tin mới đúng giá/hạn, đồ rời túi; vượt 5 tin đang treo hoặc 10 giao dịch / 24 giờ thì báo lỗi; dưới Trúc Cơ bị chặn.
- [x] Mua: người mua mất linh thạch, nhận đồ; người bán chưa đổi gì cho tới khi `claim` và nhận `giá − ceil(5%)`.
- [x] Mua lại tin đã bán, tin hết hạn, tin của chính mình, tin của người bị gắn cờ: báo lỗi, túi không đổi.
- [x] Hủy: đồ về túi qua `claim`; hủy tin đã bán báo lỗi. Hết hạn: `claim` trả đồ và đánh dấu `expired`.
- [x] `claim` hai lần không nhận hai lần.
- [x] `MarketAsync` liệt kê tin đang mở và mục "Của tôi" (bán + mua) đúng vai.

### Task 4b.4: Nối vào game và mở endpoint

- [x] `Program.cs`: `AddScoped<ServerMarket>()` khi `IsServer`; `GameService` ưu tiên `ServerMarket`.
- [x] `OnlineBoundary`: chỉ còn chặn `/arena`.
- [x] `online_checks`: Bob treo bán, Alice mua, hai lần mua song song chỉ một lần thành công, Bob `claim` nhận đúng tiền một lần; check "legacy market disabled" đổi thành check Lôi đài còn bị chặn.
- [x] `dotnet test` + JS tests + `online_checks`.

### Task 4b.5: Thử trên desktop

- [ ] Hai tài khoản: treo bán, mua, hủy, nhận tiền trong tab Chợ.

## Phase 4c — Lôi đài

### Thiết kế

- **Một interface, hai nơi lưu** như Chợ: `IArenaStore` (State, Begin, Finish, TrackLoadout) do `ArenaService` (Supabase / mock) và `ServerArena` (EF) triển khai. Phần dựng `PvpFighter`, chạy `PvpCombat`, nhật ký và replay trong `GameService.Arena.cs` dùng chung.
- **Begin + Finish trong cùng một request và một transaction.** Ở Server, `GameService` chạy trên backend: backend cấp seed, tự đánh trận bằng ảnh chụp đối thủ đã lưu, rồi đổi hạng. Client không báo thắng/thua. Trận lỗi giữa chừng thì cả lượt rollback, kể cả lượt đã dùng.
- Bảng `game."ArenaRanks"`: `PlayerId` (PK, FK → `Players` cascade), `Pos` (unique), `Wins`, `Losses`, `JoinedAt`. Bảng `game."ArenaFights"`: `Id`, `AttackerId`, `DefenderId`, tên hai bên, `Win`, `AttackerRank`, `DefenderRank`, `Swapped`, `Seed`, `Status`, `Practice`, `CreatedAt`; index `(AttackerId, CreatedAt)`, `(DefenderId, CreatedAt)`.
- **Ảnh chụp kỹ năng** (`PvpFighter`) lưu ở cột mới `LeaderboardEntries.Loadout`, ghi cùng ảnh chụp bảng xếp hạng khi lưu nhân vật. Dòng này chỉ chủ nhân vật ghi, nên không đụng khóa với trận của người khác.
- **Khóa:** mọi thay đổi `ArenaRanks` (vào bậc thang, đổi hạng, cộng thắng/thua) chạy sau `pg_advisory_xact_lock` của Lôi đài. Thứ tự khóa luôn là "tài khoản" (`OnlineBoundary`) rồi mới tới "Lôi đài", và chỉ dòng `ArenaRanks` bị ghi bởi người khác, nên không có vòng deadlock. Hai người vào cùng lúc không lấy trùng `Pos`. Đổi chỗ hai `Pos` đi qua vị trí tạm 0 để không vi phạm unique.
- **Luật giữ như `arena_rules()`:** 15 lượt/ngày (0h giờ Việt Nam, UTC+7), nghỉ 20 giây giữa hai trận, tầm khiêu chiến `max(5, ceil(20% hạng))` phía trên; đánh người xếp dưới là tỷ thí (không đổi hạng, không tính thắng/thua). Hạng hiển thị bỏ qua người bị gắn cờ; người bị gắn cờ không vào được.
- Danh hiệu `arena` (hạng 1 Lôi đài) thêm vào `ServerLeaderboard.TitlesAsync`.

### Task 4c.1: Interface và bản Supabase

- [x] `IArenaStore` trong `Game/Arena.cs`; `ArenaService` triển khai (giữ mock).
- [x] `GameService.Arena.cs` dùng interface; phần "gửi điểm ngay" và danh hiệu đi qua `ServerLeaderboard` khi có, `LeaderboardService` khi không.

### Task 4c.2: Entity + migration

- [x] `ArenaRank`, `ArenaFight`, cột `LeaderboardEntry.Loadout`; check lệch schema FAIL trước khi có migration.
- [x] `dotnet ef migrations add OnlineArena …` + `OnlineSchema.Grants`; áp lên Supabase local.

### Task 4c.3: `ServerArena` (TDD, SQLite in-memory)

- [x] Mở Lôi đài lần đầu thì đứng cuối bậc thang; người bị gắn cờ không vào và không hiện.
- [x] Thắng người trong tầm phía trên thì đổi chỗ, cộng thắng/thua đúng hai bên; thua thì giữ hạng.
- [x] Người ngoài tầm bị từ chối; đánh người xếp dưới là tỷ thí, không đổi hạng và không tính thắng/thua.
- [x] Hết 15 lượt trong ngày và còn trong 20 giây nghỉ thì bị từ chối.
- [x] Lịch sử hiện đúng góc nhìn người đánh và người bị đánh; danh hiệu `arena` về hạng 1.
- [x] Ảnh chụp kỹ năng được lưu khi lưu nhân vật và được dùng làm đối thủ.

### Task 4c.4: Nối vào game và mở endpoint

- [x] `Program.cs`: `AddScoped<ServerArena>()` khi `IsServer`; `OnlineBoundary` bỏ chặn `/arena`.
- [x] `online_checks`: hai tài khoản vào Lôi đài, người hạng dưới khiêu chiến người trên, hạng đổi hoặc giữ đúng theo kết quả, lượt và thời gian nghỉ do server đếm; check "legacy arena disabled" bỏ đi.
- [x] `dotnet test` + JS tests + `online_checks`.

### Task 4c.5: Thử trên desktop

- [ ] Hai tài khoản: mở tab Lôi đài, khiêu chiến, xem trận diễn lại, hạng và lịch sử đổi đúng.

## Không làm trong Phase 4

- Không đổi Offline: vẫn dùng Supabase anon cũ (`docs/*/supabase.sql`).
- Không di chuyển dữ liệu bảng xếp hạng / chợ / lôi đài từ Supabase cũ sang `game`.
- Không deploy cloud (Phase 5).
