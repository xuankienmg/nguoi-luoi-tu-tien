# Kế hoạch hiệu ứng đòn quái, thanh nộ và tuyệt kỹ

Trạng thái: bản đề xuất để duyệt; chưa triển khai code. Ngày: 28/10/2026.

## Mục tiêu

Đòn đánh của quái dễ nhận biết hơn và có cảm giác trúng đòn rõ hơn. Boss và tinh anh có thanh nộ, báo trước tuyệt kỹ và tung chiêu khi đủ nộ. Giữ nhịp chơi tự động; không yêu cầu thao tác né thủ công.

## Hiện trạng đã khảo sát

- `wwwroot/js/battle.js` điều phối pha đánh và phản công, số sát thương, thanh máu; hiện đã vẽ nộ của tu sĩ.
- `wwwroot/js/battle-fx.js` chứa hiệu ứng và kiểu đánh của quái qua `monStyleOf` / `monAttack`.
- `Game/GameService.cs` mô phỏng trận PvE, sát thương phản công và lấy mẫu lượt trước khi trả về máy khách.
- `Game/Dtos.cs` có `CounterDamage`, `MonsterSkills`, `EnrageTurn`; chưa có trạng thái nộ riêng của quái.
- `Game/HellMonsterSkills.cs` có bốn cơ chế Địa Ngục, trong đó Nham Bộc đã tích lực rồi phản công mạnh. Phải thống nhất với tuyệt kỹ mới để tránh hai đòn tăng sát thương chồng nhau.

## Phương án

1. Chỉ thêm thanh nộ và hiệu ứng: ít thay đổi cân bằng, nhưng thanh nộ không có ý nghĩa chiến đấu.
2. **Đề xuất chọn:** thêm cơ chế nộ thật ở máy chủ, một tuyệt kỹ mỗi nhóm quái; hiệu ứng bám sự kiện trận đánh. Có thể kiểm chứng và mở rộng dần.
3. Tuyệt kỹ độc quyền cho mọi Boss ngay từ đầu: đa dạng nhất nhưng khối lượng nội dung, kiểm thử và cân bằng lớn.

## Thiết kế đề xuất

### Phạm vi và nộ

- Quái thường được tinh chỉnh đòn đánh nhưng không có thanh nộ. Boss và tinh anh PvE có nộ ở các độ khó.
- Mỗi đợt quái bắt đầu từ 0/100 nộ; không chuyển nộ sang quái tiếp theo.
- Đề xuất Boss +25 nộ, tinh anh +20 nộ sau mỗi lượt thực sự phản công. Đòn né/vô hiệu vẫn tính là một lần ra đòn; lượt bị khống chế hoặc quái chết trước khi phản công không tăng nộ.
- Bản đầu không tăng nộ khi nhận sát thương: tránh việc nhiều đòn phụ, linh thú và phản sát thương làm quái tung chiêu quá dày.
- Đầy nộ: ghi trạng thái chuẩn bị. Lượt có thể phản công kế tiếp dùng tuyệt kỹ thay đòn thường, rồi về 0 nộ.
- Bị khống chế trong lượt dự kiến tung: giữ trạng thái chuẩn bị, chờ lượt phản công hợp lệ kế tiếp. Quái chết hoặc chuyển đợt: hủy chiêu.
- Đây là thông số đề xuất, chưa phải cấu hình đã được người dùng chốt.

### Sát thương và tương tác

- Đề xuất khởi đầu: tuyệt kỹ tinh anh bằng 140%, Boss bằng 170% sát thương phản công cơ sở; tách cấu hình để điều chỉnh sau mô phỏng.
- Đi qua cùng quy trình né, vô hiệu hóa, khiên, giảm sát thương và phản sát thương đang dùng. Không gây thêm một đòn thường trong cùng lượt.
- Bản đầu không bổ sung choáng, câm lặng hoặc hiệu ứng bất lợi mới. Dùng khác biệt về hình ảnh và nhịp đánh trước khi tăng độ phức tạp.
- Cuồng bạo là trạng thái riêng, không đồng nhất với nộ. Cần kiểm tra sát thương khi Cuồng bạo gặp tuyệt kỹ.
- Nham Bộc Địa Ngục chuyển sang dùng cơ chế đầy nộ cho quái có thanh nộ; không nhân thêm hệ số 150% cũ lên tuyệt kỹ. Quái thường Địa Ngục giữ cơ chế hiện có.
- Dùng chung mô phỏng cho trận hiển thị và thu hoạch ngoại tuyến; không để máy khách tự tính kết quả.

