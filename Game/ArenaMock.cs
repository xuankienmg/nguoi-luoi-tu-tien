using System.Text.Json;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>
/// Lôi đài giả để thử trong môi trường dev (ArenaService.Mock): 18 đối thủ Hóa Thần – Luyện Hư mặc đồ thật của cảnh giới đó,
/// mang đòn thường + tuyệt kỹ của một hệ (Hỏa / Thủy / Mộc) và vài hiệu ứng trang bị. Trận tính bằng PvpCombat như bản thật,
/// chỗ đứng theo điểm (không đổi chỗ khi khiêu chiến). Chỉ nằm trong bộ nhớ: tắt game là mất, không đụng Supabase.
/// </summary>
public sealed class ArenaMock
{
    // luật như arena_rules() nhưng nới lượt để thử thoải mái
    private const int MockDaily = 9999, MinWindow = 5, WindowPct = 20;

    private sealed class Entry
    {
        public Guid Id; public string Name = ""; public int Realm, Stage; public PvpFighter? Fighter; public decimal Power;
        public int Wins, Losses;
        // hạng Đồng → Cao Thủ (như Lôi đài online): điểm, trận bảo vệ sau khi lên hạng, thưởng đã nhận, trận thật gần nhất
        public int Points, Protect;
        public string DailyClaimDay = "", WeeklyClaimWeek = "";
        public DateTime LastRealFight;
    }

    private static int TierOf(Entry e) => ArenaTierData.IndexOf(e.Points);
    /// <summary>Hạng trong bậc thang của hạng mình đang đứng (vị trí trong _ladder tính riêng từng hạng).</summary>
    /// <summary>Chỗ đứng trong hạng theo ĐIỂM (nhiều điểm đứng trên); bằng điểm thì vào trước. Khiêu chiến không đổi chỗ.</summary>
    private List<Entry> SameTier(Entry me) { var t = TierOf(me); return _ladder.Where(x => TierOf(x) == t).OrderByDescending(x => x.Points).ToList(); }   // OrderBy ổn định: bằng điểm giữ thứ tự cũ
    private long RankOf(Entry e) => SameTier(e).IndexOf(e) + 1;

    private static readonly string[] Names =
    [
        "Hàn Lập", "Mặc Đại Phu", "Nam Cung Uyển", "Lệ Phi Vũ", "Tử Linh", "Nguyên Dao", "Lý Hóa Nguyên", "Ôn Thiên Nhân", "Bạch Dao Di",
        "Hướng Chi Lễ", "Thiên Lan Thánh Nữ", "Bảo Hoa", "Cực Âm Tổ Sư", "Vạn Thiên Minh", "Lục Diệp Tôn Giả", "Phong Hi", "Ngân Nguyệt", "Thanh Dịch Cư Sĩ",
        "Diệp Phàm", "Tiêu Viêm", "Vân Triệt", "Mộ Dung Tuyết", "Lăng Hàn", "Đoạn Thiên Nhai", "Tô Mạc Già", "Cố Thanh Sơn", "Lâm Động",
        "Thạch Hạo", "Dương Khai", "Tần Vũ", "Mạnh Hạo", "Bạch Tiểu Thuần", "Phương Nguyên", "Lý Thất Dạ", "Tiêu Dật", "Hạ Hầu Đạo",
        "Cổ Nguyệt Phương Nguyên", "Chu Thanh", "Sở Phong", "Viên Cảnh Thiên", "Hoa Thanh Cung", "Liễu Thần", "Dạ Vô Tình", "Trần Bình An",
    ];
    // cân bằng 11/2026: đủ 7 hệ (hoa, thủy, mộc, thổ, lôi, băng, gió) để thử hoạt ảnh mọi hệ
    private static readonly string[] Elements = ["hoa", "thuy", "moc", "tho", "loi", "bang", "gio"];

