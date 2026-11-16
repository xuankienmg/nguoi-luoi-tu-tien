# Tích hợp Thiên Sơn Băng Kiếm

Áp dụng VFX v4 vào `hu_khong_bang_nguc` (Hư Không Băng Ngục, tuyệt kỹ Luyện Hư), giữ ID và chỉ số gameplay. Dùng bộ dựng nhân vật riêng.

1. Thêm kiểm thử timeline 90 khung/3s, damage tại 1.6s, miss, phản kích/combo, và giữ timing Xích Diễm.
2. Đưa PNG RGBA và JSON/giấy phép vào `wwwroot/img/fx/ice/thien-son-bang-kiem-v4/`.
3. Khai báo sheet riêng, ánh xạ thời gian thực vào progress .45 của damage, vẽ đúng tỉ lệ 480:330 và neo mặt đất. Reduced motion dùng một khung tĩnh, fallback giữ bộ dựng cũ khi chưa tải.
4. Đồng bộ thời gian lượt frontend/backend (3s + .25s phản kích), giữ các chiêu khác. Preview dùng cùng timeline.
5. Chạy kiểm thử Node, C# và dựng ảnh kiểm tra bằng renderer thật ở preview và battle; ghi nhận việc tích hợp.
