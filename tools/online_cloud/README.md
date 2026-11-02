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
7. Deploy lại server mỗi lần phát hành.

## 1. Project Supabase `kjbzahndcgwgxqqkzvym`

1. Kiểm tra trước trong **SQL Editor** rằng project chưa có schema hay role trùng tên. Hai câu phải trả về rỗng (hoặc chỉ là thứ do chính script này tạo từ lần chạy trước):

   ```sql
   select nspname from pg_namespace where nspname = 'game';
   select rolname from pg_roles where rolname = 'game_backend';
   ```

   **Settings → Data API → Exposed schemas:** giữ nguyên, **không** thêm `game` (client không được gọi thẳng schema này).
2. **Authentication → Sign In / Providers → Email:** **tắt** *Confirm email*: đăng ký và đăng nhập không gửi thư, đăng ký xong vào game ngay; chỉ Quên mật khẩu mới gửi email. Tắt *Anonymous sign-ins*.
3. **Authentication → JWT / Signing Keys:** chuyển sang khóa bất đối xứng (ECC/RSA). Backend xác minh token tại chỗ qua `/auth/v1/.well-known/jwks.json`; còn dùng secret HS256 cũ thì mỗi request phải hỏi lại Auth (vẫn chạy, chỉ chậm hơn).
   **Không** revoke JWT secret cũ và **không** tắt *legacy API keys*: anon key trong `appsettings.json` của các bản game offline đã phát hành vẫn ký bằng secret cũ, revoke là bảng xếp hạng / Chợ / Lôi đài offline ngừng chạy.
4. **Authentication → Sessions:** thời hạn access token 900 giây.
5. **Authentication → SMTP:** cấu hình SMTP riêng. SMTP mặc định của Supabase chỉ gửi vài thư mỗi giờ, không đủ cho thư đặt lại mật khẩu. Có SMTP riêng rồi thì nâng *Rate limit for sending emails* ở **Authentication → Rate Limits**.
6. **Authentication → URL Configuration** (điền sau khi có URL Render ở bước 4, dạng `https://nguoiluoitutien-online.onrender.com`):
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
3. Render → **New → Blueprint** → chọn repo. Render đọc `render.yaml`: web service `nguoiluoitutien-online`, Docker, region Singapore, health check `/healthz`, tắt auto-deploy.
4. Nhập các biến `sync: false`:

   | Biến | Giá trị |
   |---|---|
   | `Online__AuthUrl` | `https://kjbzahndcgwgxqqkzvym.supabase.co` |
   | `Online__PublishableKey` | Publishable key ở bước 1.7 |
   | `Online__PublicUrl` | URL công khai của service (`https://nguoiluoitutien-online.onrender.com` hoặc tên miền riêng) |
   | `ConnectionStrings__Game` | xem dưới |
   | `Online__AdminUsers` | Tuỳ chọn. User id (UID) Supabase được dùng admin mode, cách nhau bởi dấu phẩy |

   Admin mode online chỉ mở cho tài khoản trong `Online__AdminUsers` (lấy UID ở Supabase → Authentication → Users). Tài khoản khác vẫn nhận 403. Nhân vật admin online **không** bị đánh dấu: vẫn lên Bảng xếp hạng, vào Chợ và Lôi đài như người thường. Đổi biến này cần deploy lại service.

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

1. Đăng ký bằng email mới → vào thẳng trạng thái đã đăng nhập, không có thư gửi tới → đăng xuất rồi đăng nhập lại được.
2. Tạo nhân vật, vận công, tắt rồi mở lại game: dữ liệu còn.
3. Quên mật khẩu: nhận thư, liên kết mở `https://<backend>/online-reset.html`, đặt mật khẩu mới được.
4. Tab Bảng xếp hạng, Chợ, Lôi đài mở được, không báo 503.
5. Dashboard Supabase → Table Editor → schema `game`: có dữ liệu nhân vật; `anon` không đọc được bảng nào trong `game`.

## 6. Phát hành client

Bản phát hành ghi `Online:DefaultBackendUrl = https://<backend>` vào `appsettings.json` của game. Chế độ chơi mặc định là Online; người chơi chọn Offline trong launcher để dùng save trên máy. Ghi changelog trong `docs/releases`.

## 7. Deploy lại server khi phát hành

Mỗi tag `vX.Y.Z` chạy **Build & Release Game**, rồi job `deploy-server` gọi workflow **Deploy Online Server** (`.github/workflows/deploy-server.yml`). Workflow này yêu cầu Render deploy đúng commit của tag qua Deploy Hook, rồi chờ `/healthz` báo `ok`.

Cài một lần:

1. Render → service `nguoiluoitutien-online` → **Settings → Deploy Hook**: chép URL (URL này là secret).
2. GitHub → repo → **Settings → Environments → Supbase**: thêm secret `RENDER_DEPLOY_HOOK` với URL vừa chép. Nếu dùng tên miền riêng, thêm biến `ONLINE_PUBLIC_URL` (mặc định `https://nguoiluoitutien-online.onrender.com`).

Chưa có secret thì workflow bỏ qua deploy và ghi chú trong Summary; bản phát hành client vẫn ra bình thường.

Khi tag có migration mới (`Online/Migrations` hoặc `supabase/migrations` khác tag trước), workflow **không** tự deploy, vì backend mới cần schema mới:

1. Sinh lại và áp `supabase/migrations/20261026_online_authority.sql` lên Supabase cloud (mục 2).
2. GitHub → **Actions → Deploy Online Server → Run workflow**: nhập tag, đánh dấu *migrations_applied*.

Tag phải nằm trên `master` (nhánh Render theo dõi). Render chỉ chuyển traffic sang bản mới khi bản mới qua `/healthz`; deploy lỗi thì bản cũ vẫn chạy, xem log trên Render.

## Đã kiểm chứng

- Service `vandao-online` chạy trên Render, nối Supabase cloud qua Session pooler với `SSL Mode=VerifyFull`, chứng chỉ CA ở `/etc/secrets/supabase-ca.crt` và tên đăng nhập `game_backend.kjbzahndcgwgxqqkzvym`; `/healthz` trả 200.
- Đủ 5 mục ở phần 5, chạy bằng launcher với `Online__DefaultBackendUrl` trỏ về backend Render: đăng ký có thư xác nhận, dữ liệu nhân vật còn sau khi mở lại game, đặt lại mật khẩu qua `/online-reset.html`, Bảng xếp hạng / Chợ / Lôi đài không báo 503, `anon` không đọc được schema `game`.

## Chưa kiểm chứng

- Workflow **Deploy Online Server** chưa chạy lần nào: chưa thử Deploy Hook với tham số `ref=<commit>` trên Render.
- Service `nguoiluoitutien-online` (URL `https://nguoiluoitutien-online.onrender.com`) thay cho `vandao-online` từ bản sau 0.9.5, chưa deploy và chưa chạy lại các mục ở phần 5. Bản 0.9.5 đã phát hành vẫn trỏ về `https://vandao-online.onrender.com`: giữ service cũ chạy cho tới khi người chơi đã lên bản mới, Deploy Hook và Supabase Redirect URLs của cả hai cần còn trong thời gian đó.
