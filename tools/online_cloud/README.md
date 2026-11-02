# Deploy backend online lên Render + Supabase cloud

Backend online (chế độ Server) chạy trên **Render** từ `Dockerfile.online`. Tài khoản và dữ liệu nằm trên **project Supabase hiện tại** `kjbzahndcgwgxqqkzvym`, chung với bảng xếp hạng, Chợ và Lôi đài của bản offline (`public.*`, gọi bằng anon key).

Hai bên không đụng nhau: online nằm trong schema riêng `game`, chỉ role `game_backend` vào được. Script schema chỉ cấp / thu quyền trong `game`, không sửa gì ở `public`. Bản offline không dùng Supabase Auth, nên các thiết lập Auth bên dưới chỉ ảnh hưởng tài khoản online. Hai bên dùng chung hạn mức (dung lượng DB, số kết nối, băng thông) của gói Supabase.

Không gửi mật khẩu, key hay connection string qua chat hoặc commit vào repo. Mọi secret chỉ nhập trên dashboard Supabase / Render.

## Thứ tự

1. Cấu hình Auth trên project hiện tại.
2. Áp schema `game` bằng script idempotent.
3. Đặt mật khẩu cho role `game_backend`.
4. Tạo service trên Render từ `render.yaml`.
5. Kiểm tra sau deploy.
6. Phát hành client trỏ về backend mới.

## 1. Project Supabase `kjbzahndcgwgxqqkzvym`

1. Kiểm tra trước trong **SQL Editor** rằng project chưa có schema hay role trùng tên. Hai câu phải trả về rỗng (hoặc chỉ là thứ do chính script này tạo từ lần chạy trước):

   ```sql
   select nspname from pg_namespace where nspname = 'game';
   select rolname from pg_roles where rolname = 'game_backend';
   ```

   **Settings → Data API → Exposed schemas:** giữ nguyên, **không** thêm `game` (client không được gọi thẳng schema này).
2. **Authentication → Sign In / Providers → Email:** bật *Confirm email*. Tắt *Anonymous sign-ins*.
3. **Authentication → JWT / Signing Keys:** chuyển sang khóa bất đối xứng (ECC/RSA). Backend xác minh token tại chỗ qua `/auth/v1/.well-known/jwks.json`; còn dùng secret HS256 cũ thì mỗi request phải hỏi lại Auth (vẫn chạy, chỉ chậm hơn).
   **Không** revoke JWT secret cũ và **không** tắt *legacy API keys*: anon key trong `appsettings.json` của các bản game offline đã phát hành vẫn ký bằng secret cũ, revoke là bảng xếp hạng / Chợ / Lôi đài offline ngừng chạy.
4. **Authentication → Sessions:** thời hạn access token 900 giây.
5. **Authentication → SMTP:** cấu hình SMTP riêng. SMTP mặc định của Supabase chỉ gửi vài thư mỗi giờ, không đủ cho thư xác nhận và đặt lại mật khẩu.
6. **Authentication → URL Configuration** (điền sau khi có URL Render ở bước 4, dạng `https://vandao-online.onrender.com`):
   - Site URL: `https://<backend>`
   - Redirect URLs: `https://<backend>/online-reset.html`, `https://<backend>/online-login.html`
7. Ghi lại (để nhập ở Render): **Project URL** `https://kjbzahndcgwgxqqkzvym.supabase.co` và **Publishable key** (hoặc anon key cũ, cũng là key công khai). Không dùng service role / secret key ở backend.
8. **Database → Settings → SSL Configuration:** tải chứng chỉ CA (`prod-ca-2021.crt`). Backend bắt buộc `SSL Mode=VerifyFull` với DB từ xa (`OnlineOptions.Validate`).

## 2. Áp schema `game`

File `supabase/migrations/20261026_online_authority.sql` được sinh từ `Online/Migrations`. File này idempotent: chạy lại an toàn, chỉ áp các migration còn thiếu. Nó tạo schema `game`, các bảng, role `game_backend` (NOLOGIN), thu quyền của `anon` / `authenticated`, và bật RLS `backend_only`.

Chạy toàn bộ file trong **SQL Editor** của project, hoặc bằng `psql` với connection string của `postgres`. Kiểm tra:

```sql
select "MigrationId" from game."__EFMigrationsHistory" order by 1;
```

Phải có đủ `OnlineBaseline`, `OnlineLeaderboard`, `OnlineMarket`, `OnlineArena`.

