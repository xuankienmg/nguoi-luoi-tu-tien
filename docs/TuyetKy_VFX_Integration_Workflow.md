# Quy trình áp dụng tuyệt kỹ VFX vào game Vấn Đạo

Ghi nhận ngày 2026-11-05, từ lần tích hợp **Thiên Sơn Băng Kiếm v4** và sửa lỗi quái hành động sớm/FPS tụt. Dùng cùng [hướng dẫn tạo VFX](TuyetKy_VFX_Generation_Guide.md). Yêu cầu mới của người dùng được ưu tiên.

## 1. Phạm vi và nguyên tắc

- Chỉ tích hợp khi người dùng yêu cầu; tạo demo và lưu mẫu trước.
- Gắn vào **đúng skill ID**, không thay đồng loạt các chiêu cùng hệ. Giữ ID, chỉ số sát thương và logic khống chế trừ khi có yêu cầu khác.
- Nhân vật chạy asset riêng. Sheet VFX không chứa nhân vật, nền cảnh hoặc bia thử.
- Giữ thông số riêng theo chiêu. Mặc định mới là 3 giây / 90 khung / 30 FPS; không ép các chiêu cũ theo mặc định này.
- Skill cấp thấp không có trận pháp. Nếu chiêu đã học vẫn dùng được ở cảnh giới cao hơn, VFX và timeline phải tiếp tục đi cùng skill đó.
- Bản dựng gốc, bản runtime và các lớp hiệu ứng riêng đều có phiên bản rõ ràng. Không ghi đè mẫu đã duyệt để thử biến thể.

## 2. Chọn chiêu và lập bảng thông số

Đọc khai báo trong `Game/ElementSkillData.cs`, bộ dựng tại `wwwroot/js/fire-fx.js`, xem trước tại `wwwroot/js/skill-preview.js`, chiến đấu tại `wwwroot/js/battle.js` và `battle-fx.js`.

Ghi rõ: skill ID, cảnh giới học, các cảnh giới được dùng, FPS, số khung, duration, thời điểm va chạm, điểm neo, kích thước ô, lưới và khung giảm chuyển động. Giữ mô tả skill khớp với hình ảnh mới.

Mẫu đã áp dụng:

- Skill ID: `hu_khong_bang_nguc` — Hư Không Băng Ngục, học từ Luyện Hư (`realmIndex >= 5`).
- VFX nguồn: `output/ice-mountain-sword-demo-v4/`.
- Thời lượng 3000 ms, 90 khung, 30 FPS; va chạm 1600 ms, khung 48.
- Cự kiếm thon dài hình thành từ pháp trận trên trời, chỉ thấy mũi và phần lưỡi dưới. Khi chạm đất, tinh thể Huyền Băng gốc trồi lên và mục tiêu bị phủ băng.
- Không thêm cơ chế đóng băng gameplay chỉ vì có lớp đóng băng thị giác.

Bộ đã áp dụng tiếp theo: **VFX hệ Mộc v5** (2026-11-17, thay v3), gồm cả 10 chiêu Mộc chủ động.

- Nguồn: `output/moc-skill-set-demo-v5/`, ghi nhận duyệt trong `APPROVED.md`. Dựng bằng code theo ảnh minh họa `output/moc-skill-set-demo-v4/art/` (v4 chỉ là ảnh tham khảo, không đưa vào game).
- Đóng gói: `py tools/moc_fx/pack_v5.py`, xuất ra `wwwroot/img/fx/moc/v5/<skillId>/`. Đổi phiên bản bằng `GROUND_TARGET_VERSION.moc`; khi dựng lại phải cập nhật biên ảnh trong `MOC_SET_FX` theo `demo.json`.
- Khai báo: `MOC_SET_FX` trong `fire-fx.js`. Hệ Mộc đã bị bỏ khỏi `ELEMENTAL_FIRE_FX`, nên Hợp Thể → Chân Tiên dùng sheet Mộc riêng.
- Nhóm chiêu neo đất (`groundTargetFxSpec`) fit theo `coreRight`: chỉ phần lõi phải nằm trong khung. Nếu fit theo toàn bộ `visibleBounds`, chiêu cao cấp có lá và rễ sát mép ô bị thu nhỏ hơn chiêu cấp thấp. Đo ở khung 400 × 320, Thái Sơ chỉ còn 295 px so với 524 px của Đằng Mạn.

