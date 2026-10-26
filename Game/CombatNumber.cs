namespace VanDao.Game;

/// <summary>DTOs use 32-bit stats; saturate high-realm arithmetic instead of wrapping negative.</summary>
public static class CombatNumber
{
    public static int Clamp(double value) => (int)Math.Clamp(value, 0, int.MaxValue);
    public static int Round(double value) => Clamp(Math.Round(value));
    public static int Add(int a, int b) => Clamp((double)a + b);
}
