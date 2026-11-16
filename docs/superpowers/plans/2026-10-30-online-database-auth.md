# Vấn Đạo Online: lưu database online + đăng ký/đăng nhập — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Đưa PR [#65](https://github.com/xuankienmg/nguoi-luoi-tu-tien/pull/65) (`codex/supabase-online-local`) từ "chạy được trên Supabase local" thành bản online thật: nhân vật lưu trên PostgreSQL (Supabase cloud) qua backend .NET, người chơi đăng ký/đăng nhập bằng email + mật khẩu, nhớ đăng nhập, quên mật khẩu.

**Architecture:** Giữ kiến trúc của PR: desktop chạy ở chế độ `Client` (proxy `/api` → backend), backend chạy chế độ `Server` (luật game + EF Core Npgsql + `OnlineBoundary` kiểm tra token, khóa theo tài khoản, idempotency), Supabase Auth giữ tài khoản. Database chỉ backend chạm tới (role `game_backend`, RLS chặn `anon`/`authenticated`). Plan này sửa lỗi chặn của PR, chuyển schema sang EF Core Migrations, hoàn thiện auth, rồi deploy.

**Tech Stack:** .NET 8, ASP.NET Core minimal API, EF Core 8 + Npgsql 8.0.8, Supabase Auth (GoTrue) + PostgreSQL, WebView2 WinForms, JS thuần trong `wwwroot`.

---

## 0. Kết quả review PR #65

PR làm tốt phần lõi: client chỉ gửi hành động (không upload save), xác minh token với Supabase mỗi request, `pg_advisory_xact_lock` theo tài khoản + `Idempotency-Key` + transaction bao toàn request, query filter theo `OwnerId`, schema `game` tách quyền, 27 integration check. Thiết kế này nên giữ.

**Lỗi chặn (phải sửa trước khi merge):**

| # | Vấn đề | Bằng chứng |
|---|---|---|
| B1 | **Build hỏng.** `VanDao.csproj` thay `test\**` bằng `.local\**;.cache\**` trong `DefaultItemExcludes` (mất `test\**` khi giải conflict), nên `test/GemSystemTests/*.cs` bị biên dịch vào game → 558 lỗi `InlineData`/xunit. | `dotnet build VanDao.csproj` ngày 2026-10-30 trên `c4abccc` |
| B2 | **Schema lệch sau khi merge master.** `Player` có `StarterChosen`, `ManualProgress`, `GemVault`, `GemSlots` nhưng `supabase/migrations/20261026_online_authority.sql` không có → chế độ Server trên DB đã tạo sẽ lỗi `column does not exist` ở mọi truy vấn `Players`. | So `Data/Entities.cs` với file SQL |
| B3 | **Không có cách nâng cấp schema.** `online_checks prepare` sinh lại *toàn bộ* `CREATE TABLE` từ model và ghi đè file migration, nhưng chỉ chạy khi `game."Players"` chưa tồn tại. DB đã có (local hoặc cloud) không bao giờ nhận cột mới; file migration không áp được lên DB cũ. Mỗi lần game thêm cột (rất thường xuyên, xem `SchemaUpgrade` cho SQLite) online sẽ hỏng lại. | `tools/online_checks/Program.cs:24-45` |

**Lỗi/thiếu sót không chặn nhưng cần trước khi phát hành:**

- `OnlineBoundary` gọi `GET /auth/v1/user` của Supabase cho **mọi** request (kể cả `GET /players/current` mỗi lần sync) → thêm 1 round-trip mạng, phụ thuộc rate-limit của Auth. Nên xác minh JWT tại chỗ bằng JWKS.
- Bảng `OnlineRequests` lưu cả body response cho mọi POST và không bao giờ dọn → phình vô hạn.
- Phiên đăng nhập chỉ nằm trong `sessionStorage` + bộ nhớ tiến trình → **mỗi lần mở game phải đăng nhập lại**.
- Không có quên mật khẩu / đặt lại mật khẩu; chưa xử lý xác nhận email trên cloud (local tắt xác nhận).
- Chọn Online/Offline chỉ qua biến môi trường `Online__Mode`; người chơi không có UI chọn.
- Thông báo lỗi Auth hiển thị nguyên văn tiếng Anh của Supabase (`error_description`/`msg`).
- `api()` trong `wwwroot/js/core.js` thử lại POST khi 503 ngay lập tức, không backoff.
- Chợ / Lôi đài / Bảng xếp hạng trả 503 ở chế độ Server (PR đã ghi rõ — để sang plan riêng, xem Phase 4).

