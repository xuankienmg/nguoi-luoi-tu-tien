# Áp dụng hoạt ảnh niệm khi thi triển tuyệt kỹ

## Phạm vi

Dùng bản mẫu v4 làm hoạt ảnh của **tu sĩ khi thi triển tuyệt kỹ**. Tay trái nâng qua sườn, ngực và cằm, dựng ngón trỏ trước sống mũi, giữ ấn rồi hạ theo đường cũ. Động tác kết thúc ở tư thế đứng chờ, không có khung đẩy chưởng. VFX của tuyệt kỹ phát ở lớp riêng.

Đợt đầu có ba skin Trúc Cơ, Kim Đan và Luyện Hư. Chọn sheet theo skin đang mặc, độc lập với cảnh giới học chiêu và hệ của tuyệt kỹ.

## Asset mẫu v4

Nguồn dựng và [bản xem trước](../output/sprites/cast-three-skins-v4/preview.html) nằm trong `output/sprites/cast-three-skins-v4/`. Thư mục `output/` bị Git bỏ qua. Khi tích hợp, đóng gói PNG và thông số runtime dưới `wwwroot/img/skin/` để game tải được trên bản phát hành.

- PNG nền trong suốt, sheet 2400 × 880 px, lưới 5 cột × 2 hàng, ô 480 × 440 px. Đọc từ trái sang phải, từ trên xuống. Dùng 9 ô đầu và bỏ ô cuối.
- Điểm neo chung `[240, 420]` tính từ góc trên trái của ô. Chiều cao thân của `trucco-cast.png` là 336 px, `kimdan-cast.png` là 305 px và `luyenhu-cast.png` là 360 px.
- Phát một lượt trong 875 ms. Thời lượng từng khung theo `animation.json` là `[100, 60, 70, 80, 290, 80, 70, 60, 65]` ms. Khung giữ ấn có chỉ số 4 (đếm từ 0) và hiện từ mốc 310 ms đến trước 600 ms.
- Nếu thu nhỏ bản runtime, đổi đồng bộ kích thước sheet, kích thước ô, điểm neo và chiều cao thân. Giữ thời lượng từng khung.

## Cách nối vào trận và xem trước

1. **Nhận diện lượt tuyệt kỹ.** Dùng quyết định có sẵn của `battleSkillTiming` trong `wwwroot/js/fire-fx.js`. Hàm này dùng `battleCastIsUltimate` để nhận cờ `ultimate` hoặc nhận skill ID đã có mốc va chạm, rồi loại lượt PvP idle. Chỉ chọn hoạt ảnh niệm cho lượt của tu sĩ có `authoredDuration > 0`, ở pha `fight` và ngoài lượt đánh thêm (`rep`). Dáng đánh của lượt thường tiếp tục theo `skinActorAction`.
2. **Tính khung theo thời gian của lượt.** Lấy thời gian `x` ngay trước khi `battlePhase` trong `wwwroot/js/battle.js` gọi `timings[k].progress(x)` và chuyển nó sang mili giây. Bắt đầu niệm ở đầu lượt tuyệt kỹ, chọn khung bằng tổng dồn `durationsMs` và giữ tư thế đứng chờ sau 875 ms. Dùng cùng đồng hồ phát lại của trận để đi đúng nhịp ở chế độ thám hiểm ×2. Các giá trị `s` và `a` đã được ánh xạ theo mốc trúng, vì vậy không dùng chúng làm đồng hồ riêng cho sprite niệm.
3. **Vẽ ở lớp nhân vật.** Trong `wwwroot/js/battle-fx.js`, truyền trạng thái và thời gian niệm vào bộ dựng tu sĩ `drawSkinActor` tại `wwwroot/js/skill-preview.js`. Dùng profile theo `skinArt('hero')`, vẽ tại điểm neo hiện có và giữ thứ tự lớp aura trước/sau. Với skin chưa có sheet, ảnh đang tải hoặc lỗi ảnh, dùng đường vẽ nhân vật hiện có. Khi bật giảm chuyển động, dùng khung đứng chờ.
4. **Đồng bộ xem trước.** Tuyệt kỹ trong bảng kỹ năng dùng cùng cách chọn sheet, hàm chọn khung và điểm bắt đầu lượt với trận. Bản xem trước có thể lặp cả lượt để người chơi xem lại. Từng lượt chỉ phát hoạt ảnh niệm một lần.
5. **Giữ mốc gameplay và VFX.** Thời lượng 875 ms thuộc lớp nhân vật. Thời lượng lượt tuyệt kỹ, mốc va chạm từng chiêu, sát thương, khống chế và thời điểm quái phản kích tiếp tục lấy từ luồng hiện hành. Tham khảo [quy trình tích hợp VFX tuyệt kỹ](TuyetKy_VFX_Integration_Workflow.md).

