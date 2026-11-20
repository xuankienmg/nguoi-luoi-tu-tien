# Hướng dẫn tạo VFX tuyệt kỹ — Vấn Đạo

Áp dụng khi tạo, sửa, xem trước VFX tuyệt kỹ. Yêu cầu mới của người dùng ưu tiên hơn mặc định dưới đây. Tích hợp vào game: xem [Quy trình tích hợp VFX](TuyetKy_VFX_Integration_Workflow.md).

## 1. Quy tắc chung

- Phong cách **tu tiên**, có bản sắc riêng theo hệ. Nhịp: tụ lực → thi triển → va chạm → tan dần (mốc va chạm, số đợt đánh tùy chiêu).
- Mặc định **2 giây, 60 khung, 30 FPS**, phát một lần (preview được lặp), khớp lượt tuyệt kỹ trong game (`ULTIMATE_CAST_SECONDS = 2` trong `wwwroot/js/fire-fx.js`, server `BattleTiming.UltimateTurnSeconds = 2 + .25`). Sheet cũ dựng 3 giây/90 khung vẫn chạy nhưng bị phát nhanh 1,5×; chiêu mới dựng thẳng 2 giây như bộ Mộc v5/Thổ v6. Thông số skin 64 khung/16 FPS không áp dụng.
- Tạo **demo trước**, chỉ tích hợp khi được yêu cầu rõ. Mỗi lần thử lưu phiên bản mới, không ghi đè mẫu đã duyệt.
- Hiệu ứng đọc được ở kích thước game: rõ đường đi, điểm đánh, hình dạng đặc trưng.
- **VFX độc lập với nhân vật**: không rig/biến dạng, không neo tay chân, không pháp trận dưới chân hay dòng hạt từ nhân vật. Nhân vật trong preview chỉ là ảnh tham chiếu, không bake vào asset. Điểm neo và vị trí chiêu khai báo riêng.
- **Trận pháp** đặt tại vị trí thi triển (vòng ấn, phù văn, chuyển động mềm); không có đường nối thẳng giữa các điểm. **Cấp thấp không có trận pháp** (sóng va chạm mờ dần vẫn được).
- Nhiều cấp cùng chiêu: **mỗi cấp khác hình thái và cách thi triển**, không chỉ phóng to/đổi màu/nhân bản.
- **Ảnh mẫu người dùng đưa chỉ để học độ nét, chi tiết, màu.** Không dán ảnh rồi dời/xoay/phóng (bị chê "cứng"). Dựng procedural theo lớp mask/glow/lõi sáng, seed cố định. Ảnh gốc ở `output/anh-tuyet-ky/`.

### Hệ băng

- Chất liệu băng dùng lại **Huyền Băng Thiên Ngục v1** (`output/ice-cultivation-demo-v1/`, `ice.ts` Sprite Fusion Ice): mặt tinh thể, cạnh sáng, palette gốc; không dùng đa giác phẳng.
- Sau khi trúng: lớp **đóng băng** kết tinh → giữ một nhịp → nứt vỡ, tan; phủ tăng theo cấp. Tách lớp trạng thái riêng; chỉ là hình ảnh, không đổi logic khống chế.
- Tụ lực: hàn khí hội tụ, lõi sáng tăng dần rồi giải phóng; ghi thời điểm release trong metadata.

## 2. Vụ nổ theo bậc cảnh giới (chốt 2026-11-17)

Mỗi chiêu phát clip nổ **cùng hệ**: `output/vfx-no-cac-he-demo-v2/vfx-no-cac-he/<hệ>-<clip>.webp`, hệ: `hoa`, `thuy`, `moc`, `tho`, `loi`, `bang`, `phong` (Hỏa ba clip đầu ở `vfx-no-he-hoa/<clip>.webp`). Thay Đại Bạo Nổ cho mọi chiêu mới.

| Bậc | Cảnh giới | Clip | Nguồn | Điểm chạm | Đặt tại |
|---|---|---|---|---|---|
| 1–2 | Luyện Khí, Trúc Cơ | `xich-diem-tram-v1` | 347×94, 866 ms | `[300,46]` | Giữa thân mục tiêu |
| 3–5 | Kim Đan → Hóa Thần | `thien-hoa-giang-thach-v10` | 355×197, 1266 ms | `[192,172]` | Mặt đất |
| 6–7 | Luyện Hư, Hợp Thể | `viem-kiem-giang-thien-v4` | 349×254, 1266 ms | `[228,232]` | Mặt đất |
| 8–9 | Đại Thừa, Độ Kiếp | `dich-linh-chi-no-v3` | 384×232, 1000 ms | `[192,180]` | Mặt đất |
| 10+ | Chân Tiên, Kim Tiên | `ngu-chuong-chi-no-v3` | 360×152, 1533 ms | `[180,113]` | Mặt đất |

