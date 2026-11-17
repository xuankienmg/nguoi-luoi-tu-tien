# Hướng dẫn tạo VFX tuyệt kỹ — Vấn Đạo

Chốt từ trao đổi với người dùng ngày 2026-11-05. Áp dụng khi tạo, sửa hoặc xem trước hiệu ứng tuyệt kỹ trong game. Yêu cầu cụ thể mới của người dùng được ưu tiên hơn các mặc định dưới đây.

## 1. Định hướng đã chốt

- Phong cách **tu tiên**, hiệu ứng thể hiện linh lực và uy lực của pháp thuật, có bản sắc riêng theo hệ.
- Mặc định tuyệt kỹ dài **3 giây, 30 FPS, 90 khung**. Đây là mặc định rút ra từ demo thiên thạch 3 giây và mẫu sét đã duyệt; có thể đổi khi người dùng yêu cầu.
- Tạo **demo động trước**, chỉ tích hợp vào game khi người dùng yêu cầu rõ.
- Ưu tiên dùng hoặc điều chỉnh nguồn VFX có sẵn. Tham khảo thư viện Sprite Fusion và các bộ pixel art VFX trên itch.io để cải thiện cách thể hiện lửa, sét, va chạm và dư chấn.
- Chuyển động có nhịp rõ: chuẩn bị/tụ lực → thi triển → va chạm → hiệu ứng tan dần. Không bắt buộc mọi chiêu dùng cùng thời điểm va chạm hoặc cùng số đợt đánh.
- Hiệu ứng phải đọc được ở kích thước hiển thị thực tế; giữ rõ đường đi, điểm đánh và hình dạng đặc trưng.
- Tạo phiên bản mới riêng để so sánh. Không ghi đè mẫu đã duyệt khi thử biến thể.
- Khi chia tuyệt kỹ thành nhiều cấp, **mỗi cấp phải có hình thái/model và cách thi triển khác nhau**; không chỉ phóng to, đổi màu hoặc thêm bản sao của model cấp trước. Đây là yêu cầu bổ sung của người dùng khi làm ba cấp hệ băng.
- Yêu cầu mới nhất: **bỏ tương tác từ nhân vật; nhân vật chạy asset riêng**. VFX độc lập với actor: không rig/biến dạng nhân vật, không neo vào tay/chân, không pháp trận dưới chân nhân vật hoặc dòng hạt từ nhân vật tới chiêu. Nếu có nhân vật trong preview, đó chỉ là ảnh tham chiếu riêng, không nằm trong asset VFX. Yêu cầu này thay thế yêu cầu tương tác từ nhân vật ở các lần thử trước.
- **Dùng trận pháp tại vị trí thi triển của chiêu**, ưu tiên vòng ấn, phù văn và chuyển động mềm. **Bỏ đường nối thẳng/gạch thẳng giữa các điểm thi triển**; không thêm đường nối liên tục thay thế. Hạt chuyển động cục bộ trong VFX được phép, nhưng không nối từ nhân vật. Điểm neo và vị trí chiêu khai báo riêng để bộ dựng game đặt VFX, còn hoạt ảnh nhân vật chạy độc lập.
- **Skill cấp thấp không có trận pháp đi kèm**: không vòng ấn, phù văn hoặc cổng triệu hồi; chỉ giữ hình thái chiêu, chuyển động và hiệu ứng va chạm phù hợp. Quy tắc này giới hạn yêu cầu dùng trận pháp ở trên: trận pháp dành cho các cấp cao hơn. Với bộ băng ba cấp hiện tại, cấp 1 không có trận pháp, cấp 2–3 giữ trận pháp. Sóng va chạm mờ dần không phải trận pháp và vẫn có thể dùng.
- Với tuyệt kỹ băng, thêm **hiệu ứng đóng băng sau khi trúng chiêu**: lớp băng kết tinh trên mục tiêu, giữ một nhịp rồi nứt/vỡ và tan. Mức phủ băng tăng theo cấp. Giữ lớp trạng thái đóng băng riêng để bộ dựng có thể gắn vào mục tiêu độc lập với VFX tấn công; đây là yêu cầu hình ảnh, không tự thêm thời gian khống chế hoặc thay logic chiến đấu.
- Bổ sung **hiệu ứng tụ lực trước khi phát chiêu**: bụi tuyết/hàn khí hội tụ, lõi sáng tăng cường độ rồi giải phóng theo nhịp thi triển. Mỗi cấp tụ lực theo hình thái riêng; cấp thấp vẫn không có trận pháp, không thêm hiệu ứng ở nhân vật. Giữ lớp tụ lực riêng và thời điểm release trong metadata; demo ba cấp vẫn tổng 3 giây.
- Về chất liệu/mô hình băng: người dùng yêu cầu **dùng lại băng của Huyền Băng Thiên Ngục bản đầu** tại `output/ice-cultivation-demo-v1/`, nguồn `ice.ts` (Sprite Fusion Ice). Giữ mặt tinh thể, cạnh sáng, các mặt xanh đậm/nhạt và palette gốc; không thay bằng khối đa giác đơn giản thiếu chất liệu. Có thể dùng lại mô hình/chất liệu gốc trong các hình thái thi triển khác nhau theo yêu cầu mới này; không bắt buộc mỗi cấp phải có một chất liệu băng mới.

