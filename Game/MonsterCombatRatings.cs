using System.Collections.ObjectModel;

namespace VanDao.Game;

/// <summary>Eight uncapped ratings determined by difficulty and stable identity, never chapter.</summary>
public static class MonsterCombatRatings
{
    public enum Featured { Crit, CritResist, Accuracy, Dodge, Combo, ComboResist, Reflect, ReflectResist }

    public static IReadOnlyDictionary<string, Featured> BossProfiles { get; } =
        new ReadOnlyDictionary<string, Featured>(new Dictionary<string, Featured>(StringComparer.Ordinal) {
            ["boss_tu_ty_ma_vien"] = Featured.Combo,
            ["boss_hac_giao"] = Featured.CritResist,
            ["boss_bang_phuong"] = Featured.Dodge,
            ["boss_dao_ngot"] = Featured.Reflect,
            ["boss_cung_ky"] = Featured.Accuracy,
            ["boss_hon_don"] = Featured.ComboResist,
            ["boss_am_ma"] = Featured.Dodge,
            ["boss_kiem_de"] = Featured.Crit,
            ["boss_thao_thiet"] = Featured.ReflectResist,
            ["boss_cung_ky_vuong"] = Featured.Combo,
            ["boss_co_thu_yeu_vuong"] = Featured.Reflect,
            ["boss_huyen_nham_cu_nhan"] = Featured.CritResist,
            ["hell/boss_thoi_phan_quan"] = Featured.Accuracy,
            ["hell/boss_hac_hung_tinh"] = Featured.CritResist,
            ["hell/boss_thanh_anh_dai_vuong"] = Featured.Crit,
            ["hell/boss_thiet_phien_cong_chua"] = Featured.Reflect,
            ["hell/boss_nguu_ma_vuong"] = Featured.ComboResist,
            ["hell/boss_diem_la_vuong"] = Featured.ReflectResist,
            ["hell2/boss_hoang_phong_dai_vuong"] = Featured.Dodge,
            ["hell2/boss_huyet_phu_ma_nguu"] = Featured.Crit,
            ["hell2/boss_hoa_long"] = Featured.Combo,
            ["hell2/boss_cuu_vi_ho_tien"] = Featured.Accuracy,
            ["hell2/boss_kim_si_dai_bang"] = Featured.ComboResist,
            ["hell2/boss_ngu_dau_kim_long"] = Featured.Reflect,
            ["hell2/boss_luc_dau_kim_su"] = Featured.CritResist
        });

    public static string Identity(MonsterDef monster)
    {
        if (!string.IsNullOrWhiteSpace(monster.Id)) return monster.Id;
        var key = monster.Image.Replace('\\', '/');
        if (key.StartsWith("img/", StringComparison.Ordinal)) key = key[4..];
        var extension = key.LastIndexOf('.');
        return extension > key.LastIndexOf('/') ? key[..extension] : key;
    }

    private static double Baseline(int difficultyStep) => 100d + 100d * Math.Max(0, difficultyStep);

    /// <summary>cân bằng 11/2026: 8 chỉ số phụ của Boss ải ×0,4 (cũ ×0,6; gốc ×1), gồm phần cộng thêm khi cuồng bạo. Quái thường và tinh anh giữ nguyên.
    /// Boss lang thang / Boss Kỳ duyên truyền reduceBoss: false để giữ chỉ số cũ (giảm thì nhân vật mới thắng được, mất ý nghĩa phần thưởng).</summary>
    public const double BossRatingMult = 0.4;
    /// <summary>cân bằng 11/2026: chỉ số phụ Boss ải tăng ×BossRoundGrowth mỗi vòng Boss (chương 1 → 10; Thiên Giới mỗi 10 tầng).</summary>
    public const double BossRoundGrowth = 1.03;
    private static double Boss(double v, bool reduce = true, int round = 0) =>
        reduce ? Math.Round(v * BossRatingMult * Math.Pow(BossRoundGrowth, Math.Max(0, round))) : v;   // làm tròn số nguyên như các chỉ số khác

