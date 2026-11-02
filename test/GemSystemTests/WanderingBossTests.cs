using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class WanderingBossTests
{
    private static readonly DateTime Start = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EncounterIsStableForATripAndNearItsChance()
    {
        var p = new Player { Id = Guid.Parse("50000000-0000-0000-0000-000000000001"), StarterChosen = true, MapVer = 1 };
        var met = 0;
        for (var i = 0; i < 20000; i++)
        {
            var trip = Start.AddSeconds(i * 7);
            var boss = WanderingBoss.Meet(p, 5, trip);
            Assert.Equal(boss, WanderingBoss.Meet(p, 5, trip));
            if (boss is not null) met++;
        }
        Assert.InRange(met / 20000.0, WanderingBoss.Chance * .8, WanderingBoss.Chance * 1.2);
    }

    [Fact]
    public void NoEncounterAtBossStageOrBeforeStarter()
    {
        var p = new Player { Id = Guid.Parse("50000000-0000-0000-0000-000000000002"), StarterChosen = true, MapVer = 1 };
        for (var i = 0; i < 5000; i++)
        {
            var trip = Start.AddSeconds(i * 7);
            Assert.Null(WanderingBoss.Meet(p, GameData.BossAreaFor(p.Diff), trip));
        }
        var newbie = new Player { Id = p.Id, StarterChosen = false, MapVer = 1 };
        Assert.All(Enumerable.Range(0, 5000), i => Assert.Null(WanderingBoss.Meet(newbie, 5, Start.AddSeconds(i * 7))));
    }

    [Fact]
    public void StatsGrowWithRealmAndStage()
    {
        foreach (var b in WanderingBoss.All)
        {
            MonsterDef? last = null;
            for (var realm = 0; realm < GameData.Realms.Count; realm++)
            for (var stage = 0; stage < GameData.Realms[realm].Stages.Length; stage++)
            {
                var m = WanderingBoss.Monster(b, realm, stage);
                Assert.Equal("boss", m.Tier);
                Assert.Equal(b.Image, m.Image);
                Assert.True(m.Hp > 0 && m.Atk > 0);
                if (last is not null)
                {
                    Assert.True(m.Hp >= last.Hp, $"{b.Name} máu giảm ở cảnh giới {realm} tầng {stage}");
                    Assert.True(m.Atk >= last.Atk, $"{b.Name} công giảm ở cảnh giới {realm} tầng {stage}");
                }
                last = m;
            }
        }
    }

    [Fact]
    public void LootMatchesEachBoss()
    {
        var rng = new Random(7);
        var tree = WanderingBoss.All.Single(b => b.Id == "boss_co_thu_yeu_vuong");
        var golem = WanderingBoss.All.Single(b => b.Id == "boss_huyen_nham_cu_nhan");
        for (var realm = 0; realm < GameData.Realms.Count; realm++)
        for (var i = 0; i < 200; i++)
        {
            var herbs = WanderingBoss.RollLoot(tree, realm, rng);
            Assert.InRange(herbs.Values.Sum(), 1, 10);
            Assert.All(herbs.Keys, id => Assert.Equal("herb", GameData.ItemById[id].Type));

            var stones = WanderingBoss.RollLoot(golem, realm, rng);
            Assert.InRange(stones.Values.Sum(), 1, 5);
            Assert.All(stones.Keys, id => Assert.True(GemData.SoStones.Contains(id) || (realm >= 3 && GemData.CaoStones.Contains(id)), id));
        }
    }

    [Fact]
    public void BattleUsesTheBossOnlyOnAnEncounterTrip()
    {
        var p = new Player { Id = Guid.Parse("50000000-0000-0000-0000-000000000003"), StarterChosen = true, MapVer = 1, Area = 5, AreaMax = 5 };
        var trip = Enumerable.Range(0, 5000).Select(i => Start.AddSeconds(i * 7)).First(t => WanderingBoss.Meet(p, 5, t) is not null);
        var boss = WanderingBoss.Meet(p, 5, trip)!;
        var battle = GameService.BattleAt(p, 5, trip);
        Assert.Equal(boss.Name, battle.Monster);
        Assert.Single(battle.Waves!);
        Assert.NotEqual(boss.Name, GameService.BattleAt(p, 5, trip, forecast: true).Monster);
    }
}
