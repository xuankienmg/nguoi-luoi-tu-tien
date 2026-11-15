using System.Text.Json;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>Thái Hư Bí Cảnh: leo tối đa 15 tầng, mỗi tầng chọn 1 trong 2–3 cửa; máu giữ qua tầng, cổ vật chỉ hiệu lực trong lượt.
/// Trạng thái ở Player.BiCanhState (JSON <see cref="Save"/>). Mọi thứ hiện ra màn hình sinh tất định từ Seed của lượt.</summary>
public static class BiCanhData
{
    public const int DailyEntries = 2;
    public const int MaxFloor = 15;
    public static readonly int[] BossFloors = [5, 10, 15];
    public static bool IsBossFloor(int floor) => floor % 5 == 0;
    /// <summary>Ô trống cần có khi vào (đủ chỗ cho mọi loại thưởng lúc kết thúc lượt).</summary>
    public const int BagNeed = 5;

    // ---- loại cửa ----
    public const string Fight = "fight", Elite = "elite", Event = "event", Shop = "shop", Spring = "spring", Boss = "boss";
    public static readonly IReadOnlyDictionary<string, string> DoorNames = new Dictionary<string, string>
    {
        [Fight] = "Quái thường", [Elite] = "Tinh anh", [Event] = "Kỳ ngộ", [Shop] = "Thương nhân", [Spring] = "Linh tuyền", [Boss] = "Boss",
    };
    public static readonly IReadOnlyDictionary<string, string> DoorHints = new Dictionary<string, string>
    {
        [Fight] = "Tự đánh, thắng +3 Hư Thạch",
        [Elite] = "Mạnh hơn; thắng +6 Hư Thạch và chọn cổ vật",
        [Event] = "Sự kiện có lựa chọn",
        [Shop] = "Đổi Hư Thạch lấy cổ vật hoặc hồi máu",
        [Spring] = "Hồi 35% máu",
        [Boss] = "Boss có cơ chế riêng; thắng +10 Hư Thạch",
    };

    // ---- cân bằng 11/2026: bản demo ----
    public const int StonesFight = 3, StonesElite = 6, StonesBoss = 10;
    public const double SpringHeal = .35, ShopHeal = .40;
    public const int ShopHealCost = 6, ShopRelicCost = 10;
    public const double MonsterEase = .85;   // cân bằng 11/2026: giảm nhẹ máu và công mọi quái Thái Hư còn 85%
    public const double FloorRamp = .035;   // +3,5% máu/công quái mỗi tầng
    /// <summary>Hệ số so với bản sao người chơi: (máu, công, thủ).</summary>
    public static readonly (double Hp, double Atk, double Def) FightMult = (.25, .45, .85), EliteMult = (.40, .55, 1.0);   // đổi cơ chế Lôi đài (11/2026): hệ số so với CHÍNH NGƯỜI CHƠI (bản sao), không còn so với Boss lang thang
    public static (double Hp, double Atk, double Def) BossMult(int floor) => floor switch { 5 => (.55, .60, 1.0), 10 => (.80, .90, 1.0), _ => (1.0, 1.2, 1.0) };
    public const double MirrorReflect = 40;              // Kính Ảnh Yêu Cơ: chỉ số phản đòn
    public const double LeechHp = 1.35, LeechAtk = 1.1;  // Huyết Ảnh Ma Quân khi khắc hệ người chơi
    public const double GuardMult = .35;                 // Hư Linh Vệ (triệu hồi tầng 15) so với Tinh anh