## 3. Đóng gói asset nhẹ trước khi đưa vào game

1. Xuất PNG RGBA, JSON và giấy phép vào `wwwroot/img/fx/<he>/<ten-chieu>-vN/`.
2. Giữ sheet dựng gốc; tạo **sheet runtime riêng** theo kích thước hiển thị thực tế.
3. Tính bộ nhớ giải mã bằng `width × height × 4`, không đánh giá tải chỉ bằng dung lượng PNG trên đĩa.
4. Khi thu nhỏ, đổi đồng bộ `frameWidth`, `frameHeight`, anchor và `visibleBounds`. Giữ FPS, số khung, duration và mốc sự kiện.
5. Các lớp phụ còn ở độ phân giải gốc phải có kích thước ô riêng trong metadata; không ngầm dùng kích thước ô của sheet runtime.
6. Đổi tên/version file runtime khi thay ảnh để tránh dùng lại cache cũ.

Băng kiếm hiện tại:

- Bản gốc `sheet.png`: 4800 × 2970, 10 × 9 ô 480 × 330; khoảng 57 MB RGBA.
- Bản chạy `sheet-runtime-v1.png`: 2400 × 1485, 10 × 9 ô 240 × 165; khoảng 14 MB RGBA, giảm **75% số pixel**.
- Neo runtime `[170,135]`; `visibleBounds: [108,27.5,232.5,161.5]`.
- Giữ nguyên 90 khung/30 FPS/3 giây. **Không giảm FPS để che vấn đề tải ảnh**.
- Đóng gói lại bằng Python có Pillow: `python tools/ice_fx/pack_runtime.py`.

Các lớp `formation-sheet.png`, `ground-ice-sheet.png`, `freeze-status-sheet.png` vẫn riêng để chỉnh sửa/tái sử dụng. Combat hiện tải **một sheet runtime tổng hợp**, không tải tất cả lớp gốc để vẽ chồng mỗi khung.

## 4. Khai báo và vẽ đúng hình học

Trong `fireFxSpec`, khai báo nguồn, kích thước ô/lưới, số khung, FPS, duration, anchor, bounds và mốc impact. Khi dùng lại skill ở cảnh giới cao hơn, mở phạm vi cho chính ID đó; tránh chỉ thay atlas ở một realm nhưng kéo dài timing ở mọi realm.

- Giữ tỉ lệ khung chữ nhật; không vẽ ô 240 × 165 thành hình vuông.
- Đặt hiệu ứng theo **điểm neo mục tiêu/mặt đất**, không theo bounding box từng khung hoặc bàn tay nhân vật.
- Fit theo phần hình ảnh nhìn thấy, tính cả không gian cho mũi kiếm, pháp trận và băng trồi. Kiểm tra ở cả preview nhỏ và battle.
- Khi ảnh chưa tải/lỗi, giữ bộ dựng dự phòng. Khi ảnh mới đã sẵn sàng, chặn hiệu ứng cũ tương ứng để không vẽ trùng.
- Kiểm tra cả đường dựng nhân vật có atlas và đường fallback không có atlas; không để VFX chỉ chạy trên một loại skin.
- Giảm chuyển động dùng một khung tĩnh phù hợp. Băng kiếm đang dùng `reducedFrame: 60`; không đổi khung theo phase gây nhấp nháy.

## 5. Đồng bộ animation, sát thương và quái phản kích

`battleSkillTiming` là lịch thời gian dùng chung cho preview, battle và arena. `fireFxPlan` chọn khung theo lịch đó.

Với băng kiếm:

