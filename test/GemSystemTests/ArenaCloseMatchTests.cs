using VanDao.Game;
using Xunit;
using Xunit.Abstractions;

namespace GemSystemTests;

/// <summary>Trận Lôi đài sát nút: lực chiến bằng nhau / chênh nhỏ (~3% công, ~5% máu).</summary>
public class ArenaCloseMatchTests(ITestOutputHelper output)
{
    private const int Seeds = 200;

    private static PvpFighter Fighter(string name, double atkMul = 1, double hpMul = 1) =>
        PvpFighter.FromStats(name, 3, new() { ["atk"] = 500 * atkMul, ["hp"] = 4000 * hpMul, ["def"] = 200, ["crit"] = 10, ["speed"] = 50 });

    private static (int aWins, int dWins) Wins(PvpFighter a, PvpFighter d)
    {
        int aw = 0, dw = 0;
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var r = PvpCombat.Run(a, d, seed);
            if (r.Win) aw++; else dw++;
        }
        return (aw, dw);
    }

    [Fact]
    public void Equal_fighters_always_finish_deterministically_and_win_rate_is_balanced()
    {
        var a = Fighter("A"); var d = Fighter("D");
        int aWins = 0;
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var r1 = PvpCombat.Run(a, d, seed);
            var r2 = PvpCombat.Run(a, d, seed);
            Assert.NotEmpty(r1.Actions);
            Assert.True(r1.Actions.Count <= PvpCombat.MaxRounds * 20, $"seed {seed}: trận quá dài");
            var last = r1.Actions[^1];
            Assert.True(last.HpA >= 0 && last.HpD >= 0, $"seed {seed}: máu âm");
            Assert.True(last.HpA <= a.Hp * PvpCombat.HpMult && last.HpD <= d.Hp * PvpCombat.HpMult);
            // deterministic
            Assert.Equal(r1.Win, r2.Win);
            Assert.Equal(r1.Ko, r2.Ko);
            Assert.Equal(r1.Actions.Count, r2.Actions.Count);
            Assert.Equal(last.HpA, r2.Actions[^1].HpA);
            Assert.Equal(last.HpD, r2.Actions[^1].HpD);
            if (r1.Win) aWins++;
        }
        var rate = aWins / (double)Seeds;
        output.WriteLine($"MEASURE equal: A win rate = {rate:P1} ({aWins}/{Seeds})");
        // A là bên khiêu chiến: khi thân pháp bằng nhau A đi trước (xem PvpCombat), nên có thể lệch nhẹ; ngưỡng rộng.
        Assert.InRange(rate, .25, .75);
    }

    [Fact]
    public void Slightly_stronger_side_wins_more_but_weaker_still_wins_some_and_roles_are_symmetric()
    {
        var weak = Fighter("W"); var strong = Fighter("S", 1.03, 1.05);

        var (baseA, _) = Wins(Fighter("A"), Fighter("D"));
        var (strongAsA, _) = Wins(strong, weak);             // mạnh khiêu chiến
        var (_, strongAsD) = Wins(weak, strong);             // mạnh phòng thủ
        var weakWinsAsD = Seeds - strongAsA;
        var weakWinsAsA = Seeds - strongAsD;
        var strongRateA = strongAsA / (double)Seeds;
        var strongRateD = strongAsD / (double)Seeds;
        output.WriteLine($"MEASURE close: equal A rate={baseA / (double)Seeds:P1}; strong as A={strongRateA:P1}; strong as D={strongRateD:P1}; weak wins as D={weakWinsAsD}, as A={weakWinsAsA}");

        var equalRate = baseA / (double)Seeds;
        const double tolerance = .05;
        // bên mạnh (khiêu chiến) thắng nhiều hơn tỉ lệ ngang bằng (cho sai số)
        Assert.True(strongRateA >= equalRate - tolerance, $"mạnh làm A {strongRateA:P1} < ngang bằng {equalRate:P1}");
        // bên mạnh khi đứng phòng thủ thắng nhiều hơn tỉ lệ D ngang bằng (1 - equalRate)
        Assert.True(strongRateD >= (1 - equalRate) - tolerance, $"mạnh làm D {strongRateD:P1} < ngang bằng {1 - equalRate:P1}");
        // bên mạnh thắng quá nửa trung bình hai vai, nhưng yếu vẫn thắng vài trận
        Assert.True((strongRateA + strongRateD) / 2 > .5, "bên mạnh hơn phải thắng nhiều hơn trung bình");
        Assert.True(weakWinsAsD >= 3, $"bên yếu (phòng thủ) chỉ thắng {weakWinsAsD}");
        Assert.True(weakWinsAsA >= 3, $"bên yếu (khiêu chiến) chỉ thắng {weakWinsAsA}");
        // đối xứng hợp lý: hai vai không lệch quá xa
        Assert.True(Math.Abs(strongRateA - strongRateD) <= .35, $"lệch vai quá lớn: {strongRateA:P1} vs {strongRateD:P1}");
    }
}