Các quy tắc 64 khung, 16 FPS, vòng lặp 4 giây của **skin idle/phi kiếm** trong `AGENTS.md` không phải thông số của VFX tuyệt kỹ. Tuyệt kỹ là chuyển động một lần; preview có thể lặp lại để xem.


Quy ước này áp dụng khi tạo hoặc cập nhật VFX. Việc thay hiệu ứng của các chiêu đang chạy trong game vẫn cần yêu cầu tích hợp riêng.

### Vụ nổ theo bậc cảnh giới (quy tắc mới, ưu tiên hơn Đại Bạo Nổ)

Người dùng chốt ngày 2026-11-17 theo trang `output/vfx-no-cac-he-demo-v2/vfx-no-cac-he.html` (bản gốc ở Codex `t/outputs/`). Mỗi chiêu phát vụ nổ **theo bậc cảnh giới**, dùng clip **cùng hệ** của chiêu: `output/vfx-no-cac-he-demo-v2/vfx-no-cac-he/<hệ>-<clip>.webp`. Các hệ có sẵn: `hoa`, `thuy`, `moc`, `tho`, `loi`, `bang`, `phong`. Với hệ Hỏa, ba clip đầu lấy ở `vfx-no-he-hoa/<clip>.webp`. Quy tắc này thay Đại Bạo Nổ ở mục trên cho pha nổ của mọi chiêu mới.

| Bậc | Cảnh giới | Clip | Kích thước nguồn | Điểm chạm trong ảnh nguồn | Đặt tại |
|---|---|---|---|---|---|
| 1–2 | Luyện Khí, Trúc Cơ | `xich-diem-tram-v1` | 347 × 94, 866 ms | `[300,46]` | Giữa thân mục tiêu (vệt chém, không chạm đất) |
| 3–5 | Kim Đan, Nguyên Anh, Hóa Thần | `thien-hoa-giang-thach-v10` | 355 × 197, 1266 ms | `[192,172]` | Mặt đất dưới mục tiêu |
| 6–7 | Luyện Hư, Hợp Thể | `viem-kiem-giang-thien-v4` | 349 × 254, 1266 ms | `[228,232]` | Mặt đất |
| 8–9 | Đại Thừa, Độ Kiếp | `dich-linh-chi-no-v3` | 384 × 232, 1000 ms | `[192,180]` | Mặt đất |
| 10+ | Chân Tiên, Kim Tiên | `ngu-chuong-chi-no-v3` | 360 × 152, 1533 ms | `[180,113]` | Mặt đất |