    private readonly object _gate = new();
    private readonly List<Entry> _ladder = [];          // vị trí = chỉ số + 1
    private readonly List<ArenaService.HistoryRow> _history = [];
    private readonly Dictionary<long, (Guid me, Guid foe, long myRank, long foeRank, bool practice)> _pending = [];
    private int _used; private long _nextId = 1;

    public ArenaMock()
    {
        var rng = new Random(20261024);
        var i = 0;
        for (var realm = GameData.Realms.Count - 1; realm >= 0; realm--)      // mọi cảnh giới, mỗi cảnh giới 3 đối thủ ở tầng cuối / giữa / đầu
        {
            var last = GameData.Realms[realm].Stages.Length - 1;
            foreach (var stage in new[] { last, last / 2, 0 })
            {
                var f = FakeFighter(Names[i % Names.Length], realm, stage, Elements[i % Elements.Length], rng);
                _ladder.Add(new Entry { Id = new Guid(i + 1, 0x4d4f, 0x434b, 0, 0, 0, 0, 0, 0, 0, (byte)(i + 1)), Name = f.Name, Realm = realm, Stage = stage, Fighter = f, Power = Power(f) });
                i++;
            }
        }
        _ladder.Sort((a, b) => b.Power.CompareTo(a.Power));
        foreach (var e in _ladder) { e.Wins = rng.Next(0, 30); e.Losses = rng.Next(0, 20); }
        // rải đối thủ giả đều 6 hạng theo lực chiến (mạnh nhất → Cao Thủ) để thử mọi hạng; người chơi vào từ hạng Đồng
        var tiers = ArenaTierData.Tiers; var per = Math.Max(1, (_ladder.Count + tiers.Length - 1) / tiers.Length);
        for (var k = 0; k < _ladder.Count; k++)
        {
            var t = tiers.Length - 1 - Math.Min(k / per, tiers.Length - 1);
            var span = t + 1 < tiers.Length ? tiers[t + 1].Min - tiers[t].Min : 500;
            _ladder[k].Points = tiers[t].Min + rng.Next(0, span);
        }
        // lịch sử mẫu để tab Lôi đài hiện đủ cả thắng / thua / tỷ thí
        for (var h = 0; h < 8; h++)
        {
            var foe = _ladder[rng.Next(_ladder.Count)];
            var win = rng.Next(2) == 0; var practice = h % 4 == 3;
            long from = 20 + rng.Next(10), to = win && !practice ? from - rng.Next(1, 4) : from;
            _history.Add(new ArenaService.HistoryRow(_nextId++, h % 3 != 2, foe.Name, win, from, to, DateTime.UtcNow.AddMinutes(-35 * (8 - h)), practice));
        }
    }

    /// <summary>Danh hiệu giả cho top 3 (rải đủ 6 danh hiệu) để thử hiển thị danh hiệu cạnh tên.</summary>
    public static Dictionary<Guid, List<TitleDto>> MockTitles(ArenaService.StateRow s)
    {
        string[][] by = [["arena", "power"], ["realm", "progress"], ["pet", "alchemy"]];
        var d = new Dictionary<Guid, List<TitleDto>>();
        foreach (var row in s.Top.Take(3)) d[row.PlayerId] = by[(int)row.Rank - 1].Select(k => Titles.ById[k]).ToList();
        return d;
    }

