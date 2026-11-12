## 🏯 Người Lười Tu Tiên 0.14.0 — Khai sơn lập phái, Nhiệm vụ ngày mỗi sáng!

Đạo hữu ơi, bản này mở **Tông Môn** (lập phái, Boss tông, sơn môn), thêm **Nhiệm vụ ngày · Hoạt Lực**, và làm lại hiệu ứng phụ công pháp cùng hoạt ảnh trạng thái cho quái. Cập nhật Launcher và vào game thôi! 👇

### 🌀 Hiệu ứng phụ công pháp
- **Tỉ lệ hiệu ứng phụ giờ đến chủ yếu từ bị động**: chiêu chủ động chỉ còn **1% mỗi cấp** (tối đa 5%). Mỗi cấp **bị động cùng hệ** cộng thêm **0,5%**. Tổng tối đa **50%**; riêng **Choáng, Tê liệt, Đóng băng tối đa 25%** (đòn thường tối đa 12,5%).
- Tooltip luôn hiện tỉ lệ thật (chủ động + bị động). Hiệu ứng lên quái chỉ có tác dụng khi **kích hoạt công pháp cùng hệ**; nếu chưa kích hoạt, tooltip ghi rõ "Chưa có tác dụng".
- **Mộc**: đổi Hồi phục → **Nhiễm độc**: mất **3% máu tối đa mỗi lượt trong 4 lượt** (tổng 12%), yếu hơn mỗi lượt nhưng bền hơn **Thiêu đốt** (5% × 2 lượt = 10%). Hai hiệu ứng vẫn đè nhau.
- **Hiệu ứng bất lợi xử lý ở lượt của quái, trước khi quái hành động**: Choáng / Tê liệt / Đóng băng khiến quái bỏ lượt rồi mất hiệu ứng; Thiêu đốt / Nhiễm độc trừ máu ngay trước khi quái ra đòn (lượt gây hiệu ứng tính là lượt đầu), quái chết vì đốt / độc thì không kịp đánh.
- **Chiêu chủ động Mộc vẫn hồi máu**: ngoài Nhiễm độc, mỗi lần tung chiêu hồi **5% máu tối đa** cuối lượt nếu còn sống.
- **Chiêu chủ động Thủy hút máu**: mỗi lần tung chiêu hồi **10% sát thương gây ra**, tối đa **8% máu tối đa** mỗi lần. (Hồi máu / hút máu chưa áp dụng ở Lôi đài.)
- **Phong hộ**: ngoài +20 né tránh, nay cộng thêm **+20 chính xác** trong 2 lượt.
- Áp dụng cho cả Lôi đài.
- **Đòn thường cũng gây hiệu ứng bất lợi**: mỗi đòn thường trúng có tỉ lệ bằng **½ tỉ lệ chiêu chủ động cùng hệ cao nhất** (tối đa **25%**) gây Thiêu đốt / Suy yếu / Nhiễm độc / Choáng / Tê liệt / Đóng băng theo hệ công pháp đang kích hoạt. Liên kích không gây; Phong hộ (hệ Gió) không áp dụng cho đòn thường. Tooltip đòn thường hiện tỉ lệ hiện tại. Chỉ áp dụng khi thám hiểm / đánh ải, chưa áp dụng cho Lôi đài.
- **Nhiễm độc** có hoạt ảnh riêng (khói độc xanh), tách khỏi Thiêu đốt.
- **Mỗi lúc quái chỉ mang một hiệu ứng bất lợi**: hiệu ứng kích hoạt sau (từ chiêu, đòn thường, trang bị hay linh thú) đè và xóa hiệu ứng trước, kể cả choáng đầu trận của chiêu Khống. Hình lửa / khối băng / sao choáng cũ tắt ngay khi hiệu ứng mới xuất hiện.
- **Nhãn hiệu ứng dưới thanh máu quái**: Thiêu đốt, Nhiễm độc, Suy yếu, Choáng, Tê liệt, Đóng băng hiện thành nhãn màu ngay dưới thanh máu (trước đây khống nằm cạnh tên, Suy yếu và Nhiễm độc không có nhãn).
- **Hiện theo lượt**: mỗi lượt quái mất máu vì Nhiễm độc / Thiêu đốt hiện số trừ máu riêng (độc màu xanh, đốt màu cam); mỗi lượt quái còn Suy yếu hiện chữ "Suy yếu".

