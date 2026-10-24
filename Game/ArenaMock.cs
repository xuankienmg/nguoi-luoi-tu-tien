using System.Text.Json;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>
/// Lôi đài giả để thử trong môi trường dev (ArenaService.Mock): 18 đối thủ Hóa Thần – Luyện Hư mặc đồ thật của cảnh giới đó,
/// mang đòn thường + tuyệt kỹ của một hệ (Hỏa / Thủy / Mộc) và vài hiệu ứng trang bị. Trận tính bằng PvpCombat như bản thật,
/// thắng thì đổi vị trí. Chỉ nằm trong bộ nhớ: tắt game là mất, không đụng Supabase.
/// </summary>
public sealed class ArenaMock
{
    // luật như arena_rules() nhưng nới lượt để thử thoải mái
    private const int MockDaily = 99, MinWindow = 5, WindowPct = 20;

    private sealed class Entry
    {
        public Guid Id; public string Name = ""; public int Realm, Stage; public PvpFighter? Fighter; public long Power;
        public int Wins, Losses;
    }

    private static readonly string[] Names =
    [
        "Hàn Lập", "Mặc Đại Phu", "Nam Cung Uyển", "Lệ Phi Vũ", "Tử Linh", "Nguyên Dao", "Lý Hóa Nguyên", "Ôn Thiên Nhân", "Bạch Dao Di",
        "Hướng Chi Lễ", "Thiên Lan Thánh Nữ", "Bảo Hoa", "Cực Âm Tổ Sư", "Vạn Thiên Minh", "Lục Diệp Tôn Giả", "Phong Hi", "Ngân Nguyệt", "Thanh Dịch Cư Sĩ",
    ];
    private static readonly string[] Elements = ["hoa", "thuy", "moc"];

    private readonly object _gate = new();
    private readonly List<Entry> _ladder = [];          // vị trí = chỉ số + 1
    private readonly List<ArenaService.HistoryRow> _history = [];
    private readonly Dictionary<long, (Guid me, Guid foe, long myRank, long foeRank, bool practice)> _pending = [];
    private int _used; private long _nextId = 1;

    public ArenaMock()
    {
        var rng = new Random(20261024);
        var i = 0;
        foreach (var realm in new[] { 5, 4 })            // Luyện Hư, Hóa Thần
            for (var stage = GameData.Realms[realm].Stages.Length - 1; stage >= 0; stage--)
                for (var k = 0; k < 3; k++, i++)
                {
                    var f = FakeFighter(Names[i], realm, stage, Elements[i % Elements.Length], rng);
                    _ladder.Add(new Entry { Id = new Guid(i + 1, 0x4d4f, 0x434b, 0, 0, 0, 0, 0, 0, 0, (byte)(i + 1)), Name = f.Name, Realm = realm, Stage = stage, Fighter = f, Power = Power(f) });
                }
        _ladder.Sort((a, b) => b.Power.CompareTo(a.Power));
        foreach (var e in _ladder) { e.Wins = rng.Next(0, 30); e.Losses = rng.Next(0, 20); }
    }

