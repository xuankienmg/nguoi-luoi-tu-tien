using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

var root = Path.GetFullPath(args.FirstOrDefault() ?? ".");
var ids = HerbExpansion.Items.Select(x => x.Id).ToHashSet();
var checks=0;
void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
Check(ids.Count==12,"expected twelve unique expansion herbs");
foreach(var (rarity,count) in new[]{("common",3),("uncommon",3),("rare",6)})
    Check(HerbExpansion.Items.Count(x=>x.Rarity==rarity)==count,"rarity spread");
using var db=new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
var game=new GameService(db);
foreach(var herb in HerbExpansion.Items){
    Check(GameData.ItemById.ContainsKey(herb.Id)&&herb.Type=="herb","registered herb: "+herb.Id);
    Check(File.Exists(Path.Combine(root,"wwwroot",herb.Icon!)),"icon missing: "+herb.Id);
    Check(GameData.AllItems.Count(x=>x.Id==herb.Id)==1,"duplicate herb");
    var drops=GameData.HerbDrops.Where(x=>x.Item==herb.Id).ToArray();
    Check(drops.Length==1&&drops[0].Chance>0&&drops[0].Chance<=1,"drop rule");
    var drop=drops[0];
    Check(!drop.InChest,"new herbs must not change chest pools");
    foreach(var chapter in drop.Chapters){
        Check(chapter>=0&&chapter<GameData.OpenChapters,"unreachable chapter");
        Check(GameData.HerbDropsAt(chapter,drop.MinArea).Any(x=>x.Item==herb.Id),"missing at source");
        if(drop.MinArea>1)Check(!GameData.HerbDropsAt(chapter,drop.MinArea-1).Any(x=>x.Item==herb.Id),"drops too early");
    }
    var recipes=GameData.Brews.Where(x=>x.In.ContainsKey(herb.Id)).ToArray();
    Check(recipes.Length>0,"herb must be used by a recipe");
    foreach(var recipe in recipes){
    Check(recipe.In[herb.Id]>0,"positive herb dose");
    var brewer=new Player{Id=Guid.NewGuid(),Realm=Math.Max(7,GameData.ItemById[recipe.Out].MinRealm)};
    foreach(var (key,qty) in recipe.In)brewer.Add(key,qty*2);
    game.Brew(brewer,recipe.Out,1);
    Check(brewer.Count(recipe.Out)==1&&recipe.In.All(x=>brewer.Count(x.Key)==x.Value),"brew debit");
    var missing=new Player{Id=Guid.NewGuid(),Realm=brewer.Realm};
    foreach(var (key,qty) in recipe.In)if(key!=herb.Id)missing.Add(key,qty);
    var before=recipe.In.ToDictionary(x=>x.Key,x=>missing.Count(x.Key));
    try{game.Brew(missing,recipe.Out,1);throw new Exception("allowed brew without new herb");}
    catch(GameException){Check(recipe.In.All(x=>missing.Count(x.Key)==before[x.Key]),"failed brew consumed inventory");}
    }
    var seller=new Player{Id=Guid.NewGuid()};seller.Add(herb.Id,2);
    game.Sell(seller,herb.Id,1);
    Check(seller.Count(herb.Id)==1&&seller.Count("linh_thach")==GameData.ItemById[herb.Id].Sell,"herb sale");
}
foreach(var (pill,herb,qty) in new[]{("luyen_hu_dan","dia_mach_tien_dang",1),("hop_the_dan","thien_truc_duan",1),
    ("dai_thua_dan","phuong_vu_chi",2),("do_kiep_dan","phuong_vu_chi",3),("chan_tien_dan","hon_nguyen_linh_chung",4)}){
    var recipe=GameData.Brews.Single(x=>x.Out==pill);
    Check(recipe.In.GetValueOrDefault(herb)==qty,"high realm pill rare ingredient: "+pill);
    Check(GameData.ItemById.TryGetValue(herb,out var item)&&item.Rarity=="rare","high realm pill must require Rare herbs");
}
foreach(var realm in Enumerable.Range(4,5)){
    var recipe=GameData.Brews.Single(x=>x.Out==GameData.BreakPillId(realm));
    Check(recipe.In.Keys.Any(k=>ids.Contains(k)&&GameData.ItemById[k].Rarity=="rare"),"high realm break bonus pills require rare herbs");
}
foreach(var chapter in new[]{5,6,7,8,9})foreach(var diff in new[]{0,1,2}){
    var groups=PetLoot.StageGroups(GameData.Chapters[chapter],diff,10);
    Check(groups.SelectMany(x=>x.Choices).Any(x=>ids.Contains(x.Item)&&GameData.ItemById[x.Item].Rarity=="rare"),"late map must actually drop collectible rare herbs");
}
foreach(var r in Enumerable.Range(0,GameData.Realms.Count))foreach(var chest in GameData.Chests.Keys)foreach(var diff in Enumerable.Range(0,3)){
    var table=ChestRewards.Table(r,chest,diff);
    Check(Math.Abs(table.Sum(x=>x.Probability)-1)<1e-8,"chest normalization");
    Check(table.All(x=>!ids.Contains(x.Item)),"new herb appeared in chest");
}
// Bổ Nguyên Đan giữ nguyên liệu nền và phí linh thạch, thêm đúng 1 linh thảo bổ sung cùng bậc (10/2026)
foreach(var (id,cost,total,herb,qty) in new[]{("bo_nguyen_dan_ha",5000,5,"bach_diep_thao",2),("bo_nguyen_dan_trung",20000,9,"thuy_lo_diep",2),("bo_nguyen_dan_thuong",80000,23,"kim_duong_lien",1)}){
    var b=GameData.Brews.Single(x=>x.Out==id);
    Check(b.In["linh_thach"]==cost&&b.In.Where(x=>x.Key!="linh_thach"&&!ids.Contains(x.Key)).Sum(x=>x.Value)==total&&b.In.GetValueOrDefault(herb)==qty&&b.In.Keys.Count(ids.Contains)==1,"replenishing pill keeps base cost and adds one new herb: "+id);
}
// mọi đan phương đều cần ít nhất 1 linh thảo bổ sung
foreach(var b in GameData.Brews)Check(b.In.Keys.Any(ids.Contains),"pill without a new herb: "+b.Out);
// Khó / Ác mộng: mỗi chương rơi thêm linh thảo bổ sung của đúng chương đó (ngoài linh thảo chương cuối), không trùng dòng
foreach(var c in GameData.Chapters.Where(x=>x.Index<GameData.OpenChapters))foreach(var diff in new[]{1,2}){
    var own=HerbExpansion.Drops.Where(h=>h.Chapters.Contains(c.Index)).Select(h=>h.Item).ToHashSet();
    var items=PetLoot.StageGroups(c,diff,GameData.Areas).SelectMany(g=>g.Choices).Select(x=>x.Item).ToList();
    Check(own.All(items.Contains),$"hard map chapter {c.Index+1} diff {diff} misses its own new herbs");
    Check(items.Count==items.Distinct().Count(),"duplicate drop rows");
    Check(GameData.HerbDropDefsFor(c,diff).All(h=>h.Chapters.Contains(GameData.DiffHerbChapter(c,diff))||own.Contains(h.Item)),"unexpected herb on hard map");
}
Check(!GameData.HerbDropDefsFor(GameData.Chapters[0],2).Select(h=>h.Item).ToHashSet().SetEquals(GameData.HerbDropDefsFor(GameData.Chapters[4],2).Select(h=>h.Item)),"Nightmare chapters drop different herbs");
Check(GameData.HerbDropDefsFor(GameData.Chapters[3],0).SequenceEqual(GameData.HerbDrops.Where(h=>h.Chapters.Contains(3))),"Normal maps unchanged");
// rương hiếm luôn thấp hơn rương thường ở mọi chương, mức khó và ải
foreach(var c in GameData.Chapters.Where(x=>x.Index<GameData.OpenChapters))foreach(var diff in new[]{0,1,2})for(var a=1;a<=GameData.Areas;a++){
    var odds=PetLoot.StageOdds(c,diff,a);
    var normal=odds.Single(x=>x.Item==GameData.ChestId("ruong_thuong",diff)).Chance;
    var rare=odds.SingleOrDefault(x=>x.Item==GameData.ChestId("ruong_hiem",diff))?.Chance??0;
    Check(rare<normal&&(a<GameData.RareChestMinArea?rare==0:Math.Abs(rare-GameData.EliteRareChestChance)<1e-9),$"rare chest odds chapter {c.Index+1} diff {diff} area {a}");
}
// Khó / Ác mộng: thưởng tăng theo chương — chương 1 giữ như cũ, mỗi chương +8% số lượng, phẩm trang bị cao dần
Check(GameData.DiffRewardAt(0,9)==1&&GameData.DiffRewardAt(1,0)==GameData.DiffReward[1]&&GameData.DiffRewardAt(2,0)==GameData.DiffReward[2],"chapter 1 keeps old reward");
Check(Math.Abs(GameData.DiffRewardAt(2,9)-GameData.DiffReward[2]*1.72)<1e-9,"Nightmare chapter 10 ×1.72 more");
foreach(var diff in new[]{1,2}){
    double Qty(int ci,string item){var o=PetLoot.StageOdds(GameData.Chapters[ci],diff,GameData.Areas).Single(x=>x.Item==item);return o.Min+o.Max;}
    Check(Qty(9,"hon_nguyen_dao_lien")>Qty(0,"hon_nguyen_dao_lien"),"hard herb quantity rises with chapter");
    double Avg(double[] w)=>w.Select((x,i)=>x*i).Sum()/w.Sum();
    var basis=new[]{.4,.3,.2,.1,0};
    var prev=-1d;
    for(var ci=0;ci<GameData.OpenChapters;ci++){
        var q=GameData.DiffQuality(basis,diff,ci);
        Check(Math.Abs(q.Sum()-1)<1e-9,"quality weights normalized");
        Check(Avg(q)>prev,"gear quality rises with chapter");prev=Avg(q);
    }
    var old=GameData.DiffQualityShift[diff];
    var first=GameData.DiffQuality(basis,diff,0);
    Check(Math.Abs(first[0]-basis[0]*(1-old))<1e-9,"chapter 1 quality shift unchanged");
}
Check(GameData.DiffQualityShiftAt(1,9)==(GameData.HardShiftTop,0)&&GameData.DiffQualityShiftAt(2,9)==(1,GameData.NightmareDoubleShiftTop),"chapter 10 shifts");
Check(GameData.DiffQuality(new[]{0,0,0,1d,0},2,9)[4]==1&&GameData.DiffQuality(new[]{0,0,0,0,1d},2,9)[4]==1,"top quality stays capped at Vàng");
// trang bị ở ải giữ tỉ lệ gốc: phần đan tiến giai bị bỏ không chia sang trang bị (trước 10/2026: 14–41% mỗi trận)
foreach(var c in GameData.Chapters.Where(x=>x.Index<GameData.OpenChapters))foreach(var diff in new[]{0,1,2})foreach(var a in new[]{1,8,15}){
    var shown=1-PetLoot.StageOdds(c,diff,a).Where(o=>o.Gear).Aggregate(1d,(n,o)=>n*(1-o.Chance));
    var basis=1-GameData.DiffDrops(c,diff).Where(x=>GameData.GearBaseById.ContainsKey(x.Item)).Aggregate(1d,(n,x)=>n*(1-Math.Min(1,x.Chance*(1+(a-1)*.03))));
    Check(Math.Abs(shown-basis)<1e-9&&shown<.10,$"gear drop odds chapter {c.Index+1} diff {diff} area {a}");
}
Console.WriteLine($"PASS: {checks} checks for twelve herbs, high realm rare recipes, every recipe using a new herb, per-chapter hard map herbs, rare < normal chests, per-chapter hard rewards, base gear odds, brewing success/failure, selling and chest pools.");
