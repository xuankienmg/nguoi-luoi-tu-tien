using VanDao.Game;

namespace GemSystemTests;

/// <summary>Nhật ký trận Lôi đài (DuelDto): ghi rõ từng nguồn sát thương, máu hai bên khớp trận thật.</summary>
public class ArenaDuelLogTests
{
    private static PvpFighter Fighter(string name, double hp = 4000) =>
        PvpFighter.FromStats(name, 3, new() { ["atk"] = 500, ["hp"] = hp, ["def"] = 200, ["crit"] = 10, ["speed"] = 50 });

    [Fact]
    public void EveryStepNamesItsDamageSourcesAndHpFollowsTheFight()
    {
        var r = PvpCombat.Run(Fighter("A"), Fighter("D"), 7);
        var duel = DuelLog.Of(r);
        Assert.Equal(r.Actions.Count, duel.Steps.Count);
        Assert.Equal(1, duel.Steps[0].Round);
        Assert.All(duel.Steps.Zip(duel.Steps.Skip(1)), p => Assert.InRange(p.Second.Round - p.First.Round, 0, 1));   // số lượt tăng dần, mỗi lượt hai bên ra đòn
        Assert.Equal(duel.Me.Hp, Math.Max(1, r.A.Hp));
        foreach (var (s, x) in duel.Steps.Zip(r.Actions))
        {
            Assert.Equal((x.HpA, x.HpD), (s.HpMe, s.HpFoe));
            Assert.All(s.Parts, p => { Assert.True(p.Value > 0); Assert.False(string.IsNullOrEmpty(p.Name)); });
            if (x.Basic > 0) Assert.Contains(s.Parts, p => p.Kind == "basic" && p.Value == x.Basic && !p.Self);
            if (x.Control.Length == 0 && !x.Dodged && !x.Nullified) Assert.NotEmpty(s.Parts);   // lượt ra đòn thật phải có ít nhất một nguồn
        }
        Assert.Contains(duel.Steps, s => s.Parts.Any(p => p.Kind == "basic"));
    }

    [Fact]
    public void ReflectAndBurnAreSeparateSources()
    {
        var a = Fighter("A") with { Reflect = 90, PassiveBurn = true };
        var d = Fighter("D") with { Reflect = 90 };
        var steps = Enumerable.Range(1, 30).SelectMany(seed => DuelLog.Of(PvpCombat.Run(a, d, seed)).Steps).ToList();
        Assert.Contains(steps, s => s.Parts.Any(p => p.Kind == "reflect" && p.Self));   // phản đòn lên chính người ra đòn
        Assert.Contains(steps, s => s.Parts.Any(p => p.Kind == "burn" && !p.Self));
    }

    [Fact]
    public void BossDuelEndsAtTheStepWhereSharedBossHpReachesZero()
    {
        var r = PvpCombat.Run(Fighter("A"), Fighter("Boss", hp: 1e12), 7);
        var total = r.Actions.Where(a => a.ByA).Sum(a => a.Direct);
        Assert.True(total > 10);
        var duel = DuelLog.Of(r, new DuelLog.FoeOverride("Boss", null, 1000, 10));   // chỉ còn 10 máu: chết ngay đòn đầu
        Assert.Equal(0, duel.Steps[^1].HpFoe);
        Assert.True(duel.Steps.Count < r.Actions.Count);
        Assert.All(duel.Steps.SkipLast(1), s => Assert.True(s.HpFoe > 0));
        var alive = DuelLog.Of(r, new DuelLog.FoeOverride("Boss", null, 1e15m, 1e15m));   // máu rất dày: đủ mọi lượt
        Assert.Equal(r.Actions.Count, alive.Steps.Count);
    }
}
