# Kế hoạch cập nhật tám chỉ số trang bị và quái

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Chỉ triển khai sau khi các quyết định còn mở bên dưới được chốt.

**Goal:** Bổ sung và thống nhất tám chỉ số không giới hạn trên cho trang bị, nhân vật và quái, với boss tăng chỉ số theo độ khó từ Thường đến Địa Ngục.

**Architecture:** Dùng một mô hình chỉ số chung và một nơi tính đối kháng cho PvE/PvP. Sinh chỉ số quái theo độ khó, loại quái và hồ sơ boss; cộng chỉ số trang bị vào cùng luồng tổng hợp hiện hành, bảo toàn dữ liệu cũ.

**Tech Stack:** C# / .NET 8 Windows, JavaScript, xUnit trong `test/GemSystemTests`.

**Spec:** Yêu cầu trong cuộc trò chuyện ngày 29/10/2026, được ghi lại tại phần “Yêu cầu đã chốt” trong tài liệu này. Đây là kế hoạch, chưa phải thay đổi gameplay.

## Yêu cầu đã chốt

- Tám chỉ số: Bạo Kích, Kháng Bạo Kích, Chính Xác, Né, Liên Kích, Kháng Liên Kích, Phản Đòn, Kháng Phản Đòn.
- Cả tám chỉ số có giá trị từ 0% trở lên, không có giới hạn trên.
- Áp dụng cho trang bị và các thực thể chiến đấu; lưu và hiển thị đầy đủ giá trị vượt 100%.
- Cập nhật người dùng đã chốt sau task 4: nền boss Thường 100%; mỗi độ khó tiếp theo cộng 50 điểm phần trăm vào cả tám chỉ số.
- Cộng riêng 50 điểm phần trăm vào Kháng Bạo Kích, Kháng Liên Kích và Kháng Phản Đòn của tất cả quái, sau hệ số loại quái.
- Áp dụng xuyên suốt Thường → Khó → Ác Mộng → Địa Ngục; không tăng thêm theo cảnh giới/map trong công thức này.
- Boss có đúng một chỉ số chuyên biệt được cộng thêm 50 điểm phần trăm; đây là phần cộng riêng trước khi áp dụng tăng kháng toàn bộ. Sau tăng kháng, có thể có nhiều chỉ số bằng nhau ở mức cao nhất.
- Quái thường và tinh anh có chỉ số thấp hơn boss cùng độ khó.

## Global Constraints

- Giá trị chỉ số: hữu hạn, >= 0, không áp trần gameplay; từ chối NaN/Infinity ở đầu vào.
- Không cắt giá trị gốc hoặc giá trị đối kháng về 100%.
- Phân biệt chỉ số, xác suất kích hoạt và hệ số sát thương; không coi chúng là cùng một đại lượng.
- Bảo toàn nguồn cộng từ trang bị, bộ đồ, tinh thạch, công pháp, linh thú, thần thông; không cộng hai lần.
- Không đổi ID vật phẩm, ô trang bị, dữ liệu sở hữu hoặc các cơ chế không thuộc tám chỉ số.
- Có sẵn nhiều thay đổi chưa commit trong workspace: đọc lại code khi triển khai và không ghi đè công việc hiện có.

## Thang chỉ số boss

Quy ước `difficultyStep`: Thường = 0, Khó = 1, Ác Mộng = 2, Địa Ngục 1 = 3.

```text
baseRating = 100 + 50 * difficultyStep
featuredRating = baseRating + 50
finalResistance = tierScaledBaseRating + 50 + (50 nếu là chỉ số chuyên biệt của boss)
```

