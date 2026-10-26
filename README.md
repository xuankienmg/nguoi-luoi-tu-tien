# Người Lười Tu Tiên — NguoiLuoiTuTien (C# / ASP.NET Core + SQLite)

Game tu tiên 2D. Server C# xử lý toàn bộ luật chơi và lưu tiến độ vào SQLite; trình duyệt chỉ hiển thị canvas và gửi hành động.

## Giới thiệu — Tu tiên ngay trên desktop

**Người Lười Tu Tiên** dành cho những ai muốn nuôi dưỡng một tu sĩ trong lúc làm việc hoặc học tập. Game xuất hiện dưới dạng một cửa sổ nhỏ cạnh thanh taskbar: nhân vật tự thám hiểm, chiến đấu và tích lũy tu vi, còn bạn lựa chọn trang bị, công pháp và thời điểm đột phá.

Hành trình bắt đầu từ Luyện Khí và mở rộng qua 10 cảnh giới đến Chân Tiên. Mỗi bước tiến gắn với việc thu thập tài nguyên, luyện đan, xây dựng bộ trang bị và vượt qua những thử thách mới. Bạn có thể quay lại các ải đã mở để tích lũy vật phẩm, chuẩn bị cho trận Boss và lần đột phá tiếp theo.

Ngoài tu luyện, game còn có hệ thống linh thú: ấp trứng, nuôi dưỡng, tiến hóa và trang bị cho bạn đồng hành. Công pháp và các ô kỹ năng giúp bạn điều chỉnh sức mạnh của tu sĩ; hành trang, luyện chế và các hoạt động vòng quay bổ sung những hướng phát triển khác trong quá trình chơi.

Tiến độ được lưu cục bộ bằng SQLite. Bạn có thể chơi bằng cửa sổ desktop hoặc mở giao diện trong trình duyệt theo hướng dẫn bên dưới.

> **Trạng thái: đang phát triển và cập nhật.** Nội dung, hình ảnh và cân bằng có thể thay đổi giữa các phiên bản. Một số thông số trong phần hướng dẫn bên dưới mô tả cơ chế cũ; hãy đối chiếu với giao diện game và [wiki](site/README.md) được xuất từ dữ liệu hiện tại.

## Chạy

Cần .NET 8 SDK và Windows.

```
cd VanDao
dotnet run
```

File chạy sau khi build là `NguoiLuoiTuTien.exe`. Dữ liệu tiếp tục dùng thư mục `%LocalAppData%\VanDao` để giữ tiến độ từ bản cũ.

Game chạy dưới dạng **dải nhỏ 280×200 nằm sát ngay trên thanh taskbar**, giống cách "TBH: Task Bar Hero" hoạt động (cần Microsoft Edge WebView2 Runtime, có sẵn trên Windows 10/11):

- **Chuột phải** lên dải (hoặc nút bánh răng): menu cài đặt gồm bố cục màn hình, kích thước (75–200%), ghim luôn hiển thị, đặt lại vị trí, ẩn xuống khay, thoát.
- **Bố cục màn hình**: tự động theo vị trí taskbar, hoặc neo trái / giữa / phải ở mép dưới hay mép trên. Kéo thanh tiêu đề (chỗ tên cảnh giới) để đặt tự do.
- **Nút Tu luyện** (quyển sách): mở cửa sổ nhỏ Đột phá / Hành trang / Đan dược / Bản đồ bật lên phía trên dải. Esc hoặc nút đóng để quay về dạng dải.
- **Shift+F12**: đặt lại vị trí khi dải bị lạc. **Shift+F11**: đặt lại kích thước.
- **Khay hệ thống**: bấm biểu tượng để ẩn/hiện, chuột phải để mở menu.

Mọi cài đặt được nhớ cho lần mở sau.


## Đan dược, Bản đồ và Boss

