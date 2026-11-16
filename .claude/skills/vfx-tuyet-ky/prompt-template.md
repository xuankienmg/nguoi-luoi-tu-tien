# Template prompt tạo VFX tuyệt kỹ Vấn Đạo

Dán prompt bên dưới vào Codex khi đang mở repo Vấn Đạo. Thay các mục trong ngoặc vuông bằng mô tả chiêu của bạn.

```text
Tạo demo VFX tuyệt kỹ [tên chiêu] cho game Vấn Đạo.

Thông tin chiêu
- Hệ và cấp: [hệ, cấp].
- Hình thái: [vật thể hoặc luồng linh lực người chơi sẽ thấy].
- Chất liệu và màu: [ví dụ tinh thể băng xanh đậm, cạnh sáng xanh nhạt].
- Chuyển động: [xuất hiện ở đâu, lao/rơi/lan theo hướng nào, biến mất ra sao].
- Vùng đánh và điểm neo: [đơn mục tiêu hoặc diện rộng, neo tại đâu].
- Mẫu tham chiếu: [đường dẫn đúng phiên bản, chi tiết cần giữ].
- Chi tiết cần tránh: [điền nếu có].

Đọc docs/TuyetKy_VFX_Generation_Guide.md và kiểm tra asset tham chiếu trong repo trước khi dựng. Tạo demo phong cách tu tiên, phát một lần, đúng 3 giây / 90 khung / 30 FPS. Camera cố định. VFX chạy độc lập với nhân vật, giữ điểm neo nhất quán giữa các khung. Sprite sheet có nền trong suốt, không chứa nhân vật, bối cảnh, chữ hoặc UI.

Dựng đủ bốn pha: tụ lực → thi triển → va chạm → tan dần. Ghi frame release và từng frame impact trong demo.json. Tại kích thước hiển thị trong game, phải nhìn rõ hình chính của chiêu, hướng di chuyển và điểm trúng. Chừa đủ biên để tia, hạt và sóng va chạm không bị cắt.

Nếu có nhiều cấp, mỗi cấp có hình thái và cách thi triển riêng. Cấp cao chiếm vùng hiển thị lớn hơn. Cấp thấp không có trận pháp. Nếu dùng trận pháp ở cấp cao, đặt vòng ấn và phù văn tại vị trí thi triển của chiêu, chuyển động mềm, không có đường nối từ nhân vật. Với hệ băng, xuất lớp đóng băng mục tiêu riêng khỏi VFX tấn công. Với chiêu cao cấp, dùng Đại Bạo Nổ – Địa Chấn v2 tại đúng frame impact theo hướng dẫn dự án.

Lưu vào output/<ten-chieu>-demo-vN/ với số phiên bản mới. Bàn giao preview.webp hoặc APNG, index.html, poster.png, demo.json và nguồn tái dựng. Nếu xuất sprite sheet, kèm JSON ghi kích thước ô, lưới, FPS, số khung và điểm neo.

Trước khi bàn giao, xem preview động ở kích thước game và kiểm tra file giải mã đủ 90 khung, tổng đúng 3000 ms. Phạm vi công việc là demo để duyệt.
```
