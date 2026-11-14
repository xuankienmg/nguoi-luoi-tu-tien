namespace VanDao.Game;

public static partial class KyDuyen
{
    public const int PagesPerBook = 9;
    public const int DaoTamMax = 99, PathAt = 10;

    /// <summary>Chỉ số cộng vĩnh viễn (%; Crit là điểm % bạo kích; ResistAll cộng vào kháng bạo kích / liên kích / phản đòn; CritDmg là % sát thương bạo kích).</summary>
    public record Bonus(double AtkPct = 0, double DefPct = 0, double HpPct = 0, double QiPct = 0, double Crit = 0, double ResistAll = 0, double CritDmg = 0)
    {
        public static readonly Bonus None = new();
        public static Bonus operator +(Bonus a, Bonus b) => new(a.AtkPct + b.AtkPct, a.DefPct + b.DefPct, a.HpPct + b.HpPct, a.QiPct + b.QiPct, a.Crit + b.Crit, a.ResistAll + b.ResistAll, a.CritDmg + b.CritDmg);
        public string Text() => string.Join(", ", new[]
        {
            AtkPct > 0 ? $"+{AtkPct:0.#}% công" : "", DefPct > 0 ? $"+{DefPct:0.#}% thủ" : "", HpPct > 0 ? $"+{HpPct:0.#}% máu" : "",
            QiPct > 0 ? $"+{QiPct:0.#}% tốc độ tu luyện" : "", Crit > 0 ? $"+{Crit:0.#}% bạo kích" : "",
            ResistAll > 0 ? $"+{ResistAll:0.#}% mọi chỉ số kháng" : "", CritDmg > 0 ? $"+{CritDmg:0.#}% sát thương bạo kích" : "",
        }.Where(s => s.Length > 0));
    }
    public record Book(string Name, string Lore, Bonus Bonus);
    /// <summary>Kết quả một lối: trang Thiên Thư, Đạo Tâm, vật phẩm, linh thạch (× StoneUnit), phút suy yếu, lời kể.
    /// Fight = id quái trong Foes: đánh ngay, thắng mới nhận kết quả này, thua thì suy yếu FightLoseDebuff phút và không nhận gì.</summary>
    public record Outcome(int Pages, int DaoTam, (string Item, int Qty)[] Items, int Stones, int DebuffMinutes, string Text, string? Fight = null);
    /// <summary>Một lối chọn: Cost linh thạch (× StoneUnit) trả trước, rồi Chance ra Win, không thì Lose.
    /// Next: lối này dẫn tới câu hỏi thứ hai trong cùng khung (Win / Lose / Cost bỏ qua).</summary>
    public record Choice(string Label, double Chance, int Cost, Outcome Win, Outcome? Lose = null, Step? Next = null);
    /// <summary>Câu hỏi thứ hai của hội thoại hai bước.</summary>
    public record Step(string Text, Choice A, Choice B);
    public record Encounter(string Id, string Title, string Text, Choice A, Choice B);
    public record Ending(string Name, string Text, Bonus Bonus);
    /// <summary>Quái ẩn / Boss của kỳ duyên: chỉ số theo công thức Boss lang thang (cảnh giới + tầng người chơi).</summary>
    public record Foe(string Id, string Name, string Image, bool Boss);

    public const int FightLoseDebuff = 5;
    public static readonly Foe[] Foes =
    [
        new("cuu_vi_yeu_ho", "Cửu Vĩ Yêu Hồ", "img/quai_cuu_vi_ho.png", false),
        new("huyet_thi_vuong", "Huyết Thi Vương", "img/quai_ma_dang.png", false),
        new("thuy_de_co_thu", "Thủy Để Cổ Thụ Yêu", "img/boss_co_thu_yeu_vuong.png", true),
    ];

    private static Outcome O(int pages, int dao, string text, int stones = 0, int debuff = 0, params (string, int)[] items) => new(pages, dao, items, stones, debuff, text);
    /// <summary>Kết quả phải đánh foe trước: thắng mới nhận.</summary>
    private static Outcome F(string foe, int pages, int dao, string text, int stones = 0, params (string, int)[] items) => new(pages, dao, items, stones, 0, text, foe);
    /// <summary>Lối dẫn tới câu hỏi thứ hai.</summary>
    private static Choice N(string label, Step next) => new(label, 1, 0, O(0, 0, ""), null, next);