- **Thời điểm.** Phát clip một lần, bắt đầu đúng khung chạm của chiêu. Nếu clip dài hơn đoạn còn lại sau va chạm, co nhịp clip cho vừa để khung cuối của chiêu vẫn trong suốt. Không cắt cụt clip.
- **Kích thước.** Tăng dần theo cấp và không để chiêu cấp cao trông nhỏ hơn cấp dưới. Tỉ lệ đã dùng cho bộ Mộc v2 (khung xem 480 × 320, neo đất `[288,254]`): bậc 1–2 lấy 0,55 và 0,65; bậc 3–5 lấy 0,42, 0,47 và 0,52; bậc 6–7 lấy 0,58 và 0,64; bậc 8–9 lấy 0,60 và 0,64; bậc 10 lấy 0,92. Ngũ Pháp Tướng (khung 600 × 480) dùng Ngũ Chưởng ở tỉ lệ 0,88.
- **Kết hợp.** Hiệu ứng va chạm riêng của chiêu (sóng ngọc, mảnh gỗ, lá bung) vẫn giữ, vẽ chồng cùng clip nổ.
- **Mẫu đã áp dụng.** `output/moc-skill-set-demo-v2/` (hàm `explosion`, bảng `TIER_EXPLOSION`), ghi tên clip và tỉ lệ vào `demo.json` (`explosion`, `explosionScale`).

### Bộ trận pháp dùng chung v3

Bản xem và nguồn dựng nằm tại `output/tran-phap-v3/`, cùng kho với Đại Bạo Nổ – Địa Chấn v2. Mở `index.html` để chọn Thiên Ấn (Ấn chính), Thiên Ấn (Vòng ngoài), Giao Ấn, Trùng Hoàn, Tầng Trận, Địa Ấn hoặc Hợp Trận.

Mỗi mẫu có WebP nền trong suốt dài 3 giây, ảnh PNG tĩnh và metadata trong `demo.json`. Nguồn tái dựng là `render.py`. Hợp Trận là bản phối các lớp trận pháp. Khi dùng vào một chiêu, chọn trận pháp cho pha triệu hồi và kích hoạt Đại Bạo Nổ tại mốc va chạm của chiêu.

## 2. Mẫu chuẩn đã được người dùng duyệt: Thiên Lôi Kiếp

Người dùng xác nhận: **“skill sét rất ok, giữ như vậy nhé”**. Giữ nguyên bản này làm tham chiếu chất lượng, màu sắc và nhịp chuyển động cho skill sét.

Thư mục: [`output/lightning-cultivation-demo-v1/`](../output/lightning-cultivation-demo-v1/).

- Bản xem trước: `preview.webp`; trang xem: `index.html`.
- Ghi nhận duyệt: `APPROVED.md`.
- Nguồn Sprite Fusion: `lightning.ts` — MIT, Hugo Duprez.
- Dựng khung: `render.mjs`; đóng gói preview: `build.py`.
- Cấu hình/metadata: `render.json`, `demo.json`.
- Tổng thời lượng **3000 ms**, **90 khung**, **30 FPS**.
- Ba đợt sét tại thời gian nguồn **620 ms, 1020 ms, 1360 ms**; khung xuất ở 30 FPS lấy mẫu theo thời gian thực tế, không ép sự kiện thành mốc tròn.
- Lôi vân tụ, tia dẫn xuất hiện trước; sét ngoằn ngoèo phân nhánh giáng từ trên cao xuống cùng vùng mục tiêu.
- Lõi sét sáng trắng, thân xanh trắng, viền xanh tím; ánh sáng va chạm và điện hồ lan quanh điểm đánh.
- Có rung nhẹ, sóng tác động và tia điện nhỏ; điện quang tan dần về cuối.
- Cấu hình đã duyệt trong `render.mjs`: `kind: 'strike'`, `x: 460`, `ground: 302`, `top: -20`, `seed: 29`, `strokes: 3`, `clouds: true`.

