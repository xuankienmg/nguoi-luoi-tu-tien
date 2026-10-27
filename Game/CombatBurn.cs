namespace VanDao.Game;

/// <summary>Thiêu đốt không cộng dồn; kích hoạt lại làm mới hai lượt.</summary>
public sealed class CombatBurn
{
    public const int Percent = 5;
    public const int Duration = 2;
    public int TurnsLeft { get; private set; }
    public long Tick(long maxHp, bool triggered)
    {
        if (triggered) TurnsLeft = Duration;
        if (TurnsLeft <= 0) return 0;
        TurnsLeft--;
        return Math.Max(1, (long)Math.Round(maxHp * (Percent / 100.0)));
    }
    /// <summary>Kỹ năng chủ động gây thiêu đốt giữa lượt: lượt này chưa đốt thì đốt ngay (tính là lượt kích hoạt);
    /// đã đốt rồi thì chỉ làm mới để còn đủ một lượt nữa (không đốt hai lần trong một lượt).</summary>
    public long Ignite(long maxHp, bool tickedThisTurn)
    {
        if (!tickedThisTurn) return Tick(maxHp, true);
        TurnsLeft = Math.Max(TurnsLeft, Duration - 1);
        return 0;
    }
    public void Reset() => TurnsLeft = 0;
}
