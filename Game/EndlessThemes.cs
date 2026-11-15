namespace VanDao.Game;

/// <summary>Chủ đề quái mức Vô Tận: 5 quái thường, 3 tinh anh và 1 Boss mỗi chủ đề (ảnh trong wwwroot/img/endless, xuất bằng tools/endless_art).
/// Chủ đề 1–4 là bốn cụm quái Yêu Triều theo tầng, chủ đề 5 là cụm nhện pha lê.</summary>
public static class EndlessThemes
{
    public sealed record Theme(string Name, ChapterMon[] Commons, ChapterMon[] Elites, ChapterMon Boss);

    public static readonly IReadOnlyList<Theme> All =
    [
        new("Hoang Miếu Chung Yêu",
            [new("Chung Yêu", "img/endless/quai_chung_yeu.png"), new("Tán Nhãn", "img/endless/quai_tan_nhan.png"), new("Trúc Tang", "img/endless/quai_truc_tang.png"), new("Hồ Lô Yêu", "img/endless/quai_ho_lo_yeu.png"), new("Ngõa Giải", "img/endless/quai_ngoa_giai.png")],
            [new("Đăng Linh", "img/endless/tinh_dang_linh.png"), new("Miếu Kiệu", "img/endless/tinh_mieu_kieu.png"), new("Sứ Khôi Sư", "img/endless/tinh_su_khoi_su.png")],
            new("Vô Thủ Chung Ma", "img/endless/boss_vo_thu_chung_ma.png")),
        new("Nê Trạch Thủy Quật",
            [new("Manh Kỳ Ngư", "img/endless/quai_manh_ky_ngu.png"), new("Nê Loa", "img/endless/quai_ne_loa.png"), new("Trúc Ngư Lung", "img/endless/quai_truc_ngu_lung.png"), new("Nha Điệt", "img/endless/quai_nha_diet.png"), new("Châu Bạng", "img/endless/quai_chau_bang.png")],
            [new("Nê Ngư Tướng", "img/endless/tinh_ne_ngu_tuong.png"), new("Liên Cốt", "img/endless/tinh_lien_cot.png"), new("Độ Hồn Diện", "img/endless/tinh_do_hon_dien.png")],
            new("Thiết Giáp Ngạc Quy", "img/endless/boss_thiet_giap_ngac_quy.png")),
        new("Sa Mạc Cổ Địa",
            [new("Sa Thử", "img/endless/quai_sa_thu.png"), new("Cốt Ngô Công", "img/endless/quai_cot_ngo_cong.png"), new("Diều Độc Nhãn", "img/endless/quai_dieu_doc_nhan.png"), new("Gai Sa Tích", "img/endless/quai_gai_sa_tich.png"), new("Sa Úng", "img/endless/quai_sa_ung.png")],
            [new("Thực Cốt Điểu Tướng", "img/endless/tinh_thuc_cot_dieu_tuong.png"), new("Bích Châm Hạt", "img/endless/tinh_bich_cham_hat.png"), new("Sa Lậu Linh", "img/endless/tinh_sa_lau_linh.png")],
            new("Lục Tí Sa Ma", "img/endless/boss_luc_ti_sa_ma.png")),
        new("Tinh Nguyệt Thiên Cảnh",
            [new("Tinh Nghi", "img/endless/quai_tinh_nghi.png"), new("Vân Diêu", "img/endless/quai_van_dieu.png"), new("Nguyệt Thố", "img/endless/quai_nguyet_tho.png"), new("Tinh Nham Trùng", "img/endless/quai_tinh_nham_trung.png"), new("U Tinh Đăng", "img/endless/quai_u_tinh_dang.png")],
            [new("Nghịch Tháp", "img/endless/tinh_nghich_thap.png"), new("Nguyệt Hạc", "img/endless/tinh_nguyet_hac.png"), new("Tam Diện Tinh Thạch", "img/endless/tinh_tam_dien_tinh_thach.png")],
            new("Túc Mệnh Chu Hoàng", "img/endless/boss_tuc_menh_chu_hoang.png")),
        new("Pha Lê Chu Huyệt",
            [new("Châu Nhãn Chu", "img/endless/quai_chau_nhan_chu.png"), new("Tinh Toa Chu", "img/endless/quai_tinh_toa_chu.png"), new("Tinh Noãn Ký Chu", "img/endless/quai_tinh_noan_ky_chu.png"), new("Nguyệt Ti Nga", "img/endless/quai_nguyet_ti_nga.png"), new("Xích Kinh Chu", "img/endless/quai_xich_kinh_chu.png")],
            [new("Bạch Tơ Tế Sư", "img/endless/tinh_bach_to_te_su.png"), new("Lam Giáp Chu Tướng", "img/endless/tinh_lam_giap_chu_tuong.png"), new("Tử Tinh Mệnh Sư", "img/endless/tinh_tu_tinh_menh_su.png")],
            new("Hắc Liên Chu Hậu", "img/endless/boss_hac_lien_chu_hau.png")),
    ];
}
