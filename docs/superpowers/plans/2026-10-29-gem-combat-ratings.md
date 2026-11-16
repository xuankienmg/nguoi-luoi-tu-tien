# Kế hoạch cập nhật Tinh Thạch theo tám chỉ số chiến đấu

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Các bước dùng checkbox để theo dõi. Task 1–4 đã triển khai; Task 5 chưa triển khai.

**Goal:** Cho Tinh Thạch hỗ trợ đủ tám chỉ số mới, cộng dồn không giới hạn trên và đồng bộ với trang bị, nhân vật, PvE/PvP, giao diện và wiki.

**Architecture:** Giữ 10 dòng Tinh Thạch và hệ cấp 1–10. Một cấu hình ngân sách thuần trả về `CombatRatings`, được chuyển sang các trường/hiệu ứng ngọc hiện có; `GemData.GetSocketedBonuses` tổng hợp đúng một lần, `Stats` là nguồn tổng dùng chung cho chiến đấu và Arena.

**Tech Stack:** C#/.NET 8, JavaScript, xUnit, Node test runner, công cụ xuất wiki và mô phỏng hiện có.

**Spec:** Yêu cầu tạo plan ngày 29/10/2026 và hệ tám chỉ số đã triển khai trong `docs/superpowers/plans/2026-10-29-equipment-combat-ratings.md`. Người dùng đã duyệt phân bổ/ngân sách khi yêu cầu thực hiện Task 1. Ngân sách vẫn là cấu hình khởi điểm để đánh giá cân bằng ở Task 5.

## Yêu cầu và phạm vi

- Hỗ trợ Bạo Kích, Kháng Bạo Kích, Chính Xác, Né, Liên Kích, Kháng Liên Kích, Phản Đòn, Kháng Phản Đòn.
- Cả tám chỉ số từ 0% trở lên, không trần trên; giữ đầy đủ khi cộng, lưu, hiển thị và tính đối kháng.
- Tái sử dụng phép trừ đối kháng hiện hành; hiệu số >=100% kích hoạt chắc chắn một lần.
- Phản Đòn là cơ hội phản công, không phải hệ số nhân sát thương phản lại.
- Tinh Thạch là một nguồn cộng vào tổng; không sửa công thức chiến đấu, hồ sơ boss hay ngân sách trang bị trong đợt này.
- Giữ hiệu ứng hỗ trợ cũ không bị thay thế trực tiếp bởi chỉ số chính mới.

## Global Constraints

- Giữ ID ngọc, tên dòng, cấp/phẩm, icon, số lượng sở hữu, vị trí khảm, dữ liệu ngọc trên từng bản sao trang bị.
- Giữ 10 cấp, hệ phẩm, quy tắc 1/2/3 lỗ, nhóm khảm công kích/phòng thủ và chi phí tháo hiện tại.
- Giữ hợp 9 ngọc, tỉ lệ thành công, bảng rơi và quy tắc cấp 10; không thêm dòng ngọc hoặc ảnh mới.
- Không bỏ trần của Block, giảm sát thương, tốc độ chạy, hồi máu, hấp thụ, nộ hoặc đòn thêm chắc chắn: chúng không thuộc tám chỉ số này.
- Không đổi cơ chế Ám Tinh cấp 10: đòn thêm chắc chắn là hiệu ứng riêng, không phải Liên Kích/Phản Đòn.
- Không lưu tổng chỉ số dẫn xuất vào save; tính lại từ những ngọc đang có hiệu lực.
- Workspace đang có nhiều thay đổi chưa commit; đọc lại source và giữ các chỉnh sửa hiện có khi thực hiện.

## Hiện trạng đã kiểm tra