    /// <summary>Chỉ số thứ hai đi cặp với chỉ số đặc trưng của Boss (Boss chỉ có 2 chỉ số cao): bạo ↔ kháng bạo, chính xác ↔ né, liên kích ↔ kháng liên kích, phản ↔ kháng phản.</summary>
    public static Featured Partner(Featured f) => f switch {
        Featured.Crit => Featured.CritResist, Featured.CritResist => Featured.Crit,
        Featured.Accuracy => Featured.Dodge, Featured.Dodge => Featured.Accuracy,
        Featured.Combo => Featured.ComboResist, Featured.ComboResist => Featured.Combo,
        Featured.Reflect => Featured.ReflectResist, _ => Featured.Reflect
    };

    private static CombatRatings Raise(CombatRatings r, Featured f, double high) => f switch {
        Featured.Crit => r with { Crit = high },
        Featured.CritResist => r with { CritResist = high },
        Featured.Accuracy => r with { Accuracy = high },
        Featured.Dodge => r with { Dodge = high },
        Featured.Combo => r with { Combo = high },
        Featured.ComboResist => r with { ComboResist = high },
        Featured.Reflect => r with { Reflect = high },
        _ => r with { ReflectResist = high }
    };

    /// <summary>bossRound: vòng Boss 0-based (chương 0..9; Thiên Giới tầng/10 − 1), chỉ áp cho Boss ải (reduceBoss).</summary>
    public static CombatRatings For(string monsterId, string tier, int difficultyStep, bool reduceBoss = true, int bossRound = 0)
    {
        var baseline = Baseline(difficultyStep);
        if (tier != "boss")
        {
            var value = baseline * (tier == "elite" ? .7 : .4);
            return new(value, value + 50, value + 50, value, value, value + 50, value, value + 50);
        }
        var (low, high) = (Boss(baseline, reduceBoss, bossRound), Boss(baseline + 50, reduceBoss, bossRound));
        var featured = BossProfiles.GetValueOrDefault(monsterId, Featured.Crit);
        // cân bằng 11/2026: chỉ 2 chỉ số cao (đặc trưng + chỉ số đi cặp), 6 chỉ số còn lại ở mức thấp (cũ: 3 kháng + đặc trưng)
        var all = new CombatRatings(low, low, low, low, low, low, low, low);
        return Raise(Raise(all, featured, high), Partner(featured), high);
    }

    public static MonsterDef Apply(MonsterDef monster, int difficultyStep, bool reduceBoss = true, int bossRound = 0)
    {
        var id = Identity(monster);
        var r = For(id, monster.Tier, difficultyStep, reduceBoss, bossRound);
        return monster with { Id = id, Crit = r.Crit, CritResist = r.CritResist, Accuracy = r.Accuracy,
            Dodge = r.Dodge, Combo = r.Combo, ComboResist = r.ComboResist, Reflect = r.Reflect, ReflectResist = r.ReflectResist };
    }

    public static bool RollCritical(Random rng, MonsterDef monster, double resistance)
    {
        return CombatRatingRules.Roll(rng, monster.Crit, resistance);
    }

    /// <summary>Applied once when a boss enrages: six ratings gain half the boss's base (low) rating, so the two high ratings keep their lead;
    /// Dodge and Combo keep their base values. Boss lang thang (reduceBoss: false) cộng nửa mức nền độ khó như cũ.</summary>
    public static MonsterDef EnrageResistances(MonsterDef monster, int difficultyStep, bool reduceBoss = true)
    {
        if (monster.Tier != "boss") return monster;
        // cân bằng 11/2026: Boss ải cộng nửa chỉ số thấp nhất của chính nó → giảm cùng tỉ lệ ×0,4 và tăng theo vòng Boss
        var lowest = new[] { monster.Crit, monster.CritResist, monster.Accuracy, monster.Dodge, monster.Combo, monster.ComboResist, monster.Reflect, monster.ReflectResist }.Min();
        var bonus = reduceBoss ? Math.Round(lowest / 2) : Baseline(difficultyStep) / 2;
        return monster with {
            CritResist = monster.CritResist + bonus,
            ComboResist = monster.ComboResist + bonus,
            ReflectResist = monster.ReflectResist + bonus,
            Accuracy = monster.Accuracy + bonus,
            Reflect = monster.Reflect + bonus,
            Crit = monster.Crit + bonus,
        };
    }
}
