# Ngũ Pháp Tướng · Thiên Phạt v4 — nguồn

Theo phản hồi: giữ chuyển động procedural của v1 (mượt), chỉ học độ nét/chi tiết từ ảnh gốc `../anh-tuyet-ky/` (không dán ảnh vào hiệu ứng). Sét phải bắt đầu từ trận pháp.

- Lôi Thần: mọi tia sét gốc tại tâm thiên ấn (`SEAL_ROOT`), tụ lôi mọc từ tâm ấn trước đòn, thiên ấn lóe theo mỗi đòn. Thân sét 22 px tím + sợi bện + lõi trắng, nhánh và nhánh con; lôi bộc chân mục tiêu là nhiều tia nhỏ bắn lên, bề ngang hẹp.
- Hỏa Phượng: ba tầng lông vũ hình lá có viền tách và gân, mào lửa, năm dải đuôi lá lửa nối nhau uốn sóng, đầu đuôi cuộn; dải màu 5 nấc như ảnh gốc.
- Huyền Vũ: khối nước 3 lớp (viền teal sâu → giữa → lõi sáng), vân chảy lên, hai dải nước xoắn trong mờ (mặt sau mờ hơn), lưỡi nước + bọt ở đỉnh, vương miện bọt ở đáy, bong bóng nổi.
- Nguyệt Kiếm: lưỡi trăng khuyết dày, sợi lụa sáng so le, vệt phụ trong/ngoài trễ nhịp, sao bốn cánh lấp lánh dọc lưỡi.

## Ghi chú v1

Mở rộng khuôn `../kim-long-thien-quang-demo-v4/` (clone từ Codex 2026-11-17) sang bốn pháp tướng còn lại. Mỗi pháp tướng có model đòn đánh riêng, không chỉ đổi màu.

## Dữ liệu dùng lại (nằm trong dự án)

- Pháp tướng: `../phap-tuong-ngu-tuong-demo-v3/<id>/atlas.png` (ImageGen, 90 khung 320 × 288, neo chân tu sĩ). `build.py` đọc thẳng từ thư mục này.
- Thiên ấn / địa ấn mục tiêu: `source/holy-light-formation.png`, Sprite Fusion Holy Light (MIT, Hugo Duprez, xem `source/holy-light-LICENSE.txt`), nhuộm lại theo màu hệ.
- Vụ nổ: `explosions/<hệ>-ngu-chuong-chi-no-v3.webp`, lấy nguyên file từ `../vfx-no-cac-he-demo-v2/vfx-no-cac-he/` (loi, hoa, thuy, bang). Phát một lần tại chân mục tiêu từ khung va chạm.
- Nền sân, tu sĩ, Băng Hồ: `game/` (chỉ có trong preview/poster, không nằm trong atlas).

## Tham khảo từ internet (chỉ học cách thể hiện, không chép pixel/mã)

Thư viện [Sprite Fusion Pixel Art VFX](https://www.spritefusion.com/pixel-art-vfx) (MIT), xem ngày 2026-11-17:

- **Lightning**: sét nhánh từ trời, tia dẫn trước đòn, chớp nhấp nháy → Lôi Thần (3 đòn tại khung 45/51/57).
- **Fire** + **Embers**: lõi trắng → vàng → cam → đỏ → tàn, hạt bay lên → Hỏa Phượng (đuôi lửa, cột lửa hạt).
- **Bubbles** / **Rain** (splash, ripple): giọt rơi theo trọng lực, sóng gợn → Huyền Vũ.
- **Slash**: lưỡi chém sáng ở mép, nhạt về đuôi → Nguyệt Kiếm (3 nhát + cự trảm).

Toàn bộ đòn đánh được dựng procedural bằng Pillow trong `build.py`, seed cố định.

## Dựng lại

```
py build.py                 # cả bốn
py build.py nguyet-kiem     # một bản
```

Yêu cầu Pillow. Mỗi bản xuất `atlas.png` (VFX-only, 10 × 11 ô 400 × 320, khung đầu/cuối trong suốt), `preview.webp` (105 khung, đúng 3500 ms), `poster.png`, `demo.json`.
