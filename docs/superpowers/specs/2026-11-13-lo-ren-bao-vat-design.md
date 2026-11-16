# Bảo Vật Lò Rèn (relic) — thiết kế

Bổ sung cho Lò Rèn (`docs/superpowers/plans/2026-11-13-lo-ren.md`). Đã duyệt với người dùng 13/11/2026.

## Mục tiêu
Cho người chơi chỉnh "luật" của lò rèn bằng bảo vật, và cho trang bị dư một chỗ dùng: đổi đồ lấy điểm, dùng điểm mua bảo vật trên kệ hằng ngày (phỏng theo máy relic của ForgeTax).

## Luật

| Mục | Quy tắc |
|---|---|
| Máy đổi | Chọn trang bị **trong túi** (đồ đang mặc không nằm trong túi nên không đổi được). Mỗi món mất đi, nhận **Điểm Luyện Khí = 2^phẩm** (`GameData.RarityOrder`: Phàm 1, Linh 2, Huyền 4 … Kim Tiên 1024). Chỉ nhận trang bị (`Slot` và `Base` khác null). |
| Kệ hôm nay | 3 bảo vật, chọn bằng cách xáo 10 bảo vật theo (Id nhân vật, ngày giờ VN, số lần làm mới) — cố định, tải lại vẫn ra cùng kệ. Bỏ qua bảo vật đang có trên kệ bảo vật, trừ món vừa mua hôm nay (hiện "Đã mua"). Mua bằng điểm; món đã mua không mua lại trong ngày. |
| Làm mới kệ | 20.000 linh thạch mỗi lần; xáo lại, danh sách "đã mua hôm nay" xóa. Sang ngày mới (giờ VN): số lần làm mới và "đã mua" về 0. |
| Kệ bảo vật | Tối đa **4** món, không trùng, **vĩnh viễn**. Đủ 4 thì phải **Bỏ** 1 món trước khi mua (không hoàn điểm). Bỏ được bất cứ lúc nào. |
| Tác dụng | Mọi nhát rèn ở mọi ô; bảng "Nhát kế tiếp" hiện tỉ lệ đã tính bảo vật. |
| Tu lại từ đầu | Xóa cả bảo vật, điểm, kệ (đã xóa `ForgeState`). |

## 10 bảo vật

| Id | Tên | Hiệu ứng | Điểm |
|---|---|---|---|
| `an_ho_binh` | Ấn Hộ Binh | Tỉ lệ Vỡ nát ×0,8 | 120 |
| `kim_cuong_chuy` | Kim Cương Chùy | Đại thành +4% (bớt từ Bất biến) | 100 |
| `thien_co_do` | Thiên Cơ Đồ | Thành công +8% (bớt từ Bất biến) | 110 |
| `ban_thach_de` | Bàn Thạch Đe | Tổn hại −6% (chuyển sang Bất biến; không đổi gì ở +0) | 60 |
| `hoa_tinh_thach` | Hỏa Tinh Thạch | Mỗi nhát thường +2 Hỏa Khí (thay vì +1) | 90 |
| `phuong_vu` | Phượng Vũ | Thần Hỏa che chở 5 nhát (thay vì 3) | 90 |
| `tiet_thiet_lo` | Tiết Thiết Lô | 25% nhát rèn không tốn Tinh Thiết | 70 |
| `hoi_quang_kinh` | Hồi Quang Kính | Hồi Thiên Phù: 20% không bị tiêu | 60 |
| `linh_van_khac` | Linh Văn Khắc | Chỉ số rèn ×1,1 (vd +50: +250% → +275%) | 150 |
| `bat_diet_hon` | Bất Diệt Hồn | Vỡ nát chỉ mất nửa cấp (cấp mới = cấp / 2, làm tròn xuống) | 200 |

Kết hợp xấu nhất cho Bất biến: 15% − 4% − 8% + 6% = 9% ≥ 0, nên không cần chặn thêm.

## Lưu trữ
- Mọi thứ thêm vào JSON `Player.ForgeState` (`ForgeSave`): `Relics`, `Points`, `ShopDay`, `Rerolls`, `Bought`. **Không migration DB.**
- `Stats` không đọc JSON: khi có Linh Văn Khắc, `Store` ghi thêm dấu `linhvan:1` vào `Player.ForgeLevels`; `Stats` nhân % chỉ số rèn ×1,1 khi thấy dấu này. `Seeded` (dựng lại khi JSON hỏng) bỏ qua khóa không phải ô trang bị.

## Giao diện (tab Lò Rèn)
- Dải **Kệ bảo vật** 4 ô dưới khu đe: tên + hiệu ứng ngắn; nút **Bỏ** cần bấm 2 lần (lần đầu đổi chữ thành "Bỏ thật?").
- Nút **Máy đổi bảo vật · N điểm** mở cửa sổ: trái là trang bị trong túi (mỗi dòng: tên, số lượng, điểm/món, nút **Đổi 1** và **Đổi hết**); phải là **Kệ hôm nay** 3 thẻ (tên, hiệu ứng, giá, nút **Mua** / "Đã mua" / "Đã có") và nút **Làm mới · 20.000 linh thạch**.
- Danh sách bảo vật (tên, mô tả, giá) và bảng điểm theo phẩm nằm trong `/data` (tĩnh), không gửi mỗi lần đồng bộ.

## Ngoài phạm vi
Cân bằng điểm/giá theo dữ liệu chơi thật; bảo vật nhiều bậc; nguồn điểm khác ngoài máy đổi.
