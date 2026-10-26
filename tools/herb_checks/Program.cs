using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

var root = Path.GetFullPath(args.FirstOrDefault() ?? ".");
var ids = HerbExpansion.Items.Select(x => x.Id).ToHashSet();
var checks=0;
void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
Check(ids.Count==10,"expected ten unique new herbs");
foreach(var (rarity,count) in new[]{("common",3),("uncommon",3),("rare",4)})
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
    var recipe=GameData.Brews.Single(x=>x.In.ContainsKey(herb.Id));
    Check(recipe.In[herb.Id]==1,"new herb dose");
    var brewer=new Player{Id=Guid.NewGuid(),Realm=7};
    foreach(var (key,qty) in recipe.In)brewer.Add(key,qty*2);
    game.Brew(brewer,recipe.Out,1);
    Check(brewer.Count(recipe.Out)==1&&recipe.In.All(x=>brewer.Count(x.Key)==x.Value),"brew debit");
    var missing=new Player{Id=Guid.NewGuid(),Realm=7};
    foreach(var (key,qty) in recipe.In)if(key!=herb.Id)missing.Add(key,qty);
    var before=recipe.In.ToDictionary(x=>x.Key,x=>missing.Count(x.Key));
    try{game.Brew(missing,recipe.Out,1);throw new Exception("allowed brew without new herb");}
    catch(GameException){Check(recipe.In.All(x=>missing.Count(x.Key)==before[x.Key]),"failed brew consumed inventory");}
    var seller=new Player{Id=Guid.NewGuid()};seller.Add(herb.Id,2);
    game.Sell(seller,herb.Id,1);
    Check(seller.Count(herb.Id)==1&&seller.Count("linh_thach")==GameData.ItemById[herb.Id].Sell,"herb sale");
}
foreach(var r in Enumerable.Range(0,GameData.Realms.Count))foreach(var chest in GameData.Chests.Keys)foreach(var diff in Enumerable.Range(0,3)){
    var table=ChestRewards.Table(r,chest,diff);
    Check(Math.Abs(table.Sum(x=>x.Probability)-1)<1e-8,"chest normalization");
    Check(table.All(x=>!ids.Contains(x.Item)),"new herb appeared in chest");
}
foreach(var (id,cost,total) in new[]{("bo_nguyen_dan_ha",5000,5),("bo_nguyen_dan_trung",20000,9),("bo_nguyen_dan_thuong",80000,23)}){
    var b=GameData.Brews.Single(x=>x.Out==id);
    Check(b.In["linh_thach"]==cost&&b.In.Where(x=>x.Key!="linh_thach").Sum(x=>x.Value)==total&&b.In.Keys.All(k=>!ids.Contains(k)),"changed replenishing pill");
}
Console.WriteLine($"PASS: {checks} checks for ten herbs, rarity, icons, map unlocks, brewing success/failure, selling, chest pools and existing pill balance.");
