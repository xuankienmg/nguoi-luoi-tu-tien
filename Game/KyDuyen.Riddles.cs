namespace VanDao.Game;

/// <summary>Kỳ duyên "đố trận": đề có số liệu thật, đáp án tính được. Đáp án đúng chắc chắn thắng (Chance 1), đáp án sai không được gì.
/// Client không hiện tỉ lệ / phần thưởng của lối chọn nên không lộ đáp án. Đạo Tâm giữ 0 để không lệch ba cái kết.
/// Dễ: 1 trang Thiên Thư; khó: 2 trang. Bù thêm Linh Thú Đan và Tinh Thiết (số cố định, không nhân theo cảnh giới).</summary>
public static partial class KyDuyen
{
    // cân bằng 11/2026: đố trận trả trang ít hơn kỳ duyên thường, bù bằng Linh Thú Đan + Tinh Thiết
    private static Outcome Right(int pages, string text, int stones = 0) => O(pages, 0, text, stones, 0,
        pages >= 2 ? new[] { ("linh_thu_dan", 30), ("tinh_thiet", 4) } : new[] { ("linh_thu_dan", 10), ("tinh_thiet", 2) });
    private static Outcome Wrong(string text) => O(0, 0, text);
    /// <summary>Lối liều không cần suy nghĩ: có thể trúng 1 trang, trượt thì suy yếu.</summary>
    private static Choice Gamble(string label, double chance, string win, string lose) =>
        new(label, chance, 0, O(1, 0, win), O(0, 0, lose, debuff: 5));
    private static Choice Opt(string label, Outcome o) => new(label, 1, 0, o);