- Bắt đầu đúng khung chạm; clip dài hơn phần còn lại thì co nhịp (không cắt), khung cuối chiêu phải trong suốt.
- Kích thước tăng theo cấp. Tỉ lệ bộ Mộc v2 (khung 480×320, neo đất `[288,254]`): bậc 1–2: 0,55/0,65; 3–5: 0,42/0,47/0,52; 6–7: 0,58/0,64; 8–9: 0,60/0,64; 10: 0,92. Ngũ Pháp Tướng (600×480): 0,88.
- Hiệu ứng va chạm riêng của chiêu vẫn vẽ chồng lên.
- Mẫu: `output/moc-skill-set-demo-v2/` (hàm `explosion`, `TIER_EXPLOSION`; `demo.json` ghi `explosion`, `explosionScale`).

**Trận pháp dùng chung v3**: `output/tran-phap-v3/` (`index.html`): Thiên Ấn (chính/vòng ngoài), Giao Ấn, Trùng Hoàn, Tầng Trận, Địa Ấn, Hợp Trận. Mỗi mẫu có WebP 3 giây, PNG, `demo.json`; dựng lại bằng `render.py`.

## 3. Mẫu tham chiếu

Các mẫu dưới đây dựng theo nhịp cũ (3–3,5 giây); khi dùng làm nền cho chiêu mới, dựng lại nhịp về 2 giây / 60 khung.

### Thiên Lôi Kiếp — đã duyệt (mẫu sét)

`output/lightning-cultivation-demo-v1/` (`APPROVED.md`, `preview.webp`, `index.html`, `lightning.ts` MIT Hugo Duprez, `render.mjs`, `build.py`, `render.json`, `demo.json`).

- 3000 ms / 90 khung / 30 FPS; ba đợt sét ở 620, 1020, 1360 ms (lấy mẫu theo thời gian thực).
- Lôi vân tụ, tia dẫn, sét phân nhánh giáng xuống; lõi trắng, thân xanh trắng, viền xanh tím; điện hồ, rung nhẹ, tan dần.
- Cấu hình: `kind: 'strike'`, `x: 460`, `ground: 302`, `top: -20`, `seed: 29`, `strokes: 3`, `clouds: true`.
- **Không tự đổi** palette, đường sét, số đợt, seed, thời lượng, vị trí lôi vân, độ sáng. Chiêu sét mới lấy mẫu này làm nền, lưu bản mới.

### Thiên Sơn Băng Kiếm — đã chốt, đã tích hợp

v3 (`output/ice-mountain-sword-demo-v3/`) là thiết kế chốt; v4 (`-v4/`) thêm băng trồi từ đất và đang dùng cho `hu_khong_bang_nguc` (Hư Không Băng Ngục).

- Cự kiếm thon dài, chỉ thấy mũi và lưỡi dưới; kết trong pháp trận trên trời rồi rơi xuống.
- Tụ kiếm đến 950 ms, chạm đất 1600 ms; pháp trận mở 450 ms, tan 1600–1950 ms.
- Chạm đất: băng chấn, mảnh văng, đóng băng; băng trồi dùng cụm tinh thể `ice.ts` (một cụm chính, hai phụ).
- Nguồn v4: `ice.ts`, `render-ground.mjs`, `build.py`, `ground.json`, `demo.json`; lớp `formation-sheet.png`, `ground-ice-sheet.png`, `freeze-status-sheet.png`, tổng `vfx-sheet.png` (10×9 ô 480×330, neo `[340,270]`).
- Runtime: `wwwroot/img/fx/ice/thien-son-bang-kiem-v4/sheet.png` ← `sheet-runtime-v1.png`, 10×9 ô **240×165**, neo `[170,135]`; đóng gói bằng `tools/ice_fx/pack_runtime.py`.

### Thiên Thần Hư Ảnh · Thiên Quang Giáng — demo

`output/thien-than-thien-quang-demo-v3/`. Hư ảnh giáp vàng ~3,6 lần nhân vật trên pháp trận triệu hồi.

- Kết ấn hai ngón (`hand-mudra.png`, prompt `hand-mudra-prompt.txt`), căn bàn tay vào cổ tay từng khung. Biến thể cầm kiếm: tham chiếu Thần Binh Trảm v6.
- Cột Holy Light (Pillar) qua thiên ấn, chạm đất khung 57 (1900 ms), nổ cùng điểm; khung cuối trong suốt.
- Lưới 10×9; ô gốc 576×384, neo mục tiêu `[430,320]`, triệu hồi `[151,320]`; runtime 240×160, neo `[179.1667,133.3333]`. Xem `demo.json`, `SOURCES.md`, `build.py`.

### Ngũ Pháp Tướng · Thiên Phạt v4 — đã duyệt