---

## File Structure

| File | Trách nhiệm | Phase |
|---|---|---|
| `VanDao.csproj` | khôi phục `test\**` trong `DefaultItemExcludes` | 0 |
| `Online/Migrations/*.cs` (mới) | EF Core migrations cho Npgsql, là nguồn sự thật của schema online | 1 |
| `Online/OnlineDesignTimeFactory.cs` (mới) | `IDesignTimeDbContextFactory` để `dotnet ef` dựng context Npgsql | 1 |
| `Online/OnlineSchema.cs` (mới) | SQL bảo mật (role, REVOKE, RLS) dùng chung cho migration | 1 |
| `supabase/migrations/20261026_online_authority.sql` | thay bằng script idempotent sinh từ EF | 1 |
| `tools/online_checks/Program.cs` | `prepare` áp migrations thay vì `GenerateCreateScript`; thêm check lệch schema | 1 |
| `Online/SupabaseIdentity.cs` | xác minh JWT bằng JWKS; thêm `recover`, `update-password`, `resend` | 2, 3 |
| `Online/OnlineGateway.cs` | map endpoint auth mới | 3 |
| `Online/OnlineRequestCleanup.cs` (mới) | `BackgroundService` xóa `OnlineRequests` > 24h | 2 |
| `Desktop/OnlineSessionStore.cs` (mới) | lưu refresh token mã hóa DPAPI trong `%LocalAppData%\VanDao` | 3 |
| `Desktop/WebHost.cs` | nạp/lưu phiên qua `OnlineSessionStore` | 3 |
| `wwwroot/online-forgot.html`, `wwwroot/online-reset.html` (mới) | quên/đặt lại mật khẩu | 3 |
| `wwwroot/js/online-login.js` | map lỗi Auth sang tiếng Việt, luồng quên mật khẩu, nhớ đăng nhập | 3 |
| `Dockerfile.online` (mới) | đóng gói `tools/online_host` để deploy | 5 |

---

## Phase 0 — Sửa lỗi chặn của PR (làm ngay trên nhánh `codex/supabase-online-local`)

### Task 0.1: Khôi phục build

**Files:**
- Modify: `VanDao.csproj:18`

- [ ] **Step 1: Xác nhận lỗi**

Run: `dotnet build VanDao.csproj -nologo -v q`
Expected: FAIL, ~558 lỗi `CS0246 ... 'InlineData'` trong `test/GemSystemTests`.

- [ ] **Step 2: Sửa `DefaultItemExcludes`**

```xml
    <DefaultItemExcludes>$(DefaultItemExcludes);build-check\**;publish\**;docs\**;Claude outputs\**;test\**;.local\**;.cache\**</DefaultItemExcludes>
```

- [ ] **Step 3: Build lại**

Run: `dotnet build VanDao.csproj -nologo -v q`
Expected: `0 Error(s)`

- [ ] **Step 4: Chạy test hiện có**

Run: `dotnet test test/GemSystemTests -nologo -v q`
Expected: tất cả PASS (giống master).

- [ ] **Step 5: Commit**

```bash
git add VanDao.csproj
git commit -m "fix: keep test sources out of the game build"
```

### Task 0.2: Vá tạm schema online cho 4 cột thiếu

Chỉ để PR dùng được ngay; Phase 1 thay toàn bộ bằng EF migrations.

**Files:**
- Create: `supabase/migrations/20261030_online_player_columns.sql`

- [ ] **Step 1: Viết migration**

```sql
-- Columns added on master after 20261026_online_authority.sql was generated.
BEGIN;
ALTER TABLE game."Players" ADD COLUMN IF NOT EXISTS "StarterChosen" boolean NOT NULL DEFAULT true;
ALTER TABLE game."Players" ADD COLUMN IF NOT EXISTS "ManualProgress" text NOT NULL DEFAULT '';
ALTER TABLE game."Players" ADD COLUMN IF NOT EXISTS "GemVault" text NOT NULL DEFAULT '';
ALTER TABLE game."Players" ADD COLUMN IF NOT EXISTS "GemSlots" text NOT NULL DEFAULT '';
COMMIT;
```

