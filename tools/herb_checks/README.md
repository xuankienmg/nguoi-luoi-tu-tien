# Linh thảo bổ sung

Danh mục trong `Game/HerbExpansion.cs`; dùng 10 ảnh có sẵn chưa được gán vật phẩm. Mỗi đan phương bên dưới cần thêm đúng 1 đơn vị linh thảo mới, giữ các nguyên liệu và phí linh thạch khác. Các công thức Bổ Nguyên Đan giữ nguyên.

| Phẩm | Linh thảo | Chương | Từ ải | Tỉ lệ gốc | Dùng luyện |
| --- | --- | --- | --- | --- | --- |
| Thường | Bách Diệp Thảo | 1–2 | 1 | 16% | Tụ Khí Đan |
| Thường | Thủy Lộ Diệp | 2–3 | 1 | 14% | Ngưng Kim Đan |
| Thường | Thạch Đài Thảo | 1–3 | 1 | 12% | Linh Thú Đan |
| Ít gặp | Nguyệt Lộ Hoa | 1–2 | 5 | 8% | Trúc Cơ Đan |
| Ít gặp | Băng Tinh Thảo | 1–2 | 5 | 7% | Tẩy Tủy Đan |
| Ít gặp | Phong Vũ Thảo | 3–4 | 5 | 6% | Hóa Hình Đan |
| Hiếm | Kim Dương Liên | 3–4 | 10 | 4% | Kết Anh Đan |
| Hiếm | Linh Cốt Chi | 4–5 | 10 | 3,5% | Hóa Thần Đan |
| Hiếm | Địa Mạch Tiên Đằng | 6–7 | 10 | 2,5% | Luyện Hư Đan |
| Hiếm | Thiên Trúc Duẩn | 7–9 | 10 | 2% | Hợp Thể Đan |

Tỉ lệ thực tế tăng theo ải bằng cơ chế sẵn có; Khó / Ác mộng dùng chương linh thảo quy đổi của game. Cả 10 loại chỉ rơi độc lập khi thám hiểm (`InChest=false`), không thay nhóm linh thảo hoặc xác suất mở trang bị trong các rương hiện có. Giá bán dùng hệ thống theo phẩm chung (3 / 12 / 50 linh thạch).

Tên và hình dáng dựa trên danh sách ảnh dự trữ `GameData.HerbArt`. Đây là vật phẩm hư cấu phục vụ gameplay. Có tham khảo bối cảnh tiên dược trong [Bão Phác Tử · Tiên Dược](https://ctext.org/baopuzi/xian-yao/zh) và biểu tượng linh chi trong [Ganoderma lucidum — NCBI Bookshelf](https://www.ncbi.nlm.nih.gov/books/NBK92757/); không mô phỏng công dụng y học ngoài đời.

Kiểm tra từ gốc repository:

```powershell
dotnet run --project tools/herb_checks -- <đường-dẫn-tuyệt-đối-tới-repository>
```

Kiểm tra danh mục, phân bố phẩm, ảnh, mốc nguồn rơi, luyện thành công / thiếu nguyên liệu, bán vật phẩm, tổng xác suất rương và Bổ Nguyên Đan.
