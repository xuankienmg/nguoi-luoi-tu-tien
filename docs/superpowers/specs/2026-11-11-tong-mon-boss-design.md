# Tông Môn — bản 2 (Boss tông môn hằng ngày + giao diện sơn môn)

Ngày: 2026-11-11. Nối tiếp `2026-11-11-tong-mon-design.md` (bản 1: tông, quỹ, cống hiến, cấp tông, cửa hàng, chat).
Phần sau (chưa làm): cộng Hoạt Lực khi làm việc tông.

## Phạm vi

Bật ở **mọi chế độ** (offline SQLite và online Postgres), cùng mã `ServerSect`* trên `GameDbContext`.
Gồm: (1) Boss tông hằng ngày; (2) đổi giao diện tab Tông Môn thành sơn môn có điểm bấm theo ảnh.

## Luật chơi

### Boss mỗi ngày

- Mỗi ngày (0h giờ Việt Nam) mỗi tông có 1 Boss, xoay vòng trong danh sách Boss của `SectBossData`.
- Máu Boss chốt lần đầu có người mở trang Boss trong ngày:
`MaxHp = 60% × Σ BossRef của thành viên hoạt động × 2 lượt`.
  - Thành viên hoạt động: không vắng quá 3 ngày (`Players.LastTickAt`).
  - `BossRef` = sát thương của lượt đánh gần nhất của người đó; người chưa từng đánh lấy trung bình của những người đã có;
  chưa ai có thì lấy sát thương mô phỏng của người đầu tiên mở trang Boss (luôn có ít nhất một đơn vị, nên tông 1 người vẫn có Boss hợp lý).
- Máu chốt xong không đổi trong ngày, dù có người vào/rời tông.

### Một lượt đánh

- Mỗi người **2 lượt/ngày**, làm mới 0h.
- Server chạy `PvpCombat` giữa nhân vật và **Boss bản sao của chính họ** (cùng chỉ số, công ×1,6, thủ ×1,2, máu tối đa, không linh thú / thần thông).
Mọi cảnh giới đều đánh được; build và công pháp đều được tính.
- Trận tối đa **15 hiệp**; máu Boss trong trận coi như không cạn. Nhân vật chết thì trận dừng.
- **Sát thương gây ra** (kể cả phần trúng khiên Boss) cộng vào `Damage` chung của Boss và vào `BossRef` của người đánh.
- Client chỉ hiện kết quả (sát thương, số hiệp), không diễn trận.

### Thưởng

1. **Cống hiến theo sát thương:** nhận ngay mỗi lượt = `clamp(300 × sát thương / MaxHp, 5, 30)`. Cộng cả vào quỹ tông như bản 1.
2. **Rương chung:** hạ Boss trước 0h thì mọi người đã đánh ít nhất 1 lượt nhận 1 rương, nhận thủ công. Rương chỉ nhận được trong chính ngày hạ Boss (trước 0h).

   Khởi điểm: linh thạch 3 × `StoneBase(cảnh giới)` + 1 Bổ Nguyên Trung phẩm + 50 Linh Thú Đan.
3. **Top 3 sát thương:** thêm +50 / +30 / +20 cống hiến, nhận cùng lúc với rương.
4. **Buff toàn tông 24h:** hạ Boss hôm nay thì cả tông được **+3% tu vi** trong cả ngày hôm sau, cộng cùng nhóm với buff cấp tông trong `Rate(p)`.

Boss không hạ kịp trước 0h thì không có rương, top, buff; cống hiến theo sát thương vẫn giữ.
Boss đã hạ thì chặn đánh thêm, nên bảng top cố định.
Các số trên là khởi điểm, chỉnh khi cân bằng (skill `can-bang-chi-so`).

## Giao diện sơn môn

Ảnh nền: sơn môn (nguồn `C:\Users\KienBui\.codex\generated_images\01a1c9eb-37be-7dd3-8e9f-74f59c63522f\exec-1a0e2894-c64d-4404-9542-3824acdda3d7.png`, 1536×1024).
Nén thành `wwwroot/img/sect/son_mon.jpg`.

| Vị trí trong ảnh | Tên | Mở ra |
|---|---|---|
| Chính điện (giữa trên) | Chính Điện | Tổng quan: cấp tông, nâng cấp, thông báo, cài đặt nhận đệ tử, rời tông |
| Tháp chuông (trái trên) | Tháp Đệ Tử | Thành viên + đơn xin vào (chấm đỏ khi có đơn) |
| Nhà kệ sách (trái) | Tàng Bảo Các | Cửa hàng cống hiến |
| Nhà có lò đan (phải) | Đan Phòng | Điểm danh + quyên góp |
| Bệ phong ấn, tượng khổng lồ (phải trên) | Bệ Phong Ấn | Boss tông (chấm đỏ khi còn lượt) |