- Thường: nền 100%, chuyên biệt 150%; kháng nền 150%, kháng chuyên biệt 200%.
- Khó: nền 150%, chuyên biệt 200%; kháng nền 200%, kháng chuyên biệt 250%.
- Ác Mộng: nền 200%, chuyên biệt 250%; kháng nền 250%, kháng chuyên biệt 300%.
- Địa Ngục 1: nền 250%, chuyên biệt 300%; kháng nền 300%, kháng chuyên biệt 350%.
- Khi có Địa Ngục 2: nền 300%, chuyên biệt 350%; kháng nền 350%, kháng chuyên biệt 400%.
- Không tạo thêm tầng Địa Ngục trong phạm vi thay đổi này; chỉ chuẩn bị hàm tính hỗ trợ tăng tầng.
- Chọn chỉ số nổi bật theo ID boss ổn định, không theo tên dịch hoặc chọn ngẫu nhiên mỗi trận.

## Các quyết định triển khai

Phân biệt lựa chọn người dùng đã chốt với cấu hình khởi điểm do agent chọn:

1. **Quái thường/tinh anh:** cấu hình hiện có khi thực hiện task 5 là 40%/70% nền boss (giữ các chỉnh sửa của người dùng trong workspace), sau đó ba chỉ số Kháng cộng riêng 50 điểm phần trăm. Ở Thường: nền 40%/70%, kháng 90%/120%; ở Địa Ngục 1: nền 100%/175%, kháng 150%/225%. Không nhân phần cộng 50 theo hệ số loại quái và không cộng lặp.
2. **Công thức đối kháng:** dùng phép trừ ở task 4: Bạo Kích–Kháng Bạo Kích, Né–Chính Xác, Liên Kích–Kháng Liên Kích, Phản Đòn–Kháng Phản Đòn. Kết quả âm về 0; thay công thức tỉ lệ Liên Kích cũ bằng phép trừ và không dùng Liên Kích của đối phương làm kháng.
3. **Kết quả đối kháng >100% — đã chốt khi triển khai:** chắc chắn kích hoạt một lần, không sinh nhiều lần kích hoạt. Giá trị chỉ số và kết quả đối kháng vẫn giữ đầy đủ, không cắt về 100%.
4. **Phản Đòn — người dùng đã chốt ở task 4:** % cơ hội phản công. Các nguồn `dia_mach`/`gems.Reflect` nay cộng vào cơ hội này, không nhân sát thương nhận. Không thay số sở hữu hay giá trị phần thưởng cũ.
5. **Luật phản công do agent chọn:** một phản công tối đa mỗi hành động đối phương, chỉ sau sát thương trực tiếp làm mất HP và bên nhận còn sống. Đòn phản là 100% công, trừ phòng thủ và giảm sát thương; có thể né bằng Chính Xác/Né, bị khiên/hấp thụ chặn, không bạo kích, không Liên Kích, không sinh phản công tiếp, không hút máu hay cộng nộ. Kỹ năng phản đòn linh thú giữ đường xử lý riêng; hồi sinh Hồ Điệp vẫn áp dụng khi phản công gây chí tử. Khống chế lượt thường không khóa phản ứng Phản Đòn.
6. **Ngân sách trang bị:** cấu hình theo ô/phẩm chất/cảnh giới đã triển khai ở task 2; task 3 có danh sách dòng nổi bật từng boss. Đây là cấu hình khởi điểm, tiếp tục đánh giá ở mô phỏng task 5.

## Hiện trạng và các điểm cần sửa

- `Game/GameData.cs`: `ItemDef` có `CritPct`, `SpeedPct` cũ tương ứng Né; `CombatStats` có Crit/Dodge/Accuracy/Combo, chưa có đủ tám trường. `MonsterDef` mới có Combo. `BossTrait` có `CritRes`/`ExtraRes` riêng.
- `Game/EquipmentBudget.cs`: ngân sách hiện sinh Bạo Kích, Né và tu luyện, chưa có đủ tám chỉ số.
- `Game/EquipmentEffects.cs`: có `crit_resist`, `lien_kich`, `dia_mach`; `lien_kich` đang có trần 30.
- `Game/ComboCombat.cs`: so sánh Liên Kích hai bên, đang clamp kết quả 0–100.
- `Game/MonsterCombatRatings.cs`: chỉ có Bạo Kích/Né, bắt đầu từ Khó và tăng theo chapter; khác yêu cầu mới.
- `Game/GameService.cs`: tổng hợp chỉ số, giới hạn hiển thị, PvE và kháng boss nằm ở nhiều đoạn; cần gom và tránh trừ kháng hai lần.
- `Game/PvpCombat.cs`, `Game/GameService.Arena.cs`, `Game/Arena.cs`, `Game/ArenaMock.cs`: cần mang đầy đủ chỉ số qua snapshot, validation và mô phỏng.
- `Game/Dtos.cs`: `WaveInfoDto` hiện chỉ công bố Crit/Dodge; cần đủ tám chỉ số để UI đồng nhất với trận đánh.
- `wwwroot/js/inventory.js`, `wwwroot/js/battle.js`, `wwwroot/js/i18n-en.js`: rà soát thẻ đồ, chỉ số nhân vật, thông tin quái, diễn hoạt và nhãn dịch.