**Không tự đổi** palette, đường sét, số đợt đánh, seed, thời lượng, vị trí lôi vân hoặc cường độ ánh sáng của mẫu đã duyệt. Khi người dùng yêu cầu chiêu sét khác, lấy mẫu này làm nền và lưu bản mới. Hiện tại mẫu vẫn là demo, chưa tích hợp vào game.

## 2b. Mẫu băng kiếm đã chốt: Thiên Sơn Băng Kiếm

Người dùng đã khen và yêu cầu ghi mẫu cuối vào Markdown, sau đó bổ sung yêu cầu dùng lại băng trồi từ đất của Huyền Băng. Giữ thiết kế kiếm/pháp trận ở `output/ice-mountain-sword-demo-v3/` làm tham chiếu đã chốt; bản bổ sung băng mặt đất nằm riêng tại `output/ice-mountain-sword-demo-v4/`, cần người dùng xem lại phần bổ sung.

- Cự kiếm băng **thon dài**, thân kéo dài lên ngoài khung trời; **chỉ thấy mũi và đoạn lưỡi dưới**, không hiện chuôi/hộ thủ. Không quay về kiểu kiếm ngắn, bè rộng, nhìn trọn cây của bản v1.
- **Pháp trận kết kiếm trên trời**: vòng ấn, phù văn xoay và cánh tuyết cong. Kiếm hình thành trong trận, mũi xuyên qua trận rồi rơi xuống. Không nối với nhân vật.
- Giữ chất liệu tinh thể, mặt sáng/tối và palette của **Huyền Băng Thiên Ngục ban đầu**; không thay bằng đa giác phẳng đơn giản.
- Mặc định 3 giây, 90 khung, 30 FPS. Tụ/kết kiếm đến 950 ms; chạm đất ở 1600 ms. Pháp trận mở trong 450 ms, bắt đầu tan ở 1600 ms, hết ở 1950 ms.
- Khi chạm đất: băng chấn, mảnh văng, đóng băng mục tiêu. **Băng trồi từ đất dùng cụm tinh thể gốc của Huyền Băng**, không dùng hàng gai nhỏ phóng to từ một ảnh. Bản v4 chạy trực tiếp `ice.ts`, dựng một cụm chính và hai cụm phụ, mọc lên từ đúng điểm tiếp đất rồi vỡ/tan.
- VFX độc lập với nhân vật; nhân vật chạy asset riêng. Theo yêu cầu thử áp dụng vào game, **bản v4 đã được gắn vào `hu_khong_bang_nguc` — Hư Không Băng Ngục (Luyện Hư)**, ở cả xem trước và chiến đấu. Sheet trong game: `wwwroot/img/fx/ice/thien-son-bang-kiem-v4/sheet.png`. Giữ ID và chỉ số chiêu; cập nhật mô tả cho đúng hình ảnh. Lượt này phát VFX 3 giây, sát thương tại 1,6 giây, rồi mới sang combo/pet/phản kích theo lịch chung. Giảm chuyển động giữ khung 60; tải chưa xong/lỗi dùng bộ dựng dự phòng. Các skill khác giữ thông số riêng.
- Nguồn/tái dựng v4: `ice.ts`, `render-ground.mjs`, `build.py`, `ground.json`, `demo.json`. Lớp riêng: `formation-sheet.png`, `ground-ice-sheet.png`, `freeze-status-sheet.png`; tổng hợp: `vfx-sheet.png`. Các sheet có 10 × 9 ô 480 × 330 px, 90 khung; điểm neo mục tiêu `[340,270]` trong ô.

Không ghi đè mẫu đã chốt; những thay đổi tiếp theo phải lưu thư mục phiên bản mới.