## Kiểm tra khi tích hợp

- Thử cả ba skin ở kích thước trong trận. Tay trái đi lên và xuống, chân bám đất, tư thế kết thúc là đứng chờ.
- So sánh cùng một tuyệt kỹ giữa trận và xem trước, ở tốc độ thường và thám hiểm ×2. Kiểm tra VFX và mốc trúng vẫn đồng bộ.
- Thử lượt thường, lượt đánh thêm, PvP idle, skin thiếu sheet, ảnh lỗi và chế độ giảm chuyển động.

## Bản đã tích hợp

Cả 16 tạo hình có hai atlas chiến đấu dưới `wwwroot/img/skin/combat/`. Atlas runtime có ô 240 × 220 px và neo `[120, 210]`. Trúc Cơ nam, Kim Đan nam và Luyện Hư nam dùng bộ đủ khung chuyển tiếp. 13 tạo hình còn lại dùng atlas hai ô gồm tư thế đứng và tư thế chính. Bộ dựng dùng tư thế cũ khi ảnh chưa sẵn sàng.

Lượt tuyệt kỹ phát niệm v4 theo `turnSeconds` do `battlePhase` cung cấp. Bảng kỹ năng dùng cùng bộ dựng cho VFX tuyệt kỹ dạng chuỗi. Đòn thường và liên kích phát sheet chỉ hai ngón, khớp khung duỗi hết tầm với `s = .45`. Các kỹ năng chủ động khác dùng tư thế hiện hành.

## Hoạt ảnh đòn đánh thường

Bản mẫu v1 cho Trúc Cơ, Kim Đan và Luyện Hư nằm trong `output/sprites/basic-attack-three-skins-v1/` ([xem trước](../output/sprites/basic-attack-three-skins-v1/preview.html)). Tay trái duỗi hai ngón trỏ và giữa ngang về phía mục tiêu rồi thu về tư thế đứng chờ. Mỗi sheet có 7 khung, phát một lượt trong 500 ms, lưới 4 × 2, ô 480 × 440 px, neo `[240, 420]`. Khung duỗi hết tầm là chỉ số 3 tại 175 ms. Mốc này mô tả tư thế trên sheet, còn thời điểm trúng đòn lấy từ lượt đánh hiện có.

Khi áp dụng, chọn sheet theo skin cho đòn đánh thường của tu sĩ. Luồng tuyệt kỹ dùng sheet niệm v4. Dùng thời gian của lượt để chọn khung theo `animation.json`; giữ lớp đạn, va chạm và xử lý sát thương hiện hành.

## Hoạt ảnh bay chuyển ải

**Mẫu đã chốt cho chuyển ải:** v3, lướt ngang trong tư thế đứng thẳng, hai chân giữ cố định trong suốt đoạn lướt.

Bản mẫu v3 cho ba skin nằm trong `output/sprites/stage-flight-three-skins-v3/` ([xem trước](../output/sprites/stage-flight-three-skins-v3/preview.html)). Cả ba lướt trong tư thế đứng thẳng, tay buông và giữ nguyên dáng hai chân. Mỗi skin dùng một PNG nền trong suốt 384 × 512 px. Điểm neo và chiều cao từng skin nằm trong `animation.json`.

Trong game, pha di chuyển `walk` trước khi vào ải dùng dáng lướt cố định. Trúc Cơ và Kim Đan nam/nữ dùng một khung phi kiếm lấy từ atlas gốc của từng skin. Các skin còn lại lướt với tư thế hai chân cố định. Bộ dựng chọn PNG theo ảnh `hero` của trang phục đang mặc. Tệp phi kiếm hậu tố `-sword-v1.png` và các tư thế lướt hậu tố `-v3.png` đều nằm dưới `wwwroot/img/skin/stage-flight/`. Khi ảnh phi kiếm chưa tải, bộ dựng thử tư thế lướt, ảnh đứng, rồi đến sprite pixel.

`wwwroot/js/battle.js` đặt độ cao cố định trong pha `walk`, còn `drawSkinActor` trong `wwwroot/js/skill-preview.js` vẽ một tư thế duy nhất. Khi bật giảm chuyển động, độ nâng bằng 0. Pha `enter` và các lượt đánh tiếp tục dùng bộ dựng hiện hành.

Khi chọn ải mới trên Bản đồ, màn hình chính dừng phát cảnh cũ và bắt đầu lướt ngay. Nếu máy chủ đã xác nhận, trận mới bắt đầu ở lượt đầu sau 1,5 giây lướt. Nếu phản hồi chậm hơn, màn hình đợi xác nhận trước khi bắt đầu trận. Khi trạng thái tự đổi ải, đoạn lướt bắt đầu lúc nhận trạng thái mới. Các đợt quái trong cùng một ải vẫn dùng pha `enter`.
