namespace VanDao.Game;

/// <summary>Primary gem ratings, indexed by level (1–10). Secondary bonuses are separate.</summary>
public static class GemRatingBudget
{
    private static readonly double[] Primary = { 2, 4, 6, 8, 10, 13, 16, 20, 25, 30 };

    public static CombatRatings For(string theme, int level)
    {
        if (level < 1 || level > Primary.Length) throw new ArgumentOutOfRangeException(nameof(level));
        var rating = Primary[level - 1];
        return theme switch {
            "hoa" => new(Crit: rating),
            "loi" => new(Combo: rating),
            "bang" => new(CritResist: rating),
            "tho" => new(ComboResist: rating),
            "phong" => new(Dodge: rating),
            "quang" => new(Accuracy: rating),
            "am" => new(Reflect: rating),
            "vu_tru" => new(ReflectResist: rating),
            "linh" or "phuc_nguyen" => new(),
            _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, "Unsupported gem theme.")
        };
    }
}
