using VanDao.Game;
using VanDao.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
internal static class ThunderDragonChecks
{
    public static async Task WriteFixture(string file)
    {
        using var db=new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source="+file).Options);
        db.Database.EnsureCreated();var game=new GameService(db);var p=await game.CreateAsync("Lôi Long UI test");
        p.Realm=3;p.Chapter=3;p.Area=15;p.BodyHp=1000000;
        var pet=new Pet{PlayerId=p.Id,DefId="thai_co_loi_long",Level=80,Form=2,Slot=1,AptAtk=90,AptDef=90,AptHp=90};p.Pets.Add(pet);db.Pets.Add(pet);
        foreach(var extra in new[]{new Pet{DefId="that_thai_ho_diep",Level=60,Form=2},new Pet{DefId="chu_tuoc",Level=50},new Pet{DefId="thai_co_loi_long",Level=40,Quality=4}}){extra.PlayerId=p.Id;p.Pets.Add(extra);db.Pets.Add(extra);}
        await game.SaveAsync(p);Console.WriteLine("Created isolated dragon UI fixture");
    }
    public static void Run()
    {
        if(!PetData.ById.ContainsKey("thai_co_loi_long"))throw new Exception("Missing Thái Cổ Lôi Long");
        var checks=1;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
        var def=PetData.ById["thai_co_loi_long"];
        Check(def.Rarity=="ancient"&&def.Element=="loi"&&def.SkillEvery==3,"Ancient thunder identity and cadence");
        Check(PetData.Pool(5).Length==2&&PetData.Pool(5).Contains(def),"Ancient egg shared equally by two pets");
        Check(PetData.RollHatch(5,new FixedRandom(.5,1))==def,"Second Ancient egg outcome is dragon");
        for(var form=0;form<3;form++)foreach(var stars in new[]{0,5}){
            foreach(var roll in new[]{0,.499999,.5,.999999}){
                var c=new PetCombat(def,form,new CombatStats(1000,10000,20),stars,new FixedRandom(roll),true,new CombatStats(500,5000,10));long hp=8000;
                var cast=c.Cast(2,ref hp,100000,100000,0,1);
                // Linh Uy = Công linh thú + Công chủ nhân × Đồng tâm; hệ số = 50% × 3 lượt × hình thái × (1 − 20% tê liệt); sao không nhân hệ số
                var power=500+1000*PetData.SkillBond[form];var coef=50*3*PetData.SkillFormK[form]*.8;
                Check(Math.Abs(PetData.SkillCoef(def.Skill,form)-coef)<1e-9,"Thunder coefficient from budget");
                Check(cast.Dmg==(int)Math.Round(power*coef/100),"Thunder damage from skill power, stars only via pet stats");
                Check(cast.ParalyzeTurns==(roll<.5?1:0)&&c.Paralyzed==(roll<.5),"50 percent exact boundary, fixed one turn at all stars/forms");
                Check(cast.Kind=="ancient_thunder"&&hp==8000,"Thunder damage event and no heal");
                c.BeginTurn(3,ref hp,100000,0,1);Check(!c.Paralyzed,"Paralysis expires before next turn");
            }
        }
        var refresh=new PetCombat(def,2,new CombatStats(100,1000,10),0,new FixedRandom(0));long health=1000;
        refresh.Cast(0,ref health,10000,10000,0,1);refresh.Cast(0,ref health,10000,10000,0,1);
        refresh.BeginTurn(1,ref health,10000,0,1);Check(!refresh.Paralyzed,"No stacking from repeated same-turn stun");
        refresh.Cast(2,ref health,10000,10000,0,1);refresh.NextWave();Check(!refresh.Paralyzed,"New wave clears paralysis");
        Check(PetData.SkillDescription(def.Skill,2,5).Contains("50%")&&PetData.SkillDescription(def.Skill,2,5).Contains("1 lượt"),"Tooltip retains approved chance and duration");
        var p=new Player{Id=Guid.Parse("11111111-1111-1111-1111-111111111111"),Realm=3,Chapter=3,BodyHp=1000000,Area=15,ExploreReadyAt=new DateTime(2026,10,22,10,0,0,DateTimeKind.Utc)};
        p.Pets.Add(new Pet{DefId=def.Id,Slot=1,Level=1});
        var a=GameService.BattleAt(p,15);var b=GameService.BattleAt(p,15);
        Check(JsonSerializer.Serialize(a)==JsonSerializer.Serialize(b),"Seeded dragon battle deterministic");
        var events=a.Pet!.Casts;
        Check(events.Length>0,"Real battle casts thunder");
        Check(events.Where(c=>c.ParalyzeTurns>0).All(c=>a.MonSkip[c.Turn]),"Successful paralysis blocks actual monster counterattack");
        Console.WriteLine($"PASS {checks} Thái Cổ Lôi Long checks.");
    }
    private sealed class FixedRandom(double value,int index=0):Random{public override double NextDouble()=>value;public override int Next(int maxValue)=>index%maxValue;}
}