- [ ] **Step 2: Áp lên Supabase local và chạy bộ kiểm tra**

Run (Docker + stack local đang chạy, backend đã start bằng `Start-Online.ps1 -Mode Server`):
```powershell
docker exec -i supabase_db_vandao-online-local psql -U postgres -d postgres -f - < supabase/migrations/20261030_online_player_columns.sql
dotnet run --project tools/online_checks -- test D:/projects/van-dao/VanDao
```
Expected: `ALTER TABLE` ×4, sau đó toàn bộ check PASS (27+).

- [ ] **Step 3: Commit**

```bash
git add supabase/migrations/20261030_online_player_columns.sql
git commit -m "fix: add player columns missing from the online schema"
```

---

## Phase 1 — Schema online bằng EF Core Migrations (sửa B3 tận gốc)

Mục tiêu: thêm cột vào `Player` → `dotnet ef migrations add` → có script nâng cấp cho mọi DB online; check tự động báo lỗi nếu quên.

### Task 1.1: Design-time factory + SQL bảo mật dùng chung

**Files:**
- Create: `Online/OnlineDesignTimeFactory.cs`
- Create: `Online/OnlineSchema.cs`
- Modify: `tools/online_host/online_host.csproj` (thêm `Microsoft.EntityFrameworkCore.Design` 8.0.11, `PrivateAssets=all`; thêm `../../Online/Migrations/*.cs` vào `Compile`)

- [ ] **Step 1: Factory**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VanDao.Data;

namespace VanDao.Online;

/// <summary>Used only by `dotnet ef`; online migrations always target PostgreSQL.</summary>
public sealed class OnlineDesignTimeFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("VANDAO_MIGRATIONS_DB") ?? "Host=127.0.0.1;Port=55432;Database=postgres",
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", "game"))
            .Options);
}
```

- [ ] **Step 2: Chuyển khối `security` từ `tools/online_checks/Program.cs:27-40` vào `Online/OnlineSchema.cs`**

```csharp
namespace VanDao.Online;

public static class OnlineSchema
{
    /// <summary>Only game_backend may touch schema game; Supabase anon/authenticated roles are locked out.</summary>
    public const string Security = """
    DO $$ BEGIN IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname='game_backend') THEN CREATE ROLE game_backend NOLOGIN; END IF; END $$;
    ALTER TABLE game."Players" ADD CONSTRAINT players_auth_owner_fk FOREIGN KEY ("OwnerId") REFERENCES auth.users(id) ON DELETE CASCADE;
    ALTER TABLE game."OnlineRequests" ADD CONSTRAINT requests_auth_owner_fk FOREIGN KEY ("OwnerId") REFERENCES auth.users(id) ON DELETE CASCADE;
    REVOKE ALL ON SCHEMA game FROM PUBLIC, anon, authenticated;
    REVOKE ALL ON ALL TABLES IN SCHEMA game FROM PUBLIC, anon, authenticated;
    REVOKE ALL ON ALL SEQUENCES IN SCHEMA game FROM PUBLIC, anon, authenticated;
    ALTER DEFAULT PRIVILEGES IN SCHEMA game REVOKE ALL ON TABLES FROM PUBLIC, anon, authenticated;
    GRANT USAGE ON SCHEMA game TO game_backend;
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA game TO game_backend;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA game TO game_backend;
    DO $$ DECLARE t record; BEGIN FOR t IN SELECT tablename FROM pg_tables WHERE schemaname='game' LOOP
      EXECUTE format('ALTER TABLE game.%I ENABLE ROW LEVEL SECURITY',t.tablename);
      EXECUTE format('DROP POLICY IF EXISTS backend_only ON game.%I',t.tablename);
      EXECUTE format('CREATE POLICY backend_only ON game.%I TO game_backend USING (true) WITH CHECK (true)',t.tablename);
    END LOOP; END $$;
    """;

