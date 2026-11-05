# Thiên Sơn Băng Kiếm v4 — trong game

Gắn vào Hư Không Băng Ngục (`hu_khong_bang_nguc`, Luyện Hư). Giữ ID, chỉ số và logic khống chế. VFX độc lập với actor: kiếm thon từ pháp trận trên trời, chạm đất rồi tinh thể Huyền Băng trồi lên.

90 khung / 30 FPS / 3 giây. Game dùng `sheet-runtime-v1.png`: 2400×1485, 10×9 ô 240×165, neo [170,135]. Giữ `sheet.png` 4800×2970 làm bản dựng gốc. Runtime giảm 75% bộ nhớ RGBA (57 MB → 14 MB), không giảm FPS/số khung; đóng gói bằng `tools/ice_fx/pack_runtime.py`. Damage ở khung 48 / 1.6 giây. Sau VFX là 0.25 giây phản kích; combo/pet dùng lịch chung. Giảm chuyển động giữ khung 60, lỗi/chưa tải dùng renderer cũ.

Hiển thị giữ một snapshot trận có tuyệt kỹ đến hết animation rồi bắt kịp trận mới nhất, không tích hàng đợi. Khóa quái tụ lực/phản kích trong lúc thi triển. Trạng thái thưởng/server và tốc độ kiếm tài nguyên không bị trì hoãn; thay đổi này không sửa `ExploreSeconds`.

Nguồn: output/ice-mountain-sword-demo-v4/. Sprite Fusion Ice MIT giữ trong LICENSE.txt. Tích hợp: wwwroot/js/fire-fx.js, battle-fx.js, battle.js, Game/BattleTiming.cs.