Mỗi lần thêm migration mới: sinh lại script theo mục "Thêm cột cho nhân vật" trong `tools/online_local/README.md`, áp lên Supabase cloud **trước**, rồi mới deploy backend.

## 3. Mật khẩu `game_backend`

Trên máy của người vận hành, sinh một mật khẩu mạnh rồi chạy trong SQL Editor:

```sql
ALTER ROLE game_backend LOGIN PASSWORD '<mật khẩu mạnh>';
```

Mật khẩu chỉ được lưu ở secret của Render (bước 4).

## 4. Render

1. Commit và push `Dockerfile.online`, `.dockerignore`, `render.yaml` lên nhánh Render sẽ theo dõi.
2. Xem region của project Supabase (Settings → General → Region). Nếu không phải Singapore, sửa `region` trong `render.yaml` sang region Render gần nhất trước khi tạo Blueprint, để mỗi request không phải đi xa tới DB.
3. Render → **New → Blueprint** → chọn repo. Render đọc `render.yaml`: web service `vandao-online`, Docker, region Singapore, health check `/healthz`, tắt auto-deploy.
4. Nhập các biến `sync: false`:

   | Biến | Giá trị |
   |---|---|
   | `Online__AuthUrl` | `https://kjbzahndcgwgxqqkzvym.supabase.co` |
   | `Online__PublishableKey` | Publishable key ở bước 1.7 |
   | `Online__PublicUrl` | URL công khai của service (`https://vandao-online.onrender.com` hoặc tên miền riêng) |
   | `ConnectionStrings__Game` | xem dưới |

   `Online__Mode=Server` và `Online__TrustProxy=true` đã đặt sẵn trong image.

5. **Environment → Secret Files:** thêm file tên `supabase-ca.crt` với nội dung chứng chỉ ở bước 1.8. Render gắn file vào `/etc/secrets/supabase-ca.crt`.
6. Connection string dùng **Session pooler** (dashboard → Connect → Session pooler, cổng 5432). Render chỉ ra Internet bằng IPv4, mà host trực tiếp `db.kjbzahndcgwgxqqkzvym.supabase.co` chỉ có IPv6:

   ```
   Host=<pooler-host>.pooler.supabase.com;Port=5432;Database=postgres;Username=game_backend.kjbzahndcgwgxqqkzvym;Password=<mật khẩu bước 3>;SSL Mode=VerifyFull;Root Certificate=/etc/secrets/supabase-ca.crt
   ```

   Không dùng Transaction pooler (cổng 6543): mỗi request giữ một transaction và advisory lock theo tài khoản / Lôi đài.

7. **Manual Deploy**. Log phải có `Now listening on: http://0.0.0.0:10000` và health check `/healthz` báo `{"status":"ok"}`.

Gói `starter` luôn chạy. Gói free ngủ sau 15 phút không dùng, người chơi đầu tiên phải chờ máy khởi động khá lâu.

## 5. Kiểm tra sau deploy

`tools/online_checks` chỉ chạy với stack local (chặn mọi host khác 55431/55432); không trỏ nó vào cloud.

Kiểm tra tay bằng launcher với `Online__DefaultBackendUrl=https://<backend>`:

1. Đăng ký bằng email thật → nhận thư xác nhận → bấm xác nhận → đăng nhập.
2. Tạo nhân vật, vận công, tắt rồi mở lại game: dữ liệu còn.
3. Quên mật khẩu: nhận thư, liên kết mở `https://<backend>/online-reset.html`, đặt mật khẩu mới được.
4. Tab Bảng xếp hạng, Chợ, Lôi đài mở được, không báo 503.
5. Dashboard Supabase → Table Editor → schema `game`: có dữ liệu nhân vật; `anon` không đọc được bảng nào trong `game`.

## 6. Phát hành client

Bản phát hành ghi `Online:DefaultBackendUrl = https://<backend>` vào `appsettings.json` của game. Mặc định vẫn là Offline cho tới khi người chơi chọn Online trong launcher. Ghi changelog trong `docs/releases`.

## Chưa kiểm chứng

- Image đã build và chạy thử với Supabase local (`/healthz` 200, route cần đăng nhập trả 401), nhưng chưa chạy trên Render và chưa nối Supabase cloud.
- Chưa thử `SSL Mode=VerifyFull` với chứng chỉ CA qua Session pooler của Supabase, cũng như tên đăng nhập `game_backend.kjbzahndcgwgxqqkzvym` của role tự tạo qua pooler. Nếu một trong hai không được, health check sẽ báo lỗi DB. Khi đó cần sửa cấu hình, không hạ `SSL Mode`.
