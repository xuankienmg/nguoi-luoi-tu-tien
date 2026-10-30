# Quy Tắc Làm Việc Với Git

## 1. Quy định về Branch & Push
* **TUYỆT ĐỐI KHÔNG PUSH TRỰC TIẾP LÊN `master` HOẶC `main`**:
  * Không bao giờ chạy lệnh `git push origin master` hoặc `git push origin main`.
  * Luôn kiểm tra nhánh hiện tại trước khi commit/push: `git branch --show-current`.
* **Luôn sử dụng nhánh tính năng (Feature Branch)**:
  * Khi thực hiện tính năng mới hoặc sửa lỗi, luôn tạo hoặc chuyển sang nhánh riêng (ví dụ: `Game-Launcher`, `feat/...`, `fix/...`).
  * Chỉ push lên remote branch của nhánh tính năng đó (`git push -u origin <branch-name>`).
* **Hợp nhất mã nguồn (Merge)**:
  * Để người dùng hoặc lập trình viên tự tạo và duyệt Pull Request (PR) trên GitHub để gộp vào `master`.
