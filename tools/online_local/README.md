# Thử lưu nhân vật bằng Supabase local

Backend chạy luật game và ghi PostgreSQL; client chỉ gửi hành động. Client không được gửi số tiền, cảnh giới, vật phẩm hay toàn bộ save. Supabase Auth xác thực tài khoản; mỗi tài khoản có một nhân vật. Schema `game` chỉ cấp quyền cho tài khoản backend, chặn `anon` và `authenticated` ghi trực tiếp.

## Khởi động

Cần .NET 8 SDK, Docker Desktop đang chạy và Supabase CLI. Chạy từ `D:\VanDao`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/online_local/Setup-Local.ps1
```

Nếu CLI không có trong PATH, truyền `-SupabaseCli <đường-dẫn-supabase.exe>` và `-Docker <đường-dẫn-docker.exe>`.

Mở hai terminal riêng:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/online_local/Start-Online.ps1 -Mode Server
powershell -NoProfile -ExecutionPolicy Bypass -File tools/online_local/Start-Online.ps1 -Mode Client -NoBuild
```

Mở http://127.0.0.1:5189 và đăng ký tài khoản thử. Local tắt xác nhận email. Backend ở cổng 5188, Supabase API 55431 và PostgreSQL 55432; chỉ bind loopback. Ctrl+C dừng từng host. Stack Docker dùng project riêng `vandao-online-local`; dữ liệu nằm trong volume Docker.

Email local (đặt lại mật khẩu, xác nhận) không gửi ra ngoài mà nằm ở hộp thư thử http://127.0.0.1:55434. Trang quên mật khẩu: `online-forgot.html`; liên kết trong thư mở `online-reset.html` trên backend (cổng 5188). Supabase chỉ chuyển về các địa chỉ trong `additional_redirect_urls` của `supabase/config.toml`.

Supabase Studio: http://127.0.0.1:55433. Trong Table Editor chọn schema `game` để xem bảng nhân vật; mục Authentication hiển thị tài khoản. Studio và dịch vụ postgres-meta được bật trong script setup, cổng Studio chỉ bind loopback.

## Thêm cột cho nhân vật

Schema online do EF Core Migrations trong `Online/Migrations` quản lý. Khi thêm thuộc tính vào `Data/Entities.cs`:

1. Thêm cột vào `SchemaUpgrade` trong `Data/GameDbContext.cs` như trước (save offline SQLite).
2. Tạo migration online (tên mô tả thay đổi):

   ```powershell
   dotnet tool restore
   dotnet ef migrations add AddPlayerXyz --project tools/online_host --startup-project tools/online_host --output-dir ../../Online/Migrations --namespace VanDao.Online.Migrations
   ```

   Nếu file `GameDbContextModelSnapshot.cs` được sinh vào `tools/online_host/VanDao/Online/Migrations`, chuyển nó về `Online/Migrations` rồi xóa thư mục `tools/online_host/VanDao`.
3. Sinh lại script SQL cho máy chủ thật (idempotent, chạy lại an toàn), rồi bỏ BOM ở đầu file:

   ```powershell
   dotnet ef migrations script --idempotent --project tools/online_host --startup-project tools/online_host -o supabase/migrations/20261026_online_authority.sql
   ```

4. Chạy `Setup-Local.ps1` (bước `prepare` áp migration còn thiếu lên stack local) rồi chạy kiểm tra bên dưới.

`dotnet test test/GemSystemTests` có test `OnlineMigrationsMatchTheEntityModel`, test này fail nếu model đổi mà chưa có migration.

## Chạy kiểm tra

```powershell
dotnet run --project tools/online_checks -- test D:/VanDao
```

Bộ kiểm tra tạo tài khoản thử và dữ liệu fixture trong stack local: token giả, tráo chủ sở hữu, đọc/ghi nhân vật khác, sửa save local, gán trực tiếp trạng thái, yêu cầu trùng, chi tiêu đồng thời, quyền database và các đường admin/market cũ. Không chạy trên cloud. Sau khi dừng backend nhưng giữ client, chạy lệnh `unavailable` thay `test`; sau khi bật lại backend, chạy `resume` để xác nhận dữ liệu còn nguyên.

## Đăng nhập Google / Gmail

Đã có luồng Google PKCE nhưng nút Google đang ẩn trên cả trang đăng nhập và đăng ký theo cấu hình sản phẩm hiện tại. Chưa có OAuth credentials nên chưa đăng nhập Google thật. Khi bật lại nút mà provider chưa bật, giao diện hiển thị hướng dẫn thay vì chuyển sang trang lỗi.

1. Trong Google Cloud Console, tạo OAuth client loại Web application. Cấu hình consent screen và thêm Gmail thử nghiệm nếu ứng dụng ở chế độ Testing.
2. Authorized redirect URI: `http://127.0.0.1:55431/auth/v1/callback`.
3. Trong terminal dùng để khởi động Supabase, đặt `SUPABASE_AUTH_EXTERNAL_GOOGLE_CLIENT_ID` và `SUPABASE_AUTH_EXTERNAL_GOOGLE_SECRET`. Giữ secret ngoài Git và không gửi qua chat.
4. Đổi `enabled = false` thành `true` trong mục `[auth.external.google]` của `tools/online_local/supabase/config.toml`.
5. Dừng rồi khởi động lại stack bằng Supabase CLI với `--workdir tools/online_local` để áp dụng cấu hình. Không dùng `db reset` hoặc xóa volume. Chạy lại Setup-Local.ps1 nếu CLI thay đổi port binding.

