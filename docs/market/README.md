# Chợ giao dịch giữa người chơi (Supabase)

Tab **Chợ** trong bảng Tu luyện: **Mua** (lọc theo loại, tìm tên, sắp theo đơn giá / phẩm), **Bán** (chọn món trong
hành trang, đặt số lượng + tổng giá) và **Của tôi** (tin đang treo, hủy tin, lịch sử mua bán 30 ngày).

## Cài đặt

Dùng chung project Supabase và cấu hình `Leaderboard` (Url, AnonKey) trong `appsettings.json` với bảng xếp hạng.
Chỉ cần chạy thêm `supabase.sql` trong thư mục này ở **SQL Editor** (chạy lại an toàn).

## Luật

| Luật | Giá trị | Sửa ở |
|---|---|---|
| Cảnh giới tối thiểu | Trúc Cơ | `market_rules()` trong SQL + `MarketService.MinRealm` |
| Thuế | 5% trừ vào tiền người bán nhận (linh thạch bị đốt) | `market_rules()` + `MarketService.TaxPct` |
| Tin đang treo | tối đa 5 / người | `market_rules()` + `MarketService.MaxOpen` |
| Giao dịch | tối đa 10 / 24 giờ (treo bán + mua) | `market_rules()` + `MarketService.MaxDaily` |
| Thời hạn tin | 3 ngày, hết hạn tự trả đồ | `market_rules()` + `MarketService.ListingDays` |
| Không giao dịch được | linh thạch, chìa khóa Boss, vé quay | `GameService.Tradable` |
| Admin mode | nhân vật `Tainted` không mua / bán được (vẫn nhận lại đồ, tiền) | `GameService.MarketBlocked` |
| Môi trường dev | `ASPNETCORE_ENVIRONMENT=Development` (chạy từ launchSettings): bỏ qua 2 điều kiện trên, gửi cảnh giới tối thiểu Trúc Cơ | `MarketService.Dev` |

## Luồng ký gửi

### Treo bán không giới hạn cho admin trong Development

Chạy lại `supabase.sql`, sau đó đặt `market_players.unlimited_listings = true` cho UUID nhân vật thử nghiệm trong Supabase Table Editor. Nhân vật cần đã dùng admin (`Tainted`, cờ `Admin` hoặc `AdminPoints`) và game chạy ở `Development`. Giao diện hiển thị `∞`; khi treo bán bỏ giới hạn 5 tin và hạn mức 10 giao dịch/24 giờ. Mua hàng vẫn áp dụng hạn mức ngày; thuế, thời hạn tin, số lượng và giá hợp lệ vẫn giữ nguyên.

Quyền này mặc định tắt và chỉ quản trị cơ sở dữ liệu cấp được. Cờ gửi từ client phải đi cùng quyền trên Supabase; bản phát hành và nhân vật thường không yêu cầu miễn giới hạn. Client cũ vẫn gọi được `market_list` với các tham số cũ.

1. **Treo bán**: Supabase nhận tin → game mới trừ món khỏi túi. Món nằm trên Supabase cho tới khi bán, hủy hoặc hết hạn.
2. **Mua**: game kiểm tra đủ linh thạch + chỗ trống → `market_buy` đổi tin sang "sold" trong một lệnh UPDATE
   (hai người bấm cùng lúc thì chỉ một người mua được) → game trừ linh thạch, thêm món.
3. **Nhận**: cửa sổ game gọi `market_claim` mỗi phút: món đã bán → cộng linh thạch (đã trừ thuế),
   tin hủy / hết hạn → trả đồ về túi (kể cả khi túi đầy). Mỗi tin chỉ nhận được một lần.

## Chống gian lận và giới hạn

- Mọi hàm kiểm tra khóa bí mật của nhân vật (`Players.LbSecret`, dùng chung với bảng xếp hạng); anon key không đọc /
  sửa bảng gốc được.
- Chặn người gian lận thủ công: Supabase → Table Editor → `market_players` → đặt `banned = true`
  (tin của người đó ẩn khỏi chợ, không mua / bán được nữa).
- **Không chặn được hoàn toàn** việc nhân bản đồ bằng cách chép lại file `vandao.db` cũ sau khi đã treo bán, hoặc sửa
  DB để tạo đồ — vì túi đồ nằm trên máy người chơi. Theo dõi bảng `market_listings` (người bán bất thường, giá lạ)
  để phát hiện và cấm.

## Bản vá chống mua trùng tin

Chạy `supabase/migrations/20261024_market_single_buyer.sql` trên Supabase trước khi dùng bản game gọi `market_buy_v2`. Hàm cũ `market_buy` vẫn được hỗ trợ qua cùng cơ chế khóa. Xem `tools/market_checks/README.md` để biết cách áp dụng, kiểm tra và giới hạn xác minh. Migration chưa được chạy trên Supabase thật trong phiên sửa này.
