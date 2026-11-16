# Kết quả triển khai thanh nộ và tuyệt kỹ quái

Đã triển khai ngày 28/10/2026 theo kế hoạch được duyệt trong chat.

- Máy chủ: `MonsterRageCombat` giữ state theo đợt; Boss +25, tinh anh +20 sau phản công thực sự. Đầy 100 thì lượt phản công hợp lệ tiếp theo tung tuyệt kỹ, về 0. Quái thường trả -1 để không vẽ nộ.
- Tuyệt kỹ nhân sát thương phản công cơ sở sau trừ thủ: Boss 1,7; tinh anh 1,4. Né, vô hiệu, khiên và giảm sát thương tiếp tục đi qua luồng có sẵn. Không nhân thêm Nham Bộc cũ ở Boss/tinh anh; quái thường Địa Ngục giữ cơ chế cũ.
- Replay: bổ sung `MonsterRage` và `TimingHits` cuối DTO, đều tùy chọn. Giữ đủ lượt ở đợt Boss/tinh anh (mô phỏng tối đa 150 lượt cả trận), dùng số lượt lấy mẫu cũ để tính thời gian chuyến. Client nén các lượt vào thời gian có sẵn.
- Hình ảnh: thanh nộ dưới máu, nhãn tụ lực, ba nhóm tuyệt kỹ (Cuồng Thú Phá, Liệt Địa Trảm, Huyền Linh Bạo), thêm nhấn nhịp chuẩn bị cho đòn thường. Đọc trạng thái máy chủ ở cùng mốc va chạm 0,85; hỗ trợ dữ liệu cũ, PvP, strip và giảm chuyển động.

## Kiểm chứng

- `dotnet test test/GemSystemTests/GemSystemTests.csproj -p:UseAppHost=false -p:OutputPath=D:/projects/van-dao/VanDao/build-check/monster-rage/`: 76 qua, 0 lỗi.
- `node --test test/monster-rage-client.test.cjs`: 5 qua, 0 lỗi.
- Các file JavaScript thay đổi qua `node --check`; `git diff --check` không có lỗi khoảng trắng.
- Rà soát độc lập phát hiện nhân hệ số trước trừ thủ; đã sửa và thêm kiểm tra trận thật với sát thương cơ sở 10, tuyệt kỹ Boss 17. Rà soát lại xác nhận đã giải quyết.
- Đã dựng và xem 16 snapshot bằng các hàm Canvas thật, với sprite thật: full/panel/strip/giảm chuyển động × tụ lực/ba nhóm tuyệt kỹ. Không có lỗi trang. Đây là harness riêng, chưa phải phiên game trực tiếp. Ảnh: `build-check/monster-rage-visual.png`.

## Giới hạn kiểm chứng cân bằng

`tools/hell_map_checks` dừng ở ngưỡng Boss Địa Ngục chương 4, yêu cầu Chân Tiên Sơ kỳ q5 thắng ổn định. Cùng lỗi được tái hiện bằng bản đối chứng giữ hệ số tuyệt kỹ bằng 1 và phục hồi Nham Bộc cũ, trên cùng dữ liệu workspace. Không sửa các ngưỡng kiểm tra hay chỉ số quái để che lỗi. Các kiểm tra phía sau điểm dừng chưa được xác nhận bởi lượt chạy này.

Log nằm trong thư mục build-check: `monster-rage-tests.log`, `monster-hell-final.log`, `rage-baseline-final.log`; thư mục này được Git bỏ qua.
