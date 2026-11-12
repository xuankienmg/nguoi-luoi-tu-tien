namespace VanDao.Game;

/// <summary>Sát thương theo lượt trên quái: Thiêu đốt hoặc Nhiễm độc, mỗi lúc chỉ một loại (cái mới đè cái cũ), không cộng dồn; kích hoạt lại làm mới thời gian.</summary>
public sealed class CombatBurn
{
    public const int Percent = 5;
    public const int Duration = 2;
    // cân bằng 11/2026: Nhiễm độc tách khỏi Thiêu đốt — yếu hơn mỗi lượt nhưng kéo dài (3% × 4 lượt = 12%, đốt 5% × 2 = 10%)
    public const int PoisonPercent = 3;
    public const int PoisonDuration = 4;
    public int TurnsLeft { get; private set; }
    /// <summary>Loại đang có: "burn" · "poison" (giữ loại cuối khi đã hết lượt).</summary>
    public string Kind { get; private set; } = "burn";
    public static int PercentOf(string kind) => kind == "poison" ? PoisonPercent : Percent;
    public static int DurationOf(string kind) => kind == "poison" ? PoisonDuration : Duration;
    private void Start(string kind) { Kind = kind == "poison" ? "poison" : "burn"; TurnsLeft = DurationOf(Kind); }
    /// <summary>Gây hiệu ứng (khám phá / ải): chưa trừ máu; máu trừ ở lượt của địch, trước khi địch hành động (<see cref="Tick"/>).</summary>
    public void Apply(string kind) => Start(kind);
    /// <summary>Đầu lượt: trừ máu nếu còn hiệu lực. <paramref name="triggered"/> = thiêu đốt bị động (trang bị / bị động Hỏa) vừa kích hoạt.</summary>
    public decimal Tick(decimal maxHp, bool triggered)
    {
        if (triggered) Start("burn");
        if (TurnsLeft <= 0) return 0;
        TurnsLeft--;
        return Math.Max(1, Math.Round(maxHp * PercentOf(Kind) / 100m));
    }
    /// <summary>Kỹ năng / đòn thường gây thiêu đốt hoặc nhiễm độc giữa lượt: lượt này chưa trừ máu thì trừ ngay (tính là lượt kích hoạt);
    /// đã trừ rồi thì chỉ bắt đầu hiệu ứng mới cho các lượt sau (không trừ hai lần trong một lượt).</summary>
    public decimal Ignite(decimal maxHp, bool tickedThisTurn, string kind = "burn")
    {
        Start(kind);
        if (!tickedThisTurn) return Tick(maxHp, false);
        TurnsLeft--;   // lượt này đã tính là lượt kích hoạt
        return 0;
    }
    public void Reset() => TurnsLeft = 0;
}