| Tỉ lệ hiệu ứng phụ | Cũ | Mới |
|---|---|---|
| Chiêu chủ động Lv5, chưa có bị động | 50% | **5%** |
| Chủ động Lv5 + 10 bị động cùng hệ max | 50% | **30%** |
| Chủ động Lv5 + đủ bị động cùng hệ | 50% | **50%** (khống: **25%**) |

### 📜 Nhiệm vụ ngày · Hoạt Lực (mới)
- Bảng **Sự kiện** có thêm mục **Nhiệm vụ ngày**, mở khi xong chuỗi nhiệm vụ tân thủ.
- Mỗi ngày 9 việc cố định: nhận quà đăng nhập, hạ 50 quái, hạ 1 Boss chương, khai 3 lò đan, luyện khí 2 lần, quay 10 lượt (Tài Xỉu hoặc Linh Thú), đấu 5 trận Lôi đài, giải 1 Kỳ duyên, cho linh thú ăn. Việc **tự hoàn thành** khi đủ số lần, cộng **Hoạt Lực** ngay (tối đa 115/ngày).
- **5 rương ngày** ở mốc 20 / 40 / 60 / 80 / 100 Hoạt Lực. Hoạt Lực mỗi ngày cộng dồn vào điểm tuần, mở **4 rương tuần** ở mốc 150 / 300 / 450 / 600.
- Quà gồm linh thạch và Bổ Nguyên Đan **tăng theo cảnh giới**, cùng vé Tài Xỉu, vé Linh Thú, Linh Thú Đan, Hóa Hình Đan và Huyền Thú Đản. Rương cuối ngày (100) tặng thêm **5 Cực phẩm linh thạch**, rương cuối tuần (600) tặng **20 Cực phẩm linh thạch**.
- Ngày mới lúc 00:00 giờ Việt Nam, tuần mới sáng thứ Hai. Cần online để tính tiến độ và nhận rương.