    /// <summary>Quyển i mở khi cảnh giới ≥ i.</summary>
    public static readonly Book[] Books =
    [
        new("Quyển Nhất · Khí Hải", "Trang đầu Thiên Thư kể chuyện thuở hỗn độn, linh khí tụ thành biển, người đầu tiên học cách hít thở cùng trời đất.", new(QiPct: 3)),
        new("Quyển Nhị · Đạo Cơ", "Thiên Thư chép rằng nền móng của đạo không xây bằng đá mà bằng ý chí; ai giữ được tâm thì cơ không sụp.", new(HpPct: 3)),
        new("Quyển Tam · Kim Đan", "Một vị chân nhân vô danh ngưng tụ kim đan giữa sấm sét, để lại dòng chữ: kiên nhẫn là lửa luyện đan tốt nhất.", new(AtkPct: 3)),
        new("Quyển Tứ · Nguyên Anh", "Nguyên anh rời thân dạo chơi tam giới; Thiên Thư cảnh báo kẻ quên đường về sẽ hóa cô hồn.", new(DefPct: 3)),
        new("Quyển Ngũ · Hóa Thần", "Ở cảnh giới này, Thiên Thư lần đầu nhắc đến kẻ đã xé nó thành mười quyển: một người từng đứng trên đỉnh tiên đạo.", new(ResistAll: 15)),
        new("Quyển Lục · Luyện Hư", "Hư không không phải trống rỗng; Thiên Thư vẽ lại bản đồ những vết nứt mà kẻ xé sách đã đi qua.", new(QiPct: 3)),
        new("Quyển Thất · Hợp Thể", "Kẻ xé sách muốn hợp nhất với Thiên Đạo nhưng bị chính đạo tâm phản phệ; mười quyển văng khắp hạ giới.", new(HpPct: 5)),
        new("Quyển Bát · Đại Thừa", "Thiên Thư hé lộ: mỗi người gom đủ các quyển đều phải trả lời một câu hỏi, đạo của ngươi vì ai mà tu?", new(AtkPct: 5)),
        new("Quyển Cửu · Độ Kiếp", "Chín đạo lôi kiếp được ghi tên từng đạo; người thuộc tên lôi thì lôi không đánh nhầm.", new(DefPct: 5)),
        new("Quyển Thập · Chân Tiên", "Trang cuối để trống. Thiên Thư chờ người gom đủ mười quyển tự viết lên đó con đường của mình.", new(CritDmg: 50)),
    ];

    public static readonly Ending[] Endings =
    [
        new("Chính Đạo · Thiên Thư Hộ Thế", "Ngươi viết lên trang cuối chữ \"Hộ\". Thiên Thư hóa thành lá chắn che chở chúng sinh.", new(DefPct: 5, HpPct: 5)),
        new("Ma Đạo · Thiên Thư Nghịch Mệnh", "Ngươi viết lên trang cuối chữ \"Nghịch\". Thiên Thư bốc cháy thành ngọn lửa đen trong lòng bàn tay.", new(AtkPct: 5, Crit: 2)),
        new("Vô Vi · Thiên Thư Quy Nhất", "Ngươi để trang cuối tiếp tục trống. Thiên Thư khép lại, hòa vào đạo tâm không thiện không ác.", new(AtkPct: 3, DefPct: 3, HpPct: 3)),
    ];

    /// <summary>Kỳ duyên cũ + các câu đố tính toán (KyDuyen.Riddles.cs). Dùng method để không phụ thuộc thứ tự khởi tạo field giữa các file partial.</summary>
    public static readonly Encounter[] Encounters = [.. BaseEncounters(), .. Riddles()];