    // ---- quái ----
    public record Mon(string Name, string Image);
    public static readonly Mon[] Fights =
    [
        new("Hư Không Yêu Lang", "img/quai_yeu_lang.png"), new("Độc Xà Hư Ảnh", "img/quai_doc_xa.png"),
        new("Thạch Trụ Tinh", "img/quai_thach_tru.png"), new("Băng Hổ Hư Ảnh", "img/quai_bang_ho.png"),
        new("Lôi Ưng", "img/quai_loi_ung.png"), new("Cửu Vĩ Hồ Ảnh", "img/quai_cuu_vi_ho.png"),
    ];
    public static readonly Mon[] Elites =
    [
        new("Thiết Giáp Hổ Vương", "img/tinh_thiet_giap_ho.png"), new("Hắc Mãng Thái Hư", "img/tinh_hac_mang.png"),
        new("Lôi Kỳ Lân", "img/tinh_loi_ky_lan.png"), new("Kiếm Hồn Cổ Tu", "img/tinh_kiem_hon.png"),
    ];
    public record BossDef(int Floor, string Name, string Image, string Mechanic, string MechanicText);
    public static readonly BossDef[] Bosses =
    [
        new(5, "Kính Ảnh Yêu Cơ", "img/boss_bang_phuong.png", "mirror", "Phản kính: thường xuyên phản đòn sau khi trúng đòn."),
        new(10, "Huyết Ảnh Ma Quân", "img/boss_hac_giao.png", "leech", "Hút máu: nếu hệ Thủy của nó khắc công pháp của ngươi, máu ×1,35 và công ×1,1."),
        new(15, "Thái Hư Chân Linh", "img/boss_hon_don.png", "summon", "Triệu hồi: 2 Hư Linh Vệ ra trước, đánh xong mới tới Boss (máu không hồi giữa chừng)."),
    ];
    public static BossDef BossAt(int floor) => Bosses.First(b => b.Floor == floor);
    public static readonly Mon Guard = new("Hư Linh Vệ", "img/tinh_kiem_hon.png");

    // ---- cổ vật ----
    /// <summary>Hiệu ứng cộng dồn của cổ vật / bộ. Đơn vị %.</summary>
    public record Mods(double AtkPct = 0, double ReducePct = 0, double MaxHpPct = 0, double CritHealPct = 0, double WinHealPct = 0,
        double StonePct = 0, double PerFloorAtk = 0, double EliteAtkPct = 0, double EliteReducePct = 0, double HealMult = 0)
    {
        public static Mods operator +(Mods a, Mods b) => new(a.AtkPct + b.AtkPct, a.ReducePct + b.ReducePct, a.MaxHpPct + b.MaxHpPct,
            a.CritHealPct + b.CritHealPct, a.WinHealPct + b.WinHealPct, a.StonePct + b.StonePct, a.PerFloorAtk + b.PerFloorAtk,
            a.EliteAtkPct + b.EliteAtkPct, a.EliteReducePct + b.EliteReducePct, a.HealMult + b.HealMult);
    }
    /// <summary>Element: Tâm Ấn — công pháp hệ này +ElementPct; hệ bị nó khắc −ElementWeakPct.</summary>
    public record Relic(string Id, string Name, string Set, string Icon, string Text, Mods Mods, string Element = "");
    public const double ElementPct = 30, ElementWeakPct = 15;
    public record SetDef(string Id, string Name, int Need, string Text, Mods Bonus);

    public static readonly SetDef[] Sets =
    [
        new("huyet", "Huyết Hải", 3, "Hồi máu từ cổ vật ×2", new(HealMult: 1)),
        new("ngu_hanh", "Ngũ Hành", 3, "+20% công mọi hệ", new(AtkPct: 20)),
        new("tich_luy", "Tích Lũy", 3, "Thêm +3% công mỗi tầng đã qua", new(PerFloorAtk: 3)),
        new("tram_yeu", "Trảm Yêu", 3, "+20% công với Tinh anh/Boss", new(EliteAtkPct: 20)),
    ];