## Review Focus

- Snapshot và vật phẩm cũ thiếu trường mới: đọc được, mặc định 0, bảo toàn phần thưởng đã có.
- Giá trị 0, âm, >100 và rất lớn: không trần ngầm, không NaN/Infinity hoặc tràn số khi tính sát thương.
- Đổi trang bị hoặc tải lại game: tính lại đúng, không lưu/cộng chồng chỉ số dẫn xuất.
- Boss có kháng cũ và hồ sơ mới: chỉ áp dụng một nguồn cấu hình hiệu lực, không cộng/trừ hai lần.
- Phản đòn hai chiều, khiên và đòn kết liễu: xử lý xác định, không lặp vô hạn, khớp log và HP.

## Task 1: Chuẩn hóa mô hình tám chỉ số và dữ liệu cũ

**Files:** sửa `Game/GameData.cs`, `Game/Dtos.cs`, `Game/EquipmentEffects.cs`; thêm `Game/CombatRatings.cs`, `test/GemSystemTests/CombatRatingsTests.cs`.

**Interfaces dự kiến:** `CombatRatings(double Crit = 0, double CritResist = 0, double Accuracy = 0, double Dodge = 0, double Combo = 0, double ComboResist = 0, double Reflect = 0, double ReflectResist = 0)`. Các giá trị là chỉ số %, chưa phải xác suất hoặc sát thương.

- [x] Viết test: tám trường mặc định 0; 350% và 1000% giữ nguyên khi tổng hợp/serialize; dữ liệu cũ thiếu trường mới vẫn tải được.
- [x] Chạy test mới để xác nhận thiếu hành vi cần bổ sung: 17 trường mới thất bại trước khi thêm model.
- [x] Thêm trường tùy chọn tương thích constructor/JSON cũ; `CombatRatings.From(...)` chuyển ItemDef/CombatStats/MonsterDef sang mô hình chung.
- [x] Giữ các trường Reflect mới tách khỏi phép tính phản sát thương cũ; không đổi đơn vị hoặc gameplay khi chưa chốt ý nghĩa Phản Đòn.
- [x] Chạy test mới và nhóm regression; kiểm tra input âm/NaN/Infinity tại `CombatRatings.Validate()`.

**Tiến độ:** hoàn tất task 1–4. Giữ workspace hiện tại trên nhánh `codex/equipment-combat-ratings` để bảo toàn các thay đổi chưa commit làm nền của kế hoạch. Task 5 và cập nhật wiki đã hoàn tất; xem kiểm chứng bên dưới.

## Task 2: Ngân sách trang bị và tổng chỉ số nhân vật

**Files:** sửa `Game/EquipmentBudget.cs`, `Game/EquipmentEffects.cs`, `Game/GameData.cs`, `Game/GameService.cs`; thêm `test/GemSystemTests/EquipmentCombatRatingsTests.cs`.

**Interfaces:** `GameService.Stats(Player)` trả về `CombatStats` đủ tám chỉ số, có thể chuyển thành `CombatRatings` bằng adapter task 1; tổng hợp thuần, không lưu chỉ số dẫn xuất hoặc gọi vòng lại `Stats()`.

