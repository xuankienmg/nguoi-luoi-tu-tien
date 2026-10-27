namespace VanDao.Game;

/// <summary>Combat numbers are 64-bit. Saturate at 2^53 − 1 (JS Number.MAX_SAFE_INTEGER) so the client replays exact values
/// and doubles still hold every integer, instead of wrapping negative.</summary>
public static class CombatNumber
{
    public const long Max = 9_007_199_254_740_991;
    public static long Clamp(double value) => (long)Math.Clamp(value, 0, Max);
    public static long Round(double value) => Clamp(Math.Round(value));
    public static long Add(long a, long b) => Clamp((double)a + b);
}