Auth cho phép callback browser tại `http://127.0.0.1:5189/online-login.html`; desktop mở trình duyệt hệ thống rồi nhận callback trên loopback cổng 5190 trong tối đa 5 phút. Port này phải trống. PKCE verifier giữ ở client; backend đổi code lấy phiên từ Supabase. Không lưu mật khẩu Gmail trong game.

Tham khảo: https://supabase.com/docs/guides/auth/social-login/auth-google và https://supabase.com/docs/guides/auth/sessions/pkce-flow.

## Chạy desktop online

Ứng dụng mặc định vẫn Offline. Người chơi đổi trong menu chuột phải → **Chế độ chơi** → Offline / Online; game hỏi xác nhận rồi tự mở lại. Mục Online chỉ bật khi có `Online:DefaultBackendUrl` (bản phát hành ghi URL backend thật vào `appsettings.json`). Thử với backend local:

```powershell
$env:Online__DefaultBackendUrl='http://127.0.0.1:5188'
& ./build-check/online-local/NguoiLuoiTuTien.exe
```

Đặt thẳng `Online__Mode=Client` và `Online__BackendUrl` vẫn được và luôn thắng lựa chọn trong menu.

Client không cần thông tin kết nối database. Màn đăng nhập desktop có ô "Ghi nhớ đăng nhập" (mặc định bật): phiên được lưu mã hoá DPAPI theo tài khoản Windows ở `%LocalAppData%\VanDao\online-session.bin`, mở lại game không phải đăng nhập. Bỏ chọn ô này hoặc **Chế độ chơi → Đăng xuất online** sẽ xoá file. Backend tạm không trả lời thì phiên nhớ được giữ lại, chỉ bị xoá khi Auth từ chối refresh token. Backend luôn kiểm tra token. Mất backend trả lỗi, không chuyển sang save offline.

## Phạm vi và triển khai thật

- Online bắt đầu nhân vật mới; chưa nhập save offline vì save cũ không đáng tin cậy.
- Chợ, đấu trường và bảng xếp hạng cũ đang trả 503 ở chế độ Server, chờ chuyển giao dịch về cùng backend để tránh ghi tiền và vật phẩm ở hai nơi riêng biệt.
- Các hành động thành công có khóa chống lặp; giao dịch và khóa theo tài khoản ngăn chi tiêu đồng thời. Channel có thời gian chờ do server xác định. Khóa chống lặp cũ hơn 1 ngày được tự xóa mỗi giờ.
- Backend xác minh token tại chỗ bằng JWKS của Supabase (khóa ký bất đối xứng, cache 10 phút), không gọi Auth mỗi request. Token HS256 kiểu cũ hoặc khi không tải được JWKS thì hỏi Auth như trước. Đăng xuất không thu hồi access token đã cấp: token còn dùng được đến khi hết hạn, nên trên cloud đặt `jwt_expiry` ngắn (900 giây).
- `/api/session/*` (đăng nhập, đăng ký, làm mới phiên) giới hạn 20 lần/phút mỗi IP. Khi backend chạy sau proxy HTTPS của nhà cung cấp host (Render…), đặt `Online__TrustProxy=true` để giới hạn theo IP người chơi thay vì IP proxy. Không bật khi backend nhận kết nối trực tiếp.
- `GET /healthz` trả 200 khi kết nối được database, dùng cho health check của host.
- Cloud: đặt `Online__PublicUrl` là địa chỉ HTTPS công khai của backend (liên kết đặt lại mật khẩu trỏ về `<PublicUrl>/online-reset.html`) và thêm đúng địa chỉ đó vào Redirect URLs của Supabase Auth. Bật Confirm email và SMTP riêng; nút "Gửi lại email xác nhận" hiện khi đăng nhập báo email chưa xác nhận.
- Kiểm tra `online_checks test` dùng hết lượt đăng nhập của phút đó cho 127.0.0.1; chờ một phút trước khi chạy lại.
- `.local/online-server.env.json` và `.local/supabase-status.json` chứa bí mật local, được gitignore và loại khỏi gói ứng dụng. Không chia sẻ các file này.
- Cloud cần backend riêng do bạn quản lý, HTTPS, Supabase Auth, database dùng SSL VerifyFull và role `game_backend` với mật khẩu riêng. Không phát hành backend cùng bí mật database cho người chơi.
- Migration: `supabase/migrations/20261026_online_authority.sql`, sinh từ `Online/Migrations` (xem mục "Thêm cột cho nhân vật"). Công cụ local không áp dụng các migration chợ cũ.
- Chưa deploy lên cloud. Bản local chứng minh luồng lưu và kiểm tra quyền; chưa phải kiểm thử chống gian lận toàn diện cho mọi cơ chế game.