    /// <summary>Tâm Ấn: thưởng lớn khi dùng đúng công pháp hệ đó, phạt nhẹ nếu công pháp đang dùng bị hệ đó khắc.</summary>
    private static Relic Seal(string el, string name, string icon) =>
        new($"tam_an_{el}", $"{name} Tâm Ấn", "ngu_hanh", icon, $"Công pháp {name} +{ElementPct}% sát thương; công pháp bị {name} khắc −{ElementWeakPct}%", new(), el);

    public static readonly Relic[] Relics =
    [
        new("huyet_bao", "Huyết Bạo Châu", "huyet", "🩸", "Mỗi đòn bạo hồi 2% máu", new(CritHealPct: 2)),
        new("am_huyet", "Ẩm Huyết Bôi", "huyet", "🍷", "Thắng trận hồi 8% máu", new(WinHealPct: 8)),
        new("cuong_huyet", "Cuồng Huyết Phù", "huyet", "📜", "+35% công, −20% máu tối đa", new(AtkPct: 35, MaxHpPct: -20)),
        Seal("hoa", "Hỏa", "🔥"),
        Seal("thuy", "Thủy", "💧"),
        Seal("moc", "Mộc", "🌿"),
        Seal("tho", "Thổ", "⛰️"),
        Seal("loi", "Lôi", "⚡"),
        new("bac_thang", "Bậc Thang Ngọc", "tich_luy", "🪜", "Mỗi tầng đã qua +3% công", new(PerFloorAtk: 3)),
        new("tu_bao", "Tụ Bảo Bồn", "tich_luy", "💰", "Hư Thạch nhận +50%", new(StonePct: 50)),
        new("huyen_quy", "Huyền Quy Giáp", "tich_luy", "🐢", "Giảm 15% sát thương nhận", new(ReducePct: 15)),
        new("tram_tinh", "Trảm Tinh Kiếm", "tram_yeu", "🗡️", "+30% sát thương với Tinh anh/Boss", new(EliteAtkPct: 30)),
        new("pha_ma", "Phá Ma Phù", "tram_yeu", "🧿", "Giảm 30% sát thương của Tinh anh/Boss", new(EliteReducePct: 30)),
        new("ho_than", "Hộ Thân Ngọc", "tram_yeu", "💠", "+20% máu tối đa", new(MaxHpPct: 20)),
    ];
    public static readonly IReadOnlyDictionary<string, Relic> RelicById = Relics.ToDictionary(r => r.Id);

    /// <summary>Bộ đang kích hoạt (đủ Need món cùng bộ).</summary>
    public static IEnumerable<SetDef> ActiveSets(IEnumerable<string> relics)
    {
        var owned = relics.Where(RelicById.ContainsKey).Select(id => RelicById[id].Set).ToList();
        return Sets.Where(s => owned.Count(x => x == s.Id) >= s.Need);
    }

    /// <summary>Tổng hiệu ứng cổ vật + bộ, đã tính Tâm Ấn theo công pháp đang dùng (heroElement).</summary>
    public static Mods Total(IEnumerable<string> relics, string heroElement)
    {
        var list = relics.Where(RelicById.ContainsKey).Select(id => RelicById[id]).ToList();
        var m = new Mods();
        foreach (var r in list)
        {
            m += r.Mods;
            if (r.Element.Length == 0) continue;
            if (r.Element == heroElement) m += new Mods(AtkPct: ElementPct);
            else if (ElementCounter.Edge(r.Element, heroElement) == 1) m += new Mods(AtkPct: -ElementWeakPct);
        }
        foreach (var s in ActiveSets(list.Select(r => r.Id))) m += s.Bonus;
        return m;
    }

    /// <summary>Hệ số công (nhân vào sát thương) cho trận ở tầng floor với quái loại kind.</summary>
    public static double AtkMult(Mods m, int cleared, string kind) =>
        Math.Max(.3, 1 + (m.AtkPct + m.PerFloorAtk * cleared + (kind is Elite or Boss ? m.EliteAtkPct : 0)) / 100);
    /// <summary>% sát thương nhận được giảm (trần 70%).</summary>
    public static double Reduce(Mods m, string kind) => Math.Clamp(m.ReducePct + (kind is Elite or Boss ? m.EliteReducePct : 0), 0, 70);
    public static double MaxHpMult(Mods m) => Math.Max(.2, 1 + m.MaxHpPct / 100);