    private static Encounter[] BaseEncounters() =>
    [
        new("lao_nhan", "Lão nhân bị thương", "Bên đường, một lão nhân ôm vết thương rỉ máu, trong ngực áo lộ ra mép một trang giấy vàng cổ.",
            new("Tặng linh thạch chữa thương", 1, 2, O(2, 3, "Lão nhân cảm tạ, tặng lại trang giấy cổ rồi biến mất trong gió.")),
            new("Đoạt túi trữ vật", .7, 0, O(1, -3, "Ngươi giật được túi trữ vật, bên trong có một trang Thiên Thư và ít linh thạch.", stones: 4),
                O(0, -3, "Lão nhân phất tay áo, một luồng kình lực hất ngươi văng xa.", debuff: 5))),
        new("dong_phu", "Động phủ cổ", "Vách núi sụp lộ ra cửa động phủ phủ đầy rêu, trận pháp bên trong vẫn còn le lói.",
            new("Liều vào sâu", .5, 0, O(2, 0, "Ngươi phá trận vào được mật thất, trên án thư còn hai trang Thiên Thư.", 0, 0, ("ve_tai_xiu", 2)),
                O(0, 0, "Trận pháp phản kích, ngươi phải lết ra ngoài điều tức.", debuff: 5)),
            new("Chép bia đá ngoài cửa", 1, 0, O(1, 0, "Chữ trên bia đá chính là một trang Thiên Thư bị khắc lại."))),
        new("thu_non", "Yêu thú non lạc mẹ", "Một con yêu thú non kêu thảm thiết giữa bụi gai, quanh cổ đeo một cuộn giấy nhỏ.",
            new("Nuôi dưỡng", 1, 0, O(1, 2, "Ngươi chữa thương cho nó; nó dụi đầu vào tay ngươi, nhả ra cuộn giấy.", 0, 0, ("linh_thu_dan", 10))),
            new("Bán cho thương nhân", 1, 0, O(0, -2, "Thương nhân trả giá hời, nhưng cuộn giấy theo con thú đi mất.", stones: 8))),
        new("tan_tu", "Tán tu cầu cứu", "Một tán tu bị ma tu truy đuổi chạy tới, van ngươi ra tay.",
            new("Ra tay cứu", .75, 0, O(2, 3, "Ngươi đánh lui ma tu; tán tu tặng ngươi hai trang giấy cổ giữ mạng."),
                O(0, 1, "Ma tu quá mạnh, hai người cùng trọng thương nhưng tán tu thoát được.", debuff: 5)),
            new("Làm ngơ", 1, 0, O(1, -1, "Ngươi lặng lẽ rời đi, nhặt được một trang giấy rơi trên đường tán tu chạy qua."))),
        new("ma_dao", "Ma đạo truyền thừa", "Trong hang tối, một khối ngọc đen tỏa ma khí, thì thầm hứa trao truyền thừa.",
            new("Tiếp nhận", .8, 0, O(2, -4, "Ma khí nhập thể, trong thức hải hiện lên hai trang Thiên Thư.", 0, 0, ("hoan_cot_dan", 1)),
                O(0, -4, "Ma khí phản phệ, kinh mạch rối loạn.", debuff: 10)),
            new("Hủy truyền thừa", 1, 0, O(1, 4, "Ngọc đen vỡ nát, để lại một trang Thiên Thư bị phong ấn bên trong."))),
        new("linh_tuyen", "Linh tuyền", "Giữa rừng sâu có một mạch linh tuyền sủi bọt, đáy suối lấp lánh ánh vàng.",
            new("Ngồi thiền hấp thu", 1, 0, O(1, 0, "Linh khí thấm vào kinh mạch; dưới đáy suối có một trang Thiên Thư.", stones: 5)),
            new("Đóng bình mang về", 1, 0, O(1, 0, "Ngươi đóng đầy bình linh tuyền, tiện tay vớt được trang giấy dưới đáy.", 0, 0, ("hoa_hinh_dan", 2)))),
        new("thuong_nhan", "Thương nhân bí ẩn", "Một thương nhân che mặt bày bán vài trang giấy vàng, nói là Thiên Thư thất lạc.",
            new("Mua tàn trang", 1, 3, O(2, 0, "Hàng thật. Thương nhân cười khẽ rồi biến mất cùng sạp hàng.")),
            new("Mặc cả", .5, 0, O(2, 0, "Thương nhân chịu thua tài ăn nói của ngươi, tặng luôn hai trang."),
                O(0, 0, "Thương nhân phật ý, cuốn sạp bỏ đi."))),
        new("loi_kiep", "Thiên lôi giáng đỉnh núi", "Mây đen kéo tới, thiên lôi đánh liên hồi lên đỉnh núi gần đó, giữa tia sét thấp thoáng trang giấy cháy sáng.",
            new("Lên đỉnh hứng lôi", .6, 0, O(3, 0, "Ngươi chịu ba đạo lôi, ba trang Thiên Thư rơi vào tay."),
                O(0, 0, "Lôi đánh trúng, toàn thân tê dại.", debuff: 10)),
            new("Chờ lôi tan rồi nhặt", 1, 0, O(1, 0, "Lôi tan, ngươi nhặt được một trang còn nguyên vẹn trong đống tro."))),

        // ---- Lừa bịp: lời nói hấp dẫn là bẫy, manh mối nằm trong câu chữ ----
        // manh mối: tuyết dày mà nàng không để lại dấu chân → yêu hồ hóa hình
        new("thieu_nu", "Thiếu nữ lạc đường", "Một thiếu nữ áo trắng đứng khóc bên đường núi, nhờ ngươi đưa về thôn dưới chân núi. Tuyết rơi dày, vậy mà quanh chân nàng không in một dấu chân nào.",
            N("Đưa nàng về thôn", new("Đi được nửa đường, nàng bảo đường tắt qua hang đá sẽ nhanh hơn, giọng ngọt như mật. Gió trong hang thoảng mùi máu tanh.",
                new("Theo nàng vào hang", 1, 0, F("cuu_vi_yeu_ho", 2, 0, "Ngươi chém tan hồ yêu giữa hang tối, trong ổ nó còn hai trang Thiên Thư.", stones: 4)),
                new("Từ chối, đi đường lớn", 1, 0, O(1, 1, "Nàng cười khẩy, hóa thành làn khói trắng tan mất, để lại một trang giấy trên tuyết.")))),
            new("Vạch trần yêu khí", 1, 0, F("cuu_vi_yeu_ho", 3, 2, "Yêu hồ lộ nguyên hình, chín đuôi quật loạn rồi gục xuống; ba trang Thiên Thư rơi khỏi vạt áo nó."))),
        // manh mối: tự xưng trưởng lão mà áo vá, mắt liếc túi trữ vật; Thiên Thư chỉ có mười quyển
        new("tien_nhan", "Tiên nhân truyền đạo", "Một lão giả tự xưng Thái Thượng trưởng lão, nói ngươi có tiên duyên, chỉ cần dâng linh thạch là được truyền trọn một quyển Thiên Thư. Tay áo lão vá chằng vá đụp, mắt cứ liếc về túi trữ vật của ngươi.",
            new("Dâng linh thạch", 1, 5, O(0, 0, "Lão ôm linh thạch biến mất, \"quyển Thiên Thư\" để lại chỉ là giấy trắng.")),
            N("Hỏi lão về Thiên Thư", new("Lão vuốt râu, nói Thiên Thư có mười một quyển, quyển cuối viết bằng vàng ròng, chỉ người dâng đủ linh thạch mới được xem.",
                new("Tin lời, dâng linh thạch", 1, 5, O(0, -1, "Lão cười hề hề rồi độn thổ mất dạng. Ngươi chợt nhớ Thiên Thư chỉ có mười quyển.")),
                new("Vạch mặt kẻ lừa đảo", 1, 0, O(2, 2, "Lão giật mình đánh rơi hai trang Thiên Thư thật đã trộm được, rồi co giò bỏ chạy."))))),
        // manh mối: hắn khoe giết trăm tu sĩ nhưng lưỡi đao chưa từng dính máu
        new("son_tac", "Sơn tặc chặn đường", "Ba tên sơn tặc chặn đường đòi phí qua núi. Tên cầm đầu khoe đã giết cả trăm tu sĩ, nhưng thanh đao trên tay hắn bóng loáng, lưỡi chưa từng sứt mẻ.",
            new("Nộp phí qua đường", 1, 2, O(0, -1, "Bọn chúng cười hô hố, đếm linh thạch rồi bỏ đi.")),
            new("Rút kiếm", 1, 0, O(2, 1, "Mới thấy kiếm quang, bọn chúng đã vứt đao chạy tán loạn, đánh rơi hai trang giấy cướp được.", stones: 2))),

        // ---- Đánh quái ẩn / Boss: thắng mới nhận ----
        new("day_ho", "Lục quang đáy hồ", "Đáy hồ lóe ánh lục quang, mặt nước gợn sóng đều đặn như có vật gì khổng lồ đang thở bên dưới.",
            new("Lặn xuống lấy bảo vật", 1, 0, F("thuy_de_co_thu", 3, 0, "Ngươi hạ được cổ thụ yêu dưới đáy hồ, lõi lục quang của nó bọc ba trang Thiên Thư.", 0, ("hoan_cot_dan", 1))),
            new("Ném đá dò xét", 1, 0, O(1, 0, "Một xoáy nước cuốn trang giấy ướt sũng lên bờ rồi mặt hồ lặng hẳn."))),
        // hai bước + manh mối: văn khắc "kẻ nào cúi đầu sẽ được sống" → quan tài gỗ thấp sát đất
        new("co_mo", "Cổ mộ vô danh", "Ngươi tìm thấy lối vào một cổ mộ, trên cửa đá khắc tám chữ: \"Kẻ nào cúi đầu, kẻ ấy được sống\".",
            N("Bước vào mộ", new("Trong mộ đặt hai cỗ quan tài: một bằng vàng nạm ngọc lấp lánh ngay giữa điện, một bằng gỗ mục nằm thấp sát đất trong góc.",
                new("Mở quan tài vàng", 1, 0, F("huyet_thi_vuong", 3, -1, "Thi vương bật nắp quan tài lao ra; ngươi diệt được nó, trong quan tài còn ba trang Thiên Thư.", 5)),
                new("Cúi đầu mở quan tài gỗ", 1, 0, O(2, 1, "Nắp gỗ mục mở ra nhẹ tênh, bên trong chỉ có hai trang Thiên Thư được gói cẩn thận.")))),
            new("Chép văn khắc rồi rời đi", 1, 0, O(1, 0, "Nét khắc trên cửa đá hóa ra chính là một trang Thiên Thư."))),
    ];
}
