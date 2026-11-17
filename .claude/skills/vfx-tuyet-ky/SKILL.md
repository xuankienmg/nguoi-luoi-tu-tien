---
name: vfx-tuyet-ky
description: Tạo, sửa, xem trước hoặc tích hợp hiệu ứng tuyệt kỹ (VFX chiêu thức, skill FX, sét, lửa, ấn, chưởng) cho game. Dùng khi người dùng nói "làm hiệu ứng chiêu", "VFX", "demo tuyệt kỹ", "chỉnh kích thước hiệu ứng skill", "FIRE_SET_FX".
---

# VFX tuyệt kỹ

Khi tạo, sửa hoặc xem trước VFX tuyệt kỹ, đọc và áp dụng `docs/TuyetKy_VFX_Generation_Guide.md` nếu có trên máy (thư mục `docs/` bị gitignore). Mặc định demo **2 giây, 30 FPS, 60 khung**, phong cách tu tiên; tạo demo trước, chỉ tích hợp khi được yêu cầu. **Thiên Lôi Kiếp** tại `output/lightning-cultivation-demo-v1/` là mẫu sét đã được người dùng duyệt: giữ nguyên mẫu, lưu phiên bản mới khi thử biến thể. Thông số VFX tuyệt kỹ tách biệt với tiêu chuẩn skin (skill `skin-sprite`).

**Dịch Linh Ấn v3** tại `output/dich-linh-an-demo-v3/` là mẫu đã được người dùng chọn (bàn tay, timeline, luồng khí và va chạm cấp cao). Giữ nguyên asset/nguồn mẫu; clone sang thư mục phiên bản/hệ mới khi tạo biến thể. Mô tả chuẩn: `output/dich-linh-an-demo-v3/prompt.md`.

**Ảnh mẫu người dùng đưa chỉ để học độ nét, chi tiết và màu**: dựng hiệu ứng bằng code (procedural) theo lớp, không dán ảnh mẫu rồi dời/xoay/phóng to, vì làm vậy hình bị cứng. **Sét phải bắt đầu từ trận pháp.** Mẫu đã duyệt: **Ngũ Pháp Tướng · Thiên Phạt v4** tại `output/ngu-phap-tuong-thien-phat-demo-v4/` (mục 2d trong hướng dẫn).

**Vụ nổ theo bậc cảnh giới**, dùng clip cùng hệ trong `output/vfx-no-cac-he-demo-v2/`: bậc 1–2 Xích Diễm Trảm, 3–5 Thiên Hỏa Giáng Thạch, 6–7 Viêm Kiếm Giáng Thiên, 8–9 Dịch Linh Ấn, 10+ Ngũ Chưởng. Điểm chạm và tỉ lệ ghi trong mục "Vụ nổ theo bậc cảnh giới" của hướng dẫn.

**Phạm vi skill to dần theo cấp độ của skill**: chiêu cấp cao hơn phải chiếm vùng hiển thị lớn hơn chiêu cấp thấp, cả khi dựng demo lẫn khi chỉnh kích thước lúc tích hợp (`FIRE_SET_FX` trong `wwwroot/js/fire-fx.js`: `span`, `fitTop`). Không để chiêu cấp cao trông nhỏ hơn chiêu cấp dưới.
