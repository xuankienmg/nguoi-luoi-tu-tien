using VanDao.Game;
using VanDao.Data;

var checks=0;
var fixtures=new List<object>();
void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
var expected=new Dictionary<string,string>{{"hoa","burn"},{"thuy","weaken"},{"moc","heal"},{"tho","stun"},{"loi","paralyze"},{"bang","freeze"},{"gio","wind"}};
Check(SkillSecondaryEffects.TargetSurvives(100,80,.5,30),"damage reduction must be applied before deciding whether to suppress a proc");
Check(!SkillSecondaryEffects.TargetSurvives(70,80,.5,30),"exact lethal hit must suppress an enemy proc");
foreach(var skill in SkillData.Skills){
    if(skill.Tree!="active") {Check(skill.SecondaryEffect is null,"basic/passive has a secondary proc: "+skill.Id);continue;}
    var effect=skill.SecondaryEffect;
    Check(effect is not null&&effect.Kind==expected[skill.Element],"active missing elemental effect: "+skill.Id);
    Check(SkillSecondaryEffects.Chance(skill,1)==10&&SkillSecondaryEffects.Chance(skill,5)==50,"chance must scale 10..50: "+skill.Id);
    Check(SkillSecondaryEffects.Chance(skill,0)==0&&SkillSecondaryEffects.Chance(skill,-1)==0&&SkillSecondaryEffects.Chance(skill,999)==50,"chance invalid level: "+skill.Id);
    for(var level=1;level<=5;level++){
        var state=new SkillEffectCombat();
        Check(state.TryApply(skill,level,4,new FixedRandom(level*.1-1e-7),true)?.Kind==effect!.Kind,"proc below boundary: "+skill.Id);
        Check(new SkillEffectCombat().TryApply(skill,level,4,new FixedRandom(level*.1),true) is null,"proc at boundary: "+skill.Id);
        if(effect!.Target=="monster")Check(new SkillEffectCombat().TryApply(skill,level,4,new FixedRandom(0),false) is null,"proc on killed monster: "+skill.Id);
    }
}
foreach(var active in expected.Keys)
foreach(var skill in SkillData.Skills.Where(s=>s.Tree=="active")) {
    var allowed=skill.SecondaryEffect!.Target!="monster"||skill.Element==active;
    Check((new SkillEffectCombat(active).TryApply(skill,5,0,new FixedRandom(0),true)!=null)==allowed,"active manual gate: "+active+" / "+skill.Id);
    Check(SkillSecondaryEffects.Chance(skill,5,active)==(allowed?50:0),"context chance: "+active+" / "+skill.Id);
}
SkillDef First(string el)=>SkillData.Skills.First(s=>s.Element==el&&s.Tree=="active");
foreach(var el in new[]{"tho","loi","bang"}){
    var state=new SkillEffectCombat();state.TryApply(First(el),5,4,new FixedRandom(0),true);
    Check(state.ControlKind(4)==expected[el]&&state.ControlKind(5)=="","control must last exactly one counterattack");
    Check(state.Shatter(5)==(el=="bang")&&!state.Shatter(4)&&!state.Shatter(6),"freeze bonus wrong turn");
    state.ClearEnemy();Check(state.ControlKind(4)==""&&!state.Shatter(5),"replacement must clear control and shatter");
}
{
    var state=new SkillEffectCombat();state.TryApply(First("thuy"),5,4,new FixedRandom(0),true);
    Check(state.AttackDown(3)==0&&state.AttackDown(4)==20&&state.AttackDown(5)==20&&state.AttackDown(6)==0,"weaken duration");
    state.TryApply(First("thuy"),5,5,new FixedRandom(0),true);
    Check(state.AttackDown(5)==20&&state.AttackDown(6)==20&&state.AttackDown(7)==0,"weaken refresh must not stack");
    state.TryApply(First("bang"),5,5,new FixedRandom(0),true);
    Check(state.AttackDown(5)==0&&state.ControlKind(5)=="freeze","latest debuff replaces weaken");
    state.TryApply(First("hoa"),5,5,new FixedRandom(0),true);
    Check(state.ControlKind(5)==""&&!state.Shatter(6),"burn replaces freeze");
}
{
    var state=new SkillEffectCombat();state.TryApply(First("gio"),5,4,new FixedRandom(0),true);
    Check(state.DodgeBonus(3)==0&&state.DodgeBonus(4)==20&&state.DodgeBonus(5)==20&&state.DodgeBonus(6)==0,"wind duration");
    state.TryApply(First("gio"),5,5,new FixedRandom(0),true);state.ClearEnemy();
    Check(state.DodgeBonus(5)==20&&state.DodgeBonus(6)==20&&state.DodgeBonus(7)==0,"wind refresh/monster transition must preserve exactly two hero turns");
}
foreach(var active in expected.Keys)
foreach(var el in expected.Keys.Where(e=>e!=active && First(e).SecondaryEffect!.Target=="monster")) {
    var skill=First(el);
    var p=new Player{Id=Guid.Parse("66666666-6666-6666-6666-666666666666"),Realm=3,Chapter=3,Area=15,BodyHp=1_000_000,
        Skills=skill.Id+":5",LearnedManuals=string.Join(',',expected.Keys),ManualVersion=1,ActiveManual=active,
        SkillSlots=CultivationManuals.BasicOf(active)!.Id+","+skill.Id,ExploreReadyAt=new DateTime(2026,10,23,10,0,0,DateTimeKind.Utc)};
    var battle=GameService.Battle(p);
    Check(battle.Casts.Any(c=>c.Skill==skill.Id&&c.Dmg>0),"foreign skill must still damage: "+active+" / "+el);
    Check(battle.Casts.All(c=>c.Secondary==""),"foreign debuff in real battle: "+active+" / "+el);
    Check(!battle.Burned!.Any(x=>x)&&!battle.Weakened!.Any(x=>x)&&battle.MonControl!.All(x=>x==""),"foreign debuff state leaked");
}
foreach(var el in expected.Keys){
    var skill=First(el);var found=false;
    for(var seed=0;seed<30&&!found;seed++){
        var p=new Player{Id=Guid.Parse("44444444-4444-4444-4444-444444444444"),Realm=3,Chapter=3,Area=15,BodyHp=1_000_000,
            Skills=skill.Id+":5",LearnedManuals=el,ManualVersion=1,ActiveManual=el,SkillSlots=CultivationManuals.BasicOf(el)!.Id+","+skill.Id,
            ExploreReadyAt=new DateTime(2026,10,23,10,0,seed,DateTimeKind.Utc)};
        var b=GameService.Battle(p);var again=GameService.Battle(p);
        Check(System.Text.Json.JsonSerializer.Serialize(b)==System.Text.Json.JsonSerializer.Serialize(again),"secondary rolls are not deterministic: "+el);
        var procs=b.Casts.Where(c=>c.Secondary==expected[el]).ToArray();
        if(procs.Length==0)continue;found=true;
        fixtures.Add(new {element=el,skill=skill.Id,battle=b});
        Check(b.Weakened?.Length==b.Hits&&b.WindGuard?.Length==b.Hits,"state arrays not sampled with turns");
        Check(b.CounterDamage?.Length==b.Hits&&b.HeroHpLeft.All(hp=>hp>=0&&hp<=b.HeroHp),"counter damage sample or health cap invalid");
        if(el is "tho" or "loi" or "bang")foreach(var proc in procs)
            if(b.MonHpLeft[proc.Turn]>0)Check(b.MonSkip[proc.Turn]&&b.MonControl![proc.Turn]==expected[el],"successful control did not block real counterattack: "+el);
        if(el=="thuy"){
            Check(b.Weakened!.Any(x=>x),"weaken absent from real battle");
            foreach(var i in Enumerable.Range(0,b.Hits).Where(i=>b.Weakened![i]&&b.MonHpLeft[i]>0&&(b.EnrageTurn<0||i<b.EnrageTurn))){
                var waveAtk=b.Waves?.Last(w=>w.Start<=i).Atk??b.MonsterAtk;
                var expectedHit=Math.Max(1,(int)Math.Round(Math.Max(1,waveAtk*.8-GameService.Stats(p).Def)));
                Check(b.CounterDamage![i]==expectedHit,$"weakening damage mismatch: turn={i}, actual={b.CounterDamage[i]}, expected={expectedHit}, atk0={b.MonsterAtk}, def={GameService.Stats(p).Def}, waveAtk={b.Waves?.Last(w=>w.Start<=i).Atk}");
            }
        }
        if(el=="gio")Check(b.WindGuard!.Any(x=>x),"wind absent from real battle");
        if(el=="moc")Check(procs.Any(c=>b.Heal![c.Turn]>0),"wood proc did not heal real missing HP");
        if(el=="hoa")Check(procs.Any(c=>b.Burned![c.Turn]&&b.BurnDamage![c.Turn]>0),"fire proc did not burn");
    }
    Check(found,"no real cast proc recorded for "+el);
}
{
    var skill=First("moc");var observed=false;
    for(var seed=0;seed<30&&!observed;seed++){
        var p=new Player{Id=Guid.Parse("55555555-5555-5555-5555-555555555555"),Realm=0,Chapter=3,Area=15,
            Skills=skill.Id+":5",LearnedManuals="moc",ManualVersion=1,ActiveManual="moc",SkillSlots="thanh_diep_nhan,"+skill.Id,
            ExploreReadyAt=new DateTime(2026,10,23,11,0,seed,DateTimeKind.Utc)};
        var baseline=GameService.Battle(p);var hit=baseline.CounterDamage![0];
        p.BodyHp=Math.Max(0,hit*4+1-GameService.Stats(p).Hp);
        var battle=GameService.Battle(p);
        var proc=battle.Casts.FirstOrDefault(c=>c.Secondary=="heal"&&battle.HeroHpLeft[c.Turn]==0);
        if(proc is null)continue;
        observed=true;Check(battle.Heal![proc.Turn]==0,"wood secondary resurrected a dead hero");
    }
    Check(observed,"death-turn wood proc fixture did not exercise no-resurrection rule");
}
Console.WriteLine($"PASS {checks} secondary metadata, chance boundaries, durations, refresh, replacement and deterministic real-battle checks.");
var root=new DirectoryInfo(AppContext.BaseDirectory);
while(root is not null&&!File.Exists(Path.Combine(root.FullName,"VanDao.csproj")))root=root.Parent;
if(root is null)throw new Exception("repository root missing");
Directory.CreateDirectory(Path.Combine(root.FullName,"build-check"));
File.WriteAllText(Path.Combine(root.FullName,"build-check/secondary-fixtures.json"),System.Text.Json.JsonSerializer.Serialize(fixtures,
    new System.Text.Json.JsonSerializerOptions{WriteIndented=true,PropertyNamingPolicy=System.Text.Json.JsonNamingPolicy.CamelCase}));
class FixedRandom(double value):Random {public override double NextDouble()=>value;}
