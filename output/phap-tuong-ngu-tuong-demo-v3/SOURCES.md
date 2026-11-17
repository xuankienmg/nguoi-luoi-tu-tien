# Nguồn hình và bố cục

- Nền sân nguyệt từ `wwwroot/img/backgrounds/concept-courtyard-v1.webp` trong Vấn Đạo. Demo dùng cùng cách phủ kín khung và phủ màu tối của `wwwroot/js/scene-backdrop.js`.
- Tu sĩ Luyện Hư dùng `wwwroot/img/hero_luyenhu_stand.png`. Băng Hồ dùng `wwwroot/img/quai_bang_ho_hd.png`.
- Bố cục lấy từ `wwwroot/js/battle.js` và `wwwroot/js/battle-fx.js`. Lưới chiến đấu 400 × 320 được chiếu lên khung 600 × 480. Tu sĩ ở 27% chiều ngang, quái thường ở 70%, chân ở hàng `round(h × .80) + 1` của lưới.
- Hình năm pháp tướng được ImageGen vẽ lại từ mẫu v2 với đầu và thân quay sang phải theo góc 2/3. Các prompt có trong `prompts.txt`.
- Vòng ấn dùng lại nét phù văn Thiên Ấn từ `output/tran-phap-v3/render.py` của Vấn Đạo.
- Pha kết hình từ dưới lên tham khảo hàm `apparition()` trong `output/kim-tien-than-binh-tram-v1/build.py`. V3 thêm viền kết linh và giữ tối đa hai vệt ngắn trên mỗi quỹ đạo.

Nền và sprite hai nhân vật chỉ xuất hiện trong WebP và poster. Mỗi atlas lưu riêng VFX với điểm neo tại chân tu sĩ.

Chạy `python build.py` với Pillow để dựng lại cả năm bản. Có thể truyền thư mục pháp tướng, ví dụ `python build.py kim-long`, để dựng lại một bản.
