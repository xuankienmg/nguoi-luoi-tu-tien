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

Supabase Studio: http://127.0.0.1:55433. Trong Table Editor chọn schema `game` để xem bảng nhân vật; mục Authentication hiển thị tài khoản. Studio và dịch vụ postgres-meta được bật trong script setup, cổng Studio chỉ bind loopback.

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

Ứng dụng mặc định vẫn Offline. Để chạy bản desktop đã build bằng backend local:

```powershell
$env:Online__Mode='Client'
$env:Online__BackendUrl='http://127.0.0.1:5188'
& ./build-check/online-local/NguoiLuoiTuTien.exe
```

Client không cần thông tin kết nối database. Phiên đăng nhập dùng sessionStorage và bộ nhớ tiến trình WebView; backend luôn kiểm tra token. Mất backend trả lỗi, không chuyển sang save offline.

## Phạm vi và triển khai thật

- Online bắt đầu nhân vật mới; chưa nhập save offline vì save cũ không đáng tin cậy.
- Chợ, đấu trường và bảng xếp hạng cũ đang trả 503 ở chế độ Server, chờ chuyển giao dịch về cùng backend để tránh ghi tiền và vật phẩm ở hai nơi riêng biệt.
- Các hành động thành công có khóa chống lặp; giao dịch và khóa theo tài khoản ngăn chi tiêu đồng thời. Channel có thời gian chờ do server xác định.
- `.local/online-server.env.json` và `.local/supabase-status.json` chứa bí mật local, được gitignore và loại khỏi gói ứng dụng. Không chia sẻ các file này.
- Cloud cần backend riêng do bạn quản lý, HTTPS, Supabase Auth, database dùng SSL VerifyFull và role `game_backend` với mật khẩu riêng. Không phát hành backend cùng bí mật database cho người chơi.
- Migration: `supabase/migrations/20261026_online_authority.sql`. Công cụ local không áp dụng các migration chợ cũ.
- Chưa deploy lên cloud. Bản local chứng minh luồng lưu và kiểm tra quyền; chưa phải kiểm thử chống gian lận toàn diện cho mọi cơ chế game.