- 0–1,6 giây ánh xạ vào progress `s = 0 … .45`.
- 1,6–3 giây ánh xạ vào `s = .45 … .75`.
- Damage vẫn ở `s = .45`; quái không được tụ lực/phản kích trong lúc VFX còn chạy.
- Combo/pet/phản kích tiếp tục theo lịch chung **sau phần thi triển**; giữ riêng trường hợp trượt chiêu, né, lượt bị khống và PvP idle.
- `battleMonsterActionReady` chặn cả hành động tấn công **lẫn telegraph/tụ lực** của quái. Chỉ sửa mốc damage hoặc `monAttack` là chưa đủ.
- `Game/BattleTiming.cs` phải khớp độ dài lượt đã khai báo, kể cả chiêu trượt. Không tự thay tốc độ các chiêu khác.

## 6. Không cắt animation khi server gửi trạng thái mới

Trạng thái thám hiểm có thể cập nhật trước khi một animation dài chạy xong, nhất là khi dùng tăng tốc. **Không nén toàn bộ animation vào cooldown ngắn của server**.

Giải pháp phía hiển thị hiện tại:

1. `battleVisualFrame` giữ **một snapshot trận đang chiếu** nếu trận có tuyệt kỹ dài.
2. `battlePhase(playback)` dùng đồng hồ hiển thị riêng; đoạn combat có tuyệt kỹ không bị nhân hệ số rút ngắn.
3. Server vẫn cập nhật `S`, tài nguyên, thưởng và tiến độ bình thường. Không giữ/chặn cập nhật state toàn game.
4. Snapshot giữ cả battle và ngữ cảnh dựng: map, độ khó, stats/realm/skills cần thiết. Nếu chỉ giữ quái mà đọc map mới, tên ải, phân loại boss và kích thước quái có thể đổi giữa chiêu.
5. `stage.js` truyền rõ battle và render state của snapshot cho `drawPixelBattle`.
6. Chiếu xong thì bắt kịp **trận mới nhất**, không xếp hàng vô hạn các chuyến server đã hoàn tất. Đổi nhân vật thì bỏ snapshot cũ.

**Không tăng thời gian tính thưởng hoặc vô hiệu hóa FastMult/Tật Hành Đan để chữa lỗi hiển thị.** `ExploreSeconds` còn dùng tính tài nguyên offline; việc thêm sàn thời gian tại đó có thể làm giảm tốc độ kiếm tài nguyên. Lỗi này phải xử lý bằng lịch/snapshot hiển thị.

## 7. Kiểm thử bắt buộc trước khi bàn giao

- Toàn bộ 90 khung được đi qua, mốc damage đúng 1,6 giây, frame cuối hoàn tất trước phản kích.
- Miss vẫn phát đủ chiêu; combo/pet/counter không chen vào; PvP idle không phát chiêu.
- Các skill khác và skill đã học ở realm cao hơn có timeline/atlas đúng.
- Thay state server giữa lúc kiếm đang rơi: animation cũ vẫn chạy hết, quái chưa hành động, thưởng live vẫn cập nhật, map cũ không bị trộn với map mới.
- Bản runtime đúng kích thước và giới hạn bộ nhớ; lớp phụ không bị tải/vẽ thừa trong combat.
- Tỉ lệ ô, điểm neo và reduced-motion đúng; loading/error có fallback, không vẽ trùng.
- Dùng browser chạy renderer thật ở kích thước preview và battle. Ảnh demo xuất ngoài game không thay được bước kiểm tra này.

Các lệnh đã dùng, chạy từ thư mục gốc dự án:

```powershell
node --test test/ice-sword-sequence.test.cjs test/xich-diem-sequence.test.cjs test/hoa-than-fx.test.cjs test/monster-rage-client.test.cjs
dotnet test test/GemSystemTests --filter FullyQualifiedName~XichDiemTimingTests --no-restore --verbosity quiet
node output/ice-integration-checks/browser-check.cjs
git diff --check
```

Browser check/script và ảnh kiểm tra hiện nằm tại `output/ice-integration-checks/`; thư mục output là artifact cục bộ, có thể cần khôi phục hoặc tạo lại ở checkout khác. Các regression test và script đóng gói được lưu trong `test/` và `tools/`.