Sau phản hồi quái hành động sớm/FPS tụt: bản dùng trong game là `sheet-runtime-v1.png`, 10 × 9 ô **240 × 165 px**, neo `[170,135]`; giữ đủ 90 khung/30 FPS/3 giây. Sheet dựng gốc 480 × 330 vẫn giữ để xuất lại. Bản runtime giảm 75% số pixel giải mã (khoảng 57 MB → 14 MB). Đóng gói lại bằng `tools/ice_fx/pack_runtime.py`. Phía hiển thị giữ một snapshot trận có tuyệt kỹ cho đến khi animation kết thúc, rồi bắt kịp trạng thái mới nhất; không xếp hàng vô hạn, không trì hoãn cập nhật thưởng hay giảm lợi ích Tốc Hành. Quái chờ hết đoạn thi triển mới tụ lực/phản kích.

## 2c. Mẫu demo cao cấp — Thiên Thần Hư Ảnh · Thiên Quang Giáng

Mẫu hiện tại nằm ở `output/thien-than-thien-quang-demo-v3/`. Hư ảnh giáp vàng cao khoảng 3,6 lần nhân vật, hiện trên pháp trận triệu hồi. Pháp trận có các vòng đồng tâm, dải phù văn xoay ngược chiều, bốn ấn tròn và lõi tam giác. Nhân vật trong preview chạy asset riêng.

