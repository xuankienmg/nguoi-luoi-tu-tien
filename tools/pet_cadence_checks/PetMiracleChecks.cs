using VanDao.Game;
using VanDao.Data;
using System.Text.Json;
internal static class PetMiracleChecks
{
    public static void Run()
    {
        if(typeof(PetCombat).GetMethod("TryMiracle")==null)throw new Exception("Missing Ancient pet miracles");
        int checks=1;
        void Check(bool ok,string msg){if(!ok)throw new Exception(msg);checks++;}
        var butterfly=PetData.ById["that_thai_ho_diep"];var dragon=PetData.ById["thai_co_loi_long"];
        var owner=new CombatStats(1000,10000,10);
        foreach(var def in PetData.Definitions.Where(d=>d.Rarity!="ancient")){
            var c=new PetCombat(def,2,owner);long hp=0;
            Check(c.TryMiracle(0,ref hp,10000,100,1)==null&&!c.MiracleUsed,def.Id+" cannot use miracles");
            var p=new Player();p.Pets.Add(new Pet{DefId=def.Id});Check(GameService.PetStateFor(p).Owned.Single().Miracle==null,"No lower-quality miracle DTO");
        }
        foreach(var form in Enumerable.Range(0,3))foreach(var stars in new[]{0,5}){
            var c=new PetCombat(butterfly,form,owner,stars);long hp=1;
            Check(c.TryMiracle(0,ref hp,10000,100,1)==null,"Butterfly cannot trigger while alive");
            c.RecordOwnerTurn();c.RecordOwnerTurn();hp=-90000;
            var revive=c.TryMiracle(1,ref hp,10000,100,1,false)!;
            Check(hp==2500&&revive.Heal==2500&&revive.Guard==0&&c.Shield==0,"Fixed25% resurrection, no amplification/shield");
            Check(revive.IsMiracle&&revive.Kind=="butterfly_rebirth"&&revive.At==.9,"Explicit resurrection event after impact");
            Check(!c.SkillReady,"Miracle cannot advance normal cadence");c.RecordOwnerTurn();Check(c.SkillReady,"Miracle cannot reset normal cadence");
            c.NextWave();hp=0;Check(c.TryMiracle(2,ref hp,10000,100,1)==null,"Resurrection once across waves");
            var lightning=new PetCombat(dragon,form,owner,stars);hp=3001;
            Check(lightning.TryMiracle(0,ref hp,10000,100,1)==null,"Above30% no lightning");
            hp=0;Check(lightning.TryMiracle(0,ref hp,10000,100,1)==null,"Dead owner cannot cast lightning");
            hp=3000;var cast=lightning.TryMiracle(1,ref hp,10000,100,1)!;
            Check(cast.Dmg==2430&&cast.ParalyzeTurns==1&&hp==3000,"250% fixed damage,30% penetration, guaranteed1turn");
            Check(lightning.Paralyzed&&cast.IsMiracle,"Immediate pending counter is blocked");
            lightning.BeginTurn(2,ref hp,10000,100,1);Check(!lightning.Paralyzed,"Stun expires after1turn");
            lightning.NextWave();Check(lightning.TryMiracle(3,ref hp,10000,100,1)==null,"Lightning once across waves");
        }
        var delayed=new PetCombat(dragon,0,owner);long h=2500;delayed.TryMiracle(4,ref h,10000,100,1,false);
        delayed.BeginTurn(5,ref h,10000,100,1);Check(delayed.Paralyzed,"Post-hit lightning blocks next counter");
        delayed.BeginTurn(6,ref h,10000,100,1);Check(!delayed.Paralyzed,"Post-hit stun not2turns");
        var gear=new DivineCombat(6);var priority=new PetCombat(butterfly,2,owner,5);gear.BeginTurn();h=gear.Receive(100,10000);
        Check(h==1&&priority.TryMiracle(0,ref h,10000,100,1,false)==null&&!priority.MiracleUsed,"Gear survival precedes resurrection");
        gear.BeginTurn();h=gear.Receive(h,10);priority.TryMiracle(1,ref h,10000,100,1,false);Check(h==2500,"Resurrection available after gear save consumed");
        foreach(var def in new[]{butterfly,dragon}){
            var disabled=new PetCombat(def,2,owner,5,null,false);h=0;
            Check(disabled.TryMiracle(0,ref h,10000,100,1)==null,"Actual lower rarity blocks Ancient species miracle");
            var p=new Player();p.Pets.Add(new Pet{DefId=def.Id,Quality=4});
            Check(GameService.PetStateFor(p).Owned.Single().Miracle==null,"Lower actual quality hides miracle");
        }
        var battlePlayer=new Player{Realm=0,Chapter=6,Area=16,ExploreReadyAt=new DateTime(2026,10,22,12,0,0,DateTimeKind.Utc)};
        battlePlayer.Pets.Add(new Pet{DefId=butterfly.Id,Slot=1});
        var a=GameService.BattleAt(battlePlayer,16);var b=GameService.BattleAt(battlePlayer,16);
        Check(JsonSerializer.Serialize(a)==JsonSerializer.Serialize(b),"Miracle battle deterministic");
        Check(a.Pet!.Casts.Count(c=>c.IsMiracle)==1,"Real lethal battle resurrects exactly once");
        var revived=a.Pet.Casts.Single(c=>c.IsMiracle);
        Check(a.HeroHpLeft[revived.Turn]==Math.Max(1,(int)(a.HeroHp*.25)),"Sampled resurrection retains actual health snapshot");
        battlePlayer.Pets[0].Quality=4;
        Check(!GameService.BattleAt(battlePlayer,16).Pet!.Casts.Any(c=>c.IsMiracle),"Real battle excludes lower actual quality miracles");
        battlePlayer.Pets[0].DefId=dragon.Id;battlePlayer.Pets[0].Quality=-1;
        var baseStats=GameService.Stats(battlePlayer);
        var monster=GameData.AreaWaves(GameData.Chapters[6],16,0)[0];
        var counterDamage=Math.Max(1,monster.Atk-baseStats.Def);
        battlePlayer.BodyHp=Math.Max(0,counterDamage*4-baseStats.Hp);
        var thunderBattle=GameService.BattleAt(battlePlayer,16);
        Check(thunderBattle.Pet!.Casts.Count(c=>c.IsMiracle)==1,"Real low-health battle triggers one Heavenly Tribulation");
        var tribulation=thunderBattle.Pet.Casts.Single(c=>c.IsMiracle);
        Check(tribulation.At==.9&&thunderBattle.HeroHpLeft[tribulation.Turn]>0&&thunderBattle.HeroHpLeft[tribulation.Turn]<=thunderBattle.HeroHp*.3,"Thunder triggers after nonlethal counter at approved HP threshold");
        Check(tribulation.Turn+1<thunderBattle.Hits&&thunderBattle.MonSkip[tribulation.Turn+1],"Actual next enemy counter is paralyzed");
        Console.WriteLine($"PASS {checks} Ancient pet miracle checks.");
    }
}
