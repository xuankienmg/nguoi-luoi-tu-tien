using VanDao.Data;

namespace VanDao.Game;

/// <summary>Boss lang thang: mỗi chuyến ở ải thường có Chance gặp một trong hai Boss, thay cả 3 đợt quái của chuyến.
/// Chỉ số theo cảnh giới + tầng của người chơi (không theo chương / mức khó đang treo); thắng rơi linh thảo hoặc tinh thạch,
/// thua không lùi ải. Gặp hay không lấy hạt giống theo người chơi + ải + thời điểm chuyến nên client và server ra cùng kết quả.</summary>
public static class WanderingBoss
{
    public record Def(string Id, string Name, string Image, string Loot, int Min, int Max, string Desc);

    public const double Chance = .03;
    /// <summary>So với ải mốc của cảnh giới: máu = tổng máu cả chuyến ×HpMult, công / thủ = đợt mạnh nhất ×AtkMult / ×DefMult.</summary>
    public const double HpMult = 1.5, AtkMult = 1.25, DefMult = 1.1;

    public static readonly Def[] All =
    [
        new("boss_co_thu_yeu_vuong", "Cổ Thụ Yêu Vương", "img/boss_co_thu_yeu_vuong.png", "herb", 1, 10,
            "Cây cổ vạn năm thành yêu, rễ bám khắp rừng sâu; lõi lục quang trong thân nuôi cả trăm loại linh thảo."),
        new("boss_huyen_nham_cu_nhan", "Huyền Nham Cự Nhân", "img/boss_huyen_nham_cu_nhan.png", "stone", 1, 5,
            "Người đá khổng lồ ngủ dưới linh mạch, tinh hạch hổ phách giữa ngực kết tụ tinh thạch qua nghìn năm."),
    ];

    /// <summary>Ải mốc theo cảnh giới: (mức khó, chương đầu, chương cuối, hệ số thêm). Tầng đầu → ải 1 chương đầu, tầng cuối → ải 15 chương cuối.
    /// Bám theo nơi cảnh giới đó thường treo: Luyện Hư → Thường chương 9–10, Hợp Thể → Khó, Đại Thừa / Độ Kiếp → Ác Mộng.</summary>
    private static readonly (int Diff, int From, int To, double Extra)[] Anchors =
    [
        (0, 0, 0, 1), (0, 1, 1, 1), (0, 2, 3, 1), (0, 4, 5, 1), (0, 6, 7, 1),
        (0, 8, 9, 1), (1, 0, 9, 1), (2, 0, 4, 1), (2, 5, 9, 1), (2, 9, 9, 1.6),
    ];

    /// <summary>Linh thảo theo cảnh giới (chương nguồn, xem GameData.HerbDrops).</summary>
    private static readonly int[][] HerbChapters = [[0], [1], [2, 3], [4, 5], [6, 7], [8, 9], [8, 9], [8, 9], [8, 9], [8, 9]];

    /// <summary>Boss gặp ở chuyến này (null = đánh quái ải như thường).</summary>
    public static Def? Meet(Player p, int area, DateTime trip)
    {
        if (!p.StarterChosen || area < 1 || area >= GameData.BossAreaFor(p.Diff)) return null;
        var sec = trip.Ticks / TimeSpan.TicksPerSecond;
        var rng = new Random(unchecked((p.Id.GetHashCode() * 97 + area) * 101 + (int)(sec % 1_000_000_007)));
        if (rng.NextDouble() >= Chance) return null;
        return All[rng.Next(All.Length)];
    }

    /// <summary>Chỉ số Boss ở cảnh giới realm, tầng stage.</summary>
    public static MonsterDef Monster(Def b, int realm, int stage)
    {
        realm = Math.Clamp(realm, 0, Anchors.Length - 1);
        var (diff, from, to, extra) = Anchors[realm];
        var stages = GameData.Realms[realm].Stages.Length;
        var areas = GameData.AreasFor(diff);
        var slots = (to - from + 1) * areas;
        var pos = stages <= 1 ? slots - 1 : (int)Math.Round((double)Math.Clamp(stage, 0, stages - 1) / (stages - 1) * (slots - 1));
        var waves = GameData.AreaWaves(GameData.ChapterFor(diff, from + pos / areas), pos % areas + 1, diff);
        var boss = new MonsterDef(b.Name, b.Image,
            Hp: Math.Max(1, CombatNumber.Round((double)waves.Sum(w => w.Hp) * HpMult * extra * GameData.BossHpShift)),
            Atk: Math.Max(1, CombatNumber.Round((double)waves.Max(w => w.Atk) * AtkMult * extra * GameData.BossAtkShift)),
            Def: CombatNumber.Round((double)waves.Max(w => w.Def) * DefMult * extra),
            Tier: "boss", Id: b.Id);
        // ải 1 chương mới yếu hơn ải 15 chương trước: giữ sàn bằng Boss ở tầng cuối cảnh giới trước, không tụt khi đột phá
        if (realm > 0)
        {
            var floor = Monster(b, realm - 1, GameData.Realms[realm - 1].Stages.Length - 1);
            boss = boss with { Hp = Math.Max(boss.Hp, floor.Hp), Atk = Math.Max(boss.Atk, floor.Atk), Def = Math.Max(boss.Def, floor.Def) };
        }
        return MonsterCombatRatings.Apply(boss, diff);
    }

    /// <summary>Phần thưởng khi hạ Boss: Min–Max món, mỗi món chọn riêng theo trọng số.</summary>
    public static Dictionary<string, int> RollLoot(Def b, int realm, Random rng)
    {
        var pool = Pool(b, realm);
        var total = pool.Sum(x => x.Weight);
        var loot = new Dictionary<string, int>();
        for (var n = rng.Next(b.Min, b.Max + 1); n > 0; n--)
        {
            var r = rng.NextDouble() * total;
            var pick = pool[^1].Item;
            foreach (var (item, weight) in pool) { if (r < weight) { pick = item; break; } r -= weight; }
            loot[pick] = loot.GetValueOrDefault(pick) + 1;
        }
        return loot;
    }

    /// <summary>Linh thảo: các loại rơi ở chương ứng với cảnh giới, trọng số = tỉ lệ rơi gốc (loại hiếm ra ít hơn).
    /// Tinh thạch: Sơ trước Nguyên Anh; từ Nguyên Anh thêm Cao, hai bậc chia đều.</summary>
    public static (string Item, double Weight)[] Pool(Def b, int realm)
    {
        realm = Math.Clamp(realm, 0, HerbChapters.Length - 1);
        if (b.Loot == "herb")
            return GameData.HerbDrops.Where(h => h.Chapters.Intersect(HerbChapters[realm]).Any())
                .GroupBy(h => h.Item).Select(g => (g.Key, g.Max(h => h.Chance))).ToArray();
        var so = GemData.SoStones.OrderBy(x => x).Select(x => (x, 1.0));
        return realm < 3 ? so.ToArray() : so.Concat(GemData.CaoStones.OrderBy(x => x).Select(x => (x, 1.0))).ToArray();
    }
}
