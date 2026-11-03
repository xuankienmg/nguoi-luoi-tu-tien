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