    // ---- kỳ ngộ ----
    /// <summary>Kết quả một lối: Hp (tỉ lệ máu tối đa, ±), Stones, Relic (nhận 1 cổ vật ngẫu nhiên).</summary>
    public record Outcome(string Text, double Hp = 0, int Stones = 0, bool Relic = false);
    public record Choice(string Label, double Chance, Outcome Win, Outcome? Lose = null);
    public record EventDef(string Id, string Title, string Text, Choice A, Choice B);
    public static readonly EventDef[] Events =
    [
        new("bia_da", "Bia Đá Cổ", "Một tấm bia khắc kín đạo văn, mỗi nét chữ như muốn hút thần thức người nhìn.",
            new("Lĩnh ngộ (−10% máu)", 1, new("Đạo văn khắc vào thần thức, ngươi ngộ ra một món cổ vật.", Hp: -.10, Relic: true)),
            new("Rời đi", 1, new("Ngươi nhặt vài mảnh Hư Thạch rơi quanh bia.", Stones: 3))),
        new("linh_qua", "Linh Quả Lạ", "Trên cành khô treo một trái quả đỏ thẫm, linh khí lúc mạnh lúc yếu.",
            new("Ăn ngay (60%)", .6, new("Linh khí tràn khắp kinh mạch, thương thế lành hẳn.", Hp: .30), new("Quả có độc! Kinh mạch đau rát.", Hp: -.15)),
            new("Cất đi bán", 1, new("Thương nhân trả giá hời.", Stones: 5))),
        new("tan_hon", "Tàn Hồn Kiếm Tu", "Tàn hồn một kiếm tu tóc bạc đứng chờ người kế thừa, kiếm ý vẫn sắc lạnh.",
            new("Nhận truyền thừa (−20% máu)", 1, new("Kiếm ý xé thân nhưng để lại một món cổ vật.", Hp: -.20, Relic: true)),
            new("Siêu độ", 1, new("Tàn hồn mỉm cười tan đi, để lại một luồng sinh khí.", Hp: .20))),
        new("liet_khau", "Hư Không Liệt Khẩu", "Khe nứt hư không lấp lánh Hư Thạch, gió loạn bên trong có thể cắt da thịt.",
            new("Thò tay vào (50%)", .5, new("Ngươi rút ra một nắm Hư Thạch!", Stones: 12), new("Gió hư không quét qua cánh tay.", Hp: -.25)),
            new("Lùi lại", 1, new("Cẩn tắc vô áy náy."))),
        new("dan_lo", "Đan Lô Bỏ Hoang", "Lò đan cổ còn chút hỏa chủng, đáy lò đọng lớp cặn đan óng ánh.",
            new("Nhóm lửa điều tức", 1, new("Hơi ấm lò đan giúp ngươi hồi phục.", Hp: .30)),
            new("Đập lò lấy cặn", 1, new("Cặn đan kết tinh thành Hư Thạch.", Stones: 7))),
        new("guong", "Gương Thái Hư", "Mặt gương phản chiếu một ngươi khác, ánh mắt đầy sát khí.",
            new("Nhìn vào (70%)", .7, new("Bóng trong gương trao cho ngươi một món cổ vật.", Relic: true), new("Bóng trong gương lao ra cắn trả!", Hp: -.30)),
            new("Đập vỡ", 1, new("Mảnh gương hóa thành Hư Thạch.", Stones: 4))),
    ];
    public static readonly IReadOnlyDictionary<string, EventDef> EventById = Events.ToDictionary(e => e.Id);