- Màn mặc định của tab Tông Môn (khi đã có tông): ảnh sơn môn tỉ lệ 3:2, 5 nút đặt theo toạ độ %, thanh tên tông / cấp / buff / quỹ phía trên.
- Bấm công trình: nội dung thay ảnh; hàng tab (tab đầu là "Sơn môn") luôn hiện để quay lại và làm lối vào dự phòng. Nội dung các view cũ (`sectInfo`, `sectMembers`, `sectApps`, `sectShop`) giữ nguyên; thêm `sectDaily` tách từ `sectInfo` cho Đan Phòng và `sectBossView` mới.
- Màn hẹp: ảnh co theo khung.
- Chưa có tông: `sectLobby` giữ nguyên.
- View Boss: ảnh + tên Boss, thanh máu chung, hai ô lượt và nút **Tấn công**, kết quả lượt vừa đánh, bảng sát thương trong ngày (top 3 nổi bật), nút **Nhận rương** sau khi hạ Boss, dòng buff còn lại, trạng thái (đang đánh / đã hạ / hết ngày chưa hạ).
- Ảnh Boss luân phiên dùng lại ảnh quái sẵn có; ảnh tượng khổng lồ chỉ làm nền nút Bệ Phong Ấn.
- Chữ mới thêm bản tiếng Anh vào `wwwroot/js/i18n-en.js`.

## Kỹ thuật

### DB

- `SectBosses` (khoá SectId + Day): BossId, MaxHp, Damage, DefeatedAt. Day = ngày giờ VN.
- `SectBossHits` (khoá SectId + Day + PlayerId): Hits (≤ 2), Damage, ChestClaimed.
- `SectMembers.BossRef` (double). Mọi cột sát thương / máu là `double`: số trận có thể vượt `long`, và cộng dồn được bằng một UPDATE trên cả SQLite lẫn Postgres.
- `Sects.BuffDay` (ngày buff có hiệu lực; hạ Boss hôm nay thì = ngày mai).
- Migration EF trong `Online/Migrations/`, nối SQL vào `supabase/migrations/20261026_online_authority.sql`,
`SchemaUpgrade` tạo bảng/cột cho SQLite đã có.
- `SectBuff`: bộ đệm 60 giây đọc thêm `BuffDay`; `QiPct` = buff cấp tông + 3% khi buff còn hiệu lực.

### Đồng thời

- Số lượt: UPDATE có điều kiện `Hits < 2`; sát thương: `Damage = Damage + x`.
- Hạ Boss: UPDATE có điều kiện `DefeatedAt IS NULL`; chỉ người thắng cuộc đua đặt `BuffDay` và đánh dấu hạ.
- Nhận rương: UPDATE có điều kiện `ChestClaimed = false`.

### Mã

- `Game/SectBossData.cs`: danh sách Boss, hệ số công/thủ, hằng số máu / cống hiến / buff, bảng thưởng.
- `Game/ServerSectBoss.cs`: đọc/ghi DB, chốt máu, xếp hạng.
- `Game/GameService.SectBoss.cs`: chạy `PvpCombat`, phát thưởng vào hành trang.
- `Game/Dtos.cs`: `SectBossDto` và DTO con.
- API trong `Program.cs`: `GET /players/{id}/sect/boss`, `POST .../sect/boss/attack`, `POST .../sect/boss/chest`.
- UI: `wwwroot/js/sect.js`, `wwwroot/css/sect.css`; ghi chú phát hành ở `docs/releases/upcoming.md`.

### Lỗi cần chặn

Chưa có tông · hết lượt · Boss đã hạ · sang ngày giữa chừng · bị trục xuất giữa chừng · nhận rương hai lần · chưa đánh lượt nào mà đòi rương.

### Không đụng

`DailyQuestData.cs`, `GameService.DailyQuest.cs`, các file sự kiện Thú Triều.

## Test

- C# (SQLite in-memory): máu Boss co theo thành viên hoạt động và có sàn; giới hạn 2 lượt/ngày; sát thương cộng dồn;
hạ Boss kích hoạt buff và rương đúng một lần; top 3 đúng thứ tự; buff vào `Rate`; đổi ngày reset; người ngoài tông không đánh được;
trận mô phỏng dừng đúng 15 hiệp.
- JS: hàm thuần trong khối `sect-pure` của `sect.js` (trạng thái Boss, thanh máu, vị trí nút).
- Thử tay trên `--web`.
