namespace VanDao.Game;

/// <summary>Hệ ngũ hành của quái theo ảnh gốc (bỏ thư mục, ".png" và hậu tố biến thể "_vN"). Không có trong bảng = vô hệ.</summary>
public static class MonsterElements
{
    public static readonly IReadOnlyDictionary<string, string> ById = new Dictionary<string, string>
    {
        // quái thường
        ["quai_yeu_lang"] = "gio", ["quai_huyen_o"] = "gio",
        ["quai_doc_xa"] = "moc", ["quai_ma_dang"] = "moc", ["quai_doc_chu_nu"] = "moc",
        ["quai_thach_tru"] = "tho", ["quai_pha_son_vien"] = "tho", ["quai_tinh_thach_tich"] = "tho", ["quai_reu_thach_nhan"] = "tho", ["quai_nguu_dau"] = "tho",
        ["quai_bang_ho"] = "bang",
        ["quai_loi_ung"] = "loi",
        ["quai_cuu_vi_ho"] = "hoa", ["quai_hoa_diem_tieu_yeu"] = "hoa",
        // tinh anh
        ["tinh_thiet_giap_ho"] = "tho", ["tinh_hac_mang"] = "thuy", ["tinh_loi_ky_lan"] = "loi",
        // boss chương
        ["boss_tu_ty_ma_vien"] = "tho", ["boss_hac_giao"] = "thuy", ["boss_bang_phuong"] = "bang",
        ["boss_cung_ky"] = "gio", ["boss_cung_ky_vuong"] = "loi", ["boss_dao_ngot"] = "tho", ["boss_thao_thiet"] = "thuy",
        // boss lang thang (WanderingBoss)
        ["boss_co_thu_yeu_vuong"] = "moc", ["boss_huyen_nham_cu_nhan"] = "tho",
        // boss Địa Ngục
        ["boss_hac_hung_tinh"] = "tho", ["boss_thanh_anh_dai_vuong"] = "hoa", ["boss_thiet_phien_cong_chua"] = "gio", ["boss_nguu_ma_vuong"] = "hoa",
        // vô hệ (âm/cốt/kiếm/hỗn độn): quai_cot_khuyen, quai_thiet_giap_binh, quai_ma_dien, quai_bach_vo_thuong, quai_hac_vo_thuong,
        // tinh_cot_tuong, tinh_huyet_y_kiem_khach, tinh_cot_long, tinh_kiem_hon, tinh_bach_cot_tinh, boss_hon_don, boss_am_ma,
        // boss_kiem_de, boss_thoi_phan_quan, boss_diem_la_vuong
    };

    public static string Key(string image)
    {
        var id = Path.GetFileNameWithoutExtension(image ?? "");
        var v = id.LastIndexOf("_v", StringComparison.Ordinal);
        return v > 0 && v + 2 < id.Length && id[(v + 2)..].All(char.IsDigit) ? id[..v] : id;
    }

    public static string Of(string image) => ById.GetValueOrDefault(Key(image), "");
}
