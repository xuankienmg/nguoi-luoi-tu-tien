# Kim Long Thiên Ấn · Thiên Quang v4

- `atlas.png` gồm pháp tướng Kim Long, Thiên Ấn, các vòng ấn mục tiêu và cột Holy Light. Nguồn cột sáng Sprite Fusion Holy Light của Hugo Duprez, giấy phép MIT trong `source/holy-light-LICENSE.txt`.
- Hai WebP trong `explosions/` lấy nguyên file từ `t/outputs/vfx-no-cac-he/`. `demo.json` ghi điểm neo trong ảnh nguồn, tỷ lệ demo và khung bắt đầu của từng file.
- Khi phát trong game, neo atlas theo chân tu sĩ và phát một lần WebP nổ đã chọn tại chân mục tiêu, bắt đầu ở khung 45. Bản xem trước đã ghép sẵn hai lớp để có thể đọc trực tiếp trong trình duyệt.

Chạy `python build.py` với Pillow để dựng lại atlas và hai bản xem trước.
