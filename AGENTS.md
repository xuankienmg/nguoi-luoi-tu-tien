# Hướng dẫn dự án Vấn Đạo

## Skin nhân vật ở tư thế đứng chờ

Áp dụng khi tạo hoặc chỉnh sprite sheet nhân vật đứng chờ (idle), đặc biệt cho giao diện hành trang. Yêu cầu cụ thể mới của người dùng được ưu tiên hơn các mặc định dưới đây.

- Mặc định **64 khung, 16 FPS, vòng lặp 4 giây**, nền trong suốt.
- Bố cục chuẩn **8 cột × 8 hàng**, đọc từ trái sang phải, từ trên xuống dưới. Mỗi ô có cùng kích thước, đủ khoảng trống để tóc, kiếm và tà áo không chạm hoặc vượt sang ô bên cạnh.
- Mặc định mỗi khung **384 × 512 px**, sheet **3072 × 4096 px**; có thể đổi độ phân giải khi cần nhưng giữ bố cục và thời lượng.
- Dựa trên một skin hiện có hoặc ảnh tham chiếu đã được chọn; giữ nhất quán khuôn mặt, tỷ lệ cơ thể, tóc, trang phục, vũ khí, góc nhìn và ánh sáng.
- Chuyển động phải **nhìn thấy rõ gió nhẹ** ở kích thước hiển thị thật trong hành trang: tóc và tà áo lay mềm, có độ trễ lan dọc sợi tóc/vải, kết hợp nhịp thở nhẹ. Tránh chuyển động nhỏ đến mức trông như ảnh đứng im.
- Có **chớp mắt tự nhiên**, mặc định một nhịp mỗi vòng 4 giây. Mí mắt khép rồi mở trong khoảng 3–5 khung; không làm biến dạng khuôn mặt hoặc thay đổi hướng nhìn.
- Hiệu ứng linh lực theo trang bị: **nếu nhân vật có kiếm, chỉ đặt hiệu ứng dọc lưỡi kiếm, không thêm hiệu ứng quanh tay**. Bám đúng vị trí kiếm bằng điểm neo riêng của skin; không phỏng đoán điểm neo, không che cán kiếm/bàn tay. Giữ lớp hiệu ứng riêng để đổi màu theo cảnh giới và hỗ trợ giảm chuyển động.
- Chân và điểm tiếp đất cố định; bàn tay, cán kiếm và lưỡi kiếm giữ hình dạng, không bị uốn theo biến dạng vải. Tóc/vải chuyển động từ gốc ổn định đến ngọn linh hoạt.
- Chuyển động liên tục, không nhảy tư thế, giật, gãy khớp hoặc đổi hình giữa các khung. Ưu tiên dựng chuyển động chu kỳ từ một ảnh gốc ổn định hoặc rig; không dùng các tư thế vẽ lại độc lập thiếu nhất quán.
- Lấy mẫu 64 thời điểm t = i / 16 giây, i từ 0 đến 63. Điểm nối khung 64 về khung 1 phải liền mạch cả vị trí và tốc độ; không chèn bản sao khung đầu vào cuối để lấp số khung.
- Dùng khung cắt, tỷ lệ và điểm neo thống nhất. Không tự căn lại từng khung theo bounding box làm mất chuyển động vải hoặc gây nhấp nhô người.
- Khi tích hợp, khai báo FPS và số khung theo từng sheet; không thay đổi tốc độ của các skin khác. Giữ ảnh dự phòng khi chưa tải xong và dừng ở tư thế đầu khi bật giảm chuyển động.
- Bàn giao **PNG sprite sheet + JSON thông số cắt khung/FPS/thời lượng/điểm neo + bản xem trước động APNG hoặc tương đương**. Lưu phiên bản mới riêng để có thể so sánh.
- Trước khi bàn giao, xem chuyển động ở kích thước hành trang thực tế; kiểm tra tóc/áo đủ rõ, nhịp chớp mắt, vùng chân cố định, không cắt mất chi tiết và đoạn nối cuối–đầu. Không chỉ kiểm tra ảnh từng khung.
- Tạo asset trước; chỉ áp dụng vào game khi người dùng yêu cầu.