**Cấu hình khởi điểm do agent chọn khi thực hiện bước 2:** `rating = Round(10 * (MinRealm + 1) * Utility[quality] / 100, 1, AwayFromZero)`. Tái sử dụng mười hệ số Utility hiện có, không nhân thêm QualityScale vào trường chỉ số trực tiếp. Ví dụ Luyện Khí phẩm Phàm = 10%, cảnh giới cuối phẩm tối cao = 230% cho chỉ số chuyên biệt mỗi món. Cấu hình này cần đánh giá lại ở mô phỏng cân bằng; không phải kết luận đã cân bằng với boss.

- Vũ khí: Bạo Kích; pháp bảo (công kích/hộ thân): Kháng Phản Đòn.
- Nón và hộ phù: Kháng Bạo Kích; áo: Phản Đòn.
- Găng và dây chuyền: Chính Xác; quần: Kháng Liên Kích.
- Giày: Né; nhẫn: Liên Kích.
- Hiệu ứng, tinh thạch, thưởng bộ và các nguồn cũ vẫn cộng riêng đúng một lần; Phản Đòn mới chỉ tổng hợp dữ liệu, chưa thay đổi cơ chế phản sát thương đang chạy.

- [x] Ghi cấu hình ngân sách từng ô/phẩm chất/cảnh giới trước khi triển khai task.
- [x] Viết test một món, cả bộ, đổi/tháo đồ; tổng vượt 100%; không nhân phẩm chất hai lần.
- [x] Quan sát test fail rồi triển khai đủ tám chỉ số; bỏ trần Liên Kích trong EquipmentEffects, Stats và CombatLimits.
- [x] Kiểm tra bảng nguồn đủ tám chỉ số, có tinh thạch và thần thông phẩm 8/9; các test công pháp hiện có tiếp tục đạt.
- [x] Xác nhận serialize/tải lại và tính lặp giữ cùng tổng; giữ công/thủ/máu/tu luyện theo ngân sách cũ. Né từ giày cộng trực tiếp cả ở Luyện Khí, chỉ Né nền cảnh giới giữ mốc mở cũ.

**Kiểm chứng bước 2:** 167/167 test đạt với `dotnet test test/GemSystemTests/GemSystemTests.csproj --no-restore -p:BaseOutputPath=D:/projects/van-dao/VanDao/build-check/combat-ratings/`. Build trong test thành công; vẫn có cảnh báo WindowsBase/nullable đã có. Log: `output/equipment-ratings-suite.log`.

## Task 3: Hồ sơ boss và chỉ số quái theo độ khó

**Files:** sửa `Game/MonsterCombatRatings.cs`, `Game/GameData.cs`, `Game/HellMapData.cs`, `Game/GameService.cs`, `Game/Dtos.cs`; thêm `test/GemSystemTests/MonsterCombatRatingsTests.cs`; điều chỉnh `GemRealmBalanceTests.cs`, `HellBossResistanceTests.cs`.

**Interfaces dự kiến:** `MonsterCombatRatings.For(string monsterId, string tier, int difficultyStep) -> CombatRatings`; danh mục boss ánh xạ ID sang đúng một trường nổi bật.

- [x] Thêm `MonsterDef.Id` tùy chọn, ánh xạ 16 ID boss trong `MonsterCombatRatings.BossProfiles`. Dữ liệu cũ lấy ID từ đường dẫn asset bỏ `img/` và phần mở rộng; ID đã cấp không thay theo tên dịch/hình hiển thị.
- [x] Test toàn bộ hồ sơ ở step 0/1/2/3 với nền 100/150/200/250, phần chuyên biệt +50 và ba loại Kháng cộng thêm 50 độc lập.
- [x] Test step 4 cho nền 300/chuyên biệt 350/kháng 350 hoặc 400; mọi chương và độ khó giữ thang riêng, không kế thừa nhầm ID/chỉ số nổi bật từ boss dùng làm mẫu nhân HP/công/thủ.
- [x] Quái thường dùng 30%, tinh anh 60% nền boss. ID boss lạ dùng Bạo Kích làm dòng nổi bật, xác định và không phụ thuộc RNG.
- [x] Loại bỏ đường tính Crit/Dodge theo chapter và bảng DiffResist; `DiffTrait` giữ giảm sát thương/cuồng bạo, trả CritRes/ExtraRes bằng 0. PvE dùng kháng trong MonsterDef một lần, cập nhật khi đổi đợt.
- [x] DTO bản đồ mang đủ tám trường trực tiếp từ đợt quái đã sinh. Test đối chiếu mọi ải ở cả bốn độ khó.

