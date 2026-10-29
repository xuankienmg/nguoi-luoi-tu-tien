using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class DodgeAccuracyTests
{
    [Fact]
    public void LegacyAgilityBecomesDodgeAndDoesNotGrantAccuracy()
    {
        var player = new Player { Realm=4 };
        var stats = GameService.Stats(player);
        Assert.Equal(20, stats.Dodge);
        Assert.Equal(0, stats.Accuracy);
        var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { PropertyNamingPolicy=JsonNamingPolicy.CamelCase });
        Assert.DoesNotContain("\"speed\"", json);
        Assert.Contains("\"dodge\":20", json);
    }

    [Fact]
    public async Task DodgeDoesNotShortenExplorationAndGemsRemainIndependent()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var player = new Player { Realm=4 };
        player.Equipment.Add(new() { Slot="weapon", ItemId=GameData.AllItems.First(i => i.Slot=="weapon" && i.Rarity=="common" && i.MinRealm==4).Id });
        GemData.SetSlots(player, new() { ["weapon"]=new() { Sockets=new() { "gem_quang_lv1" } } });
        db.Players.Add(player); await db.SaveChangesAsync();
        var state = await new GameService(db).BuildStateAsync(player);
        Assert.Equal(20, state.Stats.Dodge);
        Assert.Equal(GemData.GetSocketedBonuses(player).Accuracy, state.Stats.Accuracy);
        var expected = Math.Max(GameData.MinExploreSeconds, GameData.WalkSeconds + BattleTiming.CombatSeconds(state.Battle));
        Assert.InRange(Math.Abs(state.ExploreCooldownMs-expected*1000),0,1);
        Assert.DoesNotContain(state.CombatLimits!, e => e.Id=="speed");
        Assert.Equal(state.Stats.Dodge, Assert.Single(state.CombatLimits!, e => e.Id=="dodge").Effective);
    }

    [Fact]
    public void LegacyAgilityDoesNotDeterminePvpInitiative()
    {
        var a=PvpFighter.FromStats("A",0,new() { ["atk"]=100,["hp"]=10000 });
        var d=a with { Name="D", Speed=100 };
        Assert.True(PvpCombat.Run(a,d,42).Actions[0].ByA);
    }

    [Fact]
    public void DodgeIncludesEquippedSkillOnceAndPreservesLegacyPassiveBonuses()
    {
        var player=new Player { Realm=4 };
        var passives=SkillData.Skills.Where(s=>s.Speed>0).ToDictionary(s=>s.Id,s=>s.MaxLv);
        player.SetSkillLevels(passives);
        var expected=20+passives.Sum(kv=>SkillData.ById[kv.Key].Speed*SkillData.ById[kv.Key].Factor(kv.Value)
            *CultivationManuals.Rate(CultivationManuals.Active(player),SkillData.ById[kv.Key].Element));
        Assert.Equal(expected,GameService.Stats(player).Dodge,8);
        var skill=SkillData.Skills.First(s=>s.Tree=="active");
        passives[skill.Id]=1;
        player.SetSkillLevels(passives);
        var before=GameService.Stats(player).Dodge;
        player.SkillSlots=$"{CultivationManuals.BasicOf(skill.Element)!.Id},{skill.Id}";
        var stats=GameService.Stats(player);
        Assert.Equal(before+SkillData.Active(skill,1).dodge,stats.Dodge,8);
        Assert.Equal(stats.Dodge,GameService.PvpFighterOf(player).Dodge,8);
    }

    [Fact]
    public void ArenaFallbackReadsNewDodgeAndAccuracyWhileKeepingOldProfilesCompatible()
    {
        var fighter=PvpFighter.FromStats("A",0,new() { ["atk"]=100,["hp"]=10000,["dodge"]=80,["accuracy"]=35 });
        Assert.Equal(80,fighter.Dodge); Assert.Equal(35,fighter.Gems!.Accuracy);
        var legacy=PvpFighter.FromStats("B",0,new() { ["atk"]=100,["hp"]=10000,["speed"]=20 });
        Assert.Equal(20,legacy.Dodge);
    }

    [Fact]
    public void MovementSpeedShortensOnlyWaveTravelAndPreservesAttackTime()
    {
        var battle=GameService.BattleAt(new Player { Realm=9 },1,new DateTime(2026,10,28,0,0,0,DateTimeKind.Utc));
        Assert.Equal(3,battle.Waves!.Count);
        var normal=BattleTiming.CombatSeconds(battle);
        var faster=BattleTiming.CombatSeconds(battle,100);
        Assert.Equal(GameData.WaveSeconds,normal-faster,8);
        Assert.Equal(normal-2*GameData.WaveSeconds,faster-GameData.WaveSeconds,8);
        var fighter=PvpFighter.FromStats("A",0,new() { ["atk"]=10,["hp"]=100 });
        var clean=ArenaService.Sanitize(fighter,"A",0,new() { ["dodge"]=80,["accuracy"]=35 });
        Assert.Equal(80,clean.Dodge);Assert.Equal(35,clean.Gems!.Accuracy);
    }
}