- **Linh thạch**: chỉ một loại, dùng cho lên tầng, hợp thành đan và đột phá.
- **Đan dược** (hợp thành 9 → 1, luôn thành công): Tụ Khí Đan →(9)→ Trúc Cơ Đan →(9)→ Ngưng Kim Đan →(9)→ Kết Anh Đan. Mỗi lần tốn một ít linh thạch. Đan chỉ dùng để lên tầng và đột phá, không hồi máu hay linh khí.
- **Bản đồ**: mỗi cảnh giới có 20 ải (1-1 → 1-20). Nhân vật tự đánh ải đang chọn; thắng ải cao nhất thì mở ải kế, thua thì lùi một ải. Có thể chọn ải thấp hơn để cày.
- **Boss (ải 1-21)**: cần chìa khóa rơi ở ải 1-1 đến 1-20 (Luyện Khí: Hư Thiên Tàn Đồ · Trúc Cơ: Huyết Sắc Lệnh Bài · Kim Đan: Côn Ngô Sơn Lệnh). Thắng Boss nhận yêu hạch, chìa khóa chỉ mất khi thắng.
- **Đột phá đại cảnh giới**: đầy tu vi + 1 đan bậc kế + linh thạch + 1 yêu hạch Boss (Luyện Khí → Trúc Cơ cần Kim Ô Tinh Hạch, Trúc Cơ → Kim Đan cần Huyết Giao Yêu Hạch) + thể trạng ổn định. Tỉ lệ gốc 65% (Trúc Cơ) / 50% (Kim Đan), mỗi lần thất bại +10%, tối đa 95%.
  - Thành công: vào cảnh giới mới. Đại thành công (20% số lần thành công): +10% máu và phòng thủ gốc vĩnh viễn.
  - Thất bại: mất đan và linh thạch (giữ yêu hạch), −10% tu vi, điều tức 1 phút.
  - Tẩu hỏa nhập ma (25% số lần thất bại): −25% tu vi, điều tức 2 phút, suy yếu 5 phút (tu vi ×0.7, công/thủ −10%).
  - Tab Đột phá: tu sĩ ngồi thiền giữa trận pháp, 5 giai đoạn đột phá, màn hình kết quả. Lên tầng nhỏ luôn thành công.
- **Bán trang bị**: trong Hành trang chọn món rồi bấm Bán (phím B) để nhận linh thạch; nút đồng xu cạnh nút sắp xếp để bán hàng loạt theo độ hiếm. Giá theo độ hiếm (3 / 12 / 50 / 200 / 800) × (1 + 4 × cảnh giới yêu cầu). Đồ từ Bảo phẩm trở lên phải bấm hai lần.
- **Công pháp** (tab trong cửa sổ Tu luyện): cây kỹ năng theo hệ. Hỏa hệ đã mở với 15 kỹ năng (Căn cơ, Công, Thủ, Khống, Thân pháp, Tuyệt kỹ); Thủy, Mộc, Kim, Thổ, Lôi, Băng, Phong đã có khung cây, chưa mở khóa.
  - Điểm kỹ năng: 1 ban đầu, +1 mỗi lần lên tầng, +3 mỗi đại cảnh giới, +2 mỗi lần hạ Boss. Dùng để lĩnh ngộ và nâng cấp.
  - Kỹ năng có tác dụng bị động thật trong chiến đấu: tăng công / thủ / máu / bạo kích / thân pháp / tốc chạy / tốc độ tu luyện, khống chế làm giảm công của quái.
  - Ô kỹ năng: 3 ô Chiêu thức (Công / Thủ / Khống / Thân pháp) + 1 ô Tuyệt kỹ, hiện ở đầu tab Công pháp và dưới nhân vật trong Hành trang. Lĩnh ngộ chiêu mới tự đặt vào ô trống; nút "Trang bị vào ô" / "Tháo", hoặc kéo nút kỹ năng vào ô, kéo ra ngoài để tháo.
  - Trong trận (mô phỏng từng lượt ở server): chiêu Công tự thi triển theo lượt (Hỏa Cầu cứ 3 lượt…), Thủ giảm sát thương nhận, Khống làm quái đứng yên vài lượt đầu, Thân pháp né đòn. Dải game hiện tên chiêu, hoạt ảnh Hỏa Cầu và sát thương chiêu.
  - Thêm hệ mới: khai báo trong `Game/SkillData.cs` (Open = true, các SkillDef với Row/Col trên lưới 7×5 và Req), giao diện tự vẽ cây.
  - `wwwroot/img/fx_hoa_cau.png`: sprite sheet Hỏa Cầu 8 khung 48×48 (khởi thế → vận khí → thành hình → phóng → bay → va chạm → nổ → tan).
