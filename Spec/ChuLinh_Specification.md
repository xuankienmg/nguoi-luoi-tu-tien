# Tài Liệu Đặc Tả Kỹ Thuật & Thiết Kế Hệ Thống: CHÚ LINH (Cổ Phù & Tiên Châu · Lò Hợp Thần Khí)

> **Dự án:** Người Lười Tu Tiên (Vấn Đạo)  
> **Phân hệ:** Chú Linh — Khảm Nạm 10 Vị Trí Trang Bị, Kho Bảo Vật 3 Cấp Bậc & Lò Hợp Vạn Năng (Horadric Cube)  
> **Phiên bản tài liệu:** 1.0  

---

## Mục Lục
1. [Tổng Quan Tính Năng](#1-tổng-quan-tính-năng)
2. [Kiến Trúc Mã Nguồn & Các Tệp Liên Quan](#2-kiến-trúc-mã-nguồn--các-tệp-liên-quan)
3. [Mô Hình Dữ Liệu & Lưu Trữ Database](#3-mô-hình-dữ-liệu--lưu-trữ-database)
4. [Danh Mục 25 Vật Phẩm Chú Linh (14 Cổ Phù & 11 Tiên Châu)](#4-danh-mục-25-vật-phẩm-chú-linh-14-cổ-phù--11-tiên-châu)
5. [Hệ Thống Hiệu Ứng & Công Thức Cộng Chỉ Số Chiến Đấu](#5-hệ-thống-hiệu-ứng--công-thức-cộng-chỉ-số-chiến-đấu)
6. [Cơ Chế Vị Trí Chú Linh (Khảm Nạm 10 Ô Trang Bị)](#6-cơ-chế-vị-trí-chú-linh-khảm-nạm-10-ô-trang-bị)
7. [Cơ Chế Kho Bảo Vật 3 Cấp Bậc & Hợp Nhanh 1 Chạm](#7-cơ-chế-kho-bảo-vật-3-cấp-bậc--hợp-nhanh-1-chạm)
8. [Cơ Chế Rơi Vật Phẩm Trên Bản Đồ & Boss](#8-cơ-chế-rơi-vật-phẩm-trên-bản-đồ--boss)
9. [Hệ Thống Lò Hợp Vạn Năng (Horadric Cube 5 Ô)](#9-hệ-thống-lò-hợp-vạn-năng-horadric-cube-5-ô)
10. [API Endpoints & Giao Thức Tương Tác](#10-api-endpoints--giao-thức-tương-tác)
11. [Giao Diện Người Dùng (UI/UX) & Quy Tắc Thao Tác](#11-giao-diện-người-dùng-uiux--quy-tắc-thao-tác)

---

## 1. Tổng Quan Tính Năng

**Chú Linh (Cổ Phù & Tiên Châu)** là hệ thống bồi dưỡng sức mạnh chiều sâu cho tu sĩ, kết hợp cơ chế khảm nạm ngọc/phù văn (Socket System) gắn liền với **10 vị trí ô trang bị** và cơ chế **Lò Hợp Vạn Năng 5 ô** lấy cảm hứng từ Horadric Cube (Diablo II).

### Các đặc điểm cốt lõi:
- **25 Vật phẩm Chú Linh chuyên biệt:** Bao gồm **14 loại Cổ Phù** (`type = "stone"`, thiên về hiệu ứng chiến đấu đặc biệt như giáng lôi, phá giáp, liên kích, phản đòn, hộ thuẫn...) và **11 loại Tiên Châu** (`type = "gem"`, thiên về chỉ số thuần cực lớn và tốc độ tu luyện/nộ khí đầu trận).
- **Khảm theo Vị Trí Ô Đồ (Slot-based Persistence):** Cổ Phù và Tiên Châu được chú linh trực tiếp vào 10 vị trí trang bị (`weapon`, `offhand`, `head`, `body`, `hands`, `legs`, `feet`, `necklace`, `charm`, `ring`) thay vì gắn chết vào từng món đồ lẻ. Khi người chơi thay trang bị mới hoặc tháo trang bị, toàn bộ Cổ Phù / Tiên Châu đã khảm vẫn giữ nguyên 100% hiệu lực.
- **Kho Lưu Trữ Riêng Biệt (`GemVault`) 3 Cấp Bậc:** Toàn bộ Cổ Phù và Tiên Châu được lưu trong Bảo Khố riêng, **không chiếm số ô Hành Trang (`Inventory`)**, được phân loại trực quan thành 3 cấp: **Sơ Cấp**, **Cao Cấp**, và **Đỉnh Cấp**.
- **Lò Hợp Vạn Năng 5 Ô (Universal Horadric Cube):** Cho phép người chơi đặt từ **1 đến 5 ô nguyên liệu** (Cổ Phù, Tiên Châu, hoặc 25 loại Linh Thảo trong game) theo công thức cố định (25 công thức) hoặc **phối trộn tự do bất kỳ tổ hợp nào**. Hệ thống tự động đánh giá Điểm Linh Lực (`Power Score`) để suy ra phẩm vật đầu ra, tỉ lệ thành công, chi phí linh thạch và bảo lưu một phần nguyên liệu nếu thất bại.

---

## 2. Kiến Trúc Mã Nguồn & Các Tệp Liên Quan

| Tệp mã nguồn | Vai trò & Trách nhiệm |
|---|---|
| `Game/GemData.cs` | Trái tim của hệ thống Chú Linh: định nghĩa 25 vật phẩm (`Items`), phân nhóm cấp bậc, quản lý `GemVault` & `GemSlots`, tính tổng chỉ số khảm (`GetSocketedBonuses`), bảng rơi ải/Boss (`GetStageStoneGroup`, `GetStageGemGroup`, `BossDrops`), 25 công thức cố định (`Recipes`), ánh xạ 25 linh thảo (`HerbToCuiGemMap`), và thuật toán đánh giá dung hợp động (`EvaluateFusion`). |
| `Game/GameService.cs` | Xử lý nghiệp vụ: `SocketGem`, `UnsocketGem`, `UnlockSocket`, `CraftGemRecipe`, `CraftGemFromItems`, `QuickFuseGems`, tự động chuyển vật phẩm chú linh từ túi sang `GemVault`, và tích hợp `GemData.GetSocketedBonuses` vào `GearEffects`, `Stats`, `StatSources`. |
| `Game/PetLoot.cs` | Tích hợp nhóm rơi Cổ Phù (`GetStageStoneGroup`) và Tiên Châu (`GetStageGemGroup`) vào ải thường, cũng như rơi 100% tại ải Boss (`BossDrops` trong `BossMaterials`). |
| `Data/Entities.cs` | Mở rộng thực thể `Player` với 2 trường JSON: `GemVault` và `GemSlots`. Điều hướng hàm `Player.Count(itemId)` và `Player.Add(itemId, amount)` tự động đọc/ghi vào `GemVault` nếu `GemData.IsGemOrStone(itemId)` là `true`. |
| `Data/GameDbContext.cs` | Tự động nâng cấp schema SQLite (`SchemaUpgrade.Apply`) thêm cột `GemVault` và `GemSlots` vào bảng `Players`. |
| `Program.cs` | Định tuyến 6 REST API endpoints dưới `/api/players/{id:guid}/gem/...`. |
| `wwwroot/js/vault.js` | Giao diện Frontend của tab **Chú Linh** (`pane-vault`) gồm 3 sub-tab: `Vị Trí Chú Linh`, `Kho` (3 cấp bậc + Hợp nhanh), và `Lò Hợp` (5 ô tương tác + gợi ý công thức + tự động điền + mô phỏng `evaluateCube` đồng bộ 100% với backend). |
| `wwwroot/js/inventory.js` | Hiển thị chấm ngọc (`iv-pips`) trên 10 ô trang bị nhân vật, gộp chỉ số & hiệu ứng Chú Linh vào tooltip trang bị (`iv-gem-card`), và hỗ trợ mở nhanh ô Chú Linh. |
| `wwwroot/css/vault.css` | Toàn bộ định dạng giao diện, màu sắc phẩm chất, hiệu ứng phát sáng của lỗ khảm và hoạt ảnh rèn trong Lò Hợp. |

---

## 3. Mô Hình Dữ Liệu & Lưu Trữ Database

### 3.1. Cột dữ liệu trên bảng `Players` (SQLite)
Trong `Data/Entities.cs` và `Data/GameDbContext.cs`:
- `GemVault` (`TEXT NOT NULL DEFAULT ''`): Chuỗi JSON biểu diễn `Dictionary<string, int>` lưu số lượng từng loại Cổ Phù / Tiên Châu đang có trong Kho (chỉ giữ các khóa có `Value > 0`).
  ```json
  {
    "stone_viem_so": 12,
    "gem_ruby_cui": 5,
    "gem_thien_phu": 1
  }
  ```
- `GemSlots` (`TEXT NOT NULL DEFAULT ''`): Chuỗi JSON biểu diễn `Dictionary<string, GemSlotData>` lưu trạng thái lỗ khảm của 10 vị trí trang bị.
  ```json
  {
    "weapon": {
      "Sockets": ["gem_thien_phu", "stone_liet_loi", "gem_ruby_xin"],
      "Extra": 1
    },
    "body": {
      "Sockets": ["stone_huyen_bang", ""],
      "Extra": 0
    }
  }
  ```

### 3.2. Các cấu trúc Record / Class trong `Game/GemData.cs`
- **`GemSlotData`**:
  - `List<string> Sockets`: Danh sách ID vật phẩm đang khảm tại từng lỗ (tối đa 3 phần tử, chuỗi rỗng `""` biểu thị lỗ trống).
  - `int Extra`: Số lỗ khảm đã được đục mở sớm bằng **Khai Khiếu Cổ Phù** (`stone_socket`).
- **`GemBonuses`**:
  - Lưu tổng chỉ số cộng thêm từ mọi lỗ khảm hợp lệ trên 10 ô trang bị:
    `Atk`, `Def`, `Hp`, `Crit`, `Speed`, `QiPct`, `Pen`, `Guard`, `SkillPct`, `Rage`, `ExtraHit`, `Shield`, `Reflect`, `Regen`, `Lightning`, `LightningPct`.
- **`GemRecipeDef`**:
  - Định nghĩa công thức cố định: `Id`, `Name`, `Category` (`stone` | `gem` | `transcend` | `herb`), `ResultItem`, `ResultCount`, `Inputs` (`Dictionary<string, int>`), `Stones` (chi phí linh thạch), `Desc`, `Chance` (0.0 – 1.0).
- **`FusionEvaluation`**:
  - Kết quả đánh giá tổ hợp nguyên liệu trong Lò Hợp: `IsValid`, `RecipeId`, `Name`, `Desc`, `ResultItem` (cố định hoặc `null` nếu ngẫu nhiên từ pool), `PossibleResults` (`List<string>`), `ResultCount`, `Stones`, `Chance`, `RequiredInputs`, `Hint`.
- **`GemStateDto`**:
  - DTO trả về cho Client trong `PlayerStateDto.GemState`: gồm `vault`, `slots`, `maxSockets`, và `recipes`.

### 3.3. Cơ chế Tự Động Đồng Bộ Túi Đồ -> Bảo Khố
- Trong `Player.Add(itemId, amount)` và `Player.Count(itemId)`: mọi thao tác cộng/trừ/kiểm tra vật phẩm có `GemData.IsGemOrStone(itemId) == true` đều tự động định tuyến sang `GemData.Add` và `GemData.GetCount`.
- Khi tải trạng thái người chơi (`GameService.cs`), nếu trong bảng `Inventory` cũ còn tồn đọng bất kỳ vật phẩm Cổ Phù / Tiên Châu nào, hệ thống tự động chuyển toàn bộ số lượng đó vào `GemVault` và xóa khỏi `Inventory` để giải phóng ô túi đồ.

---

## 4. Danh Mục 25 Vật Phẩm Chú Linh (14 Cổ Phù & 11 Tiên Châu)

Hệ thống bao gồm đúng **25 vật phẩm** được chia làm 6 nhóm theo loại (`stone` / `gem`) và 3 tầng Kho (`so` / `cao` / `dinh`):

### 4.1. Nhóm 1: Cổ Phù Sơ Cấp (5 vật phẩm · Loại: `stone` · Kho: `Sơ Cấp` · Phẩm: `uncommon` Lam · Điểm Linh Lực: `2`)

| ID | Tên vật phẩm | Màu (`Color`) | Icon | Chỉ số cơ bản | Hiệu ứng (`ItemEffect`) | Mô tả thuộc tính (`Special`) |
|---|---|---|---|---|---|---|
| `stone_viem_so` | Viêm Cổ Phù · Sơ | `#ff4d4f` | `img/core/core_01.png` | `+20 Công` | `linh_ap` (Tier 1): `+3%` sát thương kỹ năng | Chú linh: +20 Công · +3% Sát thương kỹ năng |
| `stone_bang_so` | Băng Cổ Phù · Sơ | `#40a9ff` | `img/core/core_04.png` | `+20 Thủ` | `ho_the` (Tier 1): Giảm `3%` sát thương nhận | Chú linh: +20 Thủ · Giảm 3% sát thương nhận |
| `stone_loi_so` | Lôi Cổ Phù · Sơ | `#9254de` | `img/core/core_07.png` | `+3% Bạo kích` | `thien_loi` (Tier 1): Mỗi lượt `5%` cơ hội giáng sét `20%` công | Chú linh: +3% Bạo kích · 5% giáng Thiên lôi |
| `stone_kim_so` | Kim Cổ Phù · Sơ | `#faad14` | `img/core/core_10.png` | — | `pha_giap` (Tier 1): Bỏ qua `4%` thủ quái<br>`lien_kich` (Tier 1): `3%` cơ hội đánh thêm 1 đòn | Chú linh: Bỏ qua 4% thủ quái · +3% Liên kích |
| `stone_tho_so` | Thổ Cổ Phù · Sơ | `#a07040` | `img/core/core_14.png` | `+180 Máu` | `tho_thuan` (Tier 1): Khiên đầu trận `+4%` máu | Chú linh: +180 Máu · Khiên đầu trận +4% máu |

### 4.2. Nhóm 2: Cổ Phù Cao Cấp (5 vật phẩm · Loại: `stone` · Kho: `Cao Cấp` · Phẩm: `mythic` Cam · Điểm Linh Lực: `6`)

| ID | Tên vật phẩm | Màu (`Color`) | Icon | Chỉ số cơ bản | Hiệu ứng (`ItemEffect`) | Mô tả thuộc tính (`Special`) |
|---|---|---|---|---|---|---|
| `stone_viem_cao` | Viêm Cổ Phù · Cao | `#ff7a45` | `img/core/core_02.png` | `+60 Công` | `linh_ap` (Tier 2): `+8%` sát thương kỹ năng | Chú linh: +60 Công · +8% Sát thương kỹ năng |
| `stone_bang_cao` | Băng Cổ Phù · Cao | `#1890ff` | `img/core/core_05.png` | `+60 Thủ` | `ho_the` (Tier 2): Giảm `8%` sát thương nhận | Chú linh: +60 Thủ · Giảm 8% sát thương nhận |
| `stone_loi_cao` | Lôi Cổ Phù · Cao | `#722ed1` | `img/core/core_09.png` | `+8% Bạo kích` | `thien_loi` (Tier 2): Mỗi lượt `12%` cơ hội giáng sét `40%` công | Chú linh: +8% Bạo kích · 12% giáng Thiên lôi |
| `stone_kim_cao` | Kim Cổ Phù · Cao | `#d48806` | `img/core/core_11.png` | — | `pha_giap` (Tier 2): Bỏ qua `10%` thủ quái<br>`lien_kich` (Tier 2): `7%` cơ hội đánh thêm 1 đòn | Chú linh: Bỏ qua 10% thủ quái · +7% Liên kích |
| `stone_tho_cao` | Thổ Cổ Phù · Cao | `#874d00` | `img/core/core_13.png` | `+500 Máu` | `tho_thuan` (Tier 2): Khiên đầu trận `+10%` máu | Chú linh: +500 Máu · Khiên đầu trận +10% máu |

### 4.3. Nhóm 3: Cổ Phù Biến Dị & Khai Khiếu (4 vật phẩm · Loại: `stone` · Điểm Linh Lực: `10`)

| ID | Tên vật phẩm | Kho | Phẩm (`Rarity`) | Màu | Icon | Chỉ số cơ bản | Hiệu ứng (`ItemEffect`) | Mô tả thuộc tính (`Special`) |
|---|---|---|---|---|---|---|---|---|
| `stone_liet_loi` | Liệt Lôi Cổ Phù | `Đỉnh Cấp` | `divine` (Đỏ) | `#f5222d` | `img/core/core_12.png` | `+90 Công`<br>`+5% Bạo kích` | `thien_loi` (Tier 3): `15%` giáng sét `50%` công<br>`linh_ap` (Tier 3): `+6%` sát thương kỹ năng | Chú linh: +90 Công · +5% Bạo kích · 15% giáng Thiên lôi |
| `stone_huyen_bang` | Huyền Băng Cổ Phù | `Đỉnh Cấp` | `divine` (Đỏ) | `#13c2c2` | `img/core/core_08.png` | `+90 Thủ`<br>`+600 Máu` | `ho_the` (Tier 3): Giảm `10%` sát thương nhận<br>`tho_thuan` (Tier 3): Khiên đầu trận `+10%` máu | Chú linh: +90 Thủ · +600 Máu · Giảm 10% sát thương · Khiên +10% máu |
| `stone_am_duong` | Âm Dương Cổ Phù | `Đỉnh Cấp` | `divine` (Đỏ) | `#eb2f96` | `img/core/core_06.png` | `+60 Công`<br>`+60 Thủ` | `dia_mach` (Tier 3): Phản đòn `5%` sát thương quái<br>`hoi_khi` (Tier 3): Hồi `3%` máu mỗi lượt | Chú linh: +60 Công · +60 Thủ · Phản đòn 5% · Hồi 3% máu/lượt |
| `stone_socket` | Khai Khiếu Cổ Phù | `Cao Cấp` | `mythic` (Cam) | `#ffd700` | `img/core/core_15.png` | — | *(Đạo cụ mở lỗ khảm)* | Dùng để đục mở thêm 1 Lỗ Khảm cho bất kỳ ô trang bị nào (tối đa 3 lỗ). |

### 4.4. Nhóm 4: Tiên Châu Thô (5 vật phẩm · Loại: `gem` · Kho: `Sơ Cấp` · Phẩm: `rare` Lam · Điểm Linh Lực: `3`)

| ID | Tên vật phẩm | Màu (`Color`) | Icon | Chỉ số cơ bản | Hiệu ứng (`ItemEffect`) | Mô tả thuộc tính (`Special`) |
|---|---|---|---|---|---|---|
| `gem_ruby_cui` | Xích Diễm Tiên Châu · Thô | `#ff4d4f` | `img/core/core_01.png` | `+100 Công` | — | Chú linh: +100 Tấn công |
| `gem_sapphire_cui` | Huyền Băng Tiên Châu · Thô | `#40a9ff` | `img/core/core_04.png` | `+80 Thủ` | — | Chú linh: +80 Phòng thủ |
| `gem_topaz_cui` | Thái Dương Tiên Châu · Thô | `#faad14` | `img/core/core_10.png` | `+600 Máu` | — | Chú linh: +600 Máu tối đa |
| `gem_emerald_cui` | Thanh Mộc Tiên Châu · Thô | `#52c41a` | `img/core/core_03.png` | `+4% Thân pháp` | `hap_linh` (Tier 1): `+4%` tốc độ tu luyện | Chú linh: +4% Tu luyện · +4 Thân pháp |
| `gem_amethyst_cui` | Tử Lôi Tiên Châu · Thô | `#9254de` | `img/core/core_07.png` | `+4% Bạo kích` | `tinh_tam` (Tier 1): Vào trận với `15` nộ | Chú linh: +4% Bạo kích · +15 Nộ đầu trận |

### 4.5. Nhóm 5: Tiên Châu Hoàn Mỹ (5 vật phẩm · Loại: `gem` · Kho: `Cao Cấp` · Phẩm: `mythic` Cam · Điểm Linh Lực: `8`)

| ID | Tên vật phẩm | Màu (`Color`) | Icon | Chỉ số cơ bản | Hiệu ứng (`ItemEffect`) | Mô tả thuộc tính (`Special`) |
|---|---|---|---|---|---|---|
| `gem_ruby_xin` | Xích Diễm Tiên Châu · Hoàn Mỹ | `#f5222d` | `img/core/core_02.png` | `+320 Công` | — | Chú linh: +320 Tấn công |
| `gem_sapphire_xin` | Huyền Băng Tiên Châu · Hoàn Mỹ | `#1890ff` | `img/core/core_05.png` | `+260 Thủ` | — | Chú linh: +260 Phòng thủ |
| `gem_topaz_xin` | Thái Dương Tiên Châu · Hoàn Mỹ | `#d48806` | `img/core/core_11.png` | `+2.200 Máu` | — | Chú linh: +2.200 Máu tối đa |
| `gem_emerald_xin` | Thanh Mộc Tiên Châu · Hoàn Mỹ | `#389e0d` | `img/core/core_06.png` | `+10% Thân pháp` | `hap_linh` (Tier 2): `+12%` tốc độ tu luyện | Chú linh: +12% Tu luyện · +10 Thân pháp |
| `gem_amethyst_xin` | Tử Lôi Tiên Châu · Hoàn Mỹ | `#722ed1` | `img/core/core_09.png` | `+12% Bạo kích` | `tinh_tam` (Tier 2): Vào trận với `35` nộ | Chú linh: +12% Bạo kích · +35 Nộ đầu trận |

### 4.6. Nhóm 6: Cổ Phù Tiên Châu Chí Tôn (1 vật phẩm · Loại: `gem` · Kho: `Đỉnh Cấp` · Phẩm: `divine` Đỏ · Điểm Linh Lực: `14`)

| ID | Tên vật phẩm | Màu (`Color`) | Icon | Chỉ số cơ bản | Hiệu ứng (`ItemEffect`) | Mô tả thuộc tính (`Special`) |
|---|---|---|---|---|---|---|
| `gem_thien_phu` | Cổ Phù Tiên Châu | `#eb2f96` | `img/core/core_15.png` | `+380 Công`<br>`+2.000 Máu`<br>`+10% Bạo kích` | `linh_ap` (Tier 3): `+10%` sát thương kỹ năng | Chú linh: +380 Công · +2.000 Máu · +10% Bạo kích · +10% Sát thương kỹ năng |

---

## 5. Hệ Thống Hiệu Ứng & Công Thức Cộng Chỉ Số Chiến Đấu

### 5.1. Bảng 10 Hiệu Ứng Chú Linh (`ItemEffect`)
Khi khảm trên 10 ô trang bị, `GemData.GetSocketedBonuses(Player p)` quét qua tất cả các lỗ khảm đang mở (`i < maxSockets`) và cộng dồn chỉ số:

| Mã Hiệu Ứng (`fx.Id`) | Tên Hiệu Ứng | Trường trong `GemBonuses` | Quy tắc cộng dồn | Tác dụng trong chiến đấu / tu luyện |
|---|---|---|---|---|
| `linh_ap` | Linh áp | `SkillPct` | Cộng tổng `fx.Value` | Tăng `%` sát thương khi tung kỹ năng chủ động. |
| `ho_the` | Hộ thể | `Guard` | Cộng tổng `fx.Value` | Giảm `%` sát thương nhận vào từ quái/Boss. |
| `thien_loi` | Thiên lôi | `Lightning` & `LightningPct` | `Lightning += fx.Value`<br>`LightningPct = Max(fx.Value2)` | Mỗi lượt có `Lightning%` cơ hội giáng sét gây sát thương bằng `LightningPct%` Tấn công. |
| `pha_giap` | Phá giáp | `Pen` | Cộng tổng `fx.Value` | Bỏ qua `%` Phòng thủ của mục tiêu. |
| `lien_kich` | Liên kích | `ExtraHit` | Cộng tổng `fx.Value` | Tăng `%` cơ hội ra thêm đòn đánh phụ trong lượt. |
| `tho_thuan` | Thổ thuẫn | `Shield` | Cộng tổng `fx.Value` | Tạo khiên chắn đầu trận bằng `%` Máu tối đa. |
| `dia_mach` | Địa mạch | `Reflect` | Cộng tổng `fx.Value` | Phản lại `%` sát thương quái gây ra. |
| `hoi_khi` | Hồi khí | `Regen` | Cộng tổng `fx.Value` | Hồi phục `%` Máu tối đa sau mỗi lượt. |
| `hap_linh` | Hấp linh | `QiPct` | Cộng tổng `fx.Value` | Tăng `%` tốc độ hấp thu linh khí (tu luyện). |
| `tinh_tam` | Tĩnh tâm | `Rage` | Cộng tổng `fx.Value` | Cộng thẳng điểm Nộ khí ngay khi bắt đầu trận đấu. |

### 5.2. Tích hợp vào Công thức Chỉ số Nhân vật & Giới hạn Thực Chiến (`GameService.Stats`, `GearEffects`, `BattleAt`)
- **Tấn công (`Atk`):**
  $$\text{Atk} = (\text{BaseAtk} + \sum \text{GearAtk} + \text{PetAtk} + \text{Gems.Atk}) \times \text{WeakFactor} \times \left(1 + \frac{\text{SkillAtkPct} + \text{SetPct} + \text{PetTalentAtkPct}}{100}\right)$$
  *(Chỉ số Công phẳng từ Chú Linh được nhân với toàn bộ hệ số `%` Công của Công Pháp, Bộ Trang Bị và Thiên Phú Linh Thú!)*
- **Máu tối đa (`Hp`):**
  $$\text{Hp} = (\text{BaseHp} + \sum \text{GearHp} + \text{BodyHp} + \text{PetHp} + \text{Gems.Hp}) \times \left(1 + \frac{\text{SkillHpPct} + \text{GearHpPct} + \text{SetPct} + \text{PetTalentHpPct}}{100}\right)$$
- **Phòng thủ (`Def`):**
  $$\text{Def} = (\text{BaseDef} + \sum \text{GearDef} + \text{BodyDef} + \text{PetDef} + \text{Gems.Def}) \times \text{WeakFactor} \times \left(1 + \frac{\text{SkillDefPct} + \text{SetPct} + \text{PetTalentDefPct}}{100}\right)$$
- **Thân pháp (`Speed`):** Cộng trực tiếp `+ gems.Speed` (giới hạn tối đa **60%**).
- **Bạo kích (`Crit`):** Cộng trực tiếp `+ gems.Crit` (giới hạn tối đa **100%**).
- **Giới hạn hiệu ứng trong thực chiến (`BattleAt`):**
  - **Linh áp (`SkillPct`):** Tăng sát thương cho cả đòn đánh thường lẫn kỹ năng chủ động theo hệ số $(1 + \text{skillPct} / 100)$.
  - **Phá giáp (`Pen`):** Tổng bỏ qua phòng thủ quái (Cổ Phù + Trang bị + Công pháp + Linh thú) giới hạn tối đa **80%**.
  - **Hộ thể (`Guard`):** Tổng giảm sát thương nhận vào (Cổ Phù + Trang bị + Kỹ năng + Linh thú) giới hạn tối đa **60%**.
  - **Liên kích (`ExtraHit`):** Tổng tỉ lệ đánh thêm đòn (Cổ Phù + Trang bị + Linh thú) giới hạn cơ bản **30%** (hoặc **50%** khi kích hoạt Thần thông bộ Đạo).
- **Bảng phân tích nguồn chỉ số (`StatSources`):** Hiển thị một dòng riêng biệt mang tên `"Chú linh"` liệt kê đầy đủ `Atk`, `Def`, `Hp`, `Crit`, `Speed`, `Qi` từ Cổ Phù & Tiên Châu.

---

## 6. Cơ Chế Vị Trí Chú Linh (Khảm Nạm 10 Ô Trang Bị)

### 6.1. Danh sách 10 Ô Trang Bị (`GemData.EquipSlots`)
1. `weapon` — Vũ khí
2. `offhand` — Pháp bảo
3. `head` — Mũ
4. `body` — Áo
5. `hands` — Hộ uyển
6. `legs` — Quần
7. `feet` — Giày
8. `necklace` — Dây chuyền
9. `charm` — Hộ phù
10. `ring` — Nhẫn

*(Tổng cộng 10 ô × tối đa 3 lỗ/ô = **30 lỗ Chú Linh** trên toàn thân nhân vật).*

### 6.2. Quy tắc Mở Lỗ Khảm (`GemData.MaxSockets`)
Số lỗ mở trên mỗi ô trang bị `slot` được tính theo công thức:
$$\text{MaxSockets}(p, \text{slot}) = \min(3, \text{BaseSockets}(p.\text{Realm}) + \text{SlotData.Extra})$$

Trong đó số lỗ cơ bản theo Cảnh Giới (`BaseSockets`):
- **Luyện Khí (`Realm = 0`) & Trúc Cơ (`Realm = 1`):** Mặc định mở **1 lỗ** trên cả 10 ô.
- **Kim Đan (`Realm = 2`) & Nguyên Anh (`Realm = 3`):** Mặc định mở **2 lỗ** trên cả 10 ô.
- **Hóa Thần (`Realm >= 4`) trở lên:** Mặc định mở tối đa **3 lỗ** trên cả 10 ô.

**Đục lỗ sớm bằng Khai Khiếu Cổ Phù (`stone_socket`):**
- Khi người chơi chưa đạt Hóa Thần (`curMax < 3`), có thể tiêu hao `1 × stone_socket` tại ô trang bị bất kỳ để tăng `Extra` của ô đó thêm `+1` (mở ngay Lỗ 2 hoặc Lỗ 3 cho ô đó).

### 6.3. Quy tắc Khảm & Tháo
- **Khảm (`SocketGem`):**
  - Không giới hạn loại Cổ Phù hay Tiên Châu vào từng ô đồ (có thể khảm tự do bất kỳ Cổ Phù hoặc Tiên Châu nào vào bất kỳ lỗ nào, trừ `stone_socket` là đạo cụ mở lỗ).
  - Nếu lỗ mục tiêu đang có sẵn một viên cũ, viên cũ sẽ **tự động hoàn trả về Kho (`GemVault`)** và viên mới từ Kho được gắn vào ngay trong 1 thao tác.
- **Tháo (`UnsocketGem`):**
  - Hoàn toàn **miễn phí 100%**, không tốn linh thạch, không làm hỏng vật phẩm. Viên được tháo quay trở về `GemVault`.

---

## 7. Cơ Chế Kho Bảo Vật 3 Cấp Bậc & Hợp Nhanh 1 Chạm

### 7.1. Phân chia 3 Cấp Bậc Kho (`vaultTierTab`)
Trong tab **Kho**, 25 vật phẩm được chia vào 3 cấp bậc rõ ràng, mỗi cấp bậc hiển thị 2 ngăn riêng (**Ngăn Tiên Châu** ở trên, **Ngăn Cổ Phù** ở dưới):

1. **Sơ Cấp (`so`):**
   - *Ngăn Tiên Châu:* 5 loại Tiên Châu Thô (`gem_ruby_cui`, `gem_sapphire_cui`, `gem_topaz_cui`, `gem_emerald_cui`, `gem_amethyst_cui`).
   - *Ngăn Cổ Phù:* 5 loại Cổ Phù Sơ (`stone_viem_so`, `stone_bang_so`, `stone_loi_so`, `stone_kim_so`, `stone_tho_so`).
2. **Cao Cấp (`cao`):**
   - *Ngăn Tiên Châu:* 5 loại Tiên Châu Hoàn Mỹ (`gem_ruby_xin`, `gem_sapphire_xin`, `gem_topaz_xin`, `gem_emerald_xin`, `gem_amethyst_xin`).
   - *Ngăn Cổ Phù:* 5 loại Cổ Phù Cao (`stone_viem_cao`, `stone_bang_cao`, `stone_loi_cao`, `stone_kim_cao`, `stone_tho_cao`) + `stone_socket` (Khai Khiếu Cổ Phù).
3. **Đỉnh Cấp (`dinh`):**
   - *Ngăn Tiên Châu:* `gem_thien_phu` (Cổ Phù Tiên Châu).
   - *Ngăn Cổ Phù:* 3 loại Cổ Phù Biến Dị (`stone_liet_loi`, `stone_huyen_bang`, `stone_am_duong`).

### 7.2. Tính năng Hợp Nhanh 1 Chạm (`QuickFuseGems`)
Khi nhấn nút **Hợp Nhanh (1 Chạm)** tại góc phải của tab Kho (`POST /api/players/{id}/gem/quick-fuse`):
1. Hệ thống duyệt tuần tự qua 5 cặp Cổ Phù (`stone_*_so` $\rightarrow$ `stone_*_cao`):
   - Cứ mỗi `3` viên Sơ cấp cùng hệ + `200` linh thạch $\rightarrow$ nâng cấp **thành công 100%** (không gieo xác suất thất bại) thành `1` viên Cao cấp cùng hệ.
2. Tiếp tục duyệt qua 5 cặp Tiên Châu (`gem_*_cui` $\rightarrow$ `gem_*_xin`):
   - Cứ mỗi `3` viên Tiên Châu Thô cùng hệ + `400` linh thạch $\rightarrow$ nâng cấp **thành công 100%** thành `1` viên Tiên Châu Hoàn Mỹ cùng hệ.
3. Trả về tổng số lượng viên đã nâng cấp kèm hiệu ứng thông báo kết quả (`CraftResultDto` với mã `"quick_fuse"`).

---

## 8. Cơ Chế Rơi Vật Phẩm Trên Bản Đồ & Boss

### 8.1. Rơi tại Ải Thường (Ải 1 đến Ải 15 mỗi chương)
Trong `PetLoot.StageGroups(ChapterDef chapter, int diff, int area)`, mỗi lần vượt ải thành công sẽ gieo thêm 2 nhóm rơi độc lập:

#### a) Nhóm Rơi Cổ Phù (`GemData.GetStageStoneGroup`)
- **Tỉ lệ kích hoạt nhóm:** **38%** mỗi lượt vượt ải (`chance = 0.38`).
- **Phân phối bên trong nhóm theo độ khó (`diff`):**
  - **Độ khó Thường (`diff == 0`):**
    - 100% rơi Cổ Phù Sơ Cấp, chia đều 5 hệ (`stone_viem_so`, `stone_bang_so`, `stone_loi_so`, `stone_kim_so`, `stone_tho_so`), mỗi hệ trọng số `20%` (tức `7.6%` tuyệt đối/ải), số lượng `1` viên.
  - **Độ khó Khó (`diff == 1`) & Ác Mộng (`diff == 2`):**
    - **80%** rơi Cổ Phù Sơ Cấp (mỗi hệ trọng số `16%`, số lượng gốc từ `1` đến `1 + diff`, nhân thêm hệ số thưởng độ khó `GameData.DiffRewardAt(diff, chapter)`).
    - **20%** rơi Cổ Phù Cao Cấp (mỗi hệ trọng số `4%`, số lượng gốc `1`, nhân hệ số thưởng độ khó).

#### b) Nhóm Rơi Tiên Châu (`GemData.GetStageGemGroup`)
- **Tỉ lệ kích hoạt nhóm:** **12%** mỗi lượt vượt ải (`chance = 0.12`).
- **Phân phối bên trong nhóm (mọi độ khó):**
  - **90%** là Tiên Châu Thô (chia đều 5 hệ, mỗi hệ trọng số `18%`, tức `2.16%` tuyệt đối/ải).
  - **10%** là Tiên Châu Hoàn Mỹ (chia đều 5 hệ, mỗi hệ trọng số `2%`, tức `0.24%` tuyệt đối/ải).
  - Số lượng gốc: `1` viên (nhân hệ số thưởng độ khó ở mức Khó / Ác Mộng).

### 8.2. Rơi 100% Khi Hạ Boss Chương (Ải 16)
Trong `PetLoot.BossMaterials(int chapter, int diff)` và `GemData.BossDrops(int chapter, int diff)`:
- Mỗi lần đánh bại Boss ở ải 16 **chắc chắn 100% (`chance = 1.0`)** rơi:
  - **1 viên Tiên Châu Hoàn Mỹ** (`min = 1, max = 1`)
  - **`1 + diff` đến `2 + diff` viên Cổ Phù Cao Cấp** (Thường: 1–2 viên; Khó: 2–3 viên; Ác mộng: 3–4 viên)
- Loại Ngọc và Cổ Phù rơi ra tuân theo chu kỳ Ngũ Hành của Chương (`theme = chapter % 5`, với `chapter` tính từ 0):

| `chapter % 5` | Các Chương áp dụng (1-based) | Tiên Châu Hoàn Mỹ rơi (100%) | Cổ Phù Cao Cấp rơi (100%) |
|---|---|---|---|
| `0` | Chương 1, Chương 6 | `gem_ruby_xin` (Xích Diễm · Hoàn Mỹ) | `stone_viem_cao` (Viêm Cổ Phù · Cao) |
| `1` | Chương 2, Chương 7 | `gem_sapphire_xin` (Huyền Băng · Hoàn Mỹ) | `stone_bang_cao` (Băng Cổ Phù · Cao) |
| `2` | Chương 3, Chương 8 | `gem_topaz_xin` (Thái Dương · Hoàn Mỹ) | `stone_tho_cao` (Thổ Cổ Phù · Cao) |
| `3` | Chương 4, Chương 9 | `gem_amethyst_xin` (Tử Lôi · Hoàn Mỹ) | `stone_loi_cao` (Lôi Cổ Phù · Cao) |
| `4` | Chương 5, Chương 10 | `gem_emerald_xin` (Thanh Mộc · Hoàn Mỹ) | `stone_kim_cao` (Kim Cổ Phù · Cao) |

---

## 9. Hệ Thống Lò Hợp Vạn Năng (Horadric Cube 5 Ô)

Lò Hợp cho phép đặt tối đa **5 ô nguyên liệu** (`totalSlots <= 5`). Mỗi viên Cổ Phù hoặc Tiên Châu tính là **1 ô**, riêng **Linh Thảo tính 10 cây = 1 ô** (`SlotsOf(id, qty) = IsHerb(id) ? Math.Max(1, qty / 10) : qty`).

### 9.1. Bảng Ánh Xạ 25 Linh Thảo $\rightarrow$ Tiên Châu Thô (`HerbToCuiGemMap`)
Mọi loại linh thảo trong game đều mang linh tính Ngũ Hành và có thể đưa vào Lò Hợp để ngưng luyện thành Tiên Châu:

| Hệ / Tiên Châu đích | Danh sách Linh Thảo tương ứng (5 nhóm = 25 loại) |
|---|---|
| **Hỏa** $\rightarrow$ `gem_ruby_cui`<br>*(Xích Diễm Tiên Châu · Thô)* | `huyet_can_thao` (Huyết Căn Thảo), `hoa_tam_hoa` (Hỏa Tâm Hoa), `bat_tu_phuong_hoa` (Bất Tử Phượng Hoa), `phuong_vu_chi` (Phượng Vũ Chi), `am_duong_ban_dao` (Âm Dương Bàn Đào) |
| **Thủy/Băng** $\rightarrow$ `gem_sapphire_cui`<br>*(Huyền Băng Tiên Châu · Thô)* | `tu_linh_thao` (Tụ Linh Thảo), `thuy_lo_diep` (Thủy Lộ Diệp), `nguyet_lo_hoa` (Nguyệt Lộ Hoa), `bang_tinh_thao` (Băng Tinh Thảo), `cuu_diep_hon_lien` (Cửu Diệp Hồn Liên) |
| **Thổ/Kim** $\rightarrow$ `gem_topaz_cui`<br>*(Thái Dương Tiên Châu · Thô)* | `thien_nien_sam` (Thiên Niên Nhân Sâm), `thach_dai_thao` (Thạch Đài Thảo), `kim_duong_lien` (Kim Dương Liên), `dia_mach_tien_dang` (Địa Mạch Tiên Đằng), `hon_nguyen_linh_chung` (Hỗn Nguyên Linh Chủng) |
| **Mộc/Phong** $\rightarrow$ `gem_emerald_cui`<br>*(Thanh Mộc Tiên Châu · Thô)* | `bich_ngoc_linh_thao` (Bích Ngọc Linh Thảo), `tay_tuy_thao` (Tẩy Tủy Thảo), `bach_diep_thao` (Bách Diệp Thảo), `phong_vu_thao` (Phong Vũ Thảo), `kien_moc_mieu` (Kiến Mộc Miêu), `thien_truc_duan` (Thiên Trúc Đoạn) |
| **Lôi/Hồn** $\rightarrow$ `gem_amethyst_cui`<br>*(Tử Lôi Tiên Châu · Thô)* | `tu_van_sam` (Tử Vân Sâm), `loi_mach_thao` (Lôi Mạch Thảo), `linh_cot_chi` (Linh Cốt Chi), `hon_nguyen_dao_lien` (Hỗn Nguyên Đạo Liên) |

### 9.2. Bảng Điểm Linh Lực (`GemData.GetItemPowerScore`)
Để phục vụ thuật toán Dung Hợp Hỗn Hợp Vạn Năng, mỗi ô nguyên liệu có một điểm linh lực (`Power Score`):

| Nhóm nguyên liệu | Điểm Linh Lực / 1 Ô (`Power Score`) |
|---|---|
| Linh Thảo (`IsHerb` — 10 cây/ô) | **1 điểm** |
| Cổ Phù Sơ Cấp (`SoStones`) | **2 điểm** |
| Tiên Châu Thô (`CuiGems`) | **3 điểm** |
| Cổ Phù Cao Cấp (`CaoStones`) | **6 điểm** |
| Tiên Châu Hoàn Mỹ (`XinGems`) | **8 điểm** |
| Cổ Phù Biến Dị (`DivineStones`) & Khai Khiếu Cổ Phù (`stone_socket`) | **10 điểm** |
| Cổ Phù Tiên Châu (`gem_thien_phu`) | **14 điểm** |

### 9.3. Danh sách 25 Công Thức Cố Định (`GemData.Recipes`)

| STT | Mã Công Thức (`Id`) | Danh mục | Tên Công Thức | Nguyên liệu đầu vào (`Inputs`) | Sản phẩm (`ResultItem`) | Phí Linh Thạch | Tỉ lệ |
|---|---|---|---|---|---|---|---|
| 1 | `fuse_stone_viem` | `stone` | Luyện Viêm Cổ Phù · Cao | `3 × stone_viem_so` | `1 × stone_viem_cao` | 200 | 85% |
| 2 | `fuse_stone_bang` | `stone` | Luyện Băng Cổ Phù · Cao | `3 × stone_bang_so` | `1 × stone_bang_cao` | 200 | 85% |
| 3 | `fuse_stone_loi` | `stone` | Luyện Lôi Cổ Phù · Cao | `3 × stone_loi_so` | `1 × stone_loi_cao` | 200 | 85% |
| 4 | `fuse_stone_kim` | `stone` | Luyện Kim Cổ Phù · Cao | `3 × stone_kim_so` | `1 × stone_kim_cao` | 200 | 85% |
| 5 | `fuse_stone_tho` | `stone` | Luyện Thổ Cổ Phù · Cao | `3 × stone_tho_so` | `1 × stone_tho_cao` | 200 | 85% |
| 6 | `fuse_stone_liet_loi` | `stone` | Hợp Liệt Lôi Cổ Phù | `1 × stone_viem_cao` + `1 × stone_loi_cao` | `1 × stone_liet_loi` | 500 | 75% |
| 7 | `fuse_stone_huyen_bang` | `stone` | Hợp Huyền Băng Cổ Phù | `1 × stone_bang_cao` + `1 × stone_tho_cao` | `1 × stone_huyen_bang` | 500 | 75% |
| 8 | `fuse_stone_am_duong` | `stone` | Hợp Âm Dương Cổ Phù | `1 × stone_viem_cao` + `1 × stone_bang_cao` | `1 × stone_am_duong` | 500 | 75% |
| 9 | `fuse_stone_socket` | `stone` | Luyện Khai Khiếu Cổ Phù | `1 × stone_viem_so` + `1 × stone_bang_so` + `1 × stone_loi_so` + `1 × stone_kim_so` + `1 × stone_tho_so` | `1 × stone_socket` | 1.000 | **100%** |
| 10 | `fuse_gem_ruby` | `gem` | Thăng cấp Xích Diễm Tiên Châu · Hoàn Mỹ | `3 × gem_ruby_cui` | `1 × gem_ruby_xin` | 400 | 75% |
| 11 | `fuse_gem_sapphire` | `gem` | Thăng cấp Huyền Băng Tiên Châu · Hoàn Mỹ | `3 × gem_sapphire_cui` | `1 × gem_sapphire_xin` | 400 | 75% |
| 12 | `fuse_gem_topaz` | `gem` | Thăng cấp Thái Dương Tiên Châu · Hoàn Mỹ | `3 × gem_topaz_cui` | `1 × gem_topaz_xin` | 400 | 75% |
| 13 | `fuse_gem_emerald` | `gem` | Thăng cấp Thanh Mộc Tiên Châu · Hoàn Mỹ | `3 × gem_emerald_cui` | `1 × gem_emerald_xin` | 400 | 75% |
| 14 | `fuse_gem_amethyst` | `gem` | Thăng cấp Tử Lôi Tiên Châu · Hoàn Mỹ | `3 × gem_amethyst_cui` | `1 × gem_amethyst_xin` | 400 | 75% |
| 15 | `ascend_ruby` | `transcend` | Thăng hoa Xích Diễm Tiên Châu (dùng Cổ Phù) | `1 × gem_ruby_cui` + `3 × stone_viem_so` | `1 × gem_ruby_xin` | 1.000 | 75% |
| 16 | `ascend_sapphire` | `transcend` | Thăng hoa Huyền Băng Tiên Châu (dùng Cổ Phù) | `1 × gem_sapphire_cui` + `3 × stone_bang_so` | `1 × gem_sapphire_xin` | 1.000 | 75% |
| 17 | `ascend_topaz` | `transcend` | Thăng hoa Thái Dương Tiên Châu (dùng Cổ Phù) | `1 × gem_topaz_cui` + `3 × stone_tho_so` | `1 × gem_topaz_xin` | 1.000 | 75% |
| 18 | `ascend_emerald` | `transcend` | Thăng hoa Thanh Mộc Tiên Châu (dùng Cổ Phù) | `1 × gem_emerald_cui` + `3 × stone_kim_so` | `1 × gem_emerald_xin` | 1.000 | 75% |
| 19 | `ascend_amethyst` | `transcend` | Thăng hoa Tử Lôi Tiên Châu (dùng Cổ Phù) | `1 × gem_amethyst_cui` + `3 × stone_loi_so` | `1 × gem_amethyst_xin` | 1.000 | 75% |
| 20 | `ascend_thien_phu` | `transcend` | Luyện Cổ Phù Tiên Châu | `1 × gem_ruby_xin` + `1 × stone_liet_loi` | `1 × gem_thien_phu` | 5.000 | 65% |
| 21 | `craft_gem_ruby` | `herb` | Luyện Xích Diễm Tiên Châu · Thô | `10 × huyet_can_thao` | `1 × gem_ruby_cui` | 500 | 85% |
| 22 | `craft_gem_sapphire` | `herb` | Luyện Huyền Băng Tiên Châu · Thô | `10 × tu_linh_thao` | `1 × gem_sapphire_cui` | 500 | 85% |
| 23 | `craft_gem_topaz` | `herb` | Luyện Thái Dương Tiên Châu · Thô | `10 × thien_nien_sam` | `1 × gem_topaz_cui` | 500 | 85% |
| 24 | `craft_gem_emerald` | `herb` | Luyện Thanh Mộc Tiên Châu · Thô | `10 × bich_ngoc_linh_thao` | `1 × gem_emerald_cui` | 500 | 85% |
| 25 | `craft_gem_amethyst` | `herb` | Luyện Tử Lôi Tiên Châu · Thô | `10 × tu_van_sam` | `1 × gem_amethyst_cui` | 500 | 85% |

### 9.4. Thuật Toán Đánh Giá Dung Hợp Động Vạn Năng (`GemData.EvaluateFusion`)
Khi người chơi bỏ bất kỳ tổ hợp nguyên liệu nào vào 5 ô Lò Hợp, hàm `EvaluateFusion` (trên C# và `evaluateCube` trên JS) đánh giá theo **7 tầng ưu tiên tuần tự**:

1. **Tầng 1 — Khớp Công Thức Cố Định (`Recipes`):**
   - Nếu tập nguyên liệu khớp chính xác 100% với 1 trong 25 công thức cố định ở mục 9.3, trả về ngay công thức đó.
2. **Tầng 2 — Dung Hợp Linh Thảo Thuần (1 đến 5 ô linh thảo bất kỳ, mỗi ô 10 cây):**
   - **1 ô (`herb_cui_1`):** Ra `1 × Tiên Châu Thô` theo hệ của thảo dược · Phí `500` linh thạch · Tỉ lệ `85%`.
   - **2 ô (`herb_cui_2` - Song Thảo Ngưng Châu):** Ra `1 × Tiên Châu Thô` (ngẫu nhiên theo tỷ trọng các thảo dược đưa vào) · Phí `600` · Tỉ lệ `90%`.
   - **3 ô (`herb_cui_3` - Tam Thảo Tụ Linh):** Bội thu nhận **`2 × Tiên Châu Thô`** · Phí `750` · Tỉ lệ `85%`.
   - **4 ô (`herb_xin_4` - Tứ Thảo Thăng Hoa):** Đột phá thẳng lên **`1 × Tiên Châu Hoàn Mỹ`** · Phí `900` · Tỉ lệ `75%`.
   - **5 ô (`herb_xin_5` - Ngũ Thảo Đại Đạo):** Đột phá thẳng lên **`1 × Tiên Châu Hoàn Mỹ`** · Phí `1.000` · Tỉ lệ `85%`.
3. **Tầng 3 — Dung Hợp Cổ Phù Sơ Thuần (2 đến 5 ô Cổ Phù Sơ cùng hoặc khác hệ):**
   - *(Lưu ý: 3 viên cùng hệ đã khớp ở Tầng 1; 5 viên đủ Ngũ Hành đã khớp `stone_socket` ở Tầng 1)*.
   - **2 ô:** Ra `1 × Cổ Phù Cao` (theo hệ của các viên đưa vào) · Phí `150` · Tỉ lệ `60%`.
   - **3 ô khác hệ:** Ra `1 × Cổ Phù Cao` · Phí `250` · Tỉ lệ `75%`.
   - **4 ô:** Ra `1 × Cổ Phù Cao` · Phí `300` · Tỉ lệ `90%`.
   - **5 ô:** Đại dung hợp nhận **`2 × Cổ Phù Cao`** · Phí `400` · Tỉ lệ `90%`.
4. **Tầng 4 — Dung Hợp Cổ Phù Cao Thuần (2 đến 5 ô Cổ Phù Cao cùng hoặc khác hệ):**
   - *(Lưu ý: Các cặp Viêm+Lôi, Băng+Thổ, Viêm+Băng 2 ô đã khớp công thức Biến Dị đích danh ở Tầng 1)*.
   - **2 ô khác:** Biến dị ngẫu nhiên ra `1` trong 3 Cổ Phù Biến Dị (`stone_liet_loi`, `stone_huyen_bang`, `stone_am_duong`) · Phí `600` · Tỉ lệ `70%`.
   - **3 ô (Tam Tài Biến Dị):** Ra `1 × Cổ Phù Biến Dị` · Phí `750` · Tỉ lệ `82%`.
   - **4 ô (Tứ Tượng Biến Dị):** Ra `1 × Cổ Phù Biến Dị` · Phí `900` · Tỉ lệ `90%`.
   - **5 ô (`free_cao_to_thien_phu` - Ngũ Khí Triều Nguyên):** Thăng hoa thẳng lên **`1 × Cổ Phù Tiên Châu` (`gem_thien_phu`)** · Phí `2.000` · Tỉ lệ `80%`.
5. **Tầng 5 — Dung Hợp Tiên Châu Thô Thuần (2 đến 5 ô Tiên Châu Thô cùng hoặc khác loại):**
   - *(Lưu ý: 3 viên cùng loại đã khớp ở Tầng 1 với phí 400, tỉ lệ 75%)*.
   - **2 ô:** Ra `1 × Tiên Châu Hoàn Mỹ` · Phí `300` · Tỉ lệ `55%`.
   - **3 ô khác loại:** Ra `1 × Tiên Châu Hoàn Mỹ` · Phí `450` · Tỉ lệ `68%`.
   - **4 ô:** Ra `1 × Tiên Châu Hoàn Mỹ` · Phí `500` · Tỉ lệ `88%`.
   - **5 ô:** Đại dung hợp nhận **`2 × Tiên Châu Hoàn Mỹ`** · Phí `650` · Tỉ lệ `90%`.
6. **Tầng 6 — Dung Hợp Bảo Vật Cao Giai Thuần (`HighTierItems`: Tiên Châu Hoàn Mỹ, Cổ Phù Biến Dị, Khai Khiếu Cổ Phù, Cổ Phù Tiên Châu — 2 đến 5 ô):**
   - **2 ô (Song Bảo Quy Nhất):** Ra `1 × Cổ Phù Tiên Châu` (`gem_thien_phu`) · Phí `3.000` · Tỉ lệ `68%`.
   - **3 ô (Tam Hoa Tụ Đỉnh):** Ra `1 × Cổ Phù Tiên Châu` · Phí `3.500` · Tỉ lệ `80%`.
   - **4 ô (Tứ Linh Quy Nhất):** Ra `1 × Cổ Phù Tiên Châu` · Phí `4.000` · Tỉ lệ `90%`.
   - **5 ô (Ngũ Hành Chí Tôn):** Nhận liền **`2 × Cổ Phù Tiên Châu`** · Phí `5.000` · Tỉ lệ `92%`.
7. **Tầng 7 — Dung Hợp Vạn Năng Hỗn Hợp (2 đến 5 ô phối trộn khác loại/phẩm bất kỳ):**
   Tính tổng Điểm Linh Lực: $\text{totalScore} = \sum \text{GetItemPowerScore}(\text{item}) \times \text{slots}$.
   - **Nhánh 7a (`mixed_to_thien_phu`):** Khi `totalScore >= 14` HOẶC (`hasHighTierInput` và `totalScore >= 11`):
     - Sản phẩm: **`1 × Cổ Phù Tiên Châu` (`gem_thien_phu`)**
     - Phí: `2.500` linh thạch
     - Tỉ lệ: $\text{Clamp}(0.55 + \text{totalSlots} \times 0.06, 0.65, 0.90)$ (từ `67%` đến `85%`).
   - **Nhánh 7b (`mixed_high`):** Khi `totalScore >= 6`:
     - Sản phẩm: Thăng hoa lên nhóm phẩm cao hơn (Tiên Châu Hoàn Mỹ hoặc Cổ Phù Biến Dị tương ứng với nguyên liệu đầu vào).
     - Phí: $600 + \text{totalSlots} \times 100$ linh thạch (từ `800` đến `1.100`).
     - Tỉ lệ: $\text{Clamp}(0.60 + \text{totalSlots} \times 0.06, 0.68, 0.90)$ (từ `72%` đến `90%`).
   - **Nhánh 7c (`mixed_low`):** Khi `totalScore < 6`:
     - Sản phẩm: Thăng hoa lên Cổ Phù Cao Cấp hoặc Tiên Châu Thô tương ứng.
     - Phí: `350` linh thạch.
     - Tỉ lệ: $0.65 + \text{totalSlots} \times 0.05$ (từ `75%` đến `90%`).

### 9.5. Cơ Chế Bảo Lưu Nguyên Liệu Khi Thất Bại
Khi hợp thành thất bại (`failures > 0`), người chơi **không bị mất trắng toàn bộ nguyên liệu**:
- Hệ thống tìm nguyên liệu chính có số lượng yêu cầu lớn nhất:
  `primaryInput = RequiredInputs.OrderByDescending(kv => kv.Value).FirstOrDefault()`
- Hoàn trả về Kho số lượng:
  $$\text{Preserved} = \max(1, \lfloor \text{primaryInput.Value} / 2 \rfloor) \times \text{failures}$$

---

## 10. API Endpoints & Giao Thức Tương Tác

Tất cả các API đều thuộc nhóm `/api/players/{id:guid}` (phương thức `POST`) và trả về `ActionResponse` chứa `PlayerStateDto` mới nhất (bao gồm `gemState`):

| Endpoint | Tham số Query | Phương thức Backend | Mô tả chức năng |
|---|---|---|---|
| `/gem/socket` | `slot`, `socketIndex`, `itemId` | `GameService.SocketGem` | Chú linh (khảm) vật phẩm `itemId` từ Kho vào lỗ `socketIndex` (0..2) của ô trang bị `slot`. Nếu lỗ đã có vật phẩm, tự động tháo vật phẩm cũ về Kho. |
| `/gem/unsocket` | `slot`, `socketIndex` | `GameService.UnsocketGem` | Tháo vật phẩm đang khảm ở lỗ `socketIndex` của ô `slot` về Kho miễn phí. |
| `/gem/unlock` | `slot` | `GameService.UnlockSocket` | Tiêu hao `1 × stone_socket` (Khai Khiếu Cổ Phù) để mở thêm 1 lỗ khảm cho ô `slot` (tối đa 3 lỗ). |
| `/gem/craft/{recipeId}` | `n` (số lần, mặc định 1) | `GameService.CraftGemRecipe` | Hợp thành theo mã công thức cố định `recipeId` với số lần `n`. |
| `/gem/fuse-free` | `items` (chuỗi ID phân cách dấu phẩy), `n` | `GameService.CraftGemFromItems` | Dung hợp tự do từ 1–5 ô nguyên liệu trong Lò Hợp (`items=id1,id2,...`), thực hiện `n` lần. |
| `/gem/quick-fuse` | *(không có)* | `GameService.QuickFuseGems` | Hợp nhanh 1 chạm toàn bộ Cổ Phù Sơ (3 $\rightarrow$ 1) và Tiên Châu Thô (3 $\rightarrow$ 1) trong Kho. |

---

## 11. Giao Diện Người Dùng (UI/UX) & Quy Tắc Thao Tác

Giao diện **Chú Linh** (`wwwroot/js/vault.js` & `wwwroot/css/vault.css`) gồm thanh điều hướng 3 tab phụ:

### 11.1. Tab 1: Vị Trí Chú Linh (`vaultTab = 'socket'`)
Bố cục 3 cột:
- **Cột Trái (Danh sách 10 Ô Trang Bị):**
  - Hiển thị 10 vị trí trang bị cùng icon món đồ đang mặc (hoặc báo `(Trống)`), kèm các chấm sáng (`socket-slot-pips`) phản ánh màu sắc thực của từng viên Cổ Phù / Tiên Châu đang khảm.
  - **Nhấp chuột trái:** Chọn vị trí trang bị.
  - **Nhấp chuột phải (`contextmenu`):** Tháo nhanh viên Cổ Phù / Tiên Châu ở lỗ cuối cùng của ô đó về Kho.
- **Cột Giữa ( Bàn Chú Linh Trung Tâm - Center Stage):**
  - Thẻ thông tin trang bị kết hợp tổng chỉ số (`getCombinedItemStatsHtml`).
  - **3 Quả Cầu Lỗ Khảm (`socket-orb`):** Hiển thị trạng thái `Đang Khảm`, `Trống (+)`, hoặc `Khóa (🔒)`.
    - **Nhấp chuột trái:** Chọn lỗ khảm (1, 2 hoặc 3).
    - **Nhấp chuột phải hoặc Nhấp đúp chuột trái vào lỗ đang có ngọc:** Tháo ngay viên đó về Kho.
    - Nếu chọn lỗ đang khóa: Hiển thị nút **"Dùng Khai Khiếu Cổ Phù (X/1)"** để đục lỗ ngay.
  - **Bảng Tổng Hiệu Quả Chú Linh Trang Bị:** Tóm tắt trực quan cả 3 lỗ của ô đang chọn.
- **Cột Phải (Kho Vật Phẩm Chú Linh - Picker):**
  - Bộ lọc: `Tất cả` | `Tiên Châu` | `Cổ Phù`.
  - **Nhấp đúp chuột trái (hoặc chạm đúp) vào vật phẩm:** Chú linh ngay lập tức vào lỗ đang chọn.

### 11.2. Tab 2: Kho (`vaultTab = 'vault'`)
- **Thanh Cấp Bậc Kho:** 3 nút chuyển đổi `Sơ Cấp`, `Cao Cấp`, `Đỉnh Cấp` kèm badge tổng số lượng viên ở từng cấp.
- **Nút `Hợp Nhanh (1 Chạm)`:** Nằm nổi bật ở góc trên bên phải để gộp nhanh toàn bộ đá/ngọc cấp thấp.
- **2 Ngăn Trưng Bày:** Ngăn Tiên Châu và Ngăn Cổ Phù hiển thị thẻ vật phẩm với đầy đủ icon, tên theo màu phẩm chất, dòng mô tả hiệu ứng chú linh và số lượng `×N`.

### 11.3. Tab 3: Lò Hợp (`vaultTab = 'cube'`)
Bố cục 2 cột:
- **Cột Trái (Kho Nguyên Liệu & Gợi Ý Công Thức):**
  - Bộ lọc 5 danh mục: `Tất cả`, `Cổ Phù`, `Tiên Châu`, `Thăng Hoa`, `Linh Thảo`.
  - **Lưới Kho (`cube-owned-grid`):** Hiển thị mọi Cổ Phù, Tiên Châu và Linh Thảo người chơi đang sở hữu.
    - **Nhấp chuột trái:** Đặt 1 đơn vị nguyên liệu (1 viên hoặc 10 cây linh thảo) vào Lò Hợp.
    - **Nhấp chuột phải:** Lấy 1 đơn vị nguyên liệu đó ra khỏi Lò Hợp.
  - **Danh sách Gợi Ý Công Thức (`cube-recipe-list`):** Hiển thị 25 công thức cố định kèm trạng thái `Đủ / Thiếu` và tỉ lệ `%`. Nhấp vào công thức sẽ tự động điền nguyên liệu hiện có vào Lò.
- **Cột Phải (Lò Hợp 5 Ô Tương Tác):**
  - **5 Ô Đầu Vào (`cube-input-slots`):** Nhấp chuột trái hoặc phải vào bất kỳ ô nào để lấy nguyên liệu đó ra khỏi Lò.
  - **Ô Đầu Ra (`cube-output-box`):** Tự động tính toán và hiển thị ngay lập tức vật phẩm kết quả (hoặc biểu tượng `?` nếu là hộp ngẫu nhiên nhiều hệ), tên pháp quyết dung hợp, tỉ lệ thành công (`%`), chi phí linh thạch và số lần có thể hợp tối đa (`maxCraft`).
  - **4 Nút Hành Động:** `Xóa lò`, `Tự động điền`, `Hợp Thành` (1 lần), và `Tối đa ×N` (hợp hàng loạt).