`output/ngu-phap-tuong-thien-phat-demo-v4/` (`APPROVED.md`). Pháp tướng hiện sau tu sĩ → thiên ấn mở trên mục tiêu → đòn giáng xuống → nổ Ngũ Chưởng cùng hệ.

| Pháp tướng | Hệ | Va chạm | Hình thái |
|---|---|---|---|
| Lôi Thần · Cửu Tiêu Thiên Phạt | lôi | khung 45/51/57 | Thân tím 22 px, sợi bện, lõi trắng, nhánh con; gốc sét ở tâm thiên ấn (`SEAL_ROOT`), lôi bộc chân mục tiêu hẹp |
| Hỏa Phượng · Liệt Diễm Phần Thiên | hỏa | khung 47 | Phượng ra từ thiên ấn, lượn, lao xuống; cánh 3 tầng lông, đuôi 5 dải lửa; cột lửa hạt |
| Huyền Vũ · Trấn Hải Thủy Trụ | thủy | khung 46 | Thủy triền xoáy trên địa ấn → thủy trụ 3 lớp, dải xoắn, bọt, bong bóng |
| Nguyệt Kiếm · Tàn Nguyệt Tứ Trảm | băng | khung 43 | Trăng khuyết trong ấn, 3 nguyệt trảm + 1 cự trảm dọc, sao bốn cánh |

- 105 khung, 30 FPS, 3500 ms; atlas 10×11 ô 400×320, chỉ VFX, khung đầu/cuối trong suốt, nổ là lớp riêng. Dựng: `py build.py [id]`.
- Còn cải thiện: cánh Hỏa Phượng nên dang ngang, đuôi vút lên. Biến thể mới lưu `-v5`.

### Hệ lửa — bản thử, chưa duyệt

- **Hỏa liên** (`output/fire-skill-cultivation-demo-v4/`): 17 cánh 3 tầng (7+5+5), viền vàng, gân lửa, đài sen; nổ rồi tan, không cháy kéo dài. Không áp cho mọi chiêu lửa.
- **Thiên thạch** (`output/meteor-cultivation-demo-v3-itch-study/`): đá có khe nứt phát sáng cố định, lửa bao quanh, đuôi lửa; chạm đất nổ + địa chấn (sóng, nứt đất, mảnh văng, rung). Demo va chạm ở 1 giây.

## 4. Nguồn tham khảo

- [Sprite Fusion VFX](https://www.spritefusion.com/pixel-art-vfx): `list_vfx` chọn, `get_vfx` lấy mã; chạy nguồn gốc, giữ palette/lớp/nhịp, giữ thông báo MIT.
- [itch.io pixel-art effects](https://itch.io/game-assets/tag-effects/tag-pixel-art): Devkidd Fire, unTied Games Explosions, Foozle Magic Effects.
- [PVFX Foundry](https://nerijs.itch.io/pvfx-foundry) (CC0 1.0): học nhịp anticipation → peak → decay, lửa nguội từ trắng → vàng → tàn, cổng triệu hồi.
- Tham khảo ≠ dùng asset. Dùng asset trực tiếp phải đọc giấy phép riêng, lưu nguồn và credit.

## 5. Quy trình

1. Xác định hệ, hình thức chiêu, vùng đánh; đủ thông tin thì làm ngay, không hỏi lại thông số đã chốt.
2. Chọn mẫu gần nhất (sét → Thiên Lôi Kiếp).
3. Lưu `output/<ten-skill>-demo-vN/` kèm nguồn, script tái dựng, seed cố định.
4. Giữ điểm neo nhất quán, không căn từng khung theo bounding box.
5. Kiểm tra các pha ở kích thước game; không che nhân vật ngoài chủ đích, không bị cắt khung.
6. Kiểm tra số khung **và tổng duration giải mã**; WebP 60 khung dùng nhịp `33, 33, 34 ms` × 20 = 2000 ms.
7. Gửi preview động; chỉ ghi `APPROVED.md` khi người dùng duyệt.
8. Tích hợp (khi được yêu cầu): đúng skill, metadata riêng, không đổi tốc độ skill khác.

## 6. Bàn giao

- **Demo:** `preview.webp` (hoặc APNG), `index.html`, `poster.png`, `demo.json`, nguồn/script, giấy phép. Contact sheet chỉ để kiểm tra.
- **Asset tích hợp:** PNG sheet nền trong suốt + JSON (ô, lưới, số khung, FPS, duration, neo, pha, mốc đánh) + preview động. Không bake nền/nhân vật.
- Tách lớp màu/ánh sáng khi được; kiểm tra blend thực tế khi tích hợp.
- Có hình dự phòng khi tải; giảm chuyển động dùng khung tĩnh, bớt rung/chớp.
