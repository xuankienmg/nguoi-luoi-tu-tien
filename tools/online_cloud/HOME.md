# Backend online trên máy nhà + Cloudflare Tunnel (chạy song song Render)

Mục tiêu: chạy backend online trên một máy Windows ở nhà, ra Internet qua **Cloudflare Tunnel** (miễn phí, không cần mở cổng router, không cần IP tĩnh, HTTPS do Cloudflare cấp). **Giữ Render chạy song song** cho đến khi máy nhà ổn định.

Hai backend dùng chung database Supabase. Chạy cùng lúc vẫn đúng: mỗi tài khoản bị khóa advisory trong Postgres (`OnlineBoundary`), `PlayerCache` phát hiện ghi từ process khác qua `xmin` và nạp lại. Client đã phát hành vẫn trỏ về Render (`Online:DefaultBackendUrl`).

File liên quan: `deploy/home/` (compose app + cloudflared, `env.example`, `deploy-home.ps1`).

Không gửi mật khẩu, key, token hay connection string qua chat hoặc commit vào repo.

## Cần có

- Máy Windows bật 24/7, mạng ổn định. Máy tắt / mất mạng / mất điện là game online (bản này) ngừng.
- **Docker Desktop** (WSL 2) và Git, có bản clone repo này.
- **Một tên miền** đưa vào Cloudflare (gói Free). Chưa có thì mua ở Cloudflare Registrar hoặc nhà đăng ký bất kỳ rồi đổi nameserver sang Cloudflare.

## 1. Chuẩn bị máy

1. **Settings → System → Power:** *Sleep* = **Never** (khi cắm điện). Tắt *Hibernate*.
2. **Docker Desktop → Settings → General:** bật **Start Docker Desktop when you sign in**.
3. Windows Home không chạy Docker khi chưa đăng nhập: sau khi Windows Update khởi động lại, phải đăng nhập thì server mới chạy lại. Đặt *Active hours* của Windows Update để tránh giờ đông người chơi, hoặc bật tự đăng nhập (`netplwiz`) nếu máy để ở nơi an toàn.
4. (Tuỳ chọn) Docker Desktop → Resources: giới hạn RAM cho WSL nếu máy còn dùng việc khác.

## 2. Cloudflare Tunnel

1. Cloudflare dashboard → **Zero Trust** (tạo team miễn phí nếu được hỏi) → **Networks → Tunnels → Create a tunnel** → loại **Cloudflared** → đặt tên, vd `vandao-home`.
2. Ở bước cài connector chọn **Docker**: chỉ **chép chuỗi token** sau `--token` (không cần chạy lệnh đó, compose đã có cloudflared).
3. **Public Hostname** (hoặc *Routes → Published application*):
   - Subdomain `online`, Domain `<tên-miền>`
   - Service: **HTTP** `app:8080`
4. Cloudflare → tên miền → **SSL/TLS → Edge Certificates:** bật *Always Use HTTPS*.

## 3. Thư mục server

Mặc định `%USERPROFILE%\vandao-server` (đổi được bằng `-ServerDir`):

```powershell
$dir = "$env:USERPROFILE\vandao-server"
New-Item -ItemType Directory -Force "$dir\secrets" | Out-Null
Copy-Item deploy\home\env.example "$dir\.env"
notepad "$dir\.env"
```

- `TUNNEL_TOKEN`: token ở bước 2.
- `Online__PublicUrl`: `https://online.<tên-miền>` (không phải URL Render).
- Các biến còn lại và `ConnectionStrings__Game`: giữ nguyên giá trị đang dùng trên Render (session pooler).

Đặt chứng chỉ CA Supabase (`prod-ca-2021.crt`, giống Secret File của Render) vào `"$dir\secrets\supabase-ca.crt"`.

Giới hạn quyền đọc `.env` cho riêng tài khoản của bạn:

```powershell
icacls "$env:USERPROFILE\vandao-server\.env" /inheritance:r /grant:r "${env:USERNAME}:F"
```

## 4. Supabase Auth

**Authentication → URL Configuration → Redirect URLs:** **thêm** (không xóa URL Render):

- `https://online.<tên-miền>/online-reset.html`
- `https://online.<tên-miền>/online-login.html`

Site URL giữ là URL Render cho tới khi chuyển hẳn.

## 5. Deploy

Chạy từ thư mục repo, với tag đang chạy trên Render:

```powershell
powershell -ExecutionPolicy Bypass -File deploy\home\deploy-home.ps1 -Tag v0.15.2
```

Script lấy mã nguồn **của tag** (`git archive`, không dùng thư mục đang sửa dở), build image, khởi động lại app + cloudflared, chờ `/healthz`. Không qua healthz trong 3 phút thì tự quay về tag trước.

Mỗi lần phát hành bản mới: chạy lại lệnh với tag mới. Tag có migration mới thì áp migration lên Supabase trước rồi thêm `-MigrationsApplied`.

Không dùng GitHub self-hosted runner để deploy tự động: repo public, runner trên máy nhà có thể bị PR lạ chạy mã.

## 6. Kiểm tra

- Trên máy: `http://127.0.0.1:18080/healthz` trả `{"status":"ok"}`.
- Từ ngoài (điện thoại tắt Wi-Fi): `https://online.<tên-miền>/healthz`.
- Mở `https://online.<tên-miền>/` để chơi bản web: đăng nhập, đồng bộ, Chợ, Lôi đài, chat, quên mật khẩu.
- Thử bản desktop: sửa `Online:DefaultBackendUrl` trong `appsettings.json` **của bản cài trên máy bạn** sang URL mới.

Lệnh hay dùng:

```powershell
$dir = "$env:USERPROFILE\vandao-server"
docker compose --project-directory $dir ps
docker compose --project-directory $dir logs -f app
docker compose --project-directory $dir logs cloudflared
Get-Content "$dir\current-tag"
```

Container có `restart: unless-stopped`: Docker Desktop khởi động lại là server tự chạy lại.

## 7. Chuyển hẳn (khi máy nhà đã ổn định vài tuần)

1. Đổi `Online:DefaultBackendUrl` trong `appsettings.json` sang `https://online.<tên-miền>`, phát hành bản mới.
2. Supabase Auth: Site URL sang tên miền mới.
3. Giữ Render thêm một thời gian cho người chơi chưa cập nhật (có thể nâng `min_version` để buộc cập nhật). Trong lúc chờ có thể hạ Render xuống gói Free để bớt phí.
4. Sau đó tắt service Render, bỏ job `deploy-server` trong `build-release.yml`, xóa `render.yaml` và secret `RENDER_DEPLOY_HOOK`.

Dùng tên miền riêng thì sau này chuyển sang host khác (VPS, Oracle…) chỉ cần đổi nơi tunnel trỏ tới, không phải phát hành lại client.
