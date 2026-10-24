# Lôi đài (PK giữa người chơi) + Danh hiệu Đệ nhất

Tab **Lôi đài** nằm trong **Thiên bảng** (nút Bảng xếp hạng), cạnh Lực chiến / Cảnh giới / Vượt ải / Linh thú.

## Cài đặt

Dùng chung project Supabase và cấu hình `Leaderboard` (Url, AnonKey) trong `appsettings.json`.
Chạy `supabase.sql` trong thư mục này ở **SQL Editor** (sau `docs/version` và `docs/leaderboard`; chạy lại an toàn).
Chưa chạy SQL thì tab Lôi đài báo cần chạy SQL, các bảng xếp hạng cũ vẫn dùng bình thường (không có danh hiệu).

## Luật

| Luật | Giá trị | Sửa ở |
|---|---|---|
| Vào Lôi đài | lần đầu mở tab: đứng cuối bậc thang | `arena_join` |
| Tầm khiêu chiến | người xếp trên, trong `max(5, 20% hạng hiện tại)` hạng | `arena_rules()` (min_window, window_pct) |
| Tỷ thí | đánh bất kỳ ai xếp dưới: không đổi hạng, không tính thắng / thua, vẫn tốn lượt và thời gian nghỉ, có ghi nhật ký | `arena_begin` (`practice`) |
| Kết quả | thắng: **đổi vị trí** với đối thủ · thua: giữ nguyên hạng (đối thủ không mất gì) | `arena_begin` → `arena_finish` |
| Lượt | 15 trận khiêu chiến / ngày, làm mới 0h giờ Việt Nam · nghỉ 20 giây giữa hai trận | `arena_rules()` + `ArenaService.Daily` |
| Bị chặn | nhân vật admin mode (`Tainted`, trừ môi trường dev) · dữ liệu gắn cờ (`flagged`) | `GameService.ArenaBlocked`, `arena_auth` |

Giao diện: **Tam cường** (top 3) luôn hiện ở đầu; bên dưới là **Đối thủ gần hạng bạn**: bản thân, mọi người trong tầm phía trên
và 7 người có hạng gần nhất (trên hoặc dưới, trừ Tam cường). Người xếp dưới có nút **Tỷ thí** thay cho Khiêu chiến.
Nhật ký Lôi đài ghi cả trận mình khiêu chiến và trận bị người khác khiêu chiến (20 trận gần nhất).

## Trận đấu

Trận có đủ kỹ năng nên **game tính** (`Game/PvpCombat.cs`), Supabase giữ luật, thứ tự và hạng:

1. `arena_begin`: kiểm tra lượt / thời gian nghỉ / tầm, cấp **seed** ngẫu nhiên, lưu ảnh chụp của mình và trả ảnh chụp của đối thủ.
2. Game mô phỏng trận bằng seed đó (`PvpCombat.Run`).
3. `arena_finish`: báo thắng / thua cho đúng trận vừa cấp (một lần, trong 3 phút); thắng thì đổi hạng.
   Trận bắt đầu mà không báo kết quả vẫn tốn một lượt và tính là thua.

**Ảnh chụp** (`PvpFighter`, cột `arena_ranks.loadout`): chỉ số, đòn thường + tuyệt kỹ trong ô (kèm cấp), công pháp đang kích hoạt,
bị động (sát thương chiêu, phá giáp, thiêu đốt, hồi Mộc), hiệu ứng trang bị theo cấp (khiên, địa chấn, thiên lôi, hồi khí, phản đòn, liên kích, tĩnh tâm…),
thần thông bộ đồ, trang phục. Game gửi nền mỗi khi thay đổi (tối đa 30 giây / lần) và gửi kèm mỗi lần khiêu chiến.
Ảnh chụp của đối thủ được kẹp về giới hạn của game; chỉ số gốc lấy theo bảng xếp hạng.
Đối thủ **chưa có ảnh chụp** (bản game chưa có Lôi đài) mang **bộ kỹ năng giống hệt người khiêu chiến** với chỉ số thật của họ
(`PvpFighter.MirrorOf`), nên thắng thua chỉ do chỉ số; nhật ký trận ghi rõ việc này. Chỉ đánh bằng chỉ số + Hỏa Cầu thì đối thủ yếu hơn thực tế rất nhiều.
Linh thú chưa tham chiến (chỉ số cộng từ linh thú vẫn tính).

Luật mỗi lượt của một bên giống tu sĩ đánh quái:

- Bên thân pháp cao đi trước (bằng nhau: bên khiêu chiến). Máu hai bên **×3** trong Lôi đài để trận đủ dài cho tuyệt kỹ.
- Thiêu đốt → thiên lôi → địa chấn (lượt đầu) → **tuyệt kỹ khi đủ 100 nộ**, không thì đòn thường (+25 nộ) → liên kích.
- Sát thương = đòn² / (đòn + thủ đối thủ sau phá giáp); trừ tiếp khiên % và giảm công của đối thủ; bạo kích ×(1 + hệ số bạo).
  Cùng cấp thì cân, chênh lệch lớn thì bên mạnh áp đảo (khác đánh quái: đòn − thủ).