### Hiển thị và hiệu ứng

- Thanh nộ mảnh ngay dưới thanh máu quái; tinh anh màu tím, Boss màu đỏ cam. Không dựa riêng vào màu: đầy nộ có biểu tượng và chữ “Tụ lực”.
- Đòn thường: chuẩn bị ngắn → vệt đánh/đạn → chạm mục tiêu → thu chiêu; số sát thương xuất hiện cùng thời điểm va chạm.
- Nhóm hiệu ứng đầu tiên: thú cận chiến (vồ/cào), quái vũ khí (chém/quét), pháp thuật (đạn/vòng năng lượng). Dùng kiểu hiện có làm nền, ưu tiên tái sử dụng Canvas.
- Tuyệt kỹ: tăng độ rõ của tư thế chuẩn bị, hào quang tụ lực, đòn đánh đặc trưng và tên chiêu ngắn. Tinh anh gọn hơn Boss; không che thanh máu và nhân vật.
- Mốc trình diễn đề xuất trong pha phản công: 0–25% chuẩn bị, 25–60% tung chiêu, 60% va chạm, 60–100% thu chiêu. Mọi hiệu ứng cùng đọc một mốc va chạm thay vì tự hẹn giờ.
- Không kéo dài chuyến thám hiểm chỉ để phát animation. Dải taskbar dùng ít hạt và bỏ dòng tên chiêu dài; chế độ giảm chuyển động bỏ rung/nháy mạnh.
- Dữ liệu trận cũ hoặc thiếu trường mới: hiển thị đòn cũ, không báo lỗi.

## Hợp đồng dữ liệu dự kiến

Tạo `Game/MonsterRageCombat.cs` chứa cấu hình nhóm, trạng thái nộ theo đợt và các sự kiện. Đề xuất thêm vào `BattleDto` các trường tùy chọn ở cuối để giữ tương thích:

- Nộ trước và sau phản công theo lượt, giá trị 0–100.
- Trạng thái chuẩn bị tuyệt kỹ theo lượt.
- Danh sách sự kiện gồm đợt, lượt, loại `charge`/`ultimate`, mã chiêu và sát thương thực tế.

Tên trường cụ thể chốt khi triển khai sau khi đối chiếu hàm lấy mẫu. Các mảng có cùng độ dài với số lượt hiển thị. Sự kiện chuẩn bị/tung tuyệt kỹ phải được giữ lại khi lấy mẫu; không gán một chiêu bị bỏ mẫu sang lượt khác.

## Các bước triển khai

### 1. Cơ chế nộ độc lập

- [ ] Tạo `Game/MonsterRageCombat.cs`; tách state từng đợt, cấu hình nộ và hệ số sát thương.
- [ ] Thêm kiểm tra trong `test/GemSystemTests/MonsterRageTests.cs`: 4 lần phản công Boss đầy nộ, lần thứ 5 tung chiêu; tinh anh lần thứ 6; không nộ ở quái thường; không tăng khi bị mất lượt.
- [ ] Kiểm tra chết trước phản công, đầy nộ rồi bị khống chế, chuyển đợt và hai trận liên tiếp không dùng chung state.

### 2. Gắn mô phỏng PvE và sự kiện