    /// <summary>Chỉ số gốc + một món đồ thật cho mỗi ô (phẩm Linh → Địa ngẫu nhiên), đòn thường + tuyệt kỹ mạnh nhất đã mở của hệ, vài hiệu ứng trang bị.</summary>
    private static PvpFighter FakeFighter(string name, int realm, int stage, string element, Random rng)
    {
        var r = GameData.Realms[realm];
        var b = GameData.BaseStats(r, stage);
        int atk = b.Atk, hp = b.Hp, def = b.Def; double crit = 5, speed = GameData.BaseSpeed[Math.Min(realm, GameData.BaseSpeed.Length - 1)];
        foreach (var slot in r.EquipSlots)
        {
            var pool = GameData.AllItems.Where(x => x.Slot == slot && x.MinRealm == realm
                && Array.IndexOf(GameData.RarityOrder, x.Rarity) is >= 1 and <= 3).ToList();
            if (pool.Count == 0) continue;
            var it = pool[rng.Next(pool.Count)];
            atk += it.Atk; hp += it.Hp; def += it.Def; crit += it.CritPct; speed += it.SpeedPct;
        }
        var talent = 1 + rng.NextDouble() * .25;           // công pháp / linh thú: +0–25%
        var basic = SkillData.Skills.First(s => s.Element == element && s.Tree == "basic");
        var ult = SkillData.Skills.Where(s => s.Element == element && s.Tree == "active" && s.DmgBase > 0 && s.MinRealm <= realm)
            .OrderByDescending(s => s.MinRealm).ThenByDescending(s => s.DmgBase).FirstOrDefault();
        List<PvpSkill> skills = ult is null ? [] : [new PvpSkill(ult.Id, Math.Max(1, ult.MaxLv * (2 + rng.Next(3)) / 4))];
        return new PvpFighter(name, realm, stage, r.Hero, r.HeroStand,
            (int)(atk * talent), (int)(hp * talent), (int)(def * talent), Math.Min(100, crit), Math.Min(60, speed), .5,
            basic.Id, basic.DmgBase, basic.Crit, skills, element,
            rng.Next(0, 30), rng.Next(0, 25), element == "hoa" && rng.Next(2) == 0,
            rng.Next(0, 15), CombatDodge.Chance(Math.Min(60, speed), rng.Next(0, 6), 0), rng.Next(0, 10),
            rng.Next(0, 3) == 0 ? 8 : 0, rng.Next(0, 3) == 0 ? 60 : 0, rng.Next(0, 3) == 0 ? 15 : 0, 40,
            rng.Next(0, 3), rng.Next(0, 3) == 0 ? 10 : 0, rng.Next(0, 12), rng.Next(0, 4) * 10,
            element == "moc" ? 3 : 0, element == "moc" ? 10 : 0, 0);
    }

    private static long Power(PvpFighter s) => (long)Math.Round(s.Atk * 2.0 + s.Def * 3.0 + s.Hp * .4 + (s.Crit + s.Speed) * 4);
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
        GameData.Realms[e.Realm].Stages[Math.Clamp(e.Stage, 0, GameData.Realms[e.Realm].Stages.Length - 1)], e.Power, e.Wins, e.Losses, _ladder.IndexOf(e) + 1);

    public ArenaService.StateRow State(Player p)
    {
        lock (_gate)
        {
            var me = Me(p);
            long rank = _ladder.IndexOf(me) + 1, w = Window(rank);
            return new ArenaService.StateRow(Row(me), _ladder.Take(3).Select(Row).ToList(),
                Targets(me, rank, w),
                w, _used, MockDaily, null, _ladder.Count, _history.AsEnumerable().Reverse().Take(20).ToList());
        }
    }

    /// <summary>Như arena_state: bản thân, mọi người trong tầm phía trên và 7 người có hạng gần nhất (trừ top 3).</summary>
    private List<ArenaService.Row> Targets(Entry me, long rank, long w)
    {
        var rows = _ladder.Select(Row).Where(x => x.Rank > 3).ToList();
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
            long myRank = _ladder.IndexOf(me) + 1, foeRank = _ladder.IndexOf(foe) + 1;
            var practice = foeRank > myRank;                                       // xếp dưới: tỷ thí, không đổi hạng
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
            long myRank = _ladder.IndexOf(me) + 1, foeRank = foe is null ? 0 : _ladder.IndexOf(foe) + 1;
            var swap = !f.practice && win && foe is not null && foeRank < myRank;
            if (swap) { int mi = (int)myRank - 1, fi = (int)foeRank - 1; (_ladder[mi], _ladder[fi]) = (foe!, me); }
            if (!f.practice)                                                         // tỷ thí: không tính thắng / thua
                if (win) { me.Wins++; if (foe is not null) foe.Losses++; } else { me.Losses++; if (foe is not null) foe.Wins++; }
            _history.Add(new ArenaService.HistoryRow(fight, true, foe?.Name ?? "?", win, myRank, swap ? foeRank : myRank, DateTime.UtcNow, f.practice));
            return new ArenaService.FinishRow(myRank, swap ? foeRank : myRank);
        }
    }
}