- **Tư thế niệm phép.** Hư ảnh kết ấn hai ngón, giữ vai gần như đứng yên rồi phẩy nhẹ cổ tay để gọi cột sáng. Dùng `hand-mudra.png` do ImageGen tạo, với prompt ở `hand-mudra-prompt.txt`. Căn điểm nối bàn tay vào cổ tay theo từng khung và đặt giáp tay lên trên mép nối. Khi tạo biến thể dùng kiếm, giữ chuôi theo tâm nắm tay và chỉ để các ngón che chuôi. Thần Binh Trảm v6 là mẫu tham chiếu cho cách ghép kiếm.
- **Triệu hồi và va chạm.** Hư ảnh mở thiên ấn trên mục tiêu. Cột [Sprite Fusion Holy Light](https://www.spritefusion.com/pixel-art-vfx/holy-light) biến thể Pillar đi qua thiên ấn, chạm đất ở khung 57 (1.900 ms) rồi kích hoạt Đại Bạo Nổ – Địa Chấn v2 cùng khung. Giữ neo cột sáng và vòm nổ tại cùng điểm đất. Kiểm tra khung cuối trong suốt, kể cả tia dẫn và quầng sáng.
- **Thông số xuất.** 3 giây, 90 khung, 30 FPS, lưới 10 × 9. Ô gốc 576 × 384, neo mục tiêu `[430,320]`, neo triệu hồi `[151,320]`. Ô runtime 240 × 160, neo mục tiêu `[179.1667,133.3333]`. Tách các lớp pháp trận, hư ảnh, cột sáng và va chạm. Xem `demo.json`, `SOURCES.md` và `build.py` trong thư mục demo.
- **Phạm vi.** Đây là mẫu demo. Chỉ thay VFX của kỹ năng trong game khi có yêu cầu tích hợp riêng.

## 2d. Mẫu đã duyệt: Ngũ Pháp Tướng · Thiên Phạt v4

Người dùng duyệt ngày 2026-11-17 (**"Ổn rồi"**). Thư mục `output/ngu-phap-tuong-thien-phat-demo-v4/`, ghi nhận trong `APPROVED.md`. Bộ này mở rộng Kim Long Thiên Ấn · Thiên Quang v4 sang bốn pháp tướng còn lại. Mỗi pháp tướng có kiểu đánh riêng: pháp tướng hiện sau tu sĩ, thiên ấn mở trên mục tiêu, đòn đánh giáng xuống, rồi phát vụ nổ Ngũ Chưởng cùng hệ từ `vfx-no-cac-he`.

**Quy tắc khi người dùng đưa ảnh mẫu (bài học từ v2/v3):**

- **Ảnh mẫu chỉ dùng để học độ nét, chi tiết và màu.** Không dán ảnh mẫu vào hiệu ứng rồi chỉ dời, phóng to, xoay hay lật. Bản v2/v3 làm vậy và bị nhận xét "hình bị cứng". Bản v1 procedural được khen mượt hơn.
- Dựng hiệu ứng bằng code (procedural) theo lớp: mask, glow, lõi sáng. Thêm chi tiết như ảnh mẫu: nhiều tầng nhánh, sợi mảnh, vân chảy, sao lấp lánh. Dùng seed cố định.
- Lưu ảnh mẫu trong thư mục phiên bản để đối chiếu. Ảnh gốc người dùng đặt ở `output/anh-tuyet-ky/`.
- **Sét phải bắt đầu từ trận pháp.** Gốc tia sét đặt tại tâm thiên ấn (`SEAL_ROOT`). Trước đòn đầu, tia dẫn mọc ra từ tâm ấn, và thiên ấn lóe sáng theo mỗi đòn. Sét ở chân mục tiêu dày đặc nhưng **giữ bề ngang hẹp**, không loang rộng như bức tường sét trong ảnh mẫu.

**Chi tiết từng chiêu đã chốt:**

| Pháp tướng | Hệ | Va chạm | Hình thái |
|---|---|---|---|
| Lôi Thần · Cửu Tiêu Thiên Phạt | lôi | khung 45 | 3 đòn sét (khung 45/51/57). Thân tím 22 px, sợi bện, lõi trắng, có nhánh và nhánh con. Lôi bộc hẹp ở chân mục tiêu |
| Hỏa Phượng · Liệt Diễm Phần Thiên | hỏa | khung 47 | Hỏa phượng chui ra từ thiên ấn, lượn rồi lao xuống. Cánh 3 tầng lông vũ hình lá có viền tách và gân. Đuôi 5 dải lửa cuộn đầu. Cột lửa hạt |
| Huyền Vũ · Trấn Hải Thủy Trụ | thủy | khung 46 | Thủy triền xoáy tụ trên địa ấn rồi phun thủy trụ. Khối nước 3 lớp xanh ngọc, vân chảy lên, 2 dải xoắn trong mờ, bọt đỉnh, vương miện bọt ở đáy, bong bóng |
| Nguyệt Kiếm · Tàn Nguyệt Tứ Trảm | băng | khung 43 | Trăng khuyết kết trong thiên ấn, 3 nhát nguyệt trảm và 1 cự trảm dọc. Lưỡi có sợi lụa sáng, vệt phụ trễ nhịp, sao bốn cánh |

- **Thông số.** 105 khung, 30 FPS, 3500 ms, cùng nhịp Kim Long v4. Atlas chỉ chứa VFX: 10 × 11 ô 400 × 320, khung đầu và cuối trong suốt; vụ nổ là lớp phủ riêng. Dựng lại bằng `py build.py [id]`. Nguồn và tham khảo Sprite Fusion ghi trong `SOURCES.md`.
- **Có thể cải thiện sau.** Dáng cánh Hỏa Phượng còn xòe tròn, chưa dang ngang và có đuôi vút lên như ảnh gốc.
- **Phạm vi.** Đây là demo, chưa tích hợp vào game. Biến thể mới lưu thành `-v5`, không ghi đè v4.

## 3. Các ý đã thống nhất khi thử skill hệ lửa

### Hỏa liên

- Giữ bối cảnh tu tiên: vận linh/kết ấn → phóng linh hỏa → hỏa liên nở tại điểm đánh.
- Người dùng đã sửa yêu cầu: **giữ hỏa liên và làm hỏa liên chi tiết**. Yêu cầu giữ hoa này thay thế chỉ dẫn bỏ hỏa liên ở lần thử trước.
- Bản thử chi tiết có **17 cánh, 3 tầng (7 + 5 + 5)**, cánh cong, viền vàng, gân lửa phân nhánh, đài sen và hạt sen.
- Giữ cú nổ rồi mờ dần; **không thêm phần cháy kéo dài phía sau**. Hỏa liên cũng tan dần sau khi nở.
- Tham chiếu: [`output/fire-skill-cultivation-demo-v4/`](../output/fire-skill-cultivation-demo-v4/).
- Đây là demo theo yêu cầu chỉnh sửa, **chưa có lời duyệt cuối như mẫu sét**. Không áp đặt 17 cánh hoặc hình hoa sen cho mọi tuyệt kỹ hệ lửa.

### Thiên thạch

- Thiên thạch là khối đá có **những vết nứt phát sáng** trên bề mặt, **lửa bao quanh**, có đuôi lửa theo hướng rơi.
- Giữ hình khối đá rõ ràng khi chuyển động; khe nứt bám trên đá, không trượt hoặc tự đổi cấu trúc từng khung.
- Khi chạm đất: **vụ nổ + địa chấn lan xung quanh**; demo thể hiện bằng sóng trên mặt đất, nứt đất, mảnh văng và rung cảnh.
- Người dùng yêu cầu tổng animation **3 giây**. Bản demo đặt va chạm ở **1 giây**; đây là lựa chọn dựng demo, không phải quy tắc bắt buộc cho mọi chiêu.
- Sau yêu cầu học thêm từ itch.io, demo được thử lại với mặt đá có khối, khe nứt phân nhánh, mảng lửa và vụ nổ pixel rõ hơn.
- Bản tham chiếu mới nhất: [`output/meteor-cultivation-demo-v3-itch-study/`](../output/meteor-cultivation-demo-v3-itch-study/).
- Thiên thạch vẫn là **bản thử**, chưa được người dùng chốt chất lượng cuối. Không coi bản này là chuẩn đã duyệt thay cho Thiên Lôi Kiếp.

## 4. Nguồn tham khảo và cách dùng

- Sprite Fusion VFX: <https://www.spritefusion.com/pixel-art-vfx>.
- Thư viện itch.io do người dùng chỉ định: <https://itch.io/game-assets/tag-effects/tag-pixel-art>.
- Các trang đã đọc khi thử lại thiên thạch:
  - [Pixel Art Fire Asset Pack — Devkidd](https://devkidd.itch.io/pixel-fire-asset-pack).
  - [Five Free Pixel Explosions — unTied Games](https://untiedgames.itch.io/five-free-pixel-explosions).
  - [Pixel Magic Effects — Foozle](https://foozlecc.itch.io/pixel-magic-sprite-effects).

Với Sprite Fusion, dùng `list_vfx` để chọn tình huống phù hợp, rồi `get_vfx` để lấy recipe và mã nguồn. Khi dự án chạy JavaScript/TypeScript, ưu tiên chạy nguồn gốc, giữ palette, cấu trúc lớp và nhịp gốc; chỉnh cấu hình phù hợp với chiêu. Skill sét đã dùng cách này.

Phân biệt **tham khảo** với **sử dụng trực tiếp asset**. Bản thiên thạch `v3-itch-study` được dựng bằng mã procedural, không chứa asset itch.io được tải và ghép vào. Khi dùng trực tiếp asset mới, đọc giấy phép cụ thể, lưu nguồn và credit đi kèm; không mặc định mọi asset trên trang tổng hợp có cùng giấy phép. Giữ thông báo MIT khi dùng mã Sprite Fusion.

## 5. Quy trình làm tuyệt kỹ tiếp theo

Khi người dùng yêu cầu đưa VFX vào game, làm theo [Quy trình tích hợp VFX](TuyetKy_VFX_Integration_Workflow.md), đặc biệt phần asset runtime, đồng bộ phản kích và snapshot hiển thị không ảnh hưởng thưởng.

1. Đọc tài liệu này và xác định hệ, hình thức chiêu, vùng đánh từ yêu cầu hiện tại. Nếu đã đủ thông tin, bắt đầu demo ngay; không hỏi lại thông số đã chốt.
2. Chọn mẫu nguồn gần nhất. Với skill sét, ưu tiên Thiên Lôi Kiếp đã duyệt. Với hệ khác, dùng VFX tương ứng và giữ bản sắc của hệ đó.
3. Lưu dưới `output/<ten-skill>-demo-vN/`; giữ nguồn và script tái dựng. Dùng seed cố định nếu nguồn có ngẫu nhiên.
4. Dựng demo riêng, mặc định 3 giây / 30 FPS. Giữ điểm neo nhất quán; không căn lại từng khung theo bounding box.
5. Xem các giai đoạn chuẩn bị, phát chiêu, va chạm và tan dần; xem chuyển động ở kích thước game. Kiểm tra hiệu ứng không che mất nhân vật ngoài chủ đích hoặc bị cắt do khung dựng quá hẹp.
6. Kiểm tra số khung **và tổng duration được giải mã của file động**. Nếu dùng 90 khung ở 30 FPS trong WebP, có thể dùng nhịp `33, 33, 34 ms` lặp 30 lần để đạt chính xác 3000 ms.
7. Gửi preview động cho người dùng. Khi được duyệt, ghi nhận `APPROVED.md` trong đúng thư mục phiên bản; không tự coi việc đã xuất file là đã được duyệt.
8. Chỉ khi có yêu cầu tích hợp: gắn vào đúng skill, dùng metadata riêng của chiêu, giữ nhịp damage/event của game theo yêu cầu tích hợp; không đổi tốc độ các skill khác.

## 6. Bàn giao và yêu cầu kỹ thuật

- **Demo:** `preview.webp` hoặc APNG tương đương, `index.html`, ảnh `poster.png`, `demo.json`, nguồn/script và giấy phép liên quan. Ảnh contact sheet chỉ hỗ trợ kiểm tra, không thay preview động.
- **Khi xuất asset để tích hợp:** PNG sprite sheet nền trong suốt, JSON khai báo kích thước ô, lưới, số khung, FPS, duration, điểm neo, pha và các mốc đánh; kèm preview động.
- Nền cảnh và nhân vật trong demo chỉ dùng minh họa; không bake chúng vào sheet VFX.
- Tách color/light hoặc lớp hiệu ứng khi nguồn có hỗ trợ. Demo xuất ảnh có thể mô phỏng ánh sáng; khi tích hợp cần kiểm tra cách blend thực tế.
- Giữ hình dự phòng trong lúc tải; khi bật giảm chuyển động dùng khung tĩnh phù hợp, giảm rung/chớp. Không lấy việc preview tự lặp làm yêu cầu chiêu phải nối vòng vô hạn.

## 7. Mẫu yêu cầu có thể dùng lại

> Tạo demo tuyệt kỹ [tên/hệ] cho Vấn Đạo theo `docs/TuyetKy_VFX_Generation_Guide.md`. Mặc định 3 giây, 90 khung, 30 FPS, phong cách tu tiên. Nội dung chính: [mô tả]. Dùng nguồn VFX phù hợp; nếu là sét, lấy `output/lightning-cultivation-demo-v1/` đã duyệt làm tham chiếu. Lưu phiên bản mới và xuất preview động trước, chỉ tích hợp khi được yêu cầu.

## Nguồn tham khảo bổ sung: PVFX Foundry

- [PVFX Foundry — nerijs / Pixel VFX Studio](https://nerijs.itch.io/pvfx-foundry), kiểm tra ngày 2026-11-05. Bản 1.0 có 101 hiệu ứng, PNG trong suốt và JSON về frame, thời lượng, pivot, bounds; gồm lửa, băng, sét, va chạm, portal và magic circle.
- Hai kiểu Lit (glow/alpha mềm) và Strict (palette giới hạn/alpha nhị phân). Học nhịp anticipation → peak → decay, cách lửa nguội từ lõi trắng qua vàng đến tàn, và cách cổng triệu hồi phản ứng với vật thể đi qua.
- Trang tác giả công bố sprite sheets và metadata theo CC0 1.0, cho phép chỉnh sửa và dùng thương mại; lưu giấy phép của bản tải nếu sử dụng trực tiếp. Nguồn này hiện chỉ được thêm làm tham khảo, chưa dùng pixel của pack trong bộ Hỏa tích hợp.