**Phân bổ dòng nổi bật khởi điểm:**

- Tứ Tý Ma Viên, Cùng Kỳ Vương: Liên Kích.
- Hắc Giao, Hắc Hùng Tinh: Kháng Bạo Kích.
- Băng Phượng, Ám Ma: Né.
- Đào Ngột, Thiết Phiến Công Chúa: Phản Đòn.
- Cùng Kỳ, Thôi Phán Quan: Chính Xác.
- Hỗn Độn, Ngưu Ma Vương: Kháng Liên Kích.
- Kiếm Đế, Thánh Anh Đại Vương: Bạo Kích.
- Thao Thiết, Diêm La Vương: Kháng Phản Đòn.
- Boss Địa Ngục tái sử dụng ID Ám Ma/Đào Ngột/Thao Thiết/Cùng Kỳ Vương giữ cùng dòng nổi bật, với nền chỉ số Địa Ngục.

**Tích hợp task 3:** Bạo Kích, Né và hai loại kháng đang dùng trong PvE lấy từ hồ sơ quái; Kháng Bạo của nhân vật lấy tổng từ Stats. Liên Kích vẫn dùng hàm tỉ lệ hiện hành nhưng đối kháng Kháng Liên Kích, không trừ thêm trait. Chính Xác của quái, quái chủ động Liên Kích và cặp Phản Đòn chưa thêm hành vi mới; thuộc task 4.

**Kiểm chứng bước 3:** 184/184 test C# và 6/6 test client đạt. Giữ test tuyệt kỹ nộ: 17 sát thương khi không bạo kích, 26 khi bạo kích; fixture test Liên Kích thêm nhẫn để vượt kháng mới của quái. Log: `output/monster-ratings-suite.log`. Không thay HP/công/thủ, phần thưởng hoặc số tầng đã mở.

**Cập nhật tăng kháng sau task 4:** người dùng xác nhận giữ nền mới 100%/+100% mỗi độ khó và cộng riêng 50 điểm phần trăm cho cả ba loại Kháng. Đã chuẩn hóa Kháng Bạo của boss từ giá trị đang chỉnh +100 về +50 để nhất quán với Kháng Liên Kích/Kháng Phản Đòn. Cập nhật kỳ vọng theo thang mới; test phản công dùng trang bị/tinh thạch Chính Xác đủ vượt Né, test Liên Kích dùng nhẫn đủ vượt kháng, không bỏ kiểm tra HP/nộ/hồi sinh. Test gọi Apply nhiều lần xác nhận không cộng lặp phần kháng. Kết quả mới nhất: **200/200 test C# đạt**, log `output/monster-resistance-suite.log`; số 184 phía trên là kết quả lịch sử của lần triển khai task 3 ban đầu.

## Task 4: Đối kháng và phản đòn thống nhất PvE/PvP

**Files:** thêm `Game/CombatRatingRules.cs`; sửa `Game/ComboCombat.cs`, `Game/GemCombat.cs`, `Game/GameService.cs`, `Game/PvpCombat.cs`, `Game/GameService.Arena.cs`, `Game/Arena.cs`, `Game/ArenaMock.cs`; thêm `test/GemSystemTests/CombatRatingRulesTests.cs`; cập nhật `ComboTests.cs`, `DodgeAccuracyTests.cs`, `ShieldReflectTests.cs`.

**Interfaces đã triển khai:**