- `Game/GemProgression.cs` sinh 100 ngọc thuộc 10 dòng; `RebalanceLegacy` tạo ngọc cũ theo cùng cấu hình nhưng giữ ID/icon. Toàn danh mục hiện có 124 ngọc.
- Đã có Bạo Kích, Chính Xác, Né, Liên Kích và Kháng Bạo; các nguồn phân tán giữa `CritPct` và `ItemEffect`.
- `GemBonuses` đã có `Crit`, `CritResist`, `Accuracy`, `Dodge`, `ExtraHit`, `Reflect`; còn thiếu `ComboResist` và `ReflectResist`.
- `GetSocketedBonuses` vẫn dùng `Math.Min(30, extraHit)`: trần này trái với hệ chỉ số mới và phải bỏ.
- `Stats` đã cộng `gems.CritResist`, `gems.Accuracy` và các nguồn cũ; Kháng Liên Kích/Kháng Phản Đòn hiện chỉ lấy từ trang bị trực tiếp.
- `StatSources` và bảng tổng ngọc chưa có hai loại Kháng mới. Arena đã lấy tổng rõ ràng từ `Stats`, vì vậy không cộng lại ngọc vào snapshot.
- Các test cộng 30 ngọc giống nhau hiện là stress test của bộ tổng hợp. Với quy tắc nhóm khảm đang giữ nguyên, một dòng công kích/phòng thủ chỉ có tối đa 15 lỗ hợp lệ trên bộ 10 món đủ 3 lỗ; không dùng bộ 30 ngọc sai nhóm làm chuẩn cân bằng.
- Boss hiện dùng nền `100 + 50 × độ khó`, chuyên biệt +50; ba loại Kháng cộng riêng +50. Quái thường/tinh anh trong source là 40%/70% nền boss. Mô phỏng phải đọc source hiện hành, không ghi cứng lại những mốc này.

## Phương án đề xuất

Chọn **thêm chỉ số chuyên biệt cho các dòng hiện có**, thay công thức của thuộc tính trùng và giữ hiệu ứng hỗ trợ. Cách này bảo toàn danh mục và ngọc cũ. Không chọn đổi toàn bộ ngọc thành chỉ số thuần vì sẽ làm mất build hỗ trợ; không chọn tạo thêm tám dòng vì tăng gánh nặng loot, hợp thành và hình ảnh.

Phân bổ một chỉ số chính cho tám dòng:

- **Hỏa Tinh:** Bạo Kích; giữ công cộng thẳng, công nền và sát thương bạo kích.
- **Lôi Tinh:** Liên Kích; thay dòng Liên Kích cũ bằng ngân sách mới, giữ Bạo Kích phụ hiện có.
- **Băng Tinh:** Kháng Bạo Kích; thay công thức kháng cũ, giữ phòng thủ, Block và giảm sát thương tinh anh.
- **Thổ Tinh:** Kháng Liên Kích; giữ máu, hấp thụ, giảm sát thương boss và Kháng Bạo phụ hiện có.
- **Phong Tinh:** Né; giữ tốc độ chạy và Liên Kích phụ hiện có, không biến tốc độ chạy thành Né.
- **Quang Tinh:** Chính Xác; thay công thức Chính Xác cũ, giữ phần công.
- **Ám Tinh:** Phản Đòn; giữ hút máu và hiệu ứng đòn thêm cấp 10; không nhân sát thương phản công theo trị số ngọc.
- **Vũ Trụ Tinh:** Kháng Phản Đòn; giữ Né phụ và hấp thụ hiện có.
- **Tinh Thạch dòng Linh:** giữ vai trò máu/nộ, không ép thêm chỉ số đối kháng.
- **Phục Nguyên Tinh:** giữ vai trò máu/hồi phục, không ép thêm chỉ số đối kháng.

Giữ nhóm khảm hiện có của từng dòng, kể cả Phong/Ám thuộc nhóm công kích. Không đổi nhóm một ngọc đang khảm để làm mất hiệu lực ngọc cũ.

### Ngân sách chỉ số chính, đã duyệt khi triển khai Task 1

Đề xuất cùng ngân sách cho cả tám chỉ số, theo cấp 1→10:

```text
Primary = [2, 4, 6, 8, 10, 13, 16, 20, 25, 30] điểm phần trăm mỗi viên
```

- Một viên cấp 1: +2%; cấp 5: +10%; cấp 10: +30% chỉ số chính.
- Ba viên cấp 10 trên một món: +90% chỉ số chính.
- 15 viên cấp 10 đúng nhóm trên cả bộ: +450% chỉ số chính, trước cộng trang bị và hiệu ứng phụ. Đây là cộng thẳng, không phải nhân phần trăm vào chỉ số đang có.
- Ngọc hai dòng hỗ trợ không nhận ngân sách này. Hiệu ứng phụ giữ công thức cũ và cộng riêng, nên tổng một chỉ số có thể cao hơn riêng phần ngân sách chính.
- Không dùng `EquipmentEffects.QualityScale` hoặc hệ số cảnh giới để nhân thêm ngân sách ngọc. Cấp ngọc đã xác định trị số; phần thưởng công/thủ/máu nền vẫn dùng cơ chế hiện có.
- Đây là cấu hình khởi điểm để mô phỏng, không phải cam kết thắng boss. Nếu cân bằng cần đổi, sửa tập trung cấu hình và cập nhật kỳ vọng test theo quyết định đã ghi.