### 🏯 Tông Môn (mới)
- Thanh menu có thêm tab **Tông Môn**. Từ **Trúc Cơ** trở lên có thể **khai sơn lập phái** (phí bằng phí đột phá cảnh giới hiện tại, tối thiểu 10.000 linh thạch) hoặc **xin vào** tông có sẵn. Tông không giới hạn số thành viên.
- Tông chủ / Trưởng lão duyệt đơn; Tông chủ có thể bật **tự duyệt** kèm cảnh giới tối thiểu. Mỗi người chờ tối đa 3 đơn cùng lúc.
- **Chức vị**: Tông chủ (duyệt, trục xuất, phong / bãi Trưởng lão, nhường chức, nâng cấp tông), **Trưởng lão** tối đa 2 người (duyệt đơn, trục xuất đệ tử, sửa thông báo), đệ tử.
- Rời tông hoặc bị trục xuất phải **chờ 30 phút** mới vào tông khác; cống hiến chưa tiêu được giữ lại. Tông chủ vắng mặt quá 7 ngày sẽ được người còn hoạt động kế nhiệm (ưu tiên Trưởng lão).
- **Cống hiến**: điểm danh mỗi ngày +10; quyên linh thạch 3 lần/ngày, mức Nhỏ / Vừa / Lớn (2% / 5% / 10% phí đột phá) nhận +10 / +30 / +70. Mỗi điểm cống hiến cũng cộng vào **quỹ tông**.
- **Cấp tông** (Tông chủ dùng quỹ để nâng): tốc độ tu luyện cho cả tông **+2% / +4% / +6% / +8% / +10%** ở cấp 1–5, quỹ cần 500 / 1.500 / 4.000 / 10.000.
- **Bảng xếp hạng Tông Môn**: ở **Chính Điện** có nút **Xem danh sách Tông Môn**, hiện bảng xếp hạng theo cấp tông, rồi quỹ, rồi số thành viên (kèm Tông chủ); tông của bạn được tô sáng.
- **Cửa hàng cống hiến**: đổi Linh Thú Đan, Bổ Nguyên Đan (Hạ / Trung / Thượng), vé Tài Xỉu, vé Linh Thú, Hóa Hình Đan, **Thượng Cổ Thú Đản** (tông cấp 5); giới hạn mỗi tuần: **Linh Thú Đan 10.000**, các loại đan và vé **100**, Thượng Cổ Thú Đản và bí kíp 1; tông cấp cao mở thêm món. Từ **tông cấp 4** đổi được **bí kíp bị động Luyện Hư** cả 7 hệ (400 cống hiến, mỗi hệ 1 cuốn/tuần).
- Khung chat có thêm tab **Tông Môn** chỉ đồng môn thấy, kèm tin báo khi có người vào / rời, lên chức, tông lên cấp.
- **Sơn môn**: tab Tông Môn giờ là bức tranh sơn môn, bấm từng công trình để vào: **Chính Điện** (cấp tông, thông báo, cài đặt), **Đệ Tử Phòng** (thành viên, đơn xin vào), **Tàng Bảo Các** (cửa hàng cống hiến), **Đan Phòng** (điểm danh, quyên góp), **Bệ Phong Ấn** (Boss tông). Công trình còn việc cần làm sẽ có chấm đỏ.
- **Boss tông hằng ngày** (Bệ Phong Ấn): mỗi ngày cả tông cùng đánh một Boss chung, máu co theo số đồng môn đang hoạt động (Boss rất trâu: cả tông phải góp sức mới hạ được trong ngày; cống hiến mỗi lượt tính theo phần máu Boss đã đánh). Mỗi người có **2 lượt/ngày**; mỗi lượt là một trận tối đa **15 lượt** với bản sao Boss của chính bạn (công Boss **tăng thêm sau mỗi 4 lượt**: 4 lượt đầu nhẹ tay để người yếu nhất cũng có thời gian ra đòn), sát thương gây ra cộng vào máu chung. Mỗi lượt nhận cống hiến (5–30 điểm, cũng vào quỹ tông) theo phần sát thương đóng góp.
- **Hạ Boss trước 0 giờ**: mọi người đã đánh ít nhất 1 lượt nhận **1 rương** (nhận trong ngày): chắc chắn có linh thạch, 2 Bổ Nguyên Đan Trung phẩm, 50 Linh Thú Đan; cơ hội ra Bổ Nguyên Đan Thượng phẩm (20%), vé Tài Xỉu (10%), vé Linh Thú (5%), Ngộ Đạo Đan (3%, từ Nguyên Anh), Hóa Hình Đan (2%), 5 Cực phẩm linh thạch (1%), trang bị phẩm **Đạo** (1%) hoặc **Huyền Tiên** (0,5%) theo bộ cảnh giới, **top 3 sát thương** thưởng thêm **+50 / +30 / +20** cống hiến, cả tông được **+3% tu vi** suốt ngày hôm sau. Không hạ kịp thì không có rương và buff.

### 🔱 Phẩm 11 · Kim Tiên (mới)
- Trang bị và đan dược giờ có **11 phẩm**. Trang bị thêm phẩm **Kim Tiên** (hệ số **×9,4**, cao hơn Huyền Tiên ×7,5; Tu vi thám hiểm **+25%**), viền **Tử Kim** riêng.
- Phẩm Kim Tiên chỉ lên được bằng **Luyện khí**: ghép 9 trang bị **Huyền Tiên** cùng loại, tỉ lệ thành công **10%** (thất bại còn 1 món Huyền Tiên). Bộ đủ 10 món phẩm Kim Tiên vẫn giữ năng lực bộ Huyền Tiên.
- Luyện đan có **Thập Nhất Phẩm - Luyện Đan Thánh Sư**; **Kim Tiên Đan** là đan Thập Nhất Phẩm. EXP lên phẩm 11 nhân **2,25**.

