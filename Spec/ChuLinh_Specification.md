# Linh Thạch — 10 cấp và thuộc tính chiến đấu

Linh Thạch có 10 cấp tương ứng 10 phẩm của trang bị. Gồm 10 dòng, mỗi dòng đủ 10 cấp (100 vật phẩm), cộng 24 ID cũ được giữ để tương thích save: tổng 124 ID. Chỉ số của toàn bộ Linh Thạch, kể cả các ID cũ, được tính lại bằng cùng công thức.

| Cấp | Phẩm | Tỉ lệ hợp lên cấp kế |
|---|---|---|
| 1 | Phàm | 95% |
| 2 | Linh | 90% |
| 3 | Huyền | 85% |
| 4 | Địa | 80% |
| 5 | Thiên | 70% |
| 6 | Thánh | 60% |
| 7 | Thần | 50% |
| 8 | Tiên | 40% |
| 9 | Đạo | 30% |
| 10 | Huyền Tiên | Cao nhất |

Giữ quy tắc 9 viên cùng cấp, có thể khác loại. Thành công nhận 1 viên ngẫu nhiên cấp kế; thất bại nhận 1 viên ngẫu nhiên cùng cấp. Không tốn linh thạch. Hợp nhanh không hợp tiếp ngọc mới tạo trong cùng yêu cầu.

## Dòng Linh Thạch

| Dòng | Thuộc tính |
|---|---|
| Hỏa | Tấn công, sát thương bạo kích (%) |
| Lôi | Bạo kích (%), xác suất liên kích |
| Băng | Phòng thủ, xác suất block |
| Thổ | Máu, hấp thụ sát thương và xác suất chuyển phần hấp thụ thành máu |
| Phong | Tốc độ chạy, tốc độ đánh |
| Quang | Tấn công, chính xác (%) |
| Ám | Hút máu; riêng cấp 10 có +1 đòn thường chắc chắn mỗi lượt |
| Linh | Máu, nộ mỗi đòn đánh trúng |
| Phục Nguyên | Máu, hồi máu mỗi lượt |
| Vũ Trụ | Né tránh (%), hấp thụ và chuyển hóa thành máu |

Tấn công/phòng thủ/máu là số cộng thẳng, tăng theo 1,45^(cấp−1). Chỉ số phần trăm tăng theo cấp theo từng dòng. Xem `Game/GemProgression.cs` để đối chiếu hệ số chính xác. Sát thương bạo kích là phần cộng thêm vào 50% mặc định, không phải tỉ lệ bạo kích.

## Cách tính và giới hạn

Áp dụng cho cả PvE và PvP. Các chỉ số được cộng từ những lỗ đang mở, rồi áp giới hạn:

- Chính xác: tối đa +60 điểm %, trừ né tránh đối phương. Quái thường không né, tinh anh né 5%, Boss né 10% trước chính xác.
- Né tránh: trần tổng 50%, gồm công pháp, thân pháp và Linh Thạch.
- Bạo kích: trần tổng 100%. Sát thương bạo kích từ Linh Thạch cộng tối đa 200%.
- Block: tối đa 50% cơ hội giảm một nửa sát thương đòn đánh còn lại sau khiên.
- Hút máu: tối đa 20% sát thương trực tiếp thực gây lên máu, không tính sát thương vượt lượng máu còn lại hoặc sát thương lên khiên; hồi ngay sau đòn đánh, trước phản đòn.
- Tốc độ chạy: tối đa +60%, chỉ giảm thời gian đi đường. Tách khỏi né tránh và tốc độ đánh.
- Tốc độ đánh: tối đa +100%. Tích lũy tiến độ theo lượt; đủ 100% thêm 1 đòn thường. Ví dụ +25% tạo 1 đòn thêm mỗi 4 lượt. Không phải hiệu ứng +1 đòn chắc chắn của Ám Tinh cấp 10.
- Hồi máu từ Linh Thạch: tối đa 10% máu tối đa mỗi lượt.
- Hấp thụ: tối đa 40% sát thương còn lại sau khiên/block. Có tối đa 50% cơ hội chuyển lượng đã hấp thụ thành hồi máu. Lấy xác suất chuyển hóa lớn nhất trong các viên đang khảm, không cộng dồn xác suất này.
- Tăng nộ: tối đa +20 nộ mỗi đòn đánh trúng; hụt không được cộng. Các đòn thêm cũng tích nộ, tối đa 100 nộ.
- +1 đòn thường chắc chắn: chỉ Ám Tinh cấp 10, tối đa 1 dù khảm nhiều viên.
- Liên kích: cơ hội thêm 1 đòn, trần 30% khi cộng cùng nguồn trang bị/linh thú; thần thông có thể nâng trần hiện có lên 50%.
- Hồi máu không vượt máu tối đa và không hồi sinh sau khi đã bị hạ gục.

