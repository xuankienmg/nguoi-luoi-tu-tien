# Nhiệm vụ ngày/tuần (Hoạt Lực) — thiết kế

Ngày: 2026-11-11 · Trạng thái: đã duyệt hướng, chờ duyệt spec

## Mục tiêu

Lấp khoảng trống sau chuỗi nhiệm vụ tân thủ (kết thúc ở Trúc Cơ): cho người chơi danh sách việc mỗi ngày,
thưởng rương theo mốc điểm Hoạt Lực và rương tuần cộng dồn, để tạo thói quen quay lại hằng ngày.

## Quyết định đã chốt

- Mở khóa: sau khi xong chuỗi tân thủ (`NewbieQuestStep >= NewbieQuestData.Total`).
- Danh sách việc cố định, giống nhau mọi ngày.
- Nhiệm vụ tự hoàn thành (không bấm nhận từng việc); chỉ bấm nhận rương mốc.
- Quà tăng theo cảnh giới (linh thạch + phẩm Bổ Nguyên Đan); vé/đan linh thú cố định.
- Rương tuần tính bằng tổng Hoạt Lực ngày cộng dồn trong tuần.

## Danh sách việc (`DailyQuestData.Tasks`)

| Id | Việc | Sự kiện | Cần | Hoạt Lực |
|---|---|---|---|---|
| `login` | Nhận quà đăng nhập | `ClaimDailyLogin` thành công | 1 | 10 |
| `kill` | Hạ quái khi thám hiểm | mỗi trận thắng quái thường trong Tick (không tính Boss) | 50 | 15 |
| `boss` | Hạ Boss chương | mỗi lần `BossKills++` | 1 | 15 |
| `brew` | Khai lò luyện đan | điểm gọi `RecordQuestEvent(EvBrew)` | 3 | 10 |
| `fuse` | Luyện khí | điểm gọi `RecordQuestEvent(EvFuse)` | 2 | 10 |
| `spin` | Quay Tài Xỉu hoặc Linh Thú | số lượt quay thành công (cộng `n`) | 10 | 15 |
| `arena` | Đấu Lôi đài | mỗi trận Lôi đài đã đánh (thắng hoặc thua) | 5 | 15 |
| `kyduyen` | Giải Kỳ duyên | `ResolveKyDuyen` thành công | 1 | 15 |
| `feed` | Cho linh thú ăn | `Feed` thành công | 1 | 10 |

Tổng 115 điểm; trần Hoạt Lực ngày = 115, mốc cuối 100 nên không cần làm hết.
Việc hoàn thành khi `count >= need`; Hoạt Lực ngày = tổng điểm các việc đã hoàn thành.

## Rương

**Ngày** (mốc 20/40/60/80/100). Linh thạch = % phí linh thạch đột phá cảnh giới hiện tại (xem dưới).

| Mốc | Linh thạch | Vật phẩm |
|---|---|---|
| 20 | 2% | Bổ Nguyên Đan ×1 |
| 40 | 3% | Linh Thú Đan ×10 |
| 60 | 4% | Bổ Nguyên Đan ×2 |
| 80 | 5% | Vé Tài Xỉu ×2 |
| 100 | 6% | Vé Linh Thú ×1, Bổ Nguyên Đan ×2, Linh thạch thượng phẩm (`thuong_pham_linh_thach`) ×5 |

**Tuần** (điểm tuần = tổng Hoạt Lực các ngày trong tuần, tối đa 805; mốc 150/300/450/600).

| Mốc | Linh thạch | Vật phẩm |
|---|---|---|
| 150 | 10% | Vé Tài Xỉu ×3 |
| 300 | 15% | Bổ Nguyên Đan ×5, Linh Thú Đan ×30 |
| 450 | 20% | Vé Linh Thú ×2, Hóa Hình Đan ×1 |
| 600 | 25% | Vé Tài Xỉu ×5, Thú Đản Huyền (`thu_dan_huyen`, tên hiển thị "Huyền Thú Đản") ×1, Linh thạch thượng phẩm ×20 |

Một tuần đủ mốc: 22 vé Tài Xỉu, 9 vé Linh Thú — nhỏ hơn nhiều so với một vòng sự kiện Thiên Cơ.

