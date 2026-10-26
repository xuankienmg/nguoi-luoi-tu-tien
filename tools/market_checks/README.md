# Chợ: mỗi tin bán chỉ có một người mua

## Kết quả kiểm tra mã hiện tại

SQL mẫu cũ đã dùng `UPDATE ... WHERE status = 'open' RETURNING`, vốn đã kiểm tra lại điều kiện sau khi chờ khóa của PostgreSQL. Chưa có quyền đọc định nghĩa hàm đang chạy trên Supabase, nên chưa xác nhận nguyên nhân của sự cố thật. Bản vá bổ sung bảo vệ độc lập và không coi thông báo thành công cũ là bằng chứng được nhận đồ.

## Bản vá

- `supabase/migrations/20261024_market_single_buyer.sql`: khóa dòng tin bán bằng `FOR UPDATE`, kiểm tra lại trạng thái/người mua/hạn dùng sau khi có khóa, cập nhật có điều kiện và trả biên nhận chứa mã tin + người mua.
- Trigger cấm mở lại tin đã đóng, thay người mua sau khi bán, sửa nội dung hàng/giá đã ký gửi và đặt lại cờ đã nhận tiền. Người bán vẫn nhận tiền/đồ trả về bình thường.
- Hàm `market_buy` của bản game cũ chuyển tiếp vào cùng cơ chế an toàn, giữ nguyên dạng phản hồi.
- Game mới gọi `market_buy_v2` và chỉ trừ tiền/cộng đồ khi nhận đúng một biên nhận khớp mã tin, người mua, vật phẩm, số lượng và giá. Không fallback về RPC cũ khi server thiếu bản vá.

## Áp dụng trên Supabase

1. Trong SQL Editor của đúng project đang dùng cho Chợ, chạy file migration ở trên. File không xóa dữ liệu, có thể chạy lại.
2. Sau khi migration hoàn tất, phát hành/chạy bản game chứa sửa đổi trong `Game/Market.cs` và `Game/GameService.Market.cs`.
3. Dùng hai tài khoản thử mua cùng một tin, kiểm tra chỉ một tài khoản nhận đồ/trừ tiền, tin chỉ có một `buyer_id`, người bán chỉ nhận tiền một lần.

Chưa chạy migration hoặc giao dịch kiểm thử nào trên Supabase thật. Không khởi động Docker vì daemon không chạy.

## Kiểm thử cục bộ

```powershell
dotnet run --project tools/market_checks -p:NuGetAudit=false --no-launch-profile
npm install --prefix build-check/market-sql --ignore-scripts --no-audit --no-fund @electric-sql/pglite
node tools/market_checks/sql.cjs
```

- C#: 10 lần, mỗi lần 32 người mua dùng cùng một ảnh chụp tin còn mở; fake RPC dùng thao tác nhận tin nguyên tử. Xác nhận chỉ một người nhận đồ/trả tiền, chặn replay, sai mã tin/người mua/giá/số lượng/vật phẩm, phản hồi rỗng/null/trùng và server chưa cập nhật.
- SQL: chạy SQL thực trên PostgreSQL WASM dùng PGlite, kiểm tra migration chạy lại, mua lại/đổi chủ/mở lại tin, hủy/hết hạn/tự mua/người bán bị cấm, người bán nhận đúng một lần và tương thích client cũ.
- PGlite chỉ có một kết nối độc quyền: kiểm tra SQL này không chứng minh hành vi chờ khóa giữa nhiều phiên PostgreSQL thật. Cần bước thử nghiệm trên Supabase ở trên trước khi xác nhận sự cố production đã hết.

Tham chiếu: [PostgreSQL — Read Committed](https://www.postgresql.org/docs/current/transaction-iso.html#XACT-READ-COMMITTED), [PGlite API](https://pglite.dev/docs/api).