Nếu browser/test host không mở được pipe trong sandbox, ghi nhận đó là lỗi môi trường; dùng cơ chế quyền phù hợp để chạy lại. Không sửa timing cho test “qua”, không tuyên bố pass khi host chưa chạy test. Phân biệt lỗi VFX mới với lỗi sẵn có ngoài phạm vi.

## 8. Bàn giao

Ghi skill nào đã được gắn, vị trí asset/metadata, duration/impact, kiểm thử đã chạy và hạn chế còn lại. Cập nhật hướng dẫn/mẫu đã duyệt; giữ phiên bản gốc để so sánh. Sau thay đổi script/asset, mở lại hoặc tải lại game để kiểm tra bản mới thực tế.

Bộ đã áp dụng tiếp theo: **VFX hệ Thổ v6** (2026-11-17), gồm cả 10 chiêu Thổ chủ động.

- Nguồn: `output/tho-skill-set-demo-v6/` (dựng bằng `build.py`; Độ Kiếp và Chân Tiên đổi màu từ model Hỏa bằng `fire_to_stone.py`, nguồn Hỏa ở `source/`).
- Đóng gói: `py tools/tho_fx/pack_v6.py`, xuất ra `wwwroot/img/fx/tho/v6/<skillId>/`. Khai báo: `THO_SET_FX` trong `fire-fx.js` và `GROUND_TARGET_VERSION.tho = 'v6'`; Thổ đã bị bỏ khỏi `ELEMENTAL_FIRE_FX`.
- Hình ảnh các chiêu hạ một bậc so với v1; ID, tên và mốc chạm vẫn theo bậc cảnh giới.
- **Ô riêng.** Độ Kiếp (192 × 173) và Chân Tiên (360 × 167) là model Hỏa thu 0,5 nên khác ô chuẩn 240 × 160. Bảng `THO_SET_FX` có phần tử thứ 3 `[rộng, cao, neo X, neo Y, unitScale]`; `groundTargetFxSpec` dùng nó và `drawFireFx` nhân `unitScale` vào cỡ hiển thị (ô chuẩn = 1). Khi dựng lại phải cập nhật bảng theo `sheet.json` do `pack_v6.py` ghi.
- Test: `node --test test/fire-skill-set.test.cjs` kiểm tra mốc chạm, neo và khung nhìn theo kích thước ô của từng chiêu.

Bộ đã áp dụng tiếp theo: **VFX hệ Thủy v5** (2026-11-20), gồm cả 11 chiêu Luyện Khí → Kim Tiên.

- Nguồn: `output/thuy-skill-set-demo-v5/`. Đóng gói: `py tools/thuy_fx/pack_v5.py`, xuất ra `wwwroot/img/fx/water/v2/<skillId>/`. Khai báo: `WATER_SET_FX` trong `fire-fx.js` (cùng khuôn `THO_SET_FX`, gộp vào `GROUND_TARGET_FX`, `GROUND_TARGET_VERSION.thuy = 'v2'`, thư mục `GROUND_TARGET_DIR.thuy = 'water'`). Thủy đã bị bỏ khỏi `STAGE_FX` và `KIM_TIEN_LAYERS`.
- **Ô riêng:** Độ Kiếp (192 × 173), Chân Tiên (360 × 167), Kim Tiên (372 × 200) có phần tử thứ 3 `[rộng, cao, neo X, neo Y, unitScale]`. Kim Tiên thủy (Thiên Hà Giáng Thế) thay Thần Binh Trảm hệ Thủy.
- Đã bỏ `waterVortexPull` (hút quái của Thâm Hải Tuyền Qua cũ) vì Huyền Hải Thủy Mẫu là hiệu ứng khác. Asset `water/v1` giữ để đối chiếu, không còn được nạp.
- Test: `node --test test/fire-skill-set.test.cjs` kiểm tra cả 11 chiêu; `test/water-vfx-sequence.test.cjs` (chuẩn v1) đã bỏ, `test/kim-tien-fx.test.cjs` không còn hệ Thủy.