- Save cũ tự quy đổi: 3 loại linh thạch → Linh thạch (×1 / ×50 / ×1500), đan phẩm chất → đan gốc, nguyên liệu luyện đan cũ → Linh thạch.

## Cách chơi

### Tiến trình mức Khó và Ác Mộng

- **Luyện Hư:** bắt đầu các chương đầu mức Khó. Đột phá lên Hợp Thể cần Ma Viên Hạch từ Boss chương 1 mức Khó.
- **Hợp Thể:** tiến đến các chương cuối mức Khó. Đột phá lên Đại Thừa cần Cùng Kỳ Vương Hạch từ Boss chương 10 mức Khó.
- **Đại Thừa:** tiến từ đầu đến giữa mức Ác Mộng (chương 1–5). Đột phá lên Độ Kiếp cần Độ Kiếp Đan, 1.500.000 linh thạch và Ác Mộng Cùng Kỳ Hạch từ Boss chương 5 Ác Mộng.
- **Độ Kiếp:** tiếp tục các chương sau Ác Mộng. Đột phá lên Chân Tiên cần Chân Tiên Đan, 4.000.000 linh thạch và Ác Mộng Cùng Kỳ Vương Hạch từ Boss chương 10 Ác Mộng.
- **Chân Tiên:** cảnh giới cuối, gồm Sơ kỳ, Trung kỳ và Hậu kỳ. Hai cảnh giới mới có trang phục nam/nữ, sheet đứng nghỉ/bay/chém/thi pháp, tư thế thiền, hai bộ Thiên Kiếp/Thái Sơ cấp 45/50 và nhánh công pháp mới cho cả bảy hệ.

Khả năng vượt ải dựa trên tổng sức mạnh thực chiến: chỉ số cảnh giới, trang bị và hiệu ứng bộ, công pháp đang dùng và bị động, linh thú cùng cấp, sao, tư chất, tiến hóa, thức tỉnh và trang bị thú. Các nguồn có thể bù cho nhau; không bắt buộc đủ 10 ô trang bị để vào Boss. Nguyên liệu đột phá mức Khó chỉ rơi từ đúng Boss, đúng độ khó và được giữ lại khi đột phá thất bại.

- **Tự động thám hiểm** (luôn bật): mỗi chuyến nhân vật đi qua rừng, gặp quái và giao chiến theo lượt. Cảnh đánh là pixel art (sprite 48×48 trong `wwwroot/img/*_48.png`, vẽ ở độ phân giải thấp rồi phóng to không làm mịn): tu sĩ bên trái, quái bên phải, vệt kiếm khí, số sát thương, thanh máu và vòng chọn mục tiêu; hoạt ảnh khớp đúng kết quả trận do server tính; xong chuyến nhân vật nhận thêm tu vi và vật phẩm rơi theo tỉ lệ của cảnh giới. Thanh nhỏ cạnh các nút cho biết thời gian đến chuyến tiếp theo.
- **Tu vi tự tăng** (mặc định 3/giây ở Luyện Khí tầng 1), kể cả khi tắt game (tối đa 2 giờ). Khi mở lại, các chuyến thám hiểm lúc vắng mặt được cộng gộp vào một dòng nhật ký.
- **Chỉ số chiến đấu**: Tấn công, Máu, Phòng thủ = chỉ số gốc theo cảnh giới/tầng + trang bị. Trận đánh theo lượt, không ngẫu nhiên: sát thương = tấn công − phòng thủ đối thủ. Thắng thì nhận tu vi và vật phẩm; thua thì rút lui, chỉ nhận nửa tu vi. Tấn công cao hạ quái nhanh hơn nên chuyến thám hiểm ngắn hơn.
- **Trang bị** (rơi hiếm khi thám hiểm): kiếm/đao tăng tấn công; áo, quần, nón tăng máu và phòng thủ; găng tay tăng tấn công và phòng thủ; nhẫn tăng tu vi và may mắn. Bấm vào món trong Hành trang để mặc, bấm vào ô đang mặc để tháo.
- Các nút là icon; rê chuột lên để xem tooltip mô tả.