- `CombatRatingRules.Opposed(double own, double resistance) -> double`: hiệu số không âm, không trần trên, từ chối NaN/Infinity.
- `CombatRatingRules.Roll(Random random, double own, double resistance = 0) -> bool`: một lần kích hoạt, chắc chắn khi hiệu số >=100.
- `CombatRatingRules.Counter(Random random, double rating, double resistance, long actualDamage, long remainingHp) -> bool`: kiểm tra điều kiện mất HP/còn sống trước khi gieo.
- `CombatRatingRules.CounterDamage(long attack, long defense, double reduction = 0) -> long`: sát thương phản công 100% công, bảo toàn giới hạn số sát thương hiện hành.
- `PvpFighter.Ratings` và `HitAccuracy`: đọc tổng đủ tám chỉ số; `Accuracy`/`ComboRating` nullable hỗ trợ snapshot cũ dùng tinh thạch/ExtraHit, snapshot mới dùng tổng rõ ràng và không cộng lại thần thông.
- Snapshot mới `V=2`; giữ trường cũ để đọc tương thích. Snapshot v1 giữ giá trị Reflect và dùng cơ chế cơ hội phản công mới khi trận được mô phỏng bằng bản mới.
- `BattleDto.MonsterExtraDamage`/`MonsterReflectDamage`: mảng theo lượt để phân biệt Liên Kích của quái và phản công của đối thủ trong PvE/PvP. CounterDamage trong PvE bao gồm đòn thường và đòn Liên Kích, không cộng phản công vào nộ.

- [x] Ghi lựa chọn người dùng về cơ hội phản công và quy tắc một lần khi >=100%; công thức/chữ ký hàm cụ thể ở trên.
- [x] Test bằng nhau, kháng cao hơn, 350%–280%=70%, hiệu số 250% giữ nguyên và chỉ kích hoạt một lần.
- [x] Test Kháng Liên Kích độc lập với Liên Kích đối thủ; snapshot đầy đủ, legacy fallback, sanitize 1000% và NaN/Infinity.
- [x] PvE/PvP dùng chung quy tắc Bạo Kích, Né/Chính Xác, Liên Kích và Phản Đòn; tổng nhân vật đi vào Arena đúng một lần.
- [x] Test phản công hai bên, không phản đòn đã bị khiên chặn hoàn toàn hoặc gây chết bên nhận; khiên bên nhận phản công hoạt động, không cộng nộ/không lặp. Hồi sinh Hồ Điệp trên đòn phản chí tử được kiểm chứng.
- [x] Bỏ trần Combo ở công thức, snapshot, ArenaMock và helper thần thông. Trần trong phép tính xác suất kỳ vọng/giảm sát thương ngoài tám chỉ số giữ nguyên.
- [x] Test replay PvE đối chiếu thay đổi HP = sát thương phản công quái + sát thương lượt quái, kiểm tra replay PvP đúng hướng phản công. Chặn một đòn trong Liên Kích không còn báo vô hiệu toàn lượt nếu đòn còn lại gây sát thương.

**Kiểm chứng bước 4:** 199/199 test C# và 6/6 test client đạt, build trong test thành công. Log: `output/combat-step4-suite.log`. Chưa triển khai hoạt ảnh riêng của đòn Liên Kích/phản công quái; task 5 sẽ đọc các trường replay mới. Test cũ về công thức tỉ lệ Liên Kích được cập nhật sang phép trừ; test tuyệt kỹ nộ trừ phần đòn Liên Kích thường khi kiểm tra hệ số tuyệt kỹ.

## Task 5: Hiển thị và kiểm chứng tích hợp

**Files:** sửa `wwwroot/js/inventory.js`, `wwwroot/js/battle.js`, `wwwroot/js/i18n-en.js` và các nơi tiêu thụ DTO tìm được khi triển khai; cập nhật test client phù hợp.

