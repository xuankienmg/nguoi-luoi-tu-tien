using VanDao.Game;
if(args.Length==2&&args[0]=="thunder-fixture"){await ThunderDragonChecks.WriteFixture(args[1]);return;}
ThunderDragonChecks.Run();
PetMiracleChecks.Run();
ButterflyChecks.Run();
ControlChecks.Run();
DualElementChecks.Run();
PetGearDropChecks.Run();
DivineRealmChecks.Run();
int checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
foreach(var def in PetData.Definitions){
    var combat=new PetCombat(def,0,new CombatStats(100,1000,10),0);
    Check(!combat.SkillReady,def.Id+" starts charged");
    for(int i=1;i<def.SkillEvery;i++){
        combat.RecordOwnerTurn();
        Check(!combat.SkillReady,def.Id+" casts too early");
        combat.NextWave();
    }
    combat.RecordOwnerTurn();
    Check(combat.SkillReady,def.Id+" did not carry charge across waves");
    combat.NextWave();combat.RecordOwnerTurn();
    Check(combat.SkillReady,def.Id+" lost a ready cast when owner killed a wave");
    long hp=1000;
    combat.Cast(30,ref hp,10000,10000,0,1);
    Check(!combat.SkillReady,def.Id+" did not consume charge on real cast");
}
Console.WriteLine($"PASS {checks} pet cadence checks across {PetData.Definitions.Length} species.");
