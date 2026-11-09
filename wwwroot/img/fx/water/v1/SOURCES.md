# VFX Thủy · bộ runtime v1

Mỗi thư mục theo skill ID chứa một atlas WebP RGBA và `sheet.json` ghi nguồn demo, kích thước ô, điểm neo và mốc va chạm. Các atlas lấy từ demo chuyển thể các mẫu Hỏa của dự án. Nguồn Hỏa và thông tin tác giả nằm tại `wwwroot/img/fx/fire/<tên mẫu>/LICENSE.txt` và `source-demo.json`.

Ô Nguyên Anh `thuong_hai` dùng **Thâm Hải Tuyền Qua v1** tại `tham-hai-tuyen-qua-v1/`. Nguồn dựng ở `output/tham-hai-tuyen-qua-demo-v1/`, phần nước vỡ lấy từ atlas Thủy Lao Bạo. Mốc sát thương khớp khung 51, khoảng 1,13 giây trong trận. Chuyển động cuốn quái thực hiện trên sprite đang hiển thị và giữ điểm neo vòng xoáy cố định.

Ô Hóa Thần `thien_ha` dùng **Vạn Thủy Thiên Tiễn v1** tại `van-thuy-thien-tien-v1/`. Giữ nguyên atlas chuyển từ ô Luyện Hư. Sát thương khớp khung tên chạm đầu tiên, khung 46, khoảng 1,02 giây trong trận. Bản sóng cũ lưu tại `no-hai-cuong-dao-v2/`.

Ô Luyện Hư `hu_hai` dùng **Cửu U Thủy Xà v6** tại `cuu-u-thuy-xa-v6/`. Nguồn dựng ở `output/cuu-u-thuy-xa-demo-v6/`, ghép VFX xà v1 với vòm nổ từ Thái Sơ Thương Minh (`thai_so_thuy`). Sát thương hiện ở cú cắn đầu, khung 43, khoảng 0,96 giây trong trận. Vòm nổ mở ở khung 68, khoảng 1,51 giây.

Các sheet có 60 hoặc 90 khung ở tốc độ dựng 30 FPS. Game phát toàn bộ khung trong 2 giây, theo lịch tuyệt kỹ chung. Mỗi atlas giải mã tối đa 16 MiB RGBA.

Tái đóng gói từ các demo cục bộ bằng `python tools/pack_water_vfx.py` với Pillow. Thư mục `output/` chứa các nguồn dựng cục bộ.