### 🏆 Lôi đài
- **Xem người chơi từng hạng**: bấm vào một nấc trên thang hạng (Đồng → Cao Thủ) để xem người chơi ở hạng đó, xếp theo điểm; bấm lại hoặc **Đóng** để thu gọn. Khối Tam cường đã bỏ, đối thủ cùng hạng (kể cả top đầu) nằm chung danh sách khiêu chiến, mỗi người hiện điểm.
- **Giao diện tab Lôi đài làm mới**: huy hiệu và thanh điểm theo hạng, bốn ô chỉ số (hạng trong hạng, chiến tích, lượt hôm nay, điều tức), thang hạng Đồng → Cao Thủ, thưởng ngày / tuần và danh sách đối thủ có avatar tròn kèm dòng giải thích kết quả. Nút đánh luôn ghi **Khiêu chiến** (hoặc **Ngoài tầm** / **Xếp dưới** / **Hết lượt** khi chưa đánh được); không còn tỷ thí.
- Đánh Lôi đài không còn diễn ở màn hình chính: ngay trong tab Lôi đài hiện hai **avatar tròn lao vào nhau**, có **thanh máu** giảm theo từng đòn và nhật ký diễn biến (có nút **Bỏ qua**).
- **Cách đánh mới áp dụng cho Boss tông**: bấm Tấn công mở cùng popup trận (ấn tròn, thanh máu, nhật ký nguồn sát thương); thanh máu Boss là **máu chung của cả tông**.
- **Lôi đài chia hạng theo điểm**: Đồng (0) → Bạc (100) → Vàng (250) → Bạch Kim (450) → Kim Cương (700) → Cao Thủ (1.000). Mỗi hạng có **bậc thang riêng**, chỉ thách đấu được người **cùng hạng**; **chỗ đứng trong hạng xếp theo điểm**, khiêu chiến không đổi chỗ.
- **Điểm hạng**: thắng khiêu chiến **+25 đến +40** (đối thủ xếp trên mình càng xa càng nhiều), thua **−15** (không xuống dưới 0). Mọi trận cùng hạng đều tính điểm, kể cả đánh người xếp dưới (+25 khi thắng, không đổi chỗ). Lên hạng được **bảo vệ 3 trận thua** và vào cuối bậc thang hạng mới; rớt dưới mốc thì xuống hạng.
- **Thưởng theo hạng**: thưởng **ngày** (đã đánh ít nhất 1 trận thật hôm nay) và thưởng **tuần** (nhận vào **Chủ nhật**, đã đánh trong tuần), phần thưởng tăng theo hạng. Nhận ngay trong tab Lôi đài.
- Danh hiệu **Lôi Đài Bá Chủ** thuộc người điểm cao nhất.
- Máy tự tính cả trận giữa hai người chơi với **đầy đủ chỉ số, kỹ năng và linh thú xuất chiến**; kết quả và hạng mới báo ngay cuối trận.

### ⚡ Đột phá
- **Yêu hạch Boss chỉ còn rơi 20%** mỗi lần hạ Boss chương (trước đây chắc chắn rơi), cả khi tự đánh lẫn linh thú nhặt được.

### 🎨 Giao diện
- **Hiệu ứng ô trang bị phẩm cao mới**: phẩm **Đạo** dùng viền hổ phách **Nhật Diệu** (hai vệt sáng lướt quanh viền), phẩm **Huyền Tiên** dùng **Thất Sắc Kim Quang** (viền cầu vồng, góc vàng trắng, hào quang tím nhạt).
- **Công pháp Kim Tiên có icon riêng**: 21 công pháp Kim Tiên của cả 7 hệ (Thần Tướng Trảm, Bất Hủ Kim Thể và bị động học từ bí kíp) dùng icon mới, không còn mượn icon Chân Tiên phủ vàng.
- **Hiệu ứng trạng thái mới**: quái bị **Thiêu đốt, Đóng băng, Tê liệt, Choáng** giờ có hoạt ảnh riêng (lửa bám chân, khối băng, hồ quang điện, sao choáng), hiện lên khi dính và tan dần khi hết hiệu lực.

### 🛠️ Sửa lỗi
- **Kim Tiên Đan** hiện đúng phẩm **Thập Phẩm** (ngang Chân Tiên Đan), không còn hiện "undefined Phẩm" và luyện được ở cấp luyện đan tối đa.
- **Thám hiểm ×2**: tuyệt kỹ giờ diễn nhanh theo nhịp ×2, khớp với trận thật. Trước đây hình ảnh chậm hơn trận thật: rương Boss về khi trận chưa diễn xong, đánh xong lại nhảy cóc qua vài ải (thấy rõ ở Địa Ngục).
- **Tuyệt kỹ đủ sát thương mà quái không chết**: khi linh thú đang thiêu đốt quái (Chu Tước, Thái Cổ Hỏa Kỳ Lân) hoặc vừa tung kỳ tích, đòn tu sĩ đủ hạ quái nhưng thanh máu vẫn treo một đoạn, phải chờ linh thú đánh thêm mới chết. Nay đòn tu sĩ hạ được quái thì quái chết ngay.

Hẹn gặp đạo hữu trên Thiên bảng! ⚔️

#NguoiLuoiTuTien #TuTien #GameIdle #CapNhat0140
