using VanDao.Game;
using VanDao.Data;
using Microsoft.EntityFrameworkCore;
/// <summary>Trang bị linh thú: Boss rơi ngẫu nhiên ô + phẩm (tối đa Thiên); phẩm Thánh chỉ từ hợp thành 3 → 1.</summary>
internal static class PetGearDropChecks
{
    public static void Run()
    {
        int checks=0;
        void Check(bool ok,string msg){if(!ok)throw new Exception(msg);checks++;}
        Check(PetTraining.TierNames[^1]=="Thánh"&&PetTraining.MaxDropTier==4&&PetTraining.TierNames[PetTraining.MaxDropTier]=="Thiên","Top tier renamed to Thánh, drops capped at Thiên");
        foreach(var s in PetTraining.Slots){
            var top=GameData.ItemById[PetTraining.GearId(s.Id,5)];
            Check(top.Name==$"{s.Name} Thánh"&&top.Rarity=="mythic"&&top.Desc.Contains("chỉ nhận từ hợp thành"),s.Id+" Thánh item describes fusion-only source");
            Check(GameData.ItemById[PetTraining.GearId(s.Id,0)].Desc.Contains("Rơi ngẫu nhiên khi hạ Boss"),s.Id+" low tier describes random boss drop");
        }
        // Không món trang bị linh thú nào rơi ở ải thường, rương hay vòng quay: chỉ còn trong danh sách Boss
        foreach(var c in GameData.Chapters)foreach(var d in Enumerable.Range(0,GameData.DiffNames.Length)){
            for(var a=1;a<=GameData.Areas;a++)Check(!PetLoot.StageOdds(c,d,a).Any(x=>PetTraining.Gear.ContainsKey(x.Item)),"No pet gear from normal stages");
            var boss=PetLoot.BossMaterials(c.Index,d).Where(x=>PetTraining.Gear.ContainsKey(x.Item)).ToArray();
            var cap=PetTraining.DropTierCap(c.Index,d);
            Check(boss.All(x=>PetTraining.Gear[x.Item].Tier<=cap&&cap<=PetTraining.MaxDropTier),"Boss list stays within cap ≤ Thiên");
            Check(boss.Length==PetTraining.Slots.Length*(cap+1),"Every slot × tier up to cap is listed");
            Check(Math.Abs(boss.Sum(x=>x.Chance)-PetLoot.BossGearChance(d))<1e-9,"Listed odds add up to one boss gear roll");
            Check(boss.Where(x=>PetTraining.Gear[x.Item].Tier<cap).All(x=>x.Chance>boss.First(y=>y.Item==PetTraining.GearId(PetTraining.Gear[x.Item].Slot,cap)).Chance),"Lower tiers more likely");
        }
        // Gieo thật: không bao giờ ra Thánh, tối đa một món mỗi lần, đủ cả 3 ô và mọi phẩm tới trần
        var rng=new Random(7);var seen=new HashSet<string>();int hits=0;const int n=200_000;
        for(var i=0;i<n;i++){
            var rolled=PetLoot.RollBossMaterials(9,2,rng).Where(x=>PetTraining.Gear.ContainsKey(x.Item)).ToList();
            Check(rolled.Count<=1&&rolled.All(x=>x.Quantity==1),"At most one pet gear per boss kill");
            foreach(var (item,_) in rolled){Check(PetTraining.Gear[item].Tier<=PetTraining.MaxDropTier,"Never drops Thánh");seen.Add(item);hits++;}
        }
        Check(seen.Count==PetTraining.Slots.Length*(PetTraining.MaxDropTier+1),"Random slot and tier cover all 15 droppable items");
        Check(Math.Abs((double)hits/n-PetLoot.BossGearChance(2))<.005,"Actual drop rate matches displayed total");
        // Thánh vẫn hợp thành được từ 3 món Thiên
        using var db=new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection();db.Database.EnsureCreated();
        var game=new GameService(db);var p=new Player();
        var heaven=PetTraining.GearId("collar",4);p.Add(heaven,3);p.Add("linh_thach",PetTraining.FuseStones(4));
        game.FusePetGear(p,heaven,1);
        Check(p.Count(heaven)==0&&p.Count(PetTraining.GearId("collar",5))==1&&p.Count("linh_thach")==0,"3 Thiên + stones fuse into 1 Thánh");
        Console.WriteLine($"PASS {checks} pet gear drop/fusion checks.");
    }
}