    private static Encounter[] Riddles() =>
    [
        // ---- Khó (2 trang) ----
        new("hai_nen_huong", "Trận pháp hai nén hương",
            "Cổng trận đá đóng kín. Bên cạnh có hai nén hương và tấm bia khắc: \"Mỗi nén cháy không đều nhưng hết đúng một canh giờ. Muốn mở cổng, phải đo đúng 45 phút.\"",
            N("Thử mở cổng trận", new("Ngươi có hai nén hương và một hỏa tập. Châm lửa thế nào để đo đúng 45 phút?",
                Opt("Châm một đầu cả hai nén; nén nào tắt trước thì chờ thêm 15 phút", Wrong("Hai nén cháy không đều, nén tắt trước chưa chắc là nửa canh giờ; cổng đá vẫn đóng im ỉm.")),
                Opt("Châm hai đầu nén thứ nhất và một đầu nén thứ hai; nén thứ nhất tắt thì châm nốt đầu còn lại của nén thứ hai", Right(2, "Nén một tắt đúng sau 30 phút, nén hai còn cháy nửa; châm hai đầu thì 15 phút nữa tắt. Đủ 45 phút, cổng đá mở ra.")))),
            Gamble("Phá cổng bằng sức mạnh", .35, "Cổng đá nứt toác, ngươi chui vào kịp nhặt một trang Thiên Thư.", "Trận pháp phản chấn, ngươi bị hất văng.")),

        new("hop_nhan_sai", "Ba hộp nhãn sai",
            "Một thương nhân bày ba hộp gỗ dán nhãn \"Linh thạch\", \"Đan dược\", \"Cả hai\". Y thề cả ba nhãn đều dán sai, mỗi hộp chứa đúng một loại (hộp \"cả hai\" là hộp chứa lẫn hai thứ), ai đoán đúng nội dung cả ba hộp sẽ được thưởng.",
            N("Nhận thử thách", new("Ngươi chỉ được mở một hộp, lấy ra một món. Mở hộp nào thì suy ra được đúng cả ba?",
                Opt("Mở hộp nhãn \"Cả hai\"", Right(2, "Nhãn sai thì hộp này chỉ chứa một loại; thấy loại nào, hai hộp còn lại suy ra ngay. Thương nhân vỗ tay, dâng ngươi phần thưởng.")),
                Opt("Mở hộp nhãn \"Linh thạch\"", Wrong("Hộp này có thể là đan dược hoặc cả hai, thấy một món vẫn không phân biệt được. Ngươi đoán bừa và đoán sai.")))),
            Opt("Giật hộp bỏ chạy", O(0, -2, "Hộp rỗng tuếch, thương nhân bật cười sau lưng ngươi."))),

        new("hai_lao_gac", "Hai lão gác ngã ba",
            "Trước ngã ba có hai lão gác: một lão luôn nói thật, một lão luôn nói dối, ngươi không biết ai là ai. Một cửa dẫn lên đỉnh, một cửa dẫn xuống vực. Ngươi chỉ được hỏi đúng một câu cho một lão.",
            N("Lên tiếng hỏi", new("Hỏi câu nào để chắc chắn đi đúng cửa lên đỉnh?",
                Opt("\"Cửa nào dẫn lên đỉnh?\" rồi đi theo cửa lão chỉ", Wrong("Nếu gặp lão nói dối, ngươi đi thẳng xuống vực; may mà có người kéo kịp lại.")),
                Opt("\"Nếu hỏi lão kia, lão sẽ chỉ cửa nào dẫn lên đỉnh?\" rồi đi cửa còn lại", Right(2, "Lão thật chỉ lại lời dối, lão dối chỉ ngược lời thật: câu trả lời nào cũng là cửa xuống vực. Ngươi đi cửa kia và lên tới đỉnh.")))),
            Gamble("Đoán bừa một cửa", .5, "Ngươi đoán trúng cửa lên đỉnh, nhặt được một trang Thiên Thư bên vách đá.", "Ngươi đoán trượt, trượt chân suýt ngã xuống vực.")),

        new("lao_y_ban_dan", "Lão y bán đan",
            "Lão y thở dài: \"Thương thế ngươi nặng. Đan Hồi Xuân mỗi viên chữa 30% phần thương còn lại, ta cho uống ba viên liên tiếp. Đan Cố Nguyên chỉ một viên nhưng chữa 60% thương thế.\" Lão hỏi ngươi chọn cách chữa được nhiều nhất.",
            Opt("Ba viên Hồi Xuân", Right(2, "Phần thương còn lại là 0,7 × 0,7 × 0,7 = 34,3%, tức chữa được 65,7%, hơn hẳn 60%. Lão y gật đầu tặng ngươi thêm quà.")),
            Opt("Một viên Cố Nguyên", Wrong("Chữa được 60%, kém ba viên Hồi Xuân. Lão y lắc đầu: \"Tính nhẩm chưa kỹ.\""))),

        new("thien_bao_cac", "Thiên Bảo Các gửi linh thạch",
            "Chưởng quỹ Thiên Bảo Các mời gửi linh thạch ba tháng: gói Kép lãi 10% mỗi tháng, lãi nhập vào gốc tháng sau; gói Trọn lãi một lần 35% khi hết kỳ. Ngươi muốn gói nào lời hơn?",
            Opt("Gói Kép 10% mỗi tháng", Wrong("Gốc nhân 1,1 ba lần chỉ được 1,331, thấp hơn 1,35. Chưởng quỹ cười: \"Kép chưa chắc hơn.\"")),
            Opt("Gói Trọn 35%", Right(2, "1,35 lớn hơn 1,331: gói Trọn lời hơn. Chưởng quỹ phục tài tính toán, tặng ngươi quà cảm tạ."))),

        new("soc_xuc_xac", "Sòng bạc tán tu",
            "Chủ sòng lắc hai xúc xắc sáu mặt, rao: \"Cược tổng bằng 7: trúng trả gấp 5 tiền cược. Cược tổng từ 10 trở lên: trúng trả gấp 7. Tiền cược trả trước.\" Ngươi cần chọn cửa có lợi về lâu dài.",
            new("Cược tổng bằng 7, trả gấp 5", 1, 2, Wrong("Có 6 trên 36 cách ra tổng 7, kỳ vọng chỉ 5/6 vốn: càng chơi càng lỗ. Chủ sòng cười hề hề thu tiền.")),
            new("Cược tổng từ 10 trở lên, trả gấp 7", 1, 2, Right(2, "Tổng 10, 11, 12 có 3 + 2 + 1 = 6 trên 36 cách, kỳ vọng 7/6 vốn, lời về lâu dài. Ván này ngươi trúng.", stones: 7))),

        new("ba_ruong_lao_ma", "Ba rương lão ma",
            "Lão ma tu bày ba rương, chỉ một rương chứa Thiên Thư. Ngươi chỉ tay vào rương thứ nhất.",
            N("Chọn rương thứ nhất", new("Lão cười, mở rương thứ ba: trống rỗng. \"Giữ rương một hay đổi sang rương hai?\" Lão biết rõ rương nào có, và luôn mở một rương trống.",
                new("Giữ nguyên rương thứ nhất", 1.0 / 3, 0, Right(2, "Ngươi giữ đúng rương có Thiên Thư. Lão ma tu rít lên nhả quà ra."), Wrong("Rương thứ nhất rỗng. Lão cười khành khạch.")),
                new("Đổi sang rương thứ hai", 2.0 / 3, 0, Right(2, "Đổi rương có xác suất 2/3 vì lão luôn mở rương trống. Rương hai chứa Thiên Thư."), Wrong("Xác suất 2/3 vẫn có lúc hụt: rương hai rỗng, rương một mới có.")))),
            Gamble("Mở cả ba rương cùng lúc", .25, "Ma khí bùng lên nhưng ngươi kịp chộp lấy một trang Thiên Thư.", "Ma khí phản phệ, ngươi gục xuống.")),

        new("dong_nuoc_hai_binh", "Trận đồ hai chiếc bình",
            "Trận đồ đòi đong đúng 4 thăng linh tuyền. Ngươi chỉ có một bình ba thăng và một bình năm thăng, không vạch chia, suối nước thì vô tận.",
            N("Giải trận", new("Cách đong nào ra đúng 4 thăng?",
                Opt("Đầy bình năm, rót sang bình ba (bình năm còn 2); đổ bình ba, rót 2 sang bình ba; đầy lại bình năm, rót cho đầy bình ba (thêm 1)", Right(2, "Bình năm còn 5 − 1 = 4 thăng, đúng ý trận đồ. Trận đồ sáng lên.")),
                Opt("Đầy bình năm, rót sang bình ba rồi đổ bình ba đi: bình năm còn đúng 4 thăng", Wrong("Bình năm chỉ còn 2 thăng. Trận đồ xám xịt, không động đậy.")))),
            Gamble("Đong liều bằng mắt", .3, "Mắt ngươi tinh, nước vừa đúng vạch trận đồ.", "Nước tràn lênh láng, trận đồ phản chấn.")),

        // ---- Dễ (1 trang) ----
        new("tam_vien_linh_thach", "Tám viên linh thạch",
            "Thương nhân có tám viên linh thạch nhìn y hệt nhau, một viên giả hơi nhẹ hơn. Có cái cân thăng bằng, y nhờ ngươi tìm ra viên giả với số lần cân ít nhất.",
            Opt("Cân hai lần: 3 với 3, rồi cân 1 với 1 ở nhóm nhẹ hơn", Right(1, "Hai lần là đủ: cân 3–3, nhóm nhẹ hơn (hoặc hai viên để riêng) cân tiếp 1–1. Thương nhân gật gù trả công.")),
            Opt("Cân ba lần: 4 với 4, rồi 2 với 2, rồi 1 với 1", Wrong("Ba lần cũng tìm ra, nhưng đề bảo ít nhất, mà hai lần là đủ."))),

        new("quay_khuyen_mai", "Sạp đan khuyến mãi",
            "Sạp treo biển \"Mua hai tặng một!\". Ngươi nhớ hôm qua một viên đan giá một đồng, nay biển niêm yết mỗi viên hai đồng. Sạp bên vẫn bán lẻ giá cũ.",
            Opt("Sang sạp bên mua lẻ, mỗi viên một đồng", Right(1, "Mua ba viên ở sạp này tốn 2 × 2 = 4 đồng, tức 1,33 đồng một viên, đắt hơn giá cũ. Ngươi mua lẻ sạp bên là hời nhất.")),
            Opt("Mua hai tặng một ở sạp này", Wrong("Trả tiền hai viên giá hai đồng, nhận ba viên: mỗi viên 1,33 đồng, đắt hơn giá cũ. Ngươi mắc mưu rồi."))),

        new("tang_roi_giam", "Chưởng quầy đổi giá",
            "Chưởng quầy khoe: \"Đan này hôm qua tăng 20%, hôm nay giảm đúng 20%, vậy giá y như cũ!\" Ngươi nghe có điều không đúng.",
            Opt("Đúng, giá về nguyên giá", Wrong("Sai rồi: 1,2 × 0,8 = 0,96, giá nay rẻ hơn giá gốc 4%. Chưởng quầy đã lừa được ngươi gật đầu.")),
            Opt("Sai, giá nay rẻ hơn giá gốc 4%", Right(1, "1,2 × 0,8 = 0,96: giá gốc 100 chỉ còn 96. Chưởng quầy ngượng ngùng nhả ra quà bịt miệng."))),

        new("hai_tran_cung_mo", "Hai trận pháp luân phiên",
            "Hai trận pháp trấn cổng: Thanh Long mở mỗi 12 canh giờ, Bạch Hổ mở mỗi 18 canh giờ. Cả hai vừa cùng mở. Muốn lẻn qua phải chờ lần kế cả hai cùng mở lại.",
            Opt("Chờ 36 canh giờ", Right(1, "Bội chung nhỏ nhất của 12 và 18 là 36. Ngươi chờ đúng lúc, cổng mở toang.")),
            Opt("Chờ 216 canh giờ", Wrong("216 là tích 12 × 18, không phải bội chung nhỏ nhất. Chờ lâu mà vẫn sai."))),

        new("mat_ma_su_do", "Mật mã sư đồ",
            "Cửa tàng kinh khắc: \"Tuổi sư phụ nay gấp ba tuổi đồ đệ; mười hai năm sau chỉ gấp hai. Mật mã là tổng tuổi hai người hiện nay.\"",
            Opt("40", Wrong("Gọi tuổi đồ đệ x thì 3x + 12 = 2(x + 12), x = 12, tổng là 48. Cửa tàng kinh vẫn đóng.")),
            Opt("48", Right(1, "Đồ đệ 12, sư phụ 36, tổng 48. Cửa tàng kinh bật mở."))),

        new("chuong_yeu_thu", "Chuồng yêu thú",
            "Lão nuôi thú nói chuồng có Tiên Hạc hai chân và Địa Hổ bốn chân, tổng 20 cái đầu và 56 cái chân. Lão tặng quà cho ai đoán đúng số Địa Hổ.",
            Opt("8 Địa Hổ", Right(1, "8 hổ × 4 + 12 hạc × 2 = 56 chân. Lão nuôi thú cười khà khà trao quà.")),
            Opt("12 Địa Hổ", Wrong("12 hổ × 4 + 8 hạc × 2 = 64 chân, lệch 8 chân. Lão lắc đầu."))),
    ];
}
