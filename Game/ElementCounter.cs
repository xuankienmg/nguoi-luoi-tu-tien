namespace VanDao.Game;

/// <summary>Ngũ hành tương khắc: mỗi nhóm khắc nhóm cách nó 2 bước trên vòng tương sinh. Hệ rỗng = vô hệ, không khắc ai.</summary>
public static class ElementCounter
{
    // vòng tương sinh: Mộc → Hỏa → Thổ → Lôi → Thủy → Mộc · Phong cùng nhóm Mộc, Băng cùng nhóm Thủy
    private static readonly string[][] Cycle = [["moc", "gio"], ["hoa"], ["tho"], ["loi"], ["thuy", "bang"]];
    public static int Length => Cycle.Length;
    /// <summary>% sát thương thêm khi khắc · % sát thương mất khi bị khắc.</summary>
    public const double Advantage = 20, Disadvantage = 20;

    public static int Group(string element) => Array.FindIndex(Cycle, g => g.Contains(element));

    /// <summary>+1 người đánh khắc người nhận · −1 bị khắc · 0 trung tính hoặc một bên vô hệ.</summary>
    public static int Edge(string attacker, string defender)
    {
        int a = Group(attacker), d = Group(defender);
        if (a < 0 || d < 0) return 0;
        return (a + 2) % Length == d ? 1 : (d + 2) % Length == a ? -1 : 0;
    }

    public static double Multiplier(string attacker, string defender) => Edge(attacker, defender) switch
    {
        1 => 1 + Advantage / 100,
        -1 => 1 - Disadvantage / 100,
        _ => 1,
    };

    /// <summary>Linh thú nhiều hệ: hệ có quan hệ tốt nhất với mục tiêu (hòa: hệ đứng trước).</summary>
    public static string Best(IReadOnlyList<string> attackers, string defender) =>
        attackers.OrderByDescending(e => Edge(e, defender)).FirstOrDefault() ?? "";
}