    /// <summary>Chỉ số gốc + một món đồ thật cho mỗi ô (phẩm Linh → Địa ngẫu nhiên), đòn thường + tuyệt kỹ mạnh nhất đã mở của hệ, vài hiệu ứng trang bị.</summary>
    private static PvpFighter FakeFighter(string name, int realm, int stage, string element, Random rng)
    {
        var r = GameData.Realms[realm];
        var b = GameData.BaseStats(r, stage);
        decimal atk = b.Atk, hp = b.Hp, def = b.Def; double crit = 5, speed = GameData.BaseSpeed[Math.Min(realm, GameData.BaseSpeed.Length - 1)];
        var ratings = new CombatRatings();
        foreach (var slot in r.EquipSlots)
        {
            var pool = GameData.AllItems.Where(x => x.Slot == slot && x.MinRealm == realm
                && Array.IndexOf(GameData.RarityOrder, x.Rarity) is >= 1 and <= 3).ToList();
            if (pool.Count == 0) continue;
            var it = pool[rng.Next(pool.Count)];
            atk += it.Atk; hp += it.Hp; def += it.Def; crit += it.CritPct; speed += it.SpeedPct;
            ratings = ratings.Add(CombatRatings.From(it));
        }
        var talent = 1 + rng.NextDouble() * .25;           // công pháp / linh thú: +0–25%
        var basic = SkillData.Skills.First(s => s.Element == element && s.Tree == "basic");
        var ult = SkillData.Skills.Where(s => s.Element == element && s.Tree == "active" && s.DmgBase > 0 && s.MinRealm <= realm)
            .OrderByDescending(s => s.MinRealm).ThenByDescending(s => s.DmgBase).FirstOrDefault();
        List<PvpSkill> skills = ult is null ? [] : [new PvpSkill(ult.Id, Math.Max(1, ult.MaxLv * (2 + rng.Next(3)) / 4))];
        return new PvpFighter(name, realm, stage, r.Hero, r.HeroStand,
            CombatNumber.Scale(atk, talent), CombatNumber.Scale(hp, talent), CombatNumber.Scale(def, talent), crit, speed, .5,
            basic.Id, basic.DmgBase, basic.Crit, skills, element,
            rng.Next(0, 30), rng.Next(0, 25), element == "hoa" && rng.Next(2) == 0,
            rng.Next(0, 15), CombatDodge.Chance(speed, rng.Next(0, 6), 0), rng.Next(0, 10),
            rng.Next(0, 3) == 0 ? 8 : 0, rng.Next(0, 3) == 0 ? 60 : 0, rng.Next(0, 3) == 0 ? 15 : 0, 40,
            rng.Next(0, 3), rng.Next(0, 3) == 0 ? 10 : 0, rng.Next(0, 12), rng.Next(0, 4) * 10,
            element == "moc" ? 3 : 0, element == "moc" ? 10 : 0, 0,
            CritResist: ratings.CritResist, Accuracy: ratings.Accuracy, ComboRating: ratings.Combo,
            ComboResist: ratings.ComboResist, ReflectResist: ratings.ReflectResist) with { Reflect = ratings.Reflect, Pet = FakePet(element, atk, hp, def) };
    }

    /// <summary>Đối thủ giả mang linh thú cùng hệ (dạng 2, phẩm Thượng) để thử hoạt ảnh linh thú đối thủ trong Lôi đài.</summary>
    private static PvpPet FakePet(string element, decimal atk, decimal hp, decimal def)
    {
        var id = element switch { "thuy" => "bich_thuy_giao", "moc" => "thanh_moc_loc", "tho" => "kim_giap_quy", "loi" => "loi_van_dieu", "gio" => "thanh_phong_hac", "bang" => "bang_tinh_tho", _ => "hoa_linh_ho" };
        return new PvpPet(id, 2, 2, 0, 3, CombatNumber.Scale(atk, .25), CombatNumber.Scale(hp, .25), CombatNumber.Scale(def, .25));
    }

    private static decimal Power(PvpFighter s) => CombatRatings.Power(s.Atk, s.Def, s.Hp, new(
        s.Crit, s.CritResist, s.HitAccuracy, s.Dodge, s.Combo, s.ComboResist, s.Reflect, s.ReflectResist));
    private static long Window(long rank) => Math.Max(MinWindow, (long)Math.Ceiling(rank * WindowPct / 100.0));

    /// <summary>Đưa người chơi vào cuối bậc thang (lần đầu) và cập nhật tên / cảnh giới / lực chiến hiện tại.</summary>
    private Entry Me(Player p)
    {
        var me = _ladder.FirstOrDefault(e => e.Id == p.Id);
        if (me is null) { me = new Entry { Id = p.Id }; _ladder.Add(me); }
        me.Name = p.Name; me.Realm = p.Realm; me.Stage = p.Stage; me.Power = GameService.Power(p);
        return me;
    }

