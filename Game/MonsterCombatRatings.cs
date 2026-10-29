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
            ["hell/boss_thoi_phan_quan"] = Featured.Accuracy,
            ["hell/boss_hac_hung_tinh"] = Featured.CritResist,
            ["hell/boss_thanh_anh_dai_vuong"] = Featured.Crit,
            ["hell/boss_thiet_phien_cong_chua"] = Featured.Reflect,
            ["hell/boss_nguu_ma_vuong"] = Featured.ComboResist,
            ["hell/boss_diem_la_vuong"] = Featured.ReflectResist
        });

    public static string Identity(MonsterDef monster)
    {
        if (!string.IsNullOrWhiteSpace(monster.Id)) return monster.Id;
        var key = monster.Image.Replace('\\', '/');
        if (key.StartsWith("img/", StringComparison.Ordinal)) key = key[4..];
        var extension = key.LastIndexOf('.');
        return extension > key.LastIndexOf('/') ? key[..extension] : key;
    }

    public static CombatRatings For(string monsterId, string tier, int difficultyStep)
    {
        var baseline = 100d + 100d * Math.Max(0, difficultyStep);
        if (tier != "boss")
        {
            var value = baseline * (tier == "elite" ? .7 : .4);
            return new(value, value + 50, value + 50, value, value, value + 50, value, value + 50);
        }
        var result = new CombatRatings(baseline, baseline + 50, baseline, baseline, baseline, baseline + 50, baseline, baseline + 50);
        var featured = BossProfiles.GetValueOrDefault(monsterId, Featured.Crit);
        var high = baseline + 50;
        return featured switch {
            Featured.Crit => result with { Crit = high },
            Featured.CritResist => result with { CritResist = high },
            Featured.Accuracy => result with { Accuracy = high },
            Featured.Dodge => result with { Dodge = high },
            Featured.Combo => result with { Combo = high },
            Featured.ComboResist => result with { ComboResist = high },
            Featured.Reflect => result with { Reflect = high },
            _ => result with { ReflectResist = high }
        };
    }

    public static MonsterDef Apply(MonsterDef monster, int difficultyStep)
    {
        var id = Identity(monster);
        var r = For(id, monster.Tier, difficultyStep);
        return monster with { Id = id, Crit = r.Crit, CritResist = r.CritResist, Accuracy = r.Accuracy,
            Dodge = r.Dodge, Combo = r.Combo, ComboResist = r.ComboResist, Reflect = r.Reflect, ReflectResist = r.ReflectResist };
    }

    public static bool RollCritical(Random rng, MonsterDef monster, double resistance)
    {
        return CombatRatingRules.Roll(rng, monster.Crit, resistance);
    }

    /// <summary>Applied once when a boss enters enrage; other ratings keep their base values.</summary>
    public static MonsterDef EnrageResistances(MonsterDef monster) => monster.Tier != "boss" ? monster : monster with {
        CritResist = monster.CritResist * 2,
        ComboResist = monster.ComboResist * 2,
        ReflectResist = monster.ReflectResist * 2,
        Accuracy = monster.Accuracy * 2,
    };
}