- [ ] Sửa `Game/GameService.cs`, `Game/Dtos.cs`, `Game/HellMonsterSkills.cs` theo quy trình phản công hiện có.
- [ ] Giữ nguyên quy tắc né/khiên/vô hiệu; mỗi lượt chỉ một phản công. Ghi sát thương sau giảm trừ để hiệu ứng hiện đúng số.
- [ ] Giữ sự kiện quan trọng khi lấy mẫu; kiểm tra trận dài, nhiều đợt và trận không có lượt tung chiêu.
- [ ] Kiểm tra Nham Bộc không cộng hai hệ số; Cuồng bạo và tuyệt kỹ cho kết quả hữu hạn, không vượt giới hạn số.
- [ ] Chạy mô phỏng ở các độ khó, nhiều cấp sức mạnh và ngoại tuyến; so sánh tỉ lệ thắng, thời lượng, số tuyệt kỹ với bản cũ trước khi chốt hệ số.

### 3. Thanh nộ và phát lại

- [ ] Mở rộng hàm `drawBattleHp` trong `wwwroot/js/health-bars.js` và lời gọi trong `wwwroot/js/battle.js` để hỗ trợ nộ đối phương.
- [ ] Đọc trạng thái trước/sau đúng pha; không tự cộng nộ theo thời gian trên máy khách.
- [ ] Kiểm tra thanh nộ khi đổi đợt, phát lại, đổi kích thước, dữ liệu cũ và dải taskbar; không chồng tên/máu/biểu tượng khống chế.

### 4. Tinh chỉnh đòn thường và tuyệt kỹ

- [ ] Bổ sung cấu hình hiệu ứng trong `wwwroot/js/battle-fx.js`; gắn đòn trong `wwwroot/js/battle.js` theo nhóm hình ảnh hiện có.
- [ ] Đồng bộ va chạm, sát thương và phản hồi trúng đòn; chống sinh lặp hạt/tên chiêu khi vẽ nhiều frame hoặc phát lại.
- [ ] Giữ hiệu ứng né/vô hiệu đúng với kết quả máy chủ; không hiện trúng đòn khi đã né.
- [ ] Giới hạn số hạt theo chế độ và hỗ trợ giảm chuyển động; đo trên cảnh đông hiệu ứng cùng linh thú/kỹ năng tu sĩ.

### 5. Kiểm thử và duyệt hình ảnh

- [ ] Chạy toàn bộ `dotnet test test/GemSystemTests/GemSystemTests.csproj` với thư mục đầu ra riêng nếu game đang chạy.
- [ ] Thêm kiểm tra JavaScript cho ánh xạ sự kiện/nộ theo pha, ranh giới đợt, thiếu dữ liệu và chống phát chiêu hai lần.
- [ ] Dùng `tools/hell_map_checks` xác minh tương tác kỹ năng Địa Ngục.
- [ ] Kiểm tra trực quan ở full/panel/strip: quái thường, tinh anh, Boss, Boss Cuồng bạo, Địa Ngục, né/vô hiệu, chết trước tung chiêu.
- [ ] Chụp trước/sau để duyệt độ rõ của đòn đánh, thanh nộ và mức che khuất; chưa phát hành trước khi chốt cân bằng.

## Tiêu chí hoàn thành

- Chỉ Boss và tinh anh có thanh nộ; đầy nộ có báo trước và tuyệt kỹ tương ứng đúng kết quả mô phỏng.
- Không tung chiêu khi chết, không giữ nộ qua đợt và không cộng thêm đòn thường khi đã tung tuyệt kỹ.
- Không mất hoặc phát sai lượt sự kiện khi lấy mẫu trận dài.
- Người chơi phân biệt được đòn thường/tụ lực/tuyệt kỹ, cả khi không phân biệt màu.
- Không thay đổi PvP; dữ liệu cũ tiếp tục chạy; ngoại tuyến dùng cùng luật với trực tuyến.

## Cần duyệt trước khi triển khai

Phạm vi tất cả độ khó; cách tăng nộ chỉ theo phản công; báo trước một lượt phản công hợp lệ; hệ số thử nghiệm 140%/170%; chuyển Nham Bộc sang cơ chế nộ ở Boss/tinh anh. Có thể đổi từng điểm mà không cần bỏ cấu trúc kế hoạch.
