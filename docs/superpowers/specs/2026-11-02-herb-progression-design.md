# Phân bố linh thảo theo tiến trình — đề xuất

## Mục tiêu
Chia nguồn linh thảo theo chương, giảm việc phải quay lại nhiều vùng để luyện một viên đan. Mọi công thức phải có nguyên liệu farm được trước mốc đột phá tương ứng. Không đổi công dụng đan, chi phí linh thạch, thưởng Boss hoặc rương.

## Hướng đã chọn
Người dùng chọn phân bố theo tiến trình. Phương án mọi map rơi tất cả loại bị loại vì mất vai trò của từng vùng. Phương án giữ bảng chương cuối cho mọi mức khó bị loại vì tập trung nguyên liệu cấp cao ở mọi map.

## Nguồn rơi đề xuất
- Mọi mức khó dùng nhóm linh thảo của chính chương, giữ hệ số số lượng theo mức khó hiện hành.
- Giữ các nhóm chính theo bậc: chương 1 Tụ Linh Thảo / Huyết Căn Thảo; chương 2 Bích Ngọc / Hỏa Tâm; chương 3–4 Tử Vân / Lôi Mạch; chương 5–6 Thiên Niên / Cửu Diệp; chương 7–8 Kiến Mộc / Bất Tử; chương 9–10 Hỗn Nguyên / Âm Dương.
- Giữ nguồn nguyên liệu chính của bậc trước ở nhóm chương kế tiếp như hiện tại để chuyển tiếp thuận lợi; bổ sung Kiến Mộc ở chương 10 để đồng nhất với chương 9.
- Tẩy Tủy Thảo có ở mọi chương, mọi mức khó.
- Phụ liệu đầu game giữ nguồn hiện có; mở rộng Linh Cốt Chi đến chương 6 để trùng vùng Thiên Niên / Cửu Diệp.
- Thu gọn phụ liệu cấp cao: Địa Mạch Tiên Đằng chương 6–7; Thiên Trúc Duẩn chương 7–9; Phượng Vũ Chi chương 8–10; Hỗn Nguyên Linh Chủng chương 9–10. Giữ mốc ải và tỉ lệ gốc hiện có; chưa cân bằng bằng cách tăng đồng loạt tỉ lệ.
- Đồng đều được hiểu là có nguồn phù hợp ở mỗi bậc và ít giao thoa vô lý, không bắt buộc mọi chương có cùng số loại hay cùng tổng xác suất.

## Công thức đề xuất
Code ghép thêm phụ liệu qua HerbExpansion.ApplyRecipe, vì vậy phải tính công thức cuối cùng thay vì chỉ bảng Brews.

Bổ Nguyên Đan Thượng hiện có 5 loại linh thảo và linh thạch. Đề xuất bỏ Lôi Mạch Thảo (6); thay Kim Dương Liên (1) bằng Linh Cốt Chi (1). Công thức cuối: Tử Vân Sâm 12, Thiên Niên Nhân Sâm 3, Cửu Diệp Hồn Liên 2, Linh Cốt Chi 1, linh thạch 80.000. Giảm đúng một loại linh thảo (5 xuống 4); toàn bộ farm được tại chương 5–6, phù hợp nhóm nguyên liệu trung/cao.

Bổ Nguyên Đan Trung hiện bị chặn bởi Cửu Diệp Hồn Liên của chương 5–6. Đề xuất thay Cửu Diệp Hồn Liên 1 bằng Nguyệt Lộ Hoa 1 để toàn bộ nguyên liệu có ở chương 2. Giữ các số lượng khác và 20.000 linh thạch.

Các đan còn lại giữ công thức khi nguồn hiện có đáp ứng mốc tiến trình. Chỉ thay thêm nếu kiểm tra phát hiện thiếu nguồn trước mốc sử dụng.

## Phạm vi triển khai và kiểm chứng
Sửa bảng rơi, bộ chọn linh thảo theo độ khó, công thức, mô tả nguồn/nguyên liệu và dữ liệu wiki liên quan. Giữ nguyên thay đổi có sẵn của người dùng. Kiểm tra nguồn rơi của toàn bộ chương/mức khó, nguyên liệu của công thức sau khi ghép phụ liệu, và bộ test luyện đan hiện có. Báo rõ giới hạn: đây là cân đối nguồn, chưa mô phỏng kinh tế hay thời gian farm.

## Trạng thái
Đề xuất chờ người dùng duyệt; chưa sửa dữ liệu game.