    /// <summary>Re-applied after every migration so new tables get the same grants and RLS.</summary>
    public const string Grants = """
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA game TO game_backend;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA game TO game_backend;
    DO $$ DECLARE t record; BEGIN FOR t IN SELECT tablename FROM pg_tables WHERE schemaname='game' LOOP
      EXECUTE format('ALTER TABLE game.%I ENABLE ROW LEVEL SECURITY',t.tablename);
      EXECUTE format('DROP POLICY IF EXISTS backend_only ON game.%I',t.tablename);
      EXECUTE format('CREATE POLICY backend_only ON game.%I TO game_backend USING (true) WITH CHECK (true)',t.tablename);
    END LOOP; END $$;
    """;
}
```

- [ ] **Step 3: Cài công cụ**

```bash
dotnet new tool-manifest --force
dotnet tool install dotnet-ef --version 8.0.11
```

### Task 1.2: Migration baseline + migration cột mới

- [ ] **Step 1: Tạo baseline từ model hiện tại (đã có 4 cột mới)**

```bash
dotnet ef migrations add OnlineBaseline --project tools/online_host --output-dir ../../Online/Migrations --namespace VanDao.Online.Migrations
```

- [ ] **Step 2: Thêm SQL bảo mật cuối `Up()` của `OnlineBaseline`**

```csharp
            migrationBuilder.Sql(OnlineSchema.Security);
