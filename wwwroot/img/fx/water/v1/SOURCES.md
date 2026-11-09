# VFX Thủy · bộ runtime v1

Mỗi thư mục theo skill ID chứa một atlas WebP RGBA và `sheet.json` ghi nguồn demo, kích thước ô, điểm neo và mốc va chạm. Các atlas lấy từ demo chuyển thể các mẫu Hỏa của dự án. Nguồn Hỏa và thông tin tác giả nằm tại `wwwroot/img/fx/fire/<tên mẫu>/LICENSE.txt` và `source-demo.json`.

Ô Nguyên Anh `thuong_hai` dùng **Thâm Hải Tuyền Qua v1** tại `tham-hai-tuyen-qua-v1/`. Nguồn dựng ở `output/tham-hai-tuyen-qua-demo-v1/`, phần nước vỡ lấy từ atlas Thủy Lao Bạo. Mốc sát thương khớp khung 51, khoảng 1,13 giây trong trận. Chuyển động cuốn quái thực hiện trên sprite đang hiển thị và giữ điểm neo vòng xoáy cố định.

Ô Hóa Thần `thien_ha` dùng **Nộ Hải Cuồng Đào v2** tại `no-hai-cuong-dao-v2/`. Master sóng ImageGen và prompt nằm trong `output/no-hai-cuong-dao-demo-v2/`. Bộ dựng đặt một atlas tổng sau tu sĩ và trước quái, với sát thương ở khung 48, khoảng 1,07 giây trong trận.

Ô Luyện Hư `hu_hai` dùng **Vạn Thủy Thiên Tiễn v1** tại `van-thuy-thien-tien-v1/`. Nguồn dựng ở `output/van-thuy-thien-tien-demo-v1/`. Sóng va chạm lấy từ atlas Thủy Lao Bạo, với nguồn Hỏa ban đầu ghi ở `wwwroot/img/fx/fire/thien-hoa-giang-thach-v10/LICENSE.txt`. Mốc sát thương khớp khung 46, khoảng 1,02 giây trong trận.

Các sheet có 60 hoặc 90 khung ở tốc độ dựng 30 FPS. Game phát toàn bộ khung trong 2 giây, theo lịch tuyệt kỹ chung. Mỗi atlas giải mã tối đa 16 MiB RGBA.

Tái đóng gói từ các demo cục bộ bằng `python tools/pack_water_vfx.py` với Pillow. Thư mục `output/` chứa các nguồn dựng cục bộ.