Ảnh nhân vật và trang bị nằm trong `wwwroot/img`.

Muốn chơi trên trình duyệt như trước: `dotnet run -- --web` rồi mở http://localhost:5080.

Dữ liệu nằm trong `%LocalAppData%\VanDao`: `vandao.db` (tiến độ), `window.json` (cài đặt cửa sổ), `WebView2\` (cache).

## Cấu trúc

| File | Nội dung |
|---|---|
| `Program.cs` | Cấu hình DI, EF Core, các API endpoint |
| `Game/GameData.cs` | Vật phẩm, cảnh giới, ải và Boss, chuỗi hợp thành đan, tỉ lệ rơi |
| `Game/GameService.cs` | Luật chơi: tu vi theo thời gian, vận công, thám hiểm, đột phá, hợp thành đan, ải và Boss |
| `Game/Dtos.cs` | Dữ liệu trả về cho client |
| `Data/Entities.cs` | Bảng `Players`, `Inventory`, `Equipment`, `Logs` |
| `Data/GameDbContext.cs` | EF Core DbContext |
| `Desktop/GameWindow.cs` | Dải game trên taskbar: neo vị trí, menu chuột phải, phím tắt, khay hệ thống |
| `Desktop/PanelWindow.cs` | Cửa sổ Tu luyện bật lên (Đột phá / Hành trang / Đan dược / Bản đồ) |
| `Desktop/WindowSettings.cs` | Lưu cài đặt cửa sổ |
| `Desktop/WebHost.cs`, `Native.cs`, `Theme.cs` | WebView2 dùng chung, Win32 API, giao diện menu tối |
| `wwwroot/index.html` | Giao diện, tooltip, cảnh 2D trên canvas |
| `wwwroot/img/` | Sprite nhân vật theo cảnh giới, yêu lang, icon trang bị và vũ khí |

## API

| Method | Đường dẫn | Mô tả |
|---|---|---|
| GET | `/api/data` | Dữ liệu tĩnh (vật phẩm, cảnh giới, công thức) |
| POST | `/api/players` | Tạo tu sĩ mới |
| GET | `/api/players/current` | Tu sĩ đang chơi trên máy này (gần nhất), chưa có thì tạo mới |
| GET | `/api/players/{id}` | Trạng thái hiện tại |
| POST | `/api/players/{id}/channel` | Vận công |
| POST | `/api/players/{id}/equip/{itemId}` | Mặc trang bị |
| POST | `/api/players/{id}/unequip/{slot}` | Tháo trang bị |
| POST | `/api/players/{id}/advance` | Đột phá tầng / cảnh giới |
| POST | `/api/players/{id}/synth/{itemId}?n=` | Hợp thành 9 → 1 (n = số lần, 0 = tối đa) |
| POST | `/api/players/{id}/skill/{skillId}` | Lĩnh ngộ / nâng cấp kỹ năng |
| POST | `/api/players/{id}/skillslot/{slot}?skill=` | Đặt kỹ năng vào ô 0–3 (bỏ skill = tháo) |
| POST | `/api/players/{id}/sell/{itemId}?n=` | Bán trang bị lấy linh thạch |
| POST | `/api/players/{id}/sell-bulk?max=` | Bán hết trang bị có độ hiếm ≤ max (0–4) |
| POST | `/api/players/{id}/area/{area}` | Chọn ải 1–20, 21 = Boss (cần chìa khóa) |
| POST | `/api/players/{id}/reset` | Tu lại từ đầu |

## Thêm cảnh giới mới

Trong `Game/GameData.cs`:

1. Thêm vật phẩm mới vào `Items`.
2. Thêm `RealmDef` vào cuối `Realms`.
3. Thêm công thức vào `Recipes`.
4. Điền `BreakCost` và `BreakChance` cho Kim Đan để mở đường đột phá.
5. Xóa tên cảnh giới đó khỏi `LockedRealms`.

## Đổi sang SQL Server

Thay package `Microsoft.EntityFrameworkCore.Sqlite` bằng `Microsoft.EntityFrameworkCore.SqlServer`, đổi `UseSqlite` thành `UseSqlServer` trong `Program.cs` và cập nhật connection string trong `appsettings.json`.
