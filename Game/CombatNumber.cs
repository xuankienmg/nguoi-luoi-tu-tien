namespace VanDao.Game;

/// <summary>Combat numbers (máu, công, thủ, sát thương, lực chiến) are decimal, so late-game values are not capped at int/long.
/// Saturate at Max instead of throwing: decimal overflows (OverflowException) near 7.9×10^28, and combat multiplies by
/// percentages, so Max leaves ~10^4 headroom. Negative results clamp to 0.</summary>
public static class CombatNumber
{
    public const decimal Max = 1_000_000_000_000_000_000_000_000m;   // 10^24
    /// <summary>Bỏ phần lẻ (như ép kiểu số nguyên trước đây) rồi chặn trong [0, Max].</summary>
    public static decimal Clamp(double value) => double.IsNaN(value) || value <= 0 ? 0 : value >= (double)Max ? Max : Math.Truncate((decimal)value);
    public static decimal Clamp(decimal value) => Math.Clamp(Math.Truncate(value), 0, Max);
    public static decimal Round(double value) => Clamp(Math.Round(value));
    public static decimal Round(decimal value) => Clamp(Math.Round(value));
    public static decimal Add(decimal a, decimal b) => Clamp(a + b);
    /// <summary>value × factor (tỉ lệ kiểu double), làm tròn và chặn như Round.</summary>
    public static decimal Scale(decimal value, double factor) => Round(value * Factor(factor));
    /// <summary>Hệ số double → decimal không ném lỗi với NaN / vô cực.</summary>
    public static decimal Factor(double factor) => double.IsNaN(factor) ? 0 : factor >= 1e9 ? 1_000_000_000m : factor <= -1e9 ? -1_000_000_000m : (decimal)factor;
}
