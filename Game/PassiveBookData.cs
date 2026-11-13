namespace VanDao.Game;

/// <summary>
/// Bị động bậc Hóa Thần → Kim Tiên (cân bằng 11/2026): mỗi hệ có đúng 2 bị động mỗi cảnh giới.
/// Hóa Thần: bị động thứ 2 học bằng điểm. Luyện Hư trở đi: 1 bị động học bằng điểm (SkillData, FinalRealmData) + 1 bị động học từ bí kíp bị động (Book).
/// Dùng 1 bí kíp = học vĩnh viễn (cấp 1, miễn điểm); nâng cấp sau đó tốn điểm như bị động thường. Hiện chỉ bán ở Tạp Hóa VIP.
/// Dữ liệu sinh bằng script, ngân sách điểm mỗi hệ xấp xỉ nhau (xem SkillPassiveBalanceTests).
/// </summary>
public static class PassiveBooks
{
    private const string Prefix = "bd:";
    private static readonly string[] RealmNames = ["Luyện Khí", "Trúc Cơ", "Kim Đan", "Nguyên Anh", "Hóa Thần", "Luyện Hư", "Hợp Thể", "Đại Thừa", "Độ Kiếp", "Chân Tiên", "Kim Tiên"];

    public static readonly SkillDef[] Skills =
    [
        new("hoa_than_viem_tam", "hoa", "Hóa Thần Viêm Tâm", "Bị động", 5, 0, 5, 4, 5, ["phuong_hoang:1", "lieu_nguyen:1"],
            "Sát thương chiêu thức +2% mỗi cấp; Tấn công +1.5% mỗi cấp; Xuyên thấu +1% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "FIRE_PASSIVE_R4", SkillPct: 2, AtkPct: 1.5, Pen: 1, HpPct: 0.5),
        new("hu_hoa_ngung_chan", "hoa", "Hư Hỏa Ngưng Chân", "Bị động", 6, 2, 5, 5, 0, [],
            "Sát thương chiêu thức +1% mỗi cấp; Tấn công +0.5% mỗi cấp; Xuyên thấu +0.5% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "FIRE_PASSIVE_R5", SkillPct: 1, AtkPct: 0.5, Pen: 0.5, HpPct: 0.5, Book: "bd_hu_hoa_ngung_chan"),
        new("hop_the_liet_diem", "hoa", "Hợp Thể Liệt Diễm", "Bị động", 7, 2, 5, 6, 0, [],
            "Sát thương chiêu thức +2% mỗi cấp; Tấn công +1% mỗi cấp; Xuyên thấu +0.75% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "FIRE_PASSIVE_R6", SkillPct: 2, AtkPct: 1, Pen: 0.75, HpPct: 0.5, Book: "bd_hop_the_liet_diem"),
        new("dai_thua_thien_hoa_an", "hoa", "Đại Thừa Thiên Hỏa Ấn", "Bị động", 8, 2, 5, 7, 0, [],
            "Sát thương chiêu thức +2.5% mỗi cấp; Tấn công +1.5% mỗi cấp; Xuyên thấu +1% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "FIRE_PASSIVE_R7", SkillPct: 2.5, AtkPct: 1.5, Pen: 1, HpPct: 0.5, Book: "bd_dai_thua_thien_hoa_an"),
        new("kiep_hoa_doan_menh", "hoa", "Kiếp Hỏa Đoạn Mệnh", "Bị động", 9, 2, 5, 8, 0, [],
            "Sát thương chiêu thức +3% mỗi cấp; Tấn công +2% mỗi cấp; Xuyên thấu +1.25% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "FIRE_PASSIVE_R8", SkillPct: 3, AtkPct: 2, Pen: 1.25, HpPct: 0.5, Book: "bd_kiep_hoa_doan_menh"),
        new("chan_tien_bat_diet_viem", "hoa", "Chân Tiên Bất Diệt Viêm", "Bị động", 10, 2, 5, 9, 0, [],
            "Sát thương chiêu thức +3.5% mỗi cấp; Tấn công +2.5% mỗi cấp; Xuyên thấu +1.5% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "FIRE_PASSIVE_R9", SkillPct: 3.5, AtkPct: 2.5, Pen: 1.5, HpPct: 0.5, Book: "bd_chan_tien_bat_diet_viem"),
        new("hoa_than_thuy_hai_tam", "thuy", "Hóa Thần Thủy Hải Tâm", "Bị động", 5, 0, 5, 4, 5, ["huyen_minh:1", "bich_hai:1"],
            "Máu +2.5% mỗi cấp; Sát thương chiêu thức +1% mỗi cấp; Phòng thủ +1% mỗi cấp; công quái −0.5% mỗi cấp (tổng giảm tối đa 40%).", "passive", Code: "WATER_PASSIVE_R4", HpPct: 2.5, SkillPct: 1, MonAtkDown: 0.5, DefPct: 1),
        new("hu_thuy_quy_nguyen", "thuy", "Hư Thủy Quy Nguyên", "Bị động", 6, 2, 5, 5, 0, [],
            "Máu +1.5% mỗi cấp; Sát thương chiêu thức +0.5% mỗi cấp; Phòng thủ +0.5% mỗi cấp; công quái −0.25% mỗi cấp (tổng giảm tối đa 40%).", "passive", Code: "WATER_PASSIVE_R5", HpPct: 1.5, SkillPct: 0.5, MonAtkDown: 0.25, DefPct: 0.5, Book: "bd_hu_thuy_quy_nguyen"),
        new("hop_the_huyen_trieu", "thuy", "Hợp Thể Huyền Triều", "Bị động", 7, 2, 5, 6, 0, [],
            "Máu +2.5% mỗi cấp; Sát thương chiêu thức +1% mỗi cấp; Phòng thủ +1% mỗi cấp; công quái −0.5% mỗi cấp (tổng giảm tối đa 40%).", "passive", Code: "WATER_PASSIVE_R6", HpPct: 2.5, SkillPct: 1, MonAtkDown: 0.5, DefPct: 1, Book: "bd_hop_the_huyen_trieu"),
        new("dai_thua_van_luu_an", "thuy", "Đại Thừa Vạn Lưu Ấn", "Bị động", 8, 2, 5, 7, 0, [],
            "Máu +3% mỗi cấp; Sát thương chiêu thức +1.5% mỗi cấp; Phòng thủ +1.5% mỗi cấp; công quái −0.75% mỗi cấp (tổng giảm tối đa 40%).", "passive", Code: "WATER_PASSIVE_R7", HpPct: 3, SkillPct: 1.5, MonAtkDown: 0.75, DefPct: 1.5, Book: "bd_dai_thua_van_luu_an"),
        new("kiep_thuy_pha_chuong", "thuy", "Kiếp Thủy Phá Chướng", "Bị động", 9, 2, 5, 8, 0, [],
            "Máu +3.5% mỗi cấp; Sát thương chiêu thức +1.5% mỗi cấp; Phòng thủ +1.5% mỗi cấp; công quái −0.75% mỗi cấp (tổng giảm tối đa 40%).", "passive", Code: "WATER_PASSIVE_R8", HpPct: 3.5, SkillPct: 1.5, MonAtkDown: 0.75, DefPct: 1.5, Book: "bd_kiep_thuy_pha_chuong"),
        new("chan_tien_thien_ha_chu", "thuy", "Chân Tiên Thiên Hà Chú", "Bị động", 10, 2, 5, 9, 0, [],
            "Máu +4.5% mỗi cấp; Sát thương chiêu thức +2% mỗi cấp; Phòng thủ +2% mỗi cấp; công quái −1% mỗi cấp (tổng giảm tối đa 40%).", "passive", Code: "WATER_PASSIVE_R9", HpPct: 4.5, SkillPct: 2, MonAtkDown: 1, DefPct: 2, Book: "bd_chan_tien_thien_ha_chu"),
        new("hoa_than_sinh_co_tam", "moc", "Hóa Thần Sinh Cơ Tâm", "Bị động", 5, 0, 5, 4, 5, ["kho_moc_phung_xuan:1", "van_moc_dong_sinh:1"],
            "Máu +2% mỗi cấp; Sát thương chiêu thức +1% mỗi cấp; Phòng thủ +1% mỗi cấp; dưới 30% máu hồi 1% máu tối đa mỗi cấp, một lần mỗi trận, không hồi sinh.", "passive", Code: "WOOD_PASSIVE_R4", HpPct: 2, SkillPct: 1, DefPct: 1, EmergencyHealPct: 1),
        new("hu_moc_hoi_xuan_thuat", "moc", "Hư Mộc Hồi Xuân Thuật", "Bị động", 6, 2, 5, 5, 0, [],
            "Máu +1% mỗi cấp; Sát thương chiêu thức +0.5% mỗi cấp; Phòng thủ +0.5% mỗi cấp; sau lượt tung chiêu Mộc hồi 0.25% máu tối đa mỗi cấp nếu còn sống.", "passive", Code: "WOOD_PASSIVE_R5", HpPct: 1, SkillPct: 0.5, DefPct: 0.5, HealOnCastPct: 0.25, Book: "bd_hu_moc_hoi_xuan_thuat"),
        new("hop_the_co_thu_ho_than", "moc", "Hợp Thể Cổ Thụ Hộ Thân", "Bị động", 7, 2, 5, 6, 0, [],
            "Máu +2% mỗi cấp; Sát thương chiêu thức +1% mỗi cấp; Phòng thủ +1% mỗi cấp; dưới 30% máu hồi 1% máu tối đa mỗi cấp, một lần mỗi trận, không hồi sinh.", "passive", Code: "WOOD_PASSIVE_R6", HpPct: 2, SkillPct: 1, DefPct: 1, EmergencyHealPct: 1, Book: "bd_hop_the_co_thu_ho_than"),
        new("dai_thua_van_diep_sinh", "moc", "Đại Thừa Vạn Diệp Sinh", "Bị động", 8, 2, 5, 7, 0, [],
            "Máu +2.5% mỗi cấp; Sát thương chiêu thức +1.5% mỗi cấp; Phòng thủ +1.5% mỗi cấp; sau lượt tung chiêu Mộc hồi 0.5% máu tối đa mỗi cấp nếu còn sống.", "passive", Code: "WOOD_PASSIVE_R7", HpPct: 2.5, SkillPct: 1.5, DefPct: 1.5, HealOnCastPct: 0.5, Book: "bd_dai_thua_van_diep_sinh"),
        new("kiep_moc_tai_sinh", "moc", "Kiếp Mộc Tái Sinh", "Bị động", 9, 2, 5, 8, 0, [],
            "Máu +3% mỗi cấp; Sát thương chiêu thức +1.5% mỗi cấp; Phòng thủ +1.5% mỗi cấp; dưới 30% máu hồi 1% máu tối đa mỗi cấp, một lần mỗi trận, không hồi sinh.", "passive", Code: "WOOD_PASSIVE_R8", HpPct: 3, SkillPct: 1.5, DefPct: 1.5, EmergencyHealPct: 1, Book: "bd_kiep_moc_tai_sinh"),
        new("chan_tien_truong_sinh_can", "moc", "Chân Tiên Trường Sinh Căn", "Bị động", 10, 2, 5, 9, 0, [],
            "Máu +4% mỗi cấp; Sát thương chiêu thức +2% mỗi cấp; Phòng thủ +2% mỗi cấp; sau lượt tung chiêu Mộc hồi 0.5% máu tối đa mỗi cấp nếu còn sống.", "passive", Code: "WOOD_PASSIVE_R9", HpPct: 4, SkillPct: 2, DefPct: 2, HealOnCastPct: 0.5, Book: "bd_chan_tien_truong_sinh_can"),
        new("hoa_than_dia_nguyen_tam", "tho", "Hóa Thần Địa Nguyên Tâm", "Bị động", 5, 0, 5, 4, 5, ["bat_dong_son:1", "tran_nhac:1"],
            "Phòng thủ +3% mỗi cấp; Máu +2% mỗi cấp; Sát thương chiêu thức +1% mỗi cấp; Xuyên thấu +0.25% mỗi cấp.", "passive", Code: "EARTH_PASSIVE_R4", DefPct: 3, HpPct: 2, SkillPct: 1, Pen: 0.25),
        new("hu_tho_kim_cuong_the", "tho", "Hư Thổ Kim Cương Thể", "Bị động", 6, 2, 5, 5, 0, [],
            "Phòng thủ +1.5% mỗi cấp; Máu +1% mỗi cấp; Sát thương chiêu thức +0.5% mỗi cấp; Xuyên thấu +0.25% mỗi cấp.", "passive", Code: "EARTH_PASSIVE_R5", DefPct: 1.5, HpPct: 1, SkillPct: 0.5, Pen: 0.25, Book: "bd_hu_tho_kim_cuong_the"),
        new("hop_the_nhac_phong", "tho", "Hợp Thể Nhạc Phong", "Bị động", 7, 2, 5, 6, 0, [],
            "Phòng thủ +2.5% mỗi cấp; Máu +2% mỗi cấp; Sát thương chiêu thức +1% mỗi cấp; Xuyên thấu +0.25% mỗi cấp.", "passive", Code: "EARTH_PASSIVE_R6", DefPct: 2.5, HpPct: 2, SkillPct: 1, Pen: 0.25, Book: "bd_hop_the_nhac_phong"),
        new("dai_thua_hoang_tho_an", "tho", "Đại Thừa Hoàng Thổ Ấn", "Bị động", 8, 2, 5, 7, 0, [],
            "Phòng thủ +3.5% mỗi cấp; Máu +2.5% mỗi cấp; Sát thương chiêu thức +1.5% mỗi cấp; Xuyên thấu +0.25% mỗi cấp.", "passive", Code: "EARTH_PASSIVE_R7", DefPct: 3.5, HpPct: 2.5, SkillPct: 1.5, Pen: 0.25, Book: "bd_dai_thua_hoang_tho_an"),
        new("kiep_tho_bat_hoai", "tho", "Kiếp Thổ Bất Hoại", "Bị động", 9, 2, 5, 8, 0, [],
            "Phòng thủ +4% mỗi cấp; Máu +3% mỗi cấp; Sát thương chiêu thức +1.5% mỗi cấp; Xuyên thấu +0.25% mỗi cấp.", "passive", Code: "EARTH_PASSIVE_R8", DefPct: 4, HpPct: 3, SkillPct: 1.5, Pen: 0.25, Book: "bd_kiep_tho_bat_hoai"),
        new("chan_tien_van_co_son", "tho", "Chân Tiên Vạn Cổ Sơn", "Bị động", 10, 2, 5, 9, 0, [],
            "Phòng thủ +5% mỗi cấp; Máu +4% mỗi cấp; Sát thương chiêu thức +2% mỗi cấp; Xuyên thấu +0.5% mỗi cấp.", "passive", Code: "EARTH_PASSIVE_R9", DefPct: 5, HpPct: 4, SkillPct: 2, Pen: 0.5, Book: "bd_chan_tien_van_co_son"),
        new("hoa_than_dien_quang_tam", "loi", "Hóa Thần Điện Quang Tâm", "Bị động", 5, 0, 5, 4, 5, ["cuu_tieu_chan_y:1", "kiep_loi:1"],
            "Tấn công +2% mỗi cấp; Sát thương chiêu thức +1% mỗi cấp; Bạo kích +0.75% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "LIGHTNING_PASSIVE_R4", AtkPct: 2, SkillPct: 1, Crit: 0.75, HpPct: 0.5),
        new("hu_loi_thiem_dien_quyet", "loi", "Hư Lôi Thiểm Điện Quyết", "Bị động", 6, 2, 5, 5, 0, [],
            "Tấn công +1% mỗi cấp; Sát thương chiêu thức +0.5% mỗi cấp; Bạo kích +0.25% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "LIGHTNING_PASSIVE_R5", AtkPct: 1, SkillPct: 0.5, Crit: 0.25, HpPct: 0.5, Book: "bd_hu_loi_thiem_dien_quyet"),
        new("hop_the_cuu_thien_loi_an", "loi", "Hợp Thể Cửu Thiên Lôi Ấn", "Bị động", 7, 2, 5, 6, 0, [],
            "Tấn công +2% mỗi cấp; Sát thương chiêu thức +1% mỗi cấp; Bạo kích +0.75% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "LIGHTNING_PASSIVE_R6", AtkPct: 2, SkillPct: 1, Crit: 0.75, HpPct: 0.5, Book: "bd_hop_the_cuu_thien_loi_an"),
        new("dai_thua_than_loi_chu", "loi", "Đại Thừa Thần Lôi Chú", "Bị động", 8, 2, 5, 7, 0, [],
            "Tấn công +2.5% mỗi cấp; Sát thương chiêu thức +1.5% mỗi cấp; Bạo kích +0.75% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "LIGHTNING_PASSIVE_R7", AtkPct: 2.5, SkillPct: 1.5, Crit: 0.75, HpPct: 0.5, Book: "bd_dai_thua_than_loi_chu"),
        new("kiep_loi_diet_the", "loi", "Kiếp Lôi Diệt Thế", "Bị động", 9, 2, 5, 8, 0, [],
            "Tấn công +3% mỗi cấp; Sát thương chiêu thức +1.5% mỗi cấp; Bạo kích +1% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "LIGHTNING_PASSIVE_R8", AtkPct: 3, SkillPct: 1.5, Crit: 1, HpPct: 0.5, Book: "bd_kiep_loi_diet_the"),
        new("chan_tien_loi_de_an", "loi", "Chân Tiên Lôi Đế Ấn", "Bị động", 10, 2, 5, 9, 0, [],
            "Tấn công +3.5% mỗi cấp; Sát thương chiêu thức +2% mỗi cấp; Bạo kích +1.25% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "LIGHTNING_PASSIVE_R9", AtkPct: 3.5, SkillPct: 2, Crit: 1.25, HpPct: 0.5, Book: "bd_chan_tien_loi_de_an"),
        new("hoa_than_han_tam", "bang", "Hóa Thần Hàn Tâm", "Bị động", 5, 0, 5, 4, 5, ["huyen_bang_chan_the:1", "vinh_dong:1"],
            "Sát thương chiêu thức +1.5% mỗi cấp; Phòng thủ +2% mỗi cấp; Xuyên thấu +1% mỗi cấp; Máu +1.5% mỗi cấp.", "passive", Code: "ICE_PASSIVE_R4", SkillPct: 1.5, DefPct: 2, Pen: 1, HpPct: 1.5),
        new("hu_bang_ngung_suong", "bang", "Hư Băng Ngưng Sương", "Bị động", 6, 2, 5, 5, 0, [],
            "Sát thương chiêu thức +0.5% mỗi cấp; Phòng thủ +1% mỗi cấp; Xuyên thấu +0.5% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "ICE_PASSIVE_R5", SkillPct: 0.5, DefPct: 1, Pen: 0.5, HpPct: 0.5, Book: "bd_hu_bang_ngung_suong"),
        new("hop_the_cuc_han_an", "bang", "Hợp Thể Cực Hàn Ấn", "Bị động", 7, 2, 5, 6, 0, [],
            "Sát thương chiêu thức +1% mỗi cấp; Phòng thủ +2% mỗi cấp; Xuyên thấu +0.75% mỗi cấp; Máu +1.5% mỗi cấp.", "passive", Code: "ICE_PASSIVE_R6", SkillPct: 1, DefPct: 2, Pen: 0.75, HpPct: 1.5, Book: "bd_hop_the_cuc_han_an"),
        new("dai_thua_tuyet_phach", "bang", "Đại Thừa Tuyết Phách", "Bị động", 8, 2, 5, 7, 0, [],
            "Sát thương chiêu thức +1.5% mỗi cấp; Phòng thủ +2.5% mỗi cấp; Xuyên thấu +1% mỗi cấp; Máu +1.5% mỗi cấp.", "passive", Code: "ICE_PASSIVE_R7", SkillPct: 1.5, DefPct: 2.5, Pen: 1, HpPct: 1.5, Book: "bd_dai_thua_tuyet_phach"),
        new("kiep_bang_vinh_phong", "bang", "Kiếp Băng Vĩnh Phong", "Bị động", 9, 2, 5, 8, 0, [],
            "Sát thương chiêu thức +2% mỗi cấp; Phòng thủ +3% mỗi cấp; Xuyên thấu +1.25% mỗi cấp; Máu +2% mỗi cấp.", "passive", Code: "ICE_PASSIVE_R8", SkillPct: 2, DefPct: 3, Pen: 1.25, HpPct: 2, Book: "bd_kiep_bang_vinh_phong"),
        new("chan_tien_huyen_bang_dao", "bang", "Chân Tiên Huyền Băng Đạo", "Bị động", 10, 2, 5, 9, 0, [],
            "Sát thương chiêu thức +2.5% mỗi cấp; Phòng thủ +4% mỗi cấp; Xuyên thấu +1.5% mỗi cấp; Máu +2.5% mỗi cấp.", "passive", Code: "ICE_PASSIVE_R9", SkillPct: 2.5, DefPct: 4, Pen: 1.5, HpPct: 2.5, Book: "bd_chan_tien_huyen_bang_dao"),
        new("hoa_than_phong_nhan", "gio", "Hóa Thần Phong Nhãn", "Bị động", 5, 0, 5, 4, 5, ["tieu_dao_du:1", "cuu_thien_chan_y:1"],
            "Tấn công +1.5% mỗi cấp; Sát thương chiêu thức +1% mỗi cấp; Bạo kích +0.75% mỗi cấp; Né tránh +0.5% mỗi cấp.", "passive", Code: "WIND_PASSIVE_R4", AtkPct: 1.5, SkillPct: 1, Crit: 0.75, Speed: 0.5),
        new("hu_phong_lang_khong", "gio", "Hư Phong Lăng Không", "Bị động", 6, 2, 5, 5, 0, [],
            "Tấn công +0.5% mỗi cấp; Sát thương chiêu thức +0.5% mỗi cấp; Bạo kích +0.25% mỗi cấp; Né tránh +0.25% mỗi cấp.", "passive", Code: "WIND_PASSIVE_R5", AtkPct: 0.5, SkillPct: 0.5, Crit: 0.25, Speed: 0.25, Book: "bd_hu_phong_lang_khong"),
        new("hop_the_vo_anh_bo", "gio", "Hợp Thể Vô Ảnh Bộ", "Bị động", 7, 2, 5, 6, 0, [],
            "Tấn công +1% mỗi cấp; Sát thương chiêu thức +1% mỗi cấp; Bạo kích +0.75% mỗi cấp; Né tránh +0.5% mỗi cấp.", "passive", Code: "WIND_PASSIVE_R6", AtkPct: 1, SkillPct: 1, Crit: 0.75, Speed: 0.5, Book: "bd_hop_the_vo_anh_bo"),
        new("dai_thua_cuong_phong_an", "gio", "Đại Thừa Cuồng Phong Ấn", "Bị động", 8, 2, 5, 7, 0, [],
            "Tấn công +1.5% mỗi cấp; Sát thương chiêu thức +1.5% mỗi cấp; Bạo kích +0.75% mỗi cấp; Né tránh +0.75% mỗi cấp.", "passive", Code: "WIND_PASSIVE_R7", AtkPct: 1.5, SkillPct: 1.5, Crit: 0.75, Speed: 0.75, Book: "bd_dai_thua_cuong_phong_an"),
        new("kiep_phong_liet_te", "gio", "Kiếp Phong Liệt Tế", "Bị động", 9, 2, 5, 8, 0, [],
            "Tấn công +2% mỗi cấp; Sát thương chiêu thức +1.5% mỗi cấp; Bạo kích +1% mỗi cấp; Né tránh +0.75% mỗi cấp.", "passive", Code: "WIND_PASSIVE_R8", AtkPct: 2, SkillPct: 1.5, Crit: 1, Speed: 0.75, Book: "bd_kiep_phong_liet_te"),
        new("chan_tien_tieu_dao_phong", "gio", "Chân Tiên Tiêu Dao Phong", "Bị động", 10, 2, 5, 9, 0, [],
            "Tấn công +2.5% mỗi cấp; Sát thương chiêu thức +2% mỗi cấp; Bạo kích +1.25% mỗi cấp; Né tránh +1% mỗi cấp.", "passive", Code: "WIND_PASSIVE_R9", AtkPct: 2.5, SkillPct: 2, Crit: 1.25, Speed: 1, Book: "bd_chan_tien_tieu_dao_phong"),
        // Kim Tiên (11/2026): mỗi hệ thêm đúng bước tăng Độ Kiếp → Chân Tiên, giữ ngân sách các hệ ngang nhau.
        new("kim_tien_kim_o_than_viem", "hoa", "Kim Tiên Kim Ô Thần Viêm", "Bị động", 11, 2, 5, 10, 0, [],
            "Sát thương chiêu thức +4% mỗi cấp; Tấn công +3% mỗi cấp; Xuyên thấu +1.75% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "FIRE_PASSIVE_R10", SkillPct: 4, AtkPct: 3, Pen: 1.75, HpPct: 0.5, Book: "bd_kim_tien_kim_o_than_viem"),
        new("kim_tien_kim_ba_thien_thuy", "thuy", "Kim Tiên Kim Ba Thiên Thủy", "Bị động", 11, 2, 5, 10, 0, [],
            "Máu +5.5% mỗi cấp; Sát thương chiêu thức +2.5% mỗi cấp; Phòng thủ +2.5% mỗi cấp; công quái −1.25% mỗi cấp (tổng giảm tối đa 40%).", "passive", Code: "WATER_PASSIVE_R10", HpPct: 5.5, SkillPct: 2.5, MonAtkDown: 1.25, DefPct: 2.5, Book: "bd_kim_tien_kim_ba_thien_thuy"),
        new("kim_tien_bat_hu_linh_can", "moc", "Kim Tiên Bất Hủ Linh Căn", "Bị động", 11, 2, 5, 10, 0, [],
            "Máu +5% mỗi cấp; Sát thương chiêu thức +2.5% mỗi cấp; Phòng thủ +2.5% mỗi cấp; dưới 30% máu hồi 1.5% máu tối đa mỗi cấp, một lần mỗi trận, không hồi sinh.", "passive", Code: "WOOD_PASSIVE_R10", HpPct: 5, SkillPct: 2.5, DefPct: 2.5, EmergencyHealPct: 1.5, Book: "bd_kim_tien_bat_hu_linh_can"),
        new("kim_tien_kim_than_tran_the", "tho", "Kim Tiên Kim Thân Trấn Thế", "Bị động", 11, 2, 5, 10, 0, [],
            "Phòng thủ +6% mỗi cấp; Máu +5% mỗi cấp; Sát thương chiêu thức +2.5% mỗi cấp; Xuyên thấu +0.75% mỗi cấp.", "passive", Code: "EARTH_PASSIVE_R10", DefPct: 6, HpPct: 5, SkillPct: 2.5, Pen: 0.75, Book: "bd_kim_tien_kim_than_tran_the"),
        new("kim_tien_kim_tieu_loi_an", "loi", "Kim Tiên Kim Tiêu Lôi Ấn", "Bị động", 11, 2, 5, 10, 0, [],
            "Tấn công +4% mỗi cấp; Sát thương chiêu thức +2.5% mỗi cấp; Bạo kích +1.5% mỗi cấp; Máu +0.5% mỗi cấp.", "passive", Code: "LIGHTNING_PASSIVE_R10", AtkPct: 4, SkillPct: 2.5, Crit: 1.5, HpPct: 0.5, Book: "bd_kim_tien_kim_tieu_loi_an"),
        new("kim_tien_kim_phach_han_dao", "bang", "Kim Tiên Kim Phách Hàn Đạo", "Bị động", 11, 2, 5, 10, 0, [],
            "Sát thương chiêu thức +3% mỗi cấp; Phòng thủ +5% mỗi cấp; Xuyên thấu +1.75% mỗi cấp; Máu +3% mỗi cấp.", "passive", Code: "ICE_PASSIVE_R10", SkillPct: 3, DefPct: 5, Pen: 1.75, HpPct: 3, Book: "bd_kim_tien_kim_phach_han_dao"),
        new("kim_tien_kim_phong_tieu_dao", "gio", "Kim Tiên Kim Phong Tiêu Dao", "Bị động", 11, 2, 5, 10, 0, [],
            "Tấn công +3% mỗi cấp; Sát thương chiêu thức +2.5% mỗi cấp; Bạo kích +1.5% mỗi cấp; Né tránh +1.25% mỗi cấp.", "passive", Code: "WIND_PASSIVE_R10", AtkPct: 3, SkillPct: 2.5, Crit: 1.5, Speed: 1.25, Book: "bd_kim_tien_kim_phong_tieu_dao"),
    ];

    private static readonly Dictionary<string, SkillDef> ByBook = Skills.Where(s => s.Book != "").ToDictionary(s => s.Book);
    private static readonly string[] ElementColors = ["#e0521e", "#3f8fd8", "#4f9a5a", "#b08a4a", "#a46ae6", "#a8dcf0", "#8fd0b0"];
    private static readonly string[] ElementOrder = ["hoa", "thuy", "moc", "tho", "loi", "bang", "gio"];
    /// <summary>Một icon thẻ tre (vàng hổ phách) dùng chung, đổi màu theo hệ bằng bộ lọc CSS (cùng thứ tự ElementOrder).</summary>
    private static readonly string[] ElementTints =
    [
        "hue-rotate(-25deg) saturate(1.5)",                  // Hỏa: đỏ cam
        "hue-rotate(175deg) saturate(1.3) brightness(1.05)", // Thủy: lam
        "hue-rotate(85deg) saturate(1.2)",                   // Mộc: lục
        "saturate(.75) brightness(.95)",                     // Thổ: nâu vàng
        "hue-rotate(240deg) saturate(1.3)",                  // Lôi: tím
        "hue-rotate(150deg) saturate(.8) brightness(1.5)",   // Băng: lam nhạt
        "hue-rotate(115deg) saturate(1.1) brightness(1.2)",  // Phong: lục ngọc
    ];

    /// <summary>Vật phẩm bí kíp bị động (Type "manual" để dùng chung luồng hành trang; không rơi từ rương, không vào Tạp Hóa hằng ngày).</summary>
    public static readonly ItemDef[] Items = Skills.Where(s => s.Book != "").Select(s =>
    {
        var el = SkillData.Elements.First(e => e.Id == s.Element).Name;
        return new ItemDef(s.Book, "Bí Kíp " + s.Name, "manual", ElementColors[Array.IndexOf(ElementOrder, s.Element)],
            $"Bí kíp bị động hệ {el}, ghi lại tâm pháp {s.Name}.",
            Icon: "icons/bi_kip_bi_dong.png", Tint: ElementTints[Array.IndexOf(ElementOrder, s.Element)], Rarity: "legendary", Kind: "Bí kíp bị động",
            Use: $"Dùng 1 lần: học vĩnh viễn bị động {s.Name} (hệ {el}, cần cảnh giới {RealmNames[s.MinRealm]} và đã mở cây kỹ năng hệ này). Sau khi học có thể nâng cấp bằng điểm kỹ năng. " +
                (s.MinRealm == SectData.BookRealm ? "Bán ở Tạp Hóa VIP và Tàng Bảo Các (tông cấp 4)." : "Hiện chỉ bán ở Tạp Hóa VIP."));
    }).ToArray();

    public static bool Is(string itemId) => ByBook.ContainsKey(itemId);
    public static SkillDef? SkillOf(string itemId) => ByBook.GetValueOrDefault(itemId);

    /// <summary>Bí kíp bị động đã học được lưu cùng LearnedManuals dưới dạng "bd:&lt;id kỹ năng&gt;" (không làm đổi danh sách hệ đã mở).</summary>
    public static bool IsLearned(Data.Player p, SkillDef s) => p.LearnedManuals.Split(',').Contains(Prefix + s.Id);
    public static void Learn(Data.Player p, SkillDef s)
    {
        if (!IsLearned(p, s)) p.LearnedManuals = string.Join(',', p.LearnedManuals.Split(',', StringSplitOptions.RemoveEmptyEntries).Append(Prefix + s.Id));
    }
    public static IEnumerable<SkillDef> LearnedSkills(Data.Player p) => Skills.Where(s => s.Book != "" && IsLearned(p, s));
    /// <summary>Kỹ năng thuộc bí kíp mà người chơi chưa học: không nâng được.</summary>
    public static bool Locked(Data.Player p, SkillDef s) => s.Book != "" && !IsLearned(p, s);
}