    private ArenaService.Row Row(Entry e) => new(e.Id, e.Name, e.Realm, e.Stage, GameData.Realms[e.Realm].Name,
        GameData.Realms[e.Realm].Stages[Math.Clamp(e.Stage, 0, GameData.Realms[e.Realm].Stages.Length - 1)], e.Power, e.Wins, e.Losses, RankOf(e), e.Points);

    public ArenaService.StateRow State(Player p)
    {
        lock (_gate)
        {
            var me = Me(p);
            var same = SameTier(me);                                                // chỉ thấy và thách đấu người cùng hạng
            long rank = RankOf(me), w = Window(rank);
            var now = DateTime.UtcNow; var real = me.LastRealFight != default;
            var tier = new ArenaService.TierRow(me.Points, me.Protect,
                real && ArenaTierData.DayKey(me.LastRealFight) == ArenaTierData.DayKey(now),
                real && ArenaTierData.WeekKey(me.LastRealFight) == ArenaTierData.WeekKey(now),
                me.DailyClaimDay == ArenaTierData.DayKey(now), me.WeeklyClaimWeek == ArenaTierData.WeekKey(now));
            return new ArenaService.StateRow(Row(me), same.Take(3).Select(Row).ToList(),
                Targets(me, rank, w),
                w, _used, MockDaily, null, same.Count, _history.AsEnumerable().Reverse().Take(20).ToList(), tier);
        }
    }

    /// <summary>Thưởng hạng (như bản online): cần đã đánh trận thật trong ngày / tuần, chưa nhận; thưởng tuần nhận vào Chủ nhật.</summary>
    public int ClaimCheck(Player p, bool weekly)
    {
        lock (_gate)
        {
            var me = Me(p); var now = DateTime.UtcNow; var real = me.LastRealFight != default;
            if (weekly)
            {
                if (!ArenaTierData.IsSunday(now)) throw new GameException("Thưởng tuần Lôi đài nhận vào Chủ nhật.");
                if (me.WeeklyClaimWeek == ArenaTierData.WeekKey(now)) throw new GameException("Đã nhận thưởng tuần này.");
                if (!real || ArenaTierData.WeekKey(me.LastRealFight) != ArenaTierData.WeekKey(now)) throw new GameException("Cần đánh ít nhất 1 trận Lôi đài (không tính tỷ thí) trong tuần.");
            }
            else
            {
                if (me.DailyClaimDay == ArenaTierData.DayKey(now)) throw new GameException("Hôm nay đã nhận thưởng hạng.");
                if (!real || ArenaTierData.DayKey(me.LastRealFight) != ArenaTierData.DayKey(now)) throw new GameException("Cần đánh ít nhất 1 trận Lôi đài (không tính tỷ thí) hôm nay.");
            }
            return TierOf(me);
        }
    }

    public List<ArenaService.Row> TierBoard(Player p, int tier, int limit)
    {
        lock (_gate)
        {
            Me(p);
            var t = Math.Clamp(tier, 0, ArenaTierData.Tiers.Length - 1);
            return _ladder.Where(x => TierOf(x) == t).OrderByDescending(x => x.Points).Take(limit).Select(Row).ToList();
        }
    }

    public void ClaimMark(Player p, bool weekly)
    {
        lock (_gate)
        {
            var me = Me(p); var now = DateTime.UtcNow;
            if (weekly) me.WeeklyClaimWeek = ArenaTierData.WeekKey(now); else me.DailyClaimDay = ArenaTierData.DayKey(now);
        }
    }

    /// <summary>Như arena_state: bản thân, mọi người trong tầm phía trên và 7 người có hạng gần nhất (trừ top 3), chỉ trong cùng hạng.</summary>
    private List<ArenaService.Row> Targets(Entry me, long rank, long w)
    {
        var rows = SameTier(me).Select(Row).ToList();                  // gồm cả top 3 (không còn khối Tam cường)
        var near = rows.Where(x => x.PlayerId != me.Id).OrderBy(x => Math.Abs(x.Rank - rank)).ThenBy(x => x.Rank).Take(7).Select(x => x.PlayerId).ToHashSet();
        return rows.Where(x => x.PlayerId == me.Id || x.Rank >= rank - w && x.Rank < rank || near.Contains(x.PlayerId)).ToList();
    }