- [x] Hiển thị đủ tám nhãn tiếng Việt trên thẻ trang bị, tổng nhân vật, thông tin boss/quái; phân biệt chỉ số và xác suất nếu cùng xuất hiện.
- [x] Kiểm tra giá trị 0%, 350%, 1000%, số thập phân và mobile không cắt/mất chữ.
- [x] Chạy `dotnet test test/GemSystemTests/GemSystemTests.csproj`; yêu cầu toàn bộ test pass, cập nhật các kỳ vọng cũ đã bị thay bởi spec mới.
- [x] Chạy `dotnet build VanDao.csproj --no-restore`; yêu cầu build thành công.
- [x] Chạy `node --test test/monster-rage-client.test.cjs` và test client mới; yêu cầu pass.
- [x] Mô phỏng cùng bộ trang bị ở Thường/Khó/Ác Mộng/Địa Ngục 1; ghi tỉ lệ thắng, số lượt, số lần kích hoạt để đánh giá ngân sách đã chọn.
- [x] Kiểm tra trình duyệt với DB QA riêng: tháo/mặc găng cho Chính Xác 230% → 0% → 230%, tải lại giữ 230%; bảng tám chỉ số ở khung 360px. PvE/PvP và dữ liệu replay đối chiếu bằng test mô phỏng; không dùng bảng xếp hạng thật.

**Kết quả cuối task 5:** 200/200 test C#, 22/22 test Node đạt; build thành công. Các script wiki `check.cjs`, `inventory-gem-check.cjs`, `gem-check.cjs` đạt. Wiki xuất lại 1.254 vật phẩm, 10 chương, bốn độ khó/790 ải và 342 đường dẫn ảnh; Địa Ngục dùng đúng tên map, lệnh và Boss ở ải 31. Mốc boss lấy trực tiếp từ dữ liệu game (100/150/200/250), không ghi cứng; tám chỉ số và cơ chế phản công được cập nhật trong cẩm nang.

**Mô phỏng:** `tools/combat_rating_checks` chạy 512 trận với cùng bộ Chân Tiên Hậu kỳ, đủ đồ supreme, công pháp Hỏa tối đa, không ngọc/thú. Kết quả và số lượt gây sát thương Liên Kích/phản công nằm ở `output/combat-ratings-balance.json`; không tự điều chỉnh cân bằng theo một bộ thử. Boss cuối Địa Ngục thắng cả 32 lượt thử, trong khi bộ này vượt các mức trước. Đây là quan sát của fixture thiếu ngọc/thú, không phải kết luận cấu hình tối đa không vượt được Boss.

**Rà soát độc lập:** sửa hai lỗi quan trọng có test RED→GREEN: replay PvP nay dùng sát thương phản công thực làm mất HP sau khiên/miễn tử; HP không bật ngược khi chuyển sang pha Liên Kích hoặc thú. Cờ hồi sinh Hồ Điệp được hiển thị đúng sau phản công chí tử. Ảnh kiểm chứng: `output/combat-ratings-game-qa.png`, `output/combat-ratings-mobile-qa.png`. Chỉ dùng server localhost và DB QA riêng; không deploy/push.

## Tiêu chí hoàn tất

- Đủ tám chỉ số, không giới hạn trên, dữ liệu cũ vẫn đọc được.
- Boss đúng thang độ khó và đúng một chỉ số chuyên biệt được cộng thêm 50 điểm; quái thường/tinh anh thấp hơn boss.
- Trang bị có ngân sách đã chốt; tổng chỉ số và các nguồn cộng nhất quán.
- PvE/PvP áp dụng đủ bốn cặp đối kháng; phản đòn có luật rõ ràng và không tạo vòng lặp.
- Hành vi khi đối kháng vượt 100% được ghi rõ và có test, không âm thầm áp trần.
- Toàn bộ kiểm chứng liên quan pass; có kết quả mô phỏng cân bằng và danh sách thay đổi gameplay.

## Quyết định mới nhất khi thực hiện bước 5

Người dùng xác nhận lại: **giữ nền boss Thường 100%, mỗi độ khó +50 điểm phần trăm**. Ba loại Kháng vẫn cộng riêng 50 điểm. Thông số này thay thế lựa chọn +100 mỗi độ khó được ghi trong phần lịch sử tăng kháng trước đó. Giữ hệ số quái thường/tinh anh 40%/70% đang có trong source và đồng bộ test/wiki theo cấu hình đó.
