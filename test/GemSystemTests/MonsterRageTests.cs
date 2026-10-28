using VanDao.Game;
using VanDao.Data;

namespace GemSystemTests;

public class MonsterRageTests
{
    [Fact]
    public void UltimateScalesBaseCounterDamageWithoutBypassingDefenseOrInvulnerability()
    {
        Assert.Equal(17,MonsterRageCombat.ScaleCounter(10,MonsterRageCombat.BossMultiplier));
        Assert.Equal(14,MonsterRageCombat.ScaleCounter(10,MonsterRageCombat.EliteMultiplier));
        Assert.Equal(0,MonsterRageCombat.ScaleCounter(0,MonsterRageCombat.BossMultiplier));
        Assert.InRange(MonsterRageCombat.ScaleCounter(long.MaxValue,MonsterRageCombat.BossMultiplier),0,long.MaxValue);
    }

    [Fact]
    public void RealHighDefenseHeroTakes170PercentBaseCounterOnUltimate()
    {
        var p = new Player { Id=Guid.Parse("40000000-0000-0000-0000-000000000002"),Realm=0,Chapter=9,Diff=0,BodyHp=1_000_000_000_000,MapVer=1 };
        var boss = GameData.AreaWaves(GameData.ChapterFor(0,9),GameData.BossAreaFor(0),0).Single();
        p.BodyDef = boss.Atk - GameService.Stats(p).Def - 10;
        var battle = GameService.BattleAt(p,GameData.BossAreaFor(0),new DateTime(2026,10,28,0,0,0,DateTimeKind.Utc));
        Assert.Equal(10,battle.MonsterDmg);
        var hits = battle.MonsterRage!.Select((turn,index)=>(turn,index)).Where(x=>x.turn.Ultimate&&!battle.Dodged![x.index]).ToList();
        Assert.NotEmpty(hits);
        Assert.All(hits,x=>Assert.Equal(17,x.turn.Damage));
    }

    [Fact]
    public void KillingWavesBeforeCounterNeverChargesOrTransfersRage()
    {
        var p = new Player { Realm=9,Stage=2,Chapter=0,Diff=0,Admin="god",MapVer=1 };
        var battle = GameService.BattleAt(p,10,new DateTime(2026,10,28,0,0,0,DateTimeKind.Utc));
        Assert.Equal(3,battle.Waves!.Count);
        foreach(var wave in battle.Waves) {
            var turn=battle.MonsterRage![wave.Start];
            Assert.Equal(wave.Tier is "boss" or "elite"?0:-1,turn.Before);
            Assert.False(turn.Ultimate);
        }
        Assert.All(battle.MonsterRage!,t=>Assert.False(t.Ultimate));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RealBossReplayKeepsEveryChargeAndReleaseWithoutExtendingTiming(int difficulty)
    {
        var player = new Player { Id = Guid.Parse("40000000-0000-0000-0000-000000000001"), Realm=0,
            Chapter=9, Diff=difficulty, Admin="god", MapVer=1 };
        var time = new DateTime(2026,10,28,0,0,0,DateTimeKind.Utc);
        var battle = GameService.BattleAt(player,GameData.BossAreaFor(difficulty),time);
        var again = GameService.BattleAt(player,GameData.BossAreaFor(difficulty),time);
        Assert.Equal(150,battle.Hits);
        Assert.Equal(difficulty == 3 ? 150 : GameData.MaxHits,battle.TimingHits);
        Assert.Equal(battle.MonsterRage,again.MonsterRage);
        Assert.Equal(battle.Hits,battle.MonsterRage!.Length);
        Assert.Equal(30,battle.MonsterRage.Count(t=>t.Ultimate));
        for(var i=0;i<battle.Hits;i++) {
            var turn=battle.MonsterRage[i];
            Assert.Equal(i%5==4,turn.Ultimate);
            Assert.Equal(i%5*25,turn.Before);
            Assert.Equal((i+1)%5*25,turn.After);
            Assert.Equal(battle.CounterDamage![i],turn.Damage);
            Assert.Equal(0,turn.Damage); // Invulnerability remains effective against ultimates.
        }
        Assert.DoesNotContain(battle.MonsterSkills??[],e=>e.SkillId.StartsWith("nham_boc"));
    }

    [Theory]
    [InlineData("boss", 4, 1.7)]
    [InlineData("elite", 5, 1.4)]
    public void RageChargesOnCountersAndReleasesOnTheFollowingCounter(string tier, int count, double multiplier)
    {
        var state = new MonsterRageCombat(tier, "img/boss_lang.png");
        for (var i = 0; i < count; i++) Assert.Equal(1, state.CounterMultiplier());
        Assert.Equal(100, state.Rage);
        // Not calling CounterMultiplier models a controlled/dead monster: state must not advance.
        Assert.Equal(100, state.Rage);
        Assert.Equal(multiplier, state.CounterMultiplier());
        Assert.True(state.Ultimate);
        Assert.Equal(0, state.Rage);
        Assert.Equal(1, state.CounterMultiplier());
        Assert.False(state.Ultimate);
        Assert.Equal(100 / count, state.Rage);
        Assert.Equal(0, new MonsterRageCombat(tier, "img/boss_lang.png").Rage);
    }

    [Fact]
    public void NormalMonstersNeverChargeAndHellRageDoesNotStackNhamBoc()
    {
        var normal = new MonsterRageCombat("normal", "img/thach_tich.png");
        for (var i = 0; i < 20; i++) Assert.Equal(1, normal.CounterMultiplier());
        Assert.Equal(-1, normal.Rage);
        foreach (var tier in new[] { "boss", "elite" }) {
            var hell = new HellMonsterCombat(new("Test", "cot_long", 1000, 100, 0, Tier:tier), 0, 0);
            for (var t = 0; t < 12; t++) { hell.BeginTurn(t,t); Assert.Equal(1, hell.CounterMultiplier(t,t)); }
            Assert.DoesNotContain(hell.Events,e=>e.SkillId.StartsWith("nham_boc"));
        }
        var old = new HellMonsterCombat(new("Test", "thach_tich", 1000, 100, 0),0,0);
        old.BeginTurn(2,2); Assert.Equal(1,old.CounterMultiplier(2,2));
        old.BeginTurn(3,3); Assert.Equal(1.5,old.CounterMultiplier(3,3));
    }
}