    // ---- sinh ngẫu nhiên tất định theo Seed ----
    public static Random Rng(int seed, int floor, int salt) => new(unchecked(seed * 7919 + floor * 104729 + salt * 15485863));

    /// <summary>Các cửa của tầng: Boss ở tầng 5/10/15; tầng trước Boss luôn có Linh tuyền; luôn có ít nhất một cửa đánh.</summary>
    public static string[] Doors(int seed, int floor)
    {
        if (IsBossFloor(floor)) return [Boss];
        var rng = Rng(seed, floor, 1);
        var n = floor == 1 ? 2 : 2 + rng.Next(2);
        var pool = new List<(string Kind, int W)> { (Fight, 40), (Event, 20), (Spring, 12) };
        if (floor >= 2) pool.Add((Elite, 18));
        if (floor >= 3) pool.Add((Shop, 12));
        var doors = new List<string>();
        if (floor % 5 == 4) doors.Add(Spring);
        while (doors.Count < n)
        {
            var left = pool.Where(x => !doors.Contains(x.Kind)).ToList();
            var r = rng.Next(left.Sum(x => x.W));
            foreach (var (kind, w) in left) { if (r < w) { doors.Add(kind); break; } r -= w; }
        }
        if (!doors.Any(d => d is Fight or Elite)) doors[^1] = Fight;
        return doors.ToArray();
    }

    /// <summary>Quái sau cửa thứ door của tầng (thường / tinh anh).</summary>
    public static Mon MonAt(int seed, int floor, int door, string kind)
    {
        var pool = kind == Elite ? Elites : Fights;
        return pool[Rng(seed, floor, 10 + door).Next(pool.Length)];
    }

    public static EventDef EventAt(int seed, int floor) => Events[Rng(seed, floor, 2).Next(Events.Length)];

    /// <summary>Cổ vật mời chọn (3 món khác nhau, chưa có), cố định theo tầng + salt.</summary>
    public static List<string> OfferRelics(int seed, int floor, int salt, IReadOnlyCollection<string> owned, int count = 3)
    {
        var rng = Rng(seed, floor, salt);
        return Relics.Select(r => r.Id).Where(id => !owned.Contains(id)).OrderBy(_ => rng.Next()).Take(count).ToList();
    }

    /// <summary>Chỉ số quái theo cảnh giới/tầng lúc vào, loại và tầng bí cảnh.</summary>
    public static MonsterDef Monster(Mon mon, string kind, int realm, int stage, int floor)
    {
        var baseDef = WanderingBoss.Monster(new WanderingBoss.Def("bicanh", mon.Name, mon.Image, "", 0, 0, ""), realm, stage);
        var (h, a, d) = kind switch { Elite => EliteMult, Boss => BossMult(floor), _ => FightMult };
        var ramp = 1 + FloorRamp * (floor - 1);
        var m = baseDef with
        {
            Hp = Math.Max(1, CombatNumber.Round((double)baseDef.Hp * h * ramp)),
            Atk = Math.Max(1, CombatNumber.Round((double)baseDef.Atk * a * ramp)),
            Def = CombatNumber.Round((double)baseDef.Def * d),
            Tier = kind == Fight ? "" : kind == Elite ? "elite" : "boss", Id = "",
        };
        // chỉ số phụ (bạo, né, liên kích…) theo loại quái như quái ải, không giữ mức Boss lang thang; Boss giảm như Boss ải
        return MonsterCombatRatings.Apply(m, WanderingBoss.DiffFor(realm), reduceBoss: true);
    }

