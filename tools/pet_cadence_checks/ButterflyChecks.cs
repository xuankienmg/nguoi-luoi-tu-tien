using VanDao.Game;
using VanDao.Data;
using Microsoft.EntityFrameworkCore;

internal static class ButterflyChecks
{
    private sealed class FixedRandom(double value):Random { public override double NextDouble()=>value;public override int Next(int maxValue)=>0; }
    public static void Run()
    {
        var checks=0;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
        Check(!PetData.ById.ContainsKey("thuong_co_ky_lan"),"Qilin must be removed");
        var def=PetData.ById["that_thai_ho_diep"];
        Check(def.Name=="Thất Thải Hồ Điệp"&&def.Rarity=="ancient"&&def.Element=="moc","Butterfly identity");
        Check(PetData.SpeciesQuality(def)>Array.IndexOf(PetData.Qualities,"legendary"),"Ancient above Heavenly");
        Check(PetData.Definitions.Length==14&&def.Forms.Length==3,"14 species, 42 forms");
        Check(PetData.Pool(4).Length==4&&!PetData.Pool(4).Contains(def),"Original four Heavenly pets retained");
        Check(PetData.RollHatch(5,new FixedRandom(0))==def,"Ancient egg pool first species");
        foreach(var q in Enumerable.Range(0,5))Check(PetData.RollHatch(q,new FixedRandom(0))!=def,"No Butterfly in lower eggs, including Heavenly");
        Check(PetData.EggIds[5]=="thu_dan_thuong_co"&&PetData.HatchSeconds[5]==43200,"Separate 12-hour Ancient egg");
        Check(PetData.PetSpinWeights.Length==6&&Math.Abs(PetData.PetSpinWeights.Sum()-100)<1e-9,"Six normalized pet spin outcomes");
        Check(PetData.PetSpinWeights[4]==.9&&PetData.PetSpinWeights[5]==.1,"Only Heavenly chance funds Ancient egg");
        Check(GameData.ItemById["thu_dan_thuong_co"].Rarity=="ancient","Ancient egg registered");
        Check(def.SkillEvery==3&&def.Skill=="butterfly","Ancient cadence");
        var pet=new Pet{DefId=def.Id};
        Check(PetData.QualityOf(pet,def)==5&&PetData.ReleaseValue(pet,def)==160,"Ancient quality and release value");
        Check(PetData.AptMin[5]==90&&PetData.AptMax[5]==100,"Ancient aptitude");
        var player=new Player{Realm=5};player.Pets.Add(pet);
        Check(GameService.PetStateFor(player).Owned.Single().Rarity=="ancient","DTO supports Ancient rarity");
        using var db=new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection();db.Database.EnsureCreated();
        var game=new GameService(db);
        var hatchPlayer=new Player();
        game.AdminAction(hatchPlayer,"give","thu_dan_thuong_co",1);
        Check(hatchPlayer.Count("thu_dan_thuong_co")==1,"Admin gives Ancient egg directly");
        game.Hatch(hatchPlayer,"thu_dan_thuong_co",0);
        Check(hatchPlayer.Count("thu_dan_thuong_co")==0&&hatchPlayer.Hatch.Contains("thu_dan_thuong_co"),"Dedicated egg consumed into incubator");
        hatchPlayer.Hatch="0|thu_dan_thuong_co|1";
        game.CollectHatch(hatchPlayer);
        var born=hatchPlayer.Pets.Single();
        Check(PetData.Pool(5).Any(d=>d.Id==born.DefId)&&new[]{born.AptAtk,born.AptDef,born.AptHp}.All(x=>x>=90&&x<=100),"Ancient egg hatches a registered Ancient pet with Ancient aptitude");
        var lower=new Pet{DefId="chu_tuoc",Level=1,AptAtk=90,AptDef=90,AptHp=90};
        pet.AptAtk=pet.AptDef=pet.AptHp=90;
        Check(GameService.PetStats(player,pet).Hp>GameService.PetStats(player,lower).Hp,"Ancient stats above Heavenly at equal level");
        foreach(var form in Enumerable.Range(0,3))foreach(var stars in new[]{0,5}){
            var combat=new PetCombat(def,form,new CombatStats(100,10000,10),stars,null,true,new CombatStats(50,2000,10));
            var scale=PetData.StarScale(stars);long hp=2000;
            var cast=combat.Cast(4,ref hp,10000,10000,0,1);
            // Linh Hộ = Máu linh thú + Máu chủ nhân × Đồng tâm; 45% hồi máu, 55% khiên
            var life=2000+10000*PetData.SkillBond[form];var coef=PetData.SkillCoef("butterfly",form);
            Check(Math.Abs(coef-50*.15*3*PetData.SkillFormK[form])<1e-9,"Butterfly support budget");
            var rawHeal=(int)(life*coef*.45/100);
            var expected=(int)Math.Round(rawHeal*(1+.18*scale));
            Check(cast.Heal==expected&&hp==2000+expected,"Actual heal with Butterfly talent");
            Check(cast.Guard==(int)(life*coef*.55/100),"Butterfly shield from support power");
            Check(cast.Dmg==0&&cast.Kind=="butterfly","Butterfly support event");
            Check(cast.Skill==def.Forms[form].SkillName,"Correct evolved skill name");
            hp=9999;Check(combat.Cast(8,ref hp,10000,10000,0,1).Heal==1&&hp==10000,"No overheal");
            for(var i=0;i<10;i++)combat.Cast(12+i,ref hp,10000,10000,0,1);
            Check(combat.Shield==3000,"Shield capped at 30 percent");
            combat.Absorb(4000);Check(combat.Shield==0,"Shield absorbs without negative values");
            Check(combat.Healing(100,8000)==(int)Math.Round(100*(1+.18*scale)),"Talent applies above half HP");
            Check(combat.Healing(100,0)==0,"No resurrection");
            Check(PetData.SkillDescription("butterfly",form,stars).Contains(PetData.Percent(Math.Round(coef*.45,1))),"Description matches scaling");
        }
        Console.WriteLine($"PASS {checks} Thất Thải Hồ Điệp checks.");
    }
}