## Mô hình và ánh xạ dữ liệu

Đề xuất thêm `Game/GemRatingBudget.cs`:

- `GemRatingBudget.For(string theme, int level) -> CombatRatings`, level 1–10; trả tám giá trị 0 trừ chỉ số chuyên biệt của dòng. Hai dòng hỗ trợ trả 0 cho cả tám.
- Từ chối theme không hỗ trợ/cấp ngoài 1–10; `Create` hiện dùng tier 0–9 phải truyền `tier + 1`.
- Ngọc legacy được `RebalanceLegacy` đưa vào cùng đường sinh, không thêm migration đổi ID hay ghi đè save.

Biểu diễn theo quy ước đã có để giảm thay đổi:

- Bạo Kích: `ItemDef.CritPct`.
- Kháng Bạo Kích: `gem_crit_resist`.
- Chính Xác: `gem_accuracy`; Né: `gem_dodge`.
- Liên Kích: `lien_kich`; Phản Đòn: `dia_mach`.
- Hai effect ID mới: `gem_combo_resist`, `gem_reflect_resist`.
- `GemBonuses.ExtraHit` tiếp tục là tổng Liên Kích để tương thích; không thêm một trường Combo thứ hai chứa cùng giá trị.
- Thêm `ComboResist = 0`, `ReflectResist = 0` ở cuối constructor `GemBonuses` để tương thích lời gọi và JSON cũ.
- Mỗi viên chỉ có một đóng góp chính cho cùng chỉ số. Quang/Băng/Lôi phải thay giá trị effect cũ, không giữ effect cũ rồi thêm một effect chính trùng ID.
- Không dùng số 0 của ngân sách chính để xóa thuộc tính phụ. Ví dụ Lôi cấp 10 vẫn giữ `CritPct = 7.75` và có Liên Kích chính 30%; Thổ vẫn giữ Kháng Bạo phụ 5%, Vũ Trụ vẫn giữ Né phụ 5%.

## Review Focus

- Ngọc legacy và ngọc chuẩn cùng cấp/cùng dòng phải cho cùng đóng góp, giữ nguyên số lượng và vị trí khảm.
- Cấp 1/10 và chuyển tier 0–9 sang level 1–10 không lệch một cấp.
- Tổng vượt 100%/450% không bị trần ngầm ở ngọc, `Stats`, nguồn chỉ số hoặc Arena; không cộng hai lần.
- Thay/tháo đồ, hạ phẩm và lỗ khảm bị khóa không tiếp tục cho bonus từ ngọc không có hiệu lực; ngọc vẫn được bảo toàn theo quy tắc hiện có.
- Phản Đòn từ ngọc không tạo vòng lặp, không thêm nộ/hút máu hoặc sinh thêm đòn qua Ám Tinh cấp 10.

## Task 1: Cấu hình ngân sách và sinh thuộc tính ngọc

**Files:** thêm `Game/GemRatingBudget.cs`, `test/GemSystemTests/GemCombatRatingBudgetTests.cs`; sửa `Game/GemProgression.cs`.

**Interfaces:** `For(theme, level)` trả `CombatRatings` theo danh sách cấp và ánh xạ dòng ở trên; `CreateAll`/`RebalanceLegacy` giữ giao diện cũ.

- [x] Người dùng duyệt phân bổ và ngân sách khi yêu cầu thực hiện Task 1; giữ nguyên đề xuất.
- [x] Viết test literal: Hỏa cấp 1 Crit=2; Quang cấp 5 Accuracy=10; Thổ cấp 10 ComboResist=30; Vũ Trụ cấp 10 ReflectResist=30; Linh/Phục Nguyên trả tám số 0; level 0/11 và theme lạ/null bị từ chối.
- [x] Quan sát test fail do thiếu `GemRatingBudget`; triển khai helper và `GemProgression.Create` dùng level=`tier + 1`.
- [x] Kiểm tra dãy ngân sách cả 10 cấp/tám dòng và không có effect ID chính trùng lặp.
- [x] Kiểm tra các chỉ số phụ cụ thể Lôi/Thổ/Vũ Trụ, tốc độ chạy và Liên Kích phụ của Phong, Block, hút máu, nộ, hồi phục và đòn thêm Ám cấp 10.
- [x] Kiểm tra legacy qua đủ 15 loại icon/cả 10 phẩm, giữ ID/icon/tên gốc/cấp; kiểm tra 24 ID legacy + 100 ID chuẩn đều tra cứu được trong danh mục ngọc và vật phẩm.