    public ArenaService.BeginRow Begin(Player p, Guid target)
    {
        lock (_gate)
        {
            if (target == p.Id) throw new GameException("Không thể tự khiêu chiến chính mình.");
            if (_used >= MockDaily) throw new GameException($"Đã dùng hết {MockDaily} lượt khiêu chiến hôm nay. Làm mới lúc 0h.");
            var me = Me(p);
            var foe = _ladder.FirstOrDefault(e => e.Id == target) ?? throw new GameException("Đối thủ không còn ở Lôi đài.");
            if (TierOf(foe) != TierOf(me)) throw new GameException("Chỉ thách đấu được người cùng hạng.");
            long myRank = RankOf(me), foeRank = RankOf(foe);
            var practice = ArenaTierData.IsPractice(TierOf(me), myRank, foeRank);   // xếp dưới: tỷ thí, không đổi hạng (hạng cao nhất: tính điểm)
            if (!practice && myRank - foeRank > Window(myRank)) throw new GameException($"Đối thủ ngoài tầm: chỉ khiêu chiến được trong {Window(myRank)} hạng phía trên.");
            _used++;
            var id = _nextId++;
            _pending[id] = (me.Id, foe.Id, myRank, foeRank, practice);
            return new ArenaService.BeginRow(id, Random.Shared.NextInt64(2147483647), foe.Name, foe.Realm,
                foe.Fighter is null ? null : JsonSerializer.SerializeToElement(foe.Fighter), null, myRank, foeRank, practice);
        }
    }

    public ArenaService.FinishRow Finish(Player p, long fight, bool win)
    {
        lock (_gate)
        {
            if (!_pending.Remove(fight, out var f) || f.me != p.Id) throw new GameException("Trận này đã kết thúc hoặc hết hạn.");
            var me = _ladder.First(e => e.Id == f.me); var foe = _ladder.FirstOrDefault(e => e.Id == f.foe);
            long myRank = RankOf(me), foeRank = foe is null ? 0 : RankOf(foe);
            var sameTier = foe is not null && TierOf(foe) == TierOf(me);          // hai bên còn cùng hạng mới đổi chỗ / tính điểm
            if (!f.practice)                                                         // tỷ thí: không tính thắng / thua
                if (win) { me.Wins++; if (foe is not null) foe.Losses++; } else { me.Losses++; if (foe is not null) foe.Wins++; }
            // điểm hạng (như ServerArena): thắng +25..+40, thua −15 (trừ khi còn trận bảo vệ); đổi hạng thì xuống cuối bậc thang hạng mới
            int delta = 0, oldTier = TierOf(me); var ranked = !f.practice && sameTier;
            if (ranked)
            {
                if (win) delta = ArenaTierData.WinPoints(myRank, foeRank);
                else if (me.Protect > 0) me.Protect--;
                else delta = -Math.Min(ArenaTierData.LosePoints, me.Points);
                me.Points += delta;
                var nt = TierOf(me);
                if (nt > oldTier) me.Protect = ArenaTierData.ProtectMatches; else if (nt < oldTier) me.Protect = 0;
                // đổi hạng: chỗ đứng tự theo điểm trong hạng mới, không dời vị trí
            }
            if (!f.practice) me.LastRealFight = DateTime.UtcNow;
            var newRank = RankOf(me);                                              // chỗ đứng mới theo điểm vừa đổi
            _history.Add(new ArenaService.HistoryRow(fight, true, foe?.Name ?? "?", win, myRank, newRank, DateTime.UtcNow, f.practice));
            return new ArenaService.FinishRow(myRank, newRank, delta, me.Points, oldTier, TierOf(me), ranked);
        }
    }
}