```

- [ ] **Step 3: Sinh script idempotent thay file SQL cũ**

```bash
dotnet ef migrations script --idempotent --project tools/online_host -o supabase/migrations/20261026_online_authority.sql
git rm supabase/migrations/20261030_online_player_columns.sql
```

- [ ] **Step 4: DB đã có bảng (local/cloud tạo từ PR) → đánh dấu baseline đã áp, rồi chạy cột thiếu một lần**

```sql
CREATE TABLE IF NOT EXISTS game."__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
-- Thay <id> bằng tên file migration baseline, ví dụ 20261030120000_OnlineBaseline
INSERT INTO game."__EFMigrationsHistory" VALUES ('<id>', '8.0.11') ON CONFLICT DO NOTHING;
```

- [ ] **Step 5: Commit**

```bash
git add .config Online tools/online_host supabase/migrations
git commit -m "feat: manage online schema with EF Core migrations"
```

### Task 1.3: `prepare` áp migrations; thêm check lệch schema

**Files:**
- Modify: `tools/online_checks/Program.cs:24-45`
- Modify: `tools/online_checks/online_checks.csproj` (thêm `../../Online/Migrations/*.cs;../../Online/OnlineSchema.cs` vào `Compile`)

- [ ] **Step 1: Viết check thất bại trước** — thêm vào phần `test` của `Program.cs`:

```csharp
using(var model=new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseNpgsql(admin.ConnectionString).Options)){
    var pending=(await model.Database.GetPendingMigrationsAsync()).ToList();
    Check(pending.Count==0,"pending online migrations: "+string.Join(",",pending));
    Check(!model.Database.HasPendingModelChanges(),"model changed without a migration: run dotnet ef migrations add");
}
```

Ghi chú: `HasPendingModelChanges()` có từ EF Core 9. Với EF 8 dùng: `model.GetService<IMigrationsModelDiffer>().HasDifferences(snapshotModel.GetRelationalModel(), designModel.GetRelationalModel())` — lấy `snapshotModel` từ `model.GetService<IMigrationsAssembly>().ModelSnapshot!.Model` qua `IModelRuntimeInitializer.Initialize`.

- [ ] **Step 2: Chạy, xác nhận FAIL khi thêm một cột thử vào `Player` mà chưa `migrations add`**

- [ ] **Step 3: Thay khối `GenerateCreateScript` trong `prepare` bằng**

```csharp
    using var model=new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
        .UseNpgsql(admin.ConnectionString,o=>o.MigrationsHistoryTable("__EFMigrationsHistory","game")).Options);
    await model.Database.MigrateAsync();
    await new NpgsqlCommand(VanDao.Online.OnlineSchema.Grants,connection).ExecuteNonQueryAsync();
```

(xóa dòng `File.WriteAllText(migration, ...)` — `prepare` không còn ghi file migration.)

- [ ] **Step 4: Chạy lại `Setup-Local.ps1` rồi `online_checks test`** → Expected: PASS.

- [ ] **Step 5: Thêm vào README `tools/online_local/README.md` mục "Thêm cột cho nhân vật"**: thêm thuộc tính vào `Data/Entities.cs` → thêm dòng `SchemaUpgrade` (SQLite) → `dotnet ef migrations add <Tên> --project tools/online_host ...` → sinh lại script idempotent.

- [ ] **Step 6: Commit** `git commit -m "test: fail online checks on schema drift"`

---

## Phase 2 — Backend sẵn sàng production

### Task 2.1: Xác minh JWT tại chỗ bằng JWKS

**Files:** `Online/SupabaseIdentity.cs`, `VanDao.csproj` + `tools/online_host/online_host.csproj` (thêm `Microsoft.IdentityModel.JsonWebTokens` 8.x)

- Supabase cloud phải bật **asymmetric JWT signing keys** (ES256/RS256); JWKS ở `{AuthUrl}/auth/v1/.well-known/jwks.json`.
- `UserAsync(token)`: tải JWKS (cache 10 phút, `ConfigurationManager<JsonWebKeySet>`), `JsonWebTokenHandler.ValidateTokenAsync` với `ValidIssuer = {AuthUrl}/auth/v1`, `ValidAudience = "authenticated"`, `ClockSkew = 30s`; trả `sub` dạng `Guid`.
- Fallback gọi `/auth/v1/user` chỉ khi JWKS lỗi mạng; giữ nguyên 503 nếu cả hai lỗi.
- Đăng xuất (`scope=local`) không thu hồi access token đã cấp → chấp nhận tối đa `jwt_expiry` (đặt 15 phút trên cloud).
- Test (thêm vào `online_checks`): token hết hạn → 401; token ký bằng khóa khác → 401; token đúng → 200 mà không có request nào tới `/auth/v1/user` (đếm qua `DelegatingHandler`).

### Task 2.2: Dọn `OnlineRequests`

**Files:** Create `Online/OnlineRequestCleanup.cs`; Modify `Program.cs` (`if (online.IsServer) builder.Services.AddHostedService<OnlineRequestCleanup>();`)

```csharp
using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Online;

/// <summary>Idempotency keys only need to outlive client retries; drop them after a day.</summary>
public sealed class OnlineRequestCleanup(IServiceScopeFactory scopes, ILogger<OnlineRequestCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do {
            try {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                var cutoff = DateTime.UtcNow.AddDays(-1);
                await db.OnlineRequests.Where(r => r.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
            } catch (Exception e) when (e is not OperationCanceledException) {
                logger.LogWarning(e, "Online request cleanup failed");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
```

Test: chèn 1 dòng `CreatedAt = now-2d` và 1 dòng `now`, gọi một vòng → còn 1 dòng.

### Task 2.3: Health check + giới hạn tốc độ endpoint auth

- `app.MapGet("/healthz", async (GameDbContext db) => await db.Database.CanConnectAsync() ? Results.Ok() : Results.StatusCode(503))` — chỉ ở Server.
- `builder.Services.AddRateLimiter` với `FixedWindowLimiter` 10 req/phút theo IP (`X-Forwarded-For` sau reverse proxy, dùng `ForwardedHeadersOptions` giới hạn KnownProxies) cho nhóm `/api/session/{signin,signup,recover}`.
- Client retry: trong `wwwroot/js/core.js` thay `if(online&&method==='POST'&&r.status===503)r=await fetch(url,options);` bằng retry tối đa 2 lần, chờ 500ms rồi 1500ms, cùng `Idempotency-Key`.

---

## Phase 3 — Hoàn thiện đăng ký / đăng nhập

### Task 3.1: Nhớ đăng nhập trên desktop (DPAPI)

**Files:** Create `Desktop/OnlineSessionStore.cs`; Modify `Desktop/WebHost.cs` (khởi tạo `_onlineSession` từ store; ghi store mỗi lần nhận `online-authenticated`/`online-session-update`; xóa khi `session == null`).

```csharp
using System.Security.Cryptography;
using System.Text;

namespace VanDao.Desktop;

/// <summary>Keeps the online refresh session between launches, encrypted for the current Windows user.</summary>
internal static class OnlineSessionStore
{
    private static string FilePath => Path.Combine(AppPaths.DataDir, "online-session.bin");

    public static string? Load()
    {
        try { return File.Exists(FilePath) ? Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser)) : null; }
        catch (CryptographicException) { Clear(); return null; }
    }

    public static void Save(string json) =>
        File.WriteAllBytes(FilePath, ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser));

    public static void Clear() { try { File.Delete(FilePath); } catch (IOException) { } }
}
```

- Thêm checkbox "Ghi nhớ đăng nhập" (mặc định bật) vào `online-login.html`; gửi `remember` trong message `online-authenticated`; `WebHost` chỉ gọi `Save` khi `remember == true`.
- Khi mở game: access token đã hết hạn → `onlineSession.ensure()` sẵn có sẽ gọi `refresh()`; refresh token bị thu hồi → `clear()` → hiện màn đăng nhập (đã có).
- Kiểm thử thủ công: đăng nhập → tắt game → mở lại → vào thẳng nhân vật; Đăng xuất → mở lại → phải đăng nhập.

### Task 3.2: Quên mật khẩu / đặt lại mật khẩu

**Files:** `Online/SupabaseIdentity.cs`, `Online/OnlineGateway.cs`, Create `wwwroot/online-forgot.html`, `wwwroot/online-reset.html`, Modify `wwwroot/js/online-login.js`, `wwwroot/online-login.html` (link "Quên mật khẩu?").

- `SessionAsync` thêm action `recover` → `POST /auth/v1/recover` body `{ email }` kèm `redirect_to` = `Online:PublicUrl + "/online-reset.html"` (cấu hình mới trong `OnlineOptions`, phải nằm trong *Redirect URLs* của Supabase). Luôn trả 200 với cùng thông điệp ("Nếu email tồn tại, thư đặt lại đã được gửi") để không lộ email nào đã đăng ký.
- Endpoint mới `POST /api/session/password` (Bearer token từ link khôi phục) → `PUT /auth/v1/user` `{ password }`; kiểm tra độ dài 8–256 như signup.
- `online-reset.html`: đọc `access_token`/`type=recovery` từ fragment `#...` (implicit) hoặc `code` (PKCE, đổi qua `/api/session/google`-tương tự → đổi tên thành `/api/session/exchange`), form mật khẩu mới + xác nhận, xong chuyển về `online-login.html`.
- Desktop: link email mở trình duyệt hệ thống → trang reset phải phục vụ được từ **backend công khai** (Phase 5 host `wwwroot/online-*.html` tĩnh trên backend), không phải `127.0.0.1` của client.

### Task 3.3: Xác nhận email + thông báo lỗi tiếng Việt

- Cloud: bật *Confirm email*, SMTP riêng (Resend/SendGrid) vì SMTP mặc định Supabase giới hạn vài thư/giờ; template email tiếng Việt.
- Signup không trả `access_token` → đã có thông điệp; thêm nút "Gửi lại email xác nhận" → action `resend` → `POST /auth/v1/resend` `{type:"signup", email}`.
- Trong `online-login.js` thêm bảng map mã lỗi GoTrue (`error_code`) sang tiếng Việt, dùng trước khi rơi về `error_description`:

```js
const AUTH_ERRORS={
  invalid_credentials:'Email hoặc mật khẩu không đúng.',
  email_not_confirmed:'Email chưa được xác nhận. Hãy mở thư xác nhận hoặc gửi lại.',
  user_already_exists:'Email này đã có tài khoản. Hãy đăng nhập.',
  weak_password:'Mật khẩu quá yếu. Dùng ít nhất 8 ký tự, có chữ và số.',
  over_email_send_rate_limit:'Gửi email quá nhanh. Hãy chờ vài phút.',
  over_request_rate_limit:'Thao tác quá nhanh. Hãy thử lại sau.'
};
const authError=(r,fallback)=>AUTH_ERRORS[r.error_code]||r.error_description||r.msg||r.error||fallback;
```

### Task 3.4: Chọn chế độ Online/Offline trong game

- `OnlineOptions` thêm `DefaultBackendUrl` (đóng gói trong `appsettings.json` bản phát hành, là URL HTTPS của backend Phase 5).
- Lưu lựa chọn người chơi trong settings hiện có của desktop (cùng chỗ `Language`); khi khởi động: `Mode = Client` nếu người chơi chọn Online, ngược lại Offline. Đổi chế độ cần khởi động lại app (đơn giản, tránh hai `WebApplication` song song).
- Màn hình đầu (trước khi tạo nhân vật) có hai nút: "Chơi offline (lưu trên máy)" / "Chơi online (cần tài khoản)". Ghi rõ: save offline **không** chuyển lên online (quyết định của PR, giữ nguyên).
- Nút "Đăng xuất online" đang `position:fixed` góc phải — chuyển vào menu chuột phải của strip (`GameWindow.ShowMenuAtCursor`) và tab cài đặt của panel.

---

## Phase 4 — Chợ, Lôi đài, Bảng xếp hạng (plan riêng)

Hiện trả 503 ở Server mode vì `MarketService`/`ArenaService`/`LeaderboardService` ghi thẳng Supabase bằng anon key, ngoài transaction nhân vật. Cần chuyển thành bảng trong schema `game` do backend ghi trong cùng transaction `OnlineBoundary`. Đây là subsystem độc lập, đủ lớn → viết plan riêng sau khi Phase 0–3 xong. Bảng xếp hạng là phần dễ nhất (chỉ đọc `game."Players"`), nên làm trước.

---

## Phase 5 — Deploy cloud

### Task 5.1: Supabase cloud

- Tạo **project riêng** cho online (không dùng chung `kjbzahndcgwgxqqkzvym` của leaderboard/market cũ) để tách quyền và dữ liệu.
- Auth: bật Confirm email, SMTP riêng, `jwt_expiry = 900`, asymmetric signing keys, Site URL + Redirect URLs = URL backend công khai; tắt signup anonymous.
- Áp `supabase/migrations/20261026_online_authority.sql` (script idempotent) qua SQL editor hoặc `psql`.
- `ALTER ROLE game_backend LOGIN PASSWORD '<mật khẩu mạnh>'` — tạo trên máy người vận hành, lưu vào secret store của host, **không** commit, **không** gửi qua chat.
- Connection string dùng pooler cổng 5432 (session mode; advisory lock theo transaction vẫn ổn) với `SSL Mode=VerifyFull;Root Certificate=<supabase CA>` — `OnlineOptions.Validate` đã bắt buộc VerifyFull cho DB từ xa.

### Task 5.2: Host backend

- `Dockerfile.online`: `mcr.microsoft.com/dotnet/sdk:8.0` build `tools/online_host` → runtime `aspnet:8.0`, `ASPNETCORE_URLS=http://0.0.0.0:8080`, chạy `online_host.dll --web`.
- Nhà cung cấp: cần chọn (Fly.io / Render / Azure App Service / VPS). Yêu cầu: HTTPS, 1 instance là đủ lúc đầu (lock là advisory lock trong Postgres nên scale ngang vẫn đúng), region gần Supabase (Singapore).
- Env: `Online__Mode=Server`, `Online__AuthUrl`, `Online__PublishableKey`, `Online__PublicUrl`, `ConnectionStrings__Game`.
- `UseForwardedHeaders` khi chạy sau proxy; `/healthz` cho health check của host.
- Smoke test sau deploy: đăng ký email thật → xác nhận → tạo nhân vật → vận công → tắt/mở game → dữ liệu còn; chạy `online_checks` chỉ ở local (công cụ chặn mọi host khác 55431/55432 — giữ nguyên).

### Task 5.3: Phát hành client

- `appsettings.json` bản phát hành: `Online:DefaultBackendUrl = https://<backend>`; mặc định vẫn Offline cho tới khi người chơi chọn Online.
- Ghi changelog trong `docs/releases`.

---

## Thứ tự & tiêu chí xong

1. **Phase 0** → PR #65 build được, online chạy với schema hiện tại → có thể merge.
2. **Phase 1** → thêm cột không còn làm hỏng online; check báo lỗi khi quên migration.
3. **Phase 2 + 3** → trải nghiệm đăng ký/đăng nhập đầy đủ (nhớ đăng nhập, quên mật khẩu, xác nhận email, lỗi tiếng Việt, chọn chế độ).
4. **Phase 5** → backend + Supabase cloud chạy thật.
5. **Phase 4** → plan riêng.

Quyết định cần chủ dự án chốt: nhà cung cấp host backend (5.2); có dùng project Supabase mới không (5.1, khuyến nghị: có); có bật đăng nhập Google ở bản đầu không (PR đã có PKCE, cần OAuth client thật).