**Kết quả Task 1:** thêm `Game/GemRatingBudget.cs`, cập nhật `Game/GemProgression.cs`, thêm `GemCombatRatingBudgetTests.cs`. **223/223 test C# đạt**, log `output/gem-budget-suite.log`; build trong test thành công với cảnh báo hiện có. Kỳ vọng stress test Quang cấp 10 đổi từ 465 sang 900% cho 30 viên theo ngân sách mới, không dùng cấu hình 30 viên này làm chuẩn build hợp lệ.

**Ranh giới bước tiếp theo:** hai effect `gem_combo_resist` và `gem_reflect_resist` đã được sinh, nhưng chưa thêm vào `GemBonuses`/`Stats`; Task 2 sẽ nối tổng và bỏ trần Liên Kích 30% còn trong `GetSocketedBonuses`. Không đánh dấu đủ tám bonus ngọc đã hoạt động trong chiến đấu ở giai đoạn này. Giao diện/wiki và mô phỏng vẫn theo các task sau.

## Task 2: Tổng bonus ngọc và tổng nhân vật

**Files:** sửa `Game/GemData.cs`, `Game/GameService.cs`; thêm `test/GemSystemTests/GemCombatRatingAggregationTests.cs`; cập nhật `GemTenLevelTests.cs`, `GemRealmBalanceTests.cs`.

**Interfaces:** `GetSocketedBonuses(Player) -> GemBonuses` thêm hai loại Kháng; `GameService.Stats(Player)` trả tổng có ngọc, source row "Khảm Tinh Thạch" phải khớp.

- [x] Viết test tổng hợp bằng ngọc/ô hợp lệ: ba Quang cấp 10 = 90% Chính Xác; 15 Thổ cấp 10 = 450% Kháng Liên Kích; 15 Vũ Trụ cấp 10 = 450% Kháng Phản Đòn; 15 Lôi cấp 10 = 450% Liên Kích, chưa tính các nguồn khác.
- [x] Quan sát test fail; thêm đọc `gem_combo_resist`/`gem_reflect_resist`, bỏ `Math.Min(30, extraHit)` và cộng hai trường mới vào `Stats`.
- [x] Đối chiếu tổng trước/sau khảm với đóng góp dự kiến; kiểm tra tính lặp, tháo ngọc, tháo đồ, hạ phẩm, lỗ khóa và serialize/tải lại không cộng chồng.
- [x] Giữ các test cho Block=50, hấp thụ=40, tốc độ chạy=60, nộ +7, giảm sát thương tinh anh/boss=30 và đòn thêm chắc chắn=1. Không biến yêu cầu bỏ trần tám chỉ số thành bỏ trần toàn hệ ngọc.
- [x] Cập nhật stress test 30 ngọc theo công thức mới, đồng thời tách rõ test build hợp lệ tối đa 15 ngọc cùng nhóm. Không bỏ các kiểm tra chống đếm lỗ bị khóa.

## Task 3: Tích hợp chiến đấu và Arena

**Files:** rà soát/sửa khi cần `Game/GameService.Arena.cs`, `Game/PvpCombat.cs`, `Game/Arena.cs`, `Game/ArenaMock.cs`, `Game/Leaderboard.cs`; thêm `test/GemSystemTests/GemCombatRatingIntegrationTests.cs`.

**Interfaces:** tiêu thụ tổng `CombatStats`; không cộng lại `GemBonuses` lên `PvpFighter.ComboRating`/`Accuracy` đã là tổng.

- [x] Viết test nhân vật khảm Thổ/Vũ Trụ: tổng hai kháng mới trong snapshot bằng `Stats`, qua sanitize/JSON không mất hoặc cắt giá trị.
- [x] Test dữ liệu Arena cũ thiếu hai trường mới vẫn đọc được, mặc định 0; snapshot mới không cộng lại ngọc và thần thông.
- [x] Test phản công Ám Tinh bị Kháng Phản Đòn ngăn đúng; không hút máu/nộ/đòn thêm hoặc phản công tiếp từ phản công. Giữ test Ám cấp 10 có đòn thêm riêng.
- [x] Chạy lại các test chung cho 350–280=70 và hiệu số trên 100 chỉ kích hoạt một lần; không viết lại công thức đối kháng cho riêng ngọc.