**Công thức theo cảnh giới**
- Linh thạch gốc = `BreakCost["linh_thach"]` của cảnh giới hiện tại; cảnh giới không có (cuối) thì lấy cảnh giới gần nhất phía trước có giá trị. Số nhận = `max(10, round(gốc × %))`.
- Bổ Nguyên Đan: Luyện Khí–Trúc Cơ (realm 0–1) Hạ phẩm; Kim Đan–Nguyên Anh (2–3) Trung phẩm; Hóa Thần trở lên (≥4) Thượng phẩm.
- Quà tính theo cảnh giới lúc bấm nhận.

## Luật

- Chu kỳ: ngày theo 0h giờ Việt Nam (`SpinDayOffset`), tuần bắt đầu 0h thứ Hai giờ Việt Nam.
- Chỉ đếm tiến độ và nhận rương khi có giờ máy chủ (`EventNow` khác null) và `EventBlocked(p)` null; chưa mở khóa thì không đếm.
- Sang ngày mới: bộ đếm ngày và bitmask rương ngày về 0 (lười: khi ghi sự kiện, nhận rương, hoặc dựng state). Sang tuần mới: điểm tuần và bitmask rương tuần về 0.
- Khi một việc chuyển từ chưa xong sang xong, cộng điểm việc đó vào điểm tuần (đúng một lần).
- Nhận rương: mốc đã đạt, chưa nhận, đủ ô trống hành trang (cách tính như `ClaimDailyLogin`). Nhận và đánh dấu trong cùng một lần lưu.
- "Tu lại" (tạo lại nhân vật): giữ toàn bộ trạng thái nhiệm vụ ngày/tuần, như `LoginDay/LoginStep`.

## Thành phần

| Phần | Nơi |
|---|---|
| Dữ liệu: việc, mốc, quà, công thức theo cảnh giới | `Game/DailyQuestData.cs` |
| Logic: `RecordDaily(p, ev, n = 1)`, `ClaimDailyChest(p, i)`, `ClaimWeeklyChest(p, i)`, `DailyQuestStateFor(p)` | `Game/GameService.DailyQuest.cs` |
| Gắn bộ đếm | Các điểm `RecordQuestEvent` (brew, fuse), Tick (kill, boss), quay, Lôi đài, Kỳ duyên, Feed, ClaimDailyLogin |
| DTO + state | `DailyQuestDto` trong `Game/Dtos.cs`; thêm vào `BuildStateAsync` |
| Lưu trữ | `Player`: `DqDay` (text), `DqCounts` (text, dạng `kill:30,boss:1`), `DqClaimed` (int bitmask), `DqWeek` (text), `DqWeekPoints` (int), `DqWeekClaimed` (int bitmask). Migration EF `Online/Migrations/`, nối SQL vào `20261026_online_authority.sql`, cột SQLite trong `GameDbContext` |
| API | `POST /players/{id}/daily-quest/claim?i=` và `POST /players/{id}/weekly-quest/claim?i=` |
| Giao diện | Thẻ "Nhiệm vụ ngày" trong bảng sự kiện (`wwwroot/js/events.js`, `wwwroot/css/events.css`): danh sách việc + thanh tiến độ, 5 rương ngày, 4 rương tuần, đồng hồ reset, huy hiệu khi có rương nhận được. Chữ mới thêm `wwwroot/js/i18n-en.js` |
| Ghi chú phát hành | `docs/releases/upcoming.md` |

## Test

- C# (`test/GemSystemTests/DailyQuestTests.cs`): đếm sự kiện và cộng điểm tuần đúng một lần; reset ngày/tuần; không nhận trùng; chưa đủ mốc thì lỗi; khóa trước khi xong tân thủ; không đếm/không nhận khi không có giờ máy chủ; quà theo cảnh giới (linh thạch, phẩm Bổ Nguyên); thiếu ô hành trang thì lỗi.
- JS (`test/daily-quest.test.cjs`): dựng HTML thẻ từ state mẫu (tiến độ, rương sẵn nhận, đã nhận).

## Ngoài phạm vi

Nhiệm vụ ngẫu nhiên, nhiệm vụ tuần riêng, chuỗi đăng nhập liên tục, thông báo khay hệ thống cho nhiệm vụ.