    // ---- thưởng ----
    public const double StoneRealmFactor = 2.5;         // cân bằng 11/2026: mỗi cảnh giới nhân 2,5 (trước dùng KyDuyen.StoneUnit nhân 5)
    public static double StoneUnit(int realm) => 50 * Math.Pow(StoneRealmFactor, Math.Clamp(realm, 0, KyDuyen.Books.Length - 1));
    public const double StoneRewardMult = .5;           // cân bằng 11/2026: giảm linh thạch cuối ải còn một nửa
    public const int IronPerFloor = 4, IronPerBoss = 10;   // cân bằng 11/2026: tăng Tinh Thiết (trước 2/tầng + 5/Boss)
    /// <summary>Thưởng theo số tầng đã qua. stoneRng chọn loại tinh thạch.</summary>
    public static Dictionary<string, int> Rewards(int cleared, int realm, Random stoneRng)
    {
        var r = new Dictionary<string, int>();
        if (cleared <= 0) return r;
        var bosses = BossFloors.Count(f => f <= cleared);
        r["linh_thach"] = (int)Math.Min(int.MaxValue, StoneUnit(realm) * (cleared * (cleared + 1) / 2) * StoneRewardMult);
        r[ForgeData.Iron] = IronPerFloor * cleared + IronPerBoss * bosses;
        var pills = cleared / 3;
        if (pills > 0) r[GameData.PillChain[Math.Clamp(realm, 0, GameData.PillChain.Length - 1)]] = pills;
        var gems = (realm < 3 ? GemData.SoStones : GemData.SoStones.Concat(GemData.CaoStones)).OrderBy(x => x).ToArray();
        for (var i = 0; i < bosses; i++) { var g = gems[stoneRng.Next(gems.Length)]; r[g] = r.GetValueOrDefault(g) + 1; }
        if (cleared >= MaxFloor) r["ruong_hiem"] = 1;
        return r;
    }

    // ---- lưu ----
    public class Fought
    {
        public string Name { get; set; } = ""; public string Image { get; set; } = ""; public string Kind { get; set; } = "";
        public bool Win { get; set; } public int Turns { get; set; } public int Crits { get; set; }
        public double HpBefore { get; set; } public double HpAfter { get; set; } public double Healed { get; set; }
        public decimal MonsterHp { get; set; } public decimal MonsterAtk { get; set; } public string Note { get; set; } = "";
        public DuelDto? Duel { get; set; }   // diễn biến trận (panel thông tin trận chiến)
    }
    public class Run
    {
        public int Seed { get; set; } public int Entry { get; set; } public int Realm { get; set; } public int Stage { get; set; }
        public int Floor { get; set; } = 1; public int Cleared { get; set; }
        public double Hp { get; set; } = 1; public int Stones { get; set; }
        public List<string> Relics { get; set; } = [];
        public string Phase { get; set; } = "doors";
        public List<string> Offer { get; set; } = [];
        public string EventId { get; set; } = "";
        public List<int> ShopBought { get; set; } = [];
        public List<string> Log { get; set; } = [];
        public List<Fought> Fights { get; set; } = [];
        public Dictionary<string, int>? Rewards { get; set; }
        public string End { get; set; } = "";
        public void Note(string line) { Log.Add(line); if (Log.Count > 12) Log.RemoveAt(0); }
    }
    public class Save
    {
        public string Day { get; set; } = ""; public int Entries { get; set; } public int Best { get; set; }
        public Run? Run { get; set; }
    }

    public static Save Load(Player p)
    {
        if (string.IsNullOrWhiteSpace(p.BiCanhState)) return new();
        try { return JsonSerializer.Deserialize<Save>(p.BiCanhState) ?? new(); }
        catch (JsonException) { return new(); }
    }
    public static void Store(Player p, Save s) => p.BiCanhState = JsonSerializer.Serialize(s);
    /// <summary>Chỉ giữ ngày + số lượt đã vào (tán công trùng tu không được thêm lượt).</summary>
    public static string KeepDaily(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        Save s;
        try { s = JsonSerializer.Deserialize<Save>(json) ?? new(); } catch (JsonException) { return ""; }
        return JsonSerializer.Serialize(new Save { Day = s.Day, Entries = s.Entries, Best = s.Best });
    }
}
