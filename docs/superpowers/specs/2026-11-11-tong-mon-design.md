# Tông Môn — bản 1 (nền tảng + cửa hàng cống hiến)

Ngày: 2026-11-11. Phần sau (chưa làm): Boss tông môn hằng ngày; cộng Hoạt Lực khi làm việc tông.

## Phạm vi

Bật ở **mọi chế độ** (offline SQLite và online Postgres) — cùng một mã `ServerSect` chạy trên `GameDbContext`.
Không giới hạn số thành viên (server ít người).

## Luật chơi

### Tạo / gia nhập / rời
- Tạo tông: cần Trúc Cơ (`Realm >= 1`), phí = `DailyQuestData.StoneBase(realm)` linh thạch, tối thiểu 10.000.
  Tên 2–16 ký tự sau `ChatRules.Clean`, không trùng (so không phân biệt hoa thường). Mỗi người chỉ ở một tông.
- Gia nhập: gửi đơn (tối đa 3 đơn chờ cùng lúc). Tông chủ / Trưởng lão duyệt hoặc từ chối.
  Tông có thể bật **tự duyệt** kèm **cảnh giới tối thiểu**: đơn đủ điều kiện được nhận ngay.
  Khi vào một tông, mọi đơn khác của người đó bị xóa.
- Rời / bị trục xuất: chờ 24 giờ mới gửi đơn hay tạo tông mới. Cống hiến cá nhân chưa tiêu được giữ.
- Tông chủ phải nhường chức trước khi rời; tông chỉ còn Tông chủ thì "rời" = giải tán.
- Tông chủ vắng 7 ngày (theo `Players.LastTickAt`): khi có người mở trang tông, chức chuyển cho
  Trưởng lão có tổng cống hiến cao nhất, không có thì đệ tử có tổng cống hiến cao nhất (chỉ xét người không vắng 7 ngày).

### Chức vị
| Quyền | Tông chủ | Trưởng lão (tối đa 2) | Đệ tử |
|---|---|---|---|
| Duyệt / từ chối đơn | ✓ | ✓ | |
| Trục xuất | mọi người | chỉ đệ tử | |
| Bổ nhiệm / bãi nhiệm Trưởng lão, nhường chức | ✓ | | |
| Sửa thông báo | ✓ | ✓ | |
| Nâng cấp tông, cài đặt duyệt, giải tán | ✓ | | |
| Điểm danh, quyên góp, cửa hàng, chat | ✓ | ✓ | ✓ |

### Cống hiến và quỹ tông
- Mỗi điểm cống hiến người chơi kiếm được cũng cộng 1 vào **quỹ tông**.
- Điểm danh mỗi ngày (0h giờ Việt Nam): +10.
- Quyên linh thạch: tối đa 3 lần/ngày, mức Nhỏ / Vừa / Lớn = 2% / 5% / 10% `StoneBase(realm)`
  (tối thiểu 1.000 / 2.500 / 5.000), nhận +10 / +30 / +70.

### Cấp tông (Tông chủ bấm nâng cấp, trừ quỹ)
| Cấp | Quỹ để lên cấp này | Buff tu luyện | Cửa hàng mở thêm |
|---|---|---|---|
| 1 | — | +2% | Linh Thú Đan, Bổ Nguyên Hạ |
| 2 | 500 | +4% | vé Tài Xỉu, Bổ Nguyên Trung |
| 3 | 1.500 | +6% | vé Linh Thú |
| 4 | 4.000 | +8% | Bổ Nguyên Thượng, Hóa Hình Đan |
| 5 | 10.000 | +10% | Thú Đan Huyền |

Buff tu luyện cộng vào `Rate(p)` cùng nhóm % với trang bị/công pháp.

### Cửa hàng cống hiến
Giá bằng điểm cống hiến cá nhân, giới hạn lượt mua mỗi tuần (tuần bắt đầu 0h thứ Hai giờ VN).
Giá/giới hạn đặt trong `SectData.Shop`, cân theo rương Hoạt Lực.

### Chat tông
Khung chat thêm tab "Tông Môn" (chỉ hiện khi có tông). Dùng chung `ChatRules` (lọc chữ, cooldown, cảnh giới).
Tin hệ thống của tông: vào/rời/trục xuất, lên cấp, đổi chức.

## Kỹ thuật

### DB
- `Sects`: Id (long), Name, Level, Fund, Notice, AutoAccept, MinRealm, LeaderId, CreatedAt.
- `SectMembers` (khóa PlayerId, giữ dòng sau khi rời): SectId (null = không có tông), Role, Contribution,
  TotalContribution, JoinedAt, LeftAt, CheckinDay, DonateDay, DonateCount, ShopWeek, ShopBought.
- `SectApplications`: Id, SectId, PlayerId, At.
- `ChatMessages.SectId` (long?, null = kênh Thế giới).
- Migration EF trong `Online/Migrations/`, nối SQL vào `supabase/migrations/20261026_online_authority.sql`,
  `SchemaUpgrade` tạo bảng/cột cho SQLite đã có.

### Mã
- `Game/SectData.cs`: hằng số, bảng cấp, cửa hàng, quyền.
- `Game/ServerSect.cs`: truy vấn và ghi DB tông (quỹ cộng bằng UPDATE có điều kiện / atomic increment).
- `Game/GameService.Sect.cs`: hành động có đụng hành trang/linh thạch nhân vật (tạo, quyên, mua).
- `Game/Dtos.cs`: `SectPageDto` và các DTO con. API `/players/{id}/sect/...` trong `Program.cs`.
- Buff: cấp tông của người chơi đọc qua bộ đệm tĩnh 60 giây theo PlayerId, gắn vào thuộc tính `[NotMapped]` của `Player`
  để `Rate(p)` dùng; không thêm cột vào `Players`.
- UI: `wwwroot/js/sect.js`, `wwwroot/css/sect.css`, tab chat trong `chat.js`, chữ tiếng Anh trong `i18n-en.js`,
  ghi chú phát hành ở `docs/releases/upcoming.md`.

### Không đụng
`DailyQuestData.cs`, `GameService.DailyQuest.cs`, và các file sự kiện Thú Triều.

## Test
- C# (SQLite in-memory): tạo tông (phí, tên trùng, cảnh giới), đơn/duyệt/tự duyệt, rời + chờ 24h, giải tán,
  phân quyền, Tông chủ vắng 7 ngày, điểm danh/quyên (giới hạn ngày), nâng cấp, giới hạn tuần cửa hàng,
  chat tông tách kênh Thế giới, buff vào `Rate`.
- JS: hàm hiển thị trong `sect.js`.
- Thử tay trên `--web`.
