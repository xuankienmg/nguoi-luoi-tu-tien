namespace VanDao.Game;

/// <summary>Kết quả thiên kiếp: tất định theo chỉ số, xem trước được trước khi đột phá.</summary>
public record TribulationResult(int Total, int Survived, decimal Hp, decimal[] Damage, double Mitigation, double ChanceBonus);

/// <summary>
/// Thiên Kiếp: khi đột phá đại cảnh giới, tu sĩ chịu N đạo lôi kiếp. Số đạo chịu được (theo máu + phòng thủ hiện tại)
/// cộng/trừ tỉ lệ đột phá. Không dùng random, không lưu DB: tính lại từ <see cref="CombatStats"/>.
/// </summary>
public static class TribulationData
{
    public const int BaseBolts = 3;             // số đạo tối thiểu
    public const int BoltsPerTier = 2;          // +2 đạo mỗi 3 cảnh giới
    public const int MaxTier = 3;               // tối đa 3 + 2·3 = 9 đạo
    public const decimal BoltPct = 0.12m;       // sát thương đạo đầu = 12% máu gốc cảnh giới đích
    public const decimal Ramp = 0.15m;          // mỗi đạo sau mạnh hơn 15%
    public const decimal DefK = 5m;             // giảm sát thương = Def / (Def + DefGốcĐích·5)
    public const double MaxMitigation = 0.6;
    public const double MinBonus = -0.10;       // chịu 0 đạo: −10% tỉ lệ
    public const double BonusSpan = 0.25;       // chịu đủ: −10% + 25% = +15%

    public static int Bolts(int targetRealm) => BaseBolts + BoltsPerTier * Math.Clamp((targetRealm - 1) / 3, 0, MaxTier);

    public static TribulationResult Simulate(int targetRealm, CombatStats targetBase, CombatStats hero)
    {
        var total = Bolts(targetRealm);
        var def = Math.Max(0, hero.Def);
        var mit = def <= 0 ? 0 : Math.Min(MaxMitigation, (double)(def / (def + Math.Max(1, targetBase.Def) * DefK)));
        var dmg = Enumerable.Range(0, total).Select(i => Math.Round(targetBase.Hp * BoltPct * (1 + Ramp * i) * (decimal)(1 - mit))).ToArray();
        var hp = hero.Hp;
        var survived = 0;
        foreach (var d in dmg) { if (hp <= d) break; hp -= d; survived++; }
        var bonus = Math.Round(MinBonus + BonusSpan * survived / total, 3);
        return new TribulationResult(total, survived, hero.Hp, dmg, Math.Round(mit, 3), bonus);
    }
}
