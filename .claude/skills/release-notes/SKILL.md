---
name: release-notes
description: Ghi chú phát hành và phát hành phiên bản game. Dùng khi cập nhật docs/releases/upcoming.md (vd "update upcoming cho 0.11.3", "viết release note") hoặc khi phát hành / release / tag phiên bản mới (vd "release 0.11.3", "phát hành bản mới", "đánh tag").
---

# Ghi chú & phát hành phiên bản

Phần A: soạn `docs/releases/upcoming.md`. Phần B: phát hành bằng tag `vX.Y.Z`. Mọi bước `git push`/tạo PR đều cần người dùng xác nhận (hook của dự án sẽ hỏi); không tự chạy khi chưa được yêu cầu.

---

## A. Ghi chú phát hành

`docs/releases/upcoming.md` là bài đăng cho người chơi (tiếng Việt, giọng "đạo hữu"). Workflow phát hành dùng **nguyên file này tại commit của tag** làm nội dung GitHub Release; file trống thì tự sinh từ commit (kém đẹp hơn).

### A1. Xác định phiên bản
1. Tag đã phát hành: `git tag | sort -V | tail -3`.
2. Tiêu đề `upcoming.md` trùng một tag đã có → nội dung đó **đã phát hành** → viết lại file cho bản mới, chỉ gồm thay đổi sau tag đó (`git log --oneline vPREV..HEAD`).
3. Tiêu đề là bản chưa có tag → bản đang soạn: **thêm** mục, không xóa nội dung cũ.
4. Không chắc → hỏi người dùng.

### A2. Định dạng
```markdown
## <emoji> Người Lười Tu Tiên X.Y.Z — <khẩu hiệu ngắn>!

Đạo hữu ơi, <1–2 câu tóm tắt>. Cập nhật Launcher và vào game thôi! 👇

### <emoji> <Tên hệ thống>
- <Thay đổi người chơi thấy được; **in đậm** tên tính năng/nút/chỉ số>.

Hẹn gặp đạo hữu trên Thiên bảng! ⚔️

#NguoiLuoiTuTien #TuTien #GameIdle #CapNhatXYZ
```
- Hashtag bỏ dấu chấm: 0.11.3 → `#CapNhat0113`.
- Mục `###` có emoji theo hệ: 💎 Tinh Thạch, 🎒 Hành trang, ⚡ Đột phá, ⚗️ Luyện đan, 🏪 Tạp Hóa bà Dĩm, 🏆 Thiên bảng, 🐾 Linh thú, 🛠️ Sửa lỗi…
- Từ 3 dòng số liệu trở lên (giá, tỉ lệ) → bảng Markdown.

### A3. Nội dung
- Chỉ điều người chơi nhận thấy: tính năng, chỉ số, giá, tỉ lệ, sửa lỗi. Bỏ refactor, test, cache/hạ tầng server (trừ khi người chơi cảm nhận được, vd "tải nhanh hơn").
- Dùng tên trong game, không dùng tên biến/file. Con số lấy từ `Game/*.cs`, không ước đoán. Không hứa tính năng chưa có.

---

## B. Phát hành phiên bản

Cơ chế (`.github/workflows/build-release.yml`): push tag `vX.Y.Z` → build game + Launcher (win-x64, self-contained) → GitHub Release `VanDao_vX.Y.Z.zip` + `NguoiLuoiTuTienLauncher-v<LauncherVersion>.zip` → cập nhật Supabase `app_config` (`latest_version`, `download_url`, `message` = **dòng tiêu đề của tag**) → gọi `deploy-server.yml` deploy backend online (Render). Phiên bản game lấy từ tag, không sửa `VanDao.csproj`.

### B1. Kiểm tra trước khi tag (báo kết quả cho người dùng)
1. **Nhánh**: tag trên `master` đã chứa mọi PR của bản này: `git fetch origin && git log --oneline origin/master -5`. Các PR còn mở cần merge trước (hỏi người dùng). Working tree không cần sạch vì tag trỏ vào commit, nhưng tag phải đặt trên `origin/master`, không đặt trên nhánh feature.
2. **Số phiên bản**: lớn hơn tag mới nhất (workflow từ chối công bố bản cũ hơn). Patch `+0.0.1` cho sửa/cân bằng; minor khi có hệ thống mới — hỏi nếu chưa rõ.
3. **Ghi chú**: `git show origin/master:docs/releases/upcoming.md | head -3` phải có tiêu đề đúng `X.Y.Z`. Sai/thiếu → làm phần A, merge vào master trước.
4. **Test**: `dotnet test test/GemSystemTests` (toàn bộ) và `node --test test/` nếu có thay đổi JS. Lỗi → dừng, báo người dùng.
5. **Launcher**: `git diff --stat vPREV origin/master -- NguoiLuoiTuTienLauncher` có thay đổi → phải tăng `<Version>` trong `NguoiLuoiTuTienLauncher/NguoiLuoiTuTienLauncher.csproj` (nếu không, Launcher cũ không tự cập nhật). Không đổi → giữ nguyên.
6. **Migration DB**: `git diff --stat vPREV origin/master -- Online/Migrations supabase/migrations`. Có thay đổi → **cảnh báo người dùng**: workflow sẽ bỏ qua deploy server; người dùng phải áp migration lên Supabase cloud, rồi chạy tay workflow **Deploy Online Server** với version `vX.Y.Z` và tick *migrations_applied*:
   `gh workflow run deploy-server.yml -f version=vX.Y.Z -f migrations_applied=true` (chỉ chạy khi người dùng xác nhận đã áp migration).

### B2. Tạo và đẩy tag (cần người dùng xác nhận)
- Tag có chú thích; dòng `-m` hiện cho người chơi trong Launcher → viết ngắn, tiếng Việt, thường dùng khẩu hiệu ở tiêu đề `upcoming.md`:
  ```bash
  git tag -a vX.Y.Z origin/master -m "Người Lười Tu Tiên X.Y.Z — <khẩu hiệu>"
  git push origin vX.Y.Z
  ```
- Không xóa/di chuyển tag đã push. Build lỗi → sửa trên master, phát hành số mới; hoặc chạy lại đúng tag cũ: `gh workflow run build-release.yml -f version=vX.Y.Z`.

### B3. Sau khi push tag
- Kiểm tra một lần (không poll liên tục): `gh run list --workflow build-release.yml -L 1`, rồi `gh run watch <id>` chạy nền nếu người dùng muốn chờ.
- Thành công: `gh release view vX.Y.Z` có đủ 2 file zip; báo link release cho người dùng.
- Thất bại: `gh run view <id> --log-failed`, tóm tắt lỗi, đề xuất cách sửa; không tự tạo tag mới.
- Nhắc người dùng: bản tiếp theo cần viết lại `upcoming.md` (phần A1 bước 2).