- Đối thủ được **né** (thân pháp + phong hộ, tối đa 50%) và **vô hiệu** (thần thông 7 / 9) cả lượt ra đòn.
- Hiệu ứng phụ của tuyệt kỹ: choáng / tê liệt / đóng băng làm đối thủ mất lượt kế (đóng băng: đòn sau +10%), suy yếu −20% sát thương 2 lượt,
  phong hộ +20% né 2 lượt, hồi 5% máu, thiêu đốt 2 lượt.
- **Thiêu đốt** mỗi lượt = 5% máu gốc của đối thủ nhưng không quá 50% công người đốt (đánh quái: 5% máu quái).
  Không giới hạn thì đối thủ càng trâu càng mất nhiều, người yếu hơn hẳn vẫn thắng nhờ đốt.
- **Sát khí**: từ hiệp 16, sát thương trực tiếp hai bên +15% mỗi hiệp, để trận kết thúc bằng hạ gục thay vì so % máu.
- Tối đa 50 hiệp; hết hiệp chưa ai gục thì bên còn nhiều % máu hơn thắng (bằng nhau: bên thủ thắng).

### Màn hình chính

Bấm **Khiêu chiến** ở tab Lôi đài: bảng báo kết quả, cửa sổ desktop tự đóng bảng; trận diễn ở **màn hình chính** (dải taskbar / cửa sổ chính)
bằng chính hoạt ảnh trận đánh quái (`drawPixelBattle`, `wwwroot/js/arena-stage.js`): mình bên trái tung đủ chiêu và hiệu ứng,
đối thủ (tu sĩ quay mặt sang trái) lao tới phản đòn, hiện tên chiêu, bạo kích, né, khống, hồi máu. Trận dài được lấy mẫu tối đa 14 lượt.
Server giữ trận vừa đánh 2 phút trong `StateDto.Arena`; mỗi trận chỉ diễn một lần, diễn xong quay lại thám hiểm.

## Danh hiệu

Người hạng 1 của mỗi bảng nhận danh hiệu, hiện cạnh tên ở mọi bảng xếp hạng, thẻ Đệ nhất, Lôi đài và hồ sơ trang bị.
Danh hiệu đi theo hạng: mất hạng 1 thì mất danh hiệu (view `titles_public`, cache 20 giây).

| Bảng | Danh hiệu |
|---|---|
| Lôi đài | Lôi Đài Bá Chủ |
| Lực chiến | Chiến Lực Vô Song |
| Cảnh giới | Cảnh Giới Chí Tôn |
| Vượt ải | Phá Thiên Giả (cần đã vượt ít nhất 1 ải) |
| Linh thú | Vạn Thú Chi Vương (cần có linh thú) |

Tên và thứ tự hiển thị: `Titles.All` trong `Game/Arena.cs`. Danh hiệu hiện chỉ để hiển thị, không cộng chỉ số.

## Giới hạn cần biết

- Chỉ số, ảnh chụp và **kết quả trận** do game trên máy người chơi gửi lên: người rành kỹ thuật có thể báo thắng giả.
  Supabase chỉ chặn được: dùng quá lượt, đánh ngoài tầm, chọn lại seed (mỗi seed tốn một lượt), báo kết quả hai lần / quá hạn.
  Đổi hạng vẫn nguyên tử (khóa hai dòng, ràng buộc `pos` duy nhất).
- Nhân vật bị xóa khỏi bảng xếp hạng (admin mode) tự rời Lôi đài (`on delete cascade`); hạng những người sau tự dồn lên.
- Xóa / chỉnh tay: Supabase → Table Editor → `arena_ranks` (cột `pos` càng nhỏ càng cao), lịch sử ở `arena_fights`.

## Lôi đài giả để thử (chỉ dev)

Chạy ở môi trường Development với cờ `Arena:Mock=true`, ví dụ:

```
dotnet run --launch-profile "NguoiLuoiTuTien (trình duyệt)" -- --web --Arena:Mock=true
```

- 18 đối thủ giả: Luyện Hư và Hóa Thần (Sơ / Trung / Hậu kỳ, mỗi kỳ 3 người), mặc đồ thật của cảnh giới đó, phẩm Linh → Địa ngẫu nhiên,
  cộng 0–25% cho công pháp / linh thú. Xếp hạng theo lực chiến; nhân vật của bạn vào cuối bậc thang.
- Đối thủ giả mang đòn thường + tuyệt kỹ mạnh nhất đã mở của một hệ (Hỏa / Thủy / Mộc) và vài hiệu ứng trang bị; trận tính bằng `PvpCombat` như bản thật.
  Bỏ thời gian nghỉ, 99 lượt / lần chạy. Hạng 1 giả mang danh hiệu Lôi Đài Bá Chủ.
- Không gọi Supabase (không gửi điểm, không đọc danh hiệu thật); dữ liệu nằm trong bộ nhớ, tắt game là mất.
- Bản phát hành (Production) bỏ qua cờ này. Mã: `Game/ArenaMock.cs`.