## Giao diện, khảm và dữ liệu cũ

Luyện khí dùng combobox chọn Hợp trang bị, Hợp Linh Thạch, Trang bị Linh thú. Linh Thạch hiện trong túi với cấp và nền phẩm. Bảng nguồn chỉ số hiển thị các thuộc tính Linh Thạch đang kích hoạt và giới hạn.

Khảm gồm ba cột: chọn trang bị, ô khảm trung tâm, chọn Linh Thạch. Chuột phải chỉ đưa vào ô chờ, không tiêu hao. Chỉ khi bấm xác nhận mới tiêu hao 1 viên và gắn vào trang bị. Bấm Gỡ hoặc chuột phải vào viên đã khảm để trả nguyên viên về túi với phí theo phẩm viên khảm: Phàm 1.000, Linh 2.000, Huyền 3.000, Địa 4.000, Thiên 5.000, Thánh 6.000, Thần 7.000, Tiên 8.000, Đạo 9.000, Huyền Tiên 10.000 linh thạch, kể cả lỗ đang không kích hoạt; chỉ số được tính lại ngay. Giữ vị trí cuộn khi cập nhật giao diện.

Số lỗ theo phẩm trang bị: Phàm–Địa 1; Thiên–Thần 2; Tiên–Huyền Tiên 3. Chưa mặc đồ có 0 lỗ. Đổi xuống phẩm thấp giữ các viên ở lỗ dư nhưng tạm ngừng cộng chỉ số. Đã bỏ Khai Khiếu Thạch và mục Kho Linh Thạch.

Giữ các ID cũ và phẩm đã lưu. Các ID mở rộng `_t0`, `_t1`, `_t2`, `_t3` lần lượt giữ cấp 2, 3, 6, 7. Những cấp còn thiếu dùng `_lv1`, `_lv4`, `_lv5`, `_lv8`, `_lv9`, `_lv10`. Không dùng trường Tier của đan dược để định giá Linh Thạch; cấp ngọc dùng GemLevel riêng.

## Nguồn nhận

Bản đồ chỉ rơi Phàm/Linh/Huyền, tối đa 1 viên mỗi trận. Gộp hai lượt rơi cũ thành một lượt với xác suất 45,44%; không nhân số lượng theo độ khó. Boss bỏ hai nguồn ngọc cố định cũ, dùng một lượt rơi 35/45/55/65% theo độ khó, cùng giới hạn phẩm và số lượng. Phân phối phẩm được chuẩn hóa từ trọng số độ khó, chỉ giữ cấp 1–3.

Rương thường/hiếm/Boss: 8/15/25% nhận 1 Linh Thạch trong một phần thưởng duy nhất mỗi rương. Tỉ lệ tuyệt đối nhận phẩm Thần lần lượt 0,1/0,3/1%; phần Linh Thạch còn lại phân phối dưới Thần theo độ khó. Không rơi trên Thần. Giữ nhánh thú/công pháp/Ngộ Đạo và cơ chế rương Thiên Cơ/trang phục riêng.

## Loại khảm

Tấn công: Hỏa, Lôi, Phong, Quang, Ám, Linh. Phòng thủ: Băng, Thổ, Phục Nguyên, Vũ Trụ. Mũ/Giày/Áo/Hộ uyển/Quần chỉ nhận phòng thủ; vũ khí/pháp bảo/dây chuyền/hộ phù/nhẫn chỉ nhận tấn công. ID cũ phân loại theo dòng hiệu ứng sau cân bằng. Giữ ngọc đã khảm cũ; server chặn khảm mới sai nhóm trước khi tiêu hao.

Ngọc khảm gắn với từng bản sao trang bị. Khi tháo/đổi, lưu các ô vào GemSlots với khóa bag|itemId|copy; ô đang mặc vẫn giữ định dạng cũ. API equip nhận copy để chọn đúng món trùng tên; ngọc trong túi không cộng chỉ số. Món cũ đã khảm được gắn ngọc hiện tại khi tháo lần đầu, không đổi schema SQLite.
