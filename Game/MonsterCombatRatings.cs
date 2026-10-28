namespace VanDao.Game;

/// <summary>Enemy ratings begin at Hard and grow by chapter and difficulty.</summary>
public static class MonsterCombatRatings
{
    public static double Crit(string tier, int difficulty, int chapter) => difficulty < 1 ? 0 :
        tier switch { "elite" => 5 + 2 * Math.Max(0, chapter) + 5 * (difficulty - 1),
                      "boss" => 10 + 3 * Math.Max(0, chapter) + 10 * (difficulty - 1), _ => 0 };
    public static double Dodge(string tier, int difficulty, int chapter) => difficulty < 1 ? 0 :
        tier switch { "elite" => 3 + Math.Max(0, chapter) + 3 * (difficulty - 1),
                      "boss" => 5 + 2 * Math.Max(0, chapter) + 5 * (difficulty - 1), _ => 0 };
    public static bool RollCritical(Random rng, string tier, int difficulty, int chapter, double resistance)
    {
        var chance = Math.Clamp(Crit(tier, difficulty, chapter) - resistance, 0, 100);
        return chance > 0 && rng.NextDouble() * 100 < chance;
    }
}