## Task 4: Giao diện, mô tả và wiki

**Files:** sửa `wwwroot/js/inventory.js`, `wwwroot/js/vault.js`, `wwwroot/js/i18n-en.js`, `site/js/wiki.js`; cập nhật `tools/wiki_export/Program.cs` khi cần, xuất lại `site/data/wiki.json`; mở rộng test client/wiki hiện có.

- [x] Test bảng tổng ngọc hiển thị thêm Kháng Liên Kích/Kháng Phản Đòn và đúng nghĩa cơ hội Phản Đòn; bỏ mô tả Liên Kích trần 30% hoặc phản lại X% sát thương.
- [x] Thẻ viên ngọc, tooltip trang bị đã khảm, nguồn chỉ số, tổng nhân vật và wiki phải cùng trị số, không trùng một bonus ở cả phần cơ bản và hiệu ứng.
- [x] Kiểm tra 0%, 90%, 450%, 1.000,25%; số lớn không bị rút thành một giá trị có vẻ bị giới hạn hoặc bị cắt ở khung 360px.
- [x] Wiki giải thích chỉ số chính/phụ, mười cấp, nhóm khảm và tối đa 15 lỗ hợp lệ cho một dòng ở bộ đủ 30 lỗ. Nội dung ngân sách lấy từ dữ liệu xuất, không duy trì một bảng số riêng bằng tay.
- [x] Chạy `node --test test/*.test.cjs`, `node tools/wiki_export/check.cjs`, `node tools/wiki_export/gem-check.cjs`, `node tools/wiki_export/inventory-gem-check.cjs`; yêu cầu pass.

## Task 5: Mô phỏng, kiểm chứng và hoàn tất

**Files:** mở rộng `tools/combat_rating_checks/Program.cs` và README; cập nhật tài liệu tiến độ này.

- [ ] Giữ bộ không ngọc làm đối chứng; thêm bộ ngọc hỗn hợp và hai bộ thiên công/thủ, tất cả tuân thủ nhóm khảm và 1/2/3 lỗ thực tế. Cùng trang bị, kỹ năng và seed giữa các bộ để so sánh được.
- [ ] Mỗi bộ chạy bốn độ khó, chương 1/10, ải tinh anh và boss, 32 seed mỗi trường hợp. Ghi tổng tám chỉ số, tỉ lệ thắng, số lượt và số lượt Liên Kích/phản công gây sát thương; phân biệt với lần kích hoạt bị né/khiên chặn.
- [ ] Xem xét chênh lệch chuyên công/chuyên thủ và đánh đổi tốc độ hạ quái/sống sót. Không đặt tiêu chí bắt buộc một bộ thắng 100% hoặc dùng admin bất tử để chứng minh cân bằng.
- [ ] Chạy `dotnet test test/GemSystemTests/GemSystemTests.csproj --no-restore -p:BaseOutputPath=D:/projects/van-dao/VanDao/build-check/combat-ratings/` và build cùng đường output riêng; yêu cầu pass. Game đang chạy có thể khóa thư mục bin mặc định.
- [ ] Kiểm tra trình duyệt bằng DB QA riêng: khảm/tháo, đổi đồ, tải lại; giá trị trong tooltip, nguồn và tổng phải khớp. Không sửa save người dùng hoặc bảng xếp hạng thật.
- [ ] Rà soát độc lập sau khi hoàn tất; sửa lỗi có test tái hiện và cập nhật kết quả/mức ngân sách cuối trong plan. Không commit/push/deploy khi chưa có yêu cầu tương ứng.

## Tiêu chí hoàn tất

- Ngọc hỗ trợ đủ tám chỉ số qua tám dòng chuyên biệt và hai dòng hỗ trợ được giữ vai trò riêng.
- Ngân sách tập trung, ID/save cũ tương thích, không mất ngọc và không cộng trùng.
- Không còn trần ngầm đối với tám chỉ số ở nguồn Tinh Thạch; các hiệu ứng khác giữ giới hạn hiện hành.
- Tổng ngọc → nhân vật → PvE/PvP/Arena → giao diện/wiki khớp nhau.
- Có kết quả test và mô phỏng của các build hợp lệ; cấu hình khởi điểm chỉ trở thành thông số triển khai sau khi được duyệt.