Mẫu đã được chọn: `wwwroot/img/skin/inventory-female/luyen-khi-nu-idle-64-wind-v2.png`. Asset nguồn và bản xem trước: `output/sprites/inventory-female-64-wind-v2/`.

## Skin nhân vật phi kiếm (bay)

Áp dụng khi tạo hoặc thay chuyển động di chuyển bằng phi kiếm. Kế thừa tiêu chuẩn sprite ở trên: **64 khung, 16 FPS, vòng lặp 4 giây, nền trong suốt, lưới 8 × 8, ô 384 × 512 px**; giữ tạo hình, khung cắt, tỷ lệ và vòng lặp liên tục. Điểm bám chân được cố định theo mặt kiếm thay vì mặt đất.

- Mẫu tư thế: nhìn ba phần tư, hướng sang phải; kiếm nằm ngang, cán bên trái, mũi bên phải. Hai đế giày đặt rõ trên mặt kiếm; tay tạo kiếm quyết hoặc thả tự nhiên, không cầm thêm kiếm và không thêm hào quang ở tay.
- Giữ khuôn mặt, tóc, đạo bào, trang sức và giày của skin gốc. Nếu cần tư thế mới, tạo một ảnh gốc phi kiếm nhất quán rồi dùng rig/biến dạng chu kỳ; không tạo độc lập từng khung bằng AI.
- Tóc, tay áo, dải lụa và tà áo bay về phía sau (trái khi bay sang phải), có sóng chuyển động và độ trễ nhìn rõ ở kích thước thật trong game. Chớp mắt tự nhiên một nhịp mỗi vòng, khoảng 3–5 khung.
- Kiếm giữ thẳng, không uốn theo vải. Hai chân và mặt kiếm dùng chung điểm neo, không trượt hoặc tách chân khỏi kiếm; cả cụm có thể nhún nhẹ cùng nhau. Mẫu hiện tại nhún dọc biên độ 1,6 px mỗi vòng 4 giây trong khung nguồn.
- **Phi kiếm không cần tạo hiệu ứng cho kiếm**: chỉ giữ hình ảnh kiếm gốc, không tạo hào quang, linh lực, vệt sáng di chuyển hoặc sprite sheet hiệu ứng kiếm riêng. Quy tắc này thay thế yêu cầu hiệu ứng kiếm của tư thế đứng chờ khi tạo chuyển động phi kiếm.
- Metadata ghi hướng bay, kích thước ô/lưới, FPS, số khung, thời lượng, chiều cao thân, điểm neo mặt kiếm, nhịp nhún. Bàn giao PNG nhân vật kèm kiếm, JSON, ảnh tư thế nguồn và APNG/trang xem trước động; lưu phiên bản riêng.
- Chỉ tích hợp khi được yêu cầu: gắn vào trạng thái di chuyển/phi kiếm của đúng skin. Giữ tư thế đứng chờ, thi triển chiêu và tốc độ các skin khác; dùng ảnh dự phòng trong lúc tải và khung đầu khi bật giảm chuyển động.
- Kiểm tra bằng bộ dựng thật ở kích thước hiển thị trong game: gió rõ, kiếm thẳng, chân bám đúng, không có hiệu ứng kiếm bổ sung, không cắt mất tóc/áo/mũi kiếm, chớp mắt và đoạn nối cuối–đầu liền mạch.

Mẫu đã chọn: **Bích Vân Tiên Tử (Trúc Cơ nữ)**. Asset nguồn, JSON và bản xem trước: `output/sprites/bich-van-tien-tu-flight-64-v1/`. Asset trong game: `wwwroot/img/skin/flight/bich-van-tien-tu-flight-64-v1.png`. Lớp hiệu ứng kiếm cũ là asset lịch sử, không dùng làm yêu cầu cho các sprite phi kiếm mới. Khai báo và bộ dựng: `SKIN_ACTOR_SHEETS['img/hero_trucco_nu.png'].flightSpec`, `skinActorLoop`, `drawSkinActor` trong `wwwroot/js/skill-preview.js`.
