using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

const string connection="Data Source=manual-checks;Mode=Memory;Cache=Shared";
using var keeper=new Microsoft.Data.Sqlite.SqliteConnection(connection);keeper.Open();
using var db=new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
db.Database.OpenConnection();db.Database.EnsureCreated();
var game=new GameService(db);var count=0;
void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
void Reject(Action action,string message){try{action();}catch(GameException){count++;return;}throw new Exception(message);}
// Tu vi chỉ tăng sau khi chọn khởi đầu (công pháp + trứng Phàm/Linh), như người chơi thật.
var starterEgg=PetData.StarterEggs.Keys.First(e=>PetData.EggQuality(e) is >= 0 and <= 1);
foreach (var ticket in new[] { GameData.SpinTicket, GameData.PetSpinTicket }) {
    Check(GameService.Tradable(GameData.ItemById[ticket], true), "admin cannot trade ticket: " + ticket);
    Check(!GameService.Tradable(GameData.ItemById[ticket]), "normal player can list ticket: " + ticket);
}
foreach (var admin in new[] { new Player { Admin = "god" }, new Player { AdminPoints = 1 }, new Player { Tainted = true } }) {
    Check(GameService.CanSellSpinTickets(admin, true), "development admin cannot list tickets");
    Check(!GameService.CanSellSpinTickets(admin, false), "admin can list tickets outside development");
}
Check(!GameService.CanSellSpinTickets(new Player(), true), "ordinary development player can list tickets");
Check(!GameService.CanSellSpinTickets(new Player(), false), "ordinary player can list tickets");
var qiPills = new[] { "bo_nguyen_dan_ha", "bo_nguyen_dan_trung", "bo_nguyen_dan_thuong" };
for (var tier = 0; tier < qiPills.Length; tier++) {
    var pillId = qiPills[tier];
    Check(GameData.ItemById.ContainsKey(pillId), "missing qi pill: " + pillId);
    var pill = GameData.ItemById[pillId];
    Check(pill.UseQiSeconds == new[] { 300, 1800, 5400 }[tier], "wrong qi pill potency");
    var recipe = GameData.Brews.Single(b => b.Out == pillId);
    var brewer = await game.CreateAsync("Qi pill " + tier);
    game.ChooseStarter(brewer, "hoa", starterEgg);
    brewer.Realm = 2;
    foreach (var (item, qty) in recipe.In) brewer.Add(item, qty * 2);
    var beforeBrew = recipe.In.ToDictionary(kv => kv.Key, kv => brewer.Count(kv.Key));
    game.Brew(brewer, pillId, 2);
    Check(brewer.Count(pillId) == 2 && recipe.In.All(kv => brewer.Count(kv.Key) == beforeBrew[kv.Key] - kv.Value * 2), "brew did not consume exact materials");
    brewer.Qi = 0;
    var stateBefore = await game.BuildStateAsync(brewer);
    game.UseItem(brewer, pillId);
    Check(Math.Abs(brewer.Qi - Math.Min(stateBefore.QiReq, stateBefore.Rate * pill.UseQiSeconds)) < .001, "incorrect qi gain");
    Check(brewer.Count(pillId) == 1, "qi pill not consumed");
    brewer.Qi = stateBefore.QiReq - 1;
    game.UseItem(brewer, pillId);
    Check(brewer.Qi == stateBefore.QiReq, "qi pill overflowed cultivation cap");
}
var p=await game.CreateAsync("Manual checks");
Check(p.Count(CultivationManuals.Starter)==1&&CultivationManuals.Unlocked(p).Length==0,"new player starts with unread fire manual");
Reject(()=>game.UpgradeSkill(p,"liet_diem"),"locked tree learned without book");
Reject(()=>game.SetSkillSlot(p,0,"thuy_cau"),"locked free basic equipped");
Reject(()=>game.UseItem(p,"bi_kip_thuy"),"missing book learned");
Reject(()=>game.UpgradeSkill(p,"thuong_hai"),"locked higher skill learned without book");
game.UseItem(p,CultivationManuals.Starter);
Check(CultivationManuals.IsUnlocked(p,"hoa")&&p.Count(CultivationManuals.Starter)==0,"fire manual unlocks and consumes one");
p.Add(CultivationManuals.Starter,2);
Reject(()=>game.UseItem(p,CultivationManuals.Starter),"duplicate manual accepted");
Check(p.Count(CultivationManuals.Starter)==2,"duplicate manual was consumed");
p.AdminPoints=1000;p.Realm=2;
foreach(var m in CultivationManuals.Available){
    if(m.Element!="hoa"){p.Add(m.Item,1);game.UseItem(p,m.Item);}
    var root=SkillData.Skills.First(s=>s.Element==m.Element&&s.Category=="Căn cơ");
    if(GameService.SkillLevels(p).GetValueOrDefault(root.Id)==0)game.UpgradeSkill(p,root.Id);
    Check(GameService.SkillLevels(p).GetValueOrDefault(root.Id)>0,"root unavailable after manual: "+m.Element);
}
game.UpgradeSkill(p,"liet_diem");p.Add("tay_tuy_dan",1);game.UseItem(p,"tay_tuy_dan");
Check(CultivationManuals.Unlocked(p).Length==CultivationManuals.Available.Length,"respec forgot manuals");
var id=p.Id;await db.SaveChangesAsync();db.ChangeTracker.Clear();
p=await db.Players.Include(x=>x.Inventory).SingleAsync(x=>x.Id==id);
Check(CultivationManuals.Unlocked(p).Length==CultivationManuals.Available.Length&&p.ManualVersion==1,"unlocks lost after save/reload");
var state=await game.BuildStateAsync(p);
Check(state.UnlockedElements?.Length==CultivationManuals.Available.Length,"unlocks absent from state");
foreach(var manual in CultivationManuals.All.Except(CultivationManuals.Available)){
    p.Add(manual.Item,1);
    Reject(()=>game.UseItem(p,manual.Item),"unavailable manual learned");
    Check(p.Count(manual.Item)==1,"unavailable manual consumed");
    CultivationManuals.Unlock(p,manual.Element);
    Check(!CultivationManuals.IsUnlocked(p,manual.Element),"disabled tree exposed by saved unlock");
    var root=SkillData.Skills.First(s=>s.Element==manual.Element&&s.Category=="Căn cơ");
    Reject(()=>game.UpgradeSkill(p,root.Id),"disabled root learned");
    p.SetSkillLevels(new Dictionary<string,int>{{root.Id,5}});
    Check(!GameService.SkillLevels(p).ContainsKey(root.Id),"disabled passive still contributes");
}
await db.SaveChangesAsync();
game.AdminAction(p,"resetall",null,1);
Check(p.ManualVersion==1&&p.LearnedManuals==""&&p.Count(CultivationManuals.Starter)==1,"reset did not restore unread starter manual");
await db.SaveChangesAsync();
var old=new Player{Skills="thuy_linh_can:2;moc_linh_can:1",SkillSlots="thuy_cau,thuy_nguyet"};
CultivationManuals.Migrate(old);CultivationManuals.Migrate(old);
Check(CultivationManuals.Unlocked(old).Order().SequenceEqual(new[]{"hoa","moc","thuy"}),"legacy migration not idempotent or loses learned trees");
var fresh=new Player{ManualVersion=1};CultivationManuals.Migrate(fresh);
Check(CultivationManuals.Unlocked(fresh).Length==0,"new player bypassed manual gate");
for(int realm=0;realm<GameData.Realms.Count;realm++)for(int diff=0;diff<3;diff++)for(int chapter=0;chapter<10;chapter++)foreach(var chest in GameData.Chests.Keys){
    var table=ChestRewards.Table(realm,chest,diff,chapter);
    Check(Math.Abs(table.Sum(x=>x.Probability)-1)<1e-8,"chest total not one");
    Check(table.All(x=>x.Group!="pill"&&!GameData.Realms.Any(r=>r.StageCost(0).ContainsKey(x.Item))),"chest drops a realm pill");
    var manuals=table.Where(x=>x.Group=="manual").ToArray();
    Check(Math.Abs(manuals.Sum(x=>x.Probability)-(chest==GameData.BossChest?.01:0))<1e-10,"wrong manual chance");
    Check(manuals.All(x=>x.Qty==1&&GameData.ItemById[x.Item].Type=="manual"),"invalid manual reward");
    if(chest==GameData.BossChest){
        Check(manuals.Length==CultivationManuals.Available.Length&&manuals.All(x=>Math.Abs(x.Probability-.01/CultivationManuals.Available.Length)<1e-10),"manuals not equally likely");
        var cumulative=0.0;
        foreach(var reward in table){
            if(reward.Group=="manual")Check(ChestRewards.Roll(realm,chest,new FixedRandom(cumulative+reward.Probability/2),diff,chapter).Item==reward.Item,"manual cannot be rolled");
            cumulative+=reward.Probability;
        }
    }
}
// rương theo mức độ khó: id riêng, mở ra theo mức của rương dù truyền mức khác
foreach(var chest in GameData.Chests.Keys)for(int diff=0;diff<GameData.DiffNames.Length;diff++){
    var chestId=GameData.ChestId(chest,diff);
    Check(GameData.ItemById.TryGetValue(chestId,out var def)&&def.Type=="chest","missing difficulty chest: "+chestId);
    Check(GameData.ChestDiff(chestId)==diff&&GameData.ChestBase(chestId)==chest,"chest id round trip: "+chestId);
    Check(ReferenceEquals(ChestRewards.Table(1,chestId,0,3),ChestRewards.Table(1,chest,diff,3)),"difficulty chest ignores its own difficulty: "+chestId);
    Check(PetLoot.StageGroups(GameData.Chapters[0],diff,GameData.Areas).SelectMany(g=>g.Choices).Where(c=>GameData.ItemById.TryGetValue(c.Item,out var it)&&it.Type=="chest")
        .All(c=>GameData.ChestDiff(c.Item)==diff),"stage drops wrong difficulty chest");
}
// trang phục Thiên Hợp Tiên Cơ: khóa tới khi mở rương trang phục; Hợp Thể dùng chung ảnh Luyện Hư
Check(GameData.Realms[6].HeroStand==GameData.Realms[5].HeroStand,"Hợp Thể không dùng ảnh Luyện Hư");
Check(GameData.SkinById["hop_the"].MinRealm==6&&GameData.SkinById["hop_the"].UnlockItem is null,"Thiên Hợp Tiên Y phải mặc được từ Hợp Thể");
var skinBox=GameData.SkinById["hop_the_nu"].UnlockItem!;
Check(GameData.ItemById[skinBox].Type=="skin"&&GameData.ItemById[skinBox].Sell==GameData.RarityPrice["legendary"]&&GameService.Tradable(GameData.ItemById[skinBox]),"rương trang phục phải bán NPC và giao dịch ở Chợ được");
var dresser=await game.CreateAsync("Skin lock");dresser.Realm=6;
Reject(()=>game.SetSkin(dresser,"hop_the_nu"),"mặc được skin nữ khi chưa mở rương");
dresser.Skin="hop_the_nu";
Check((await game.BuildStateAsync(dresser)).Skin=="","skin nữ còn hiệu lực khi chưa mở rương");
dresser.Add(skinBox,2);await db.SaveChangesAsync();game.UseItem(dresser,skinBox);await db.SaveChangesAsync();
Check(dresser.Count(skinBox)==1&&dresser.SkinsUnlocked=="hop_the_nu","mở rương trang phục không mở khóa / không trừ rương");
game.SetSkin(dresser,"hop_the_nu");
Check((await game.BuildStateAsync(dresser)).Skin=="hop_the_nu","không mặc được skin nữ sau khi mở rương");
Reject(()=>game.UseItem(dresser,skinBox),"mở rương trang phục lần hai");
Check(dresser.Count(skinBox)==1,"mở lần hai làm mất rương");
// trang phục mở bằng rương: mặc ngay, không cần đạt cảnh giới
var lowDresser=await game.CreateAsync("Skin low realm");
lowDresser.Add(skinBox,1);game.UseItem(lowDresser,skinBox);game.SetSkin(lowDresser,"hop_the_nu");
Check(lowDresser.Realm==0&&(await game.BuildStateAsync(lowDresser)).Skin=="hop_the_nu","skin mở bằng rương phải mặc được ở Luyện Khí");
Reject(()=>game.SetSkin(lowDresser,"hop_the"),"skin theo cảnh giới vẫn phải đạt cảnh giới");
// rương Khó / Ác mộng: phẩm trang bị trung bình cao dần theo mức, tổng tỉ lệ vẫn 1
for(int realm=0;realm<GameData.Realms.Count;realm++)foreach(var chest in GameData.Chests.Keys){
    double AvgGrade(int diff){var t=ChestRewards.Table(realm,GameData.ChestId(chest,diff),0,realm).Where(x=>x.Group=="gear").ToArray();
        return t.Sum(x=>x.Probability*Array.IndexOf(GameData.RarityOrder,GameData.ItemById[x.Item].Rarity))/t.Sum(x=>x.Probability);}
    var g=new[]{AvgGrade(0),AvgGrade(1),AvgGrade(2)};
    var top=GameData.ChestTopQuality(realm);
    // vượt phẩm trần của cảnh giới chỉ được là phần Thánh → Đạo
    for(int diff=1;diff<3;diff++)Check(GameData.ChestQuality(chest,realm,diff).Select((w,i)=>(w,i)).All(x=>x.w<=0||x.i<=top||x.i>=GameData.DiffChestHighFrom&&x.i<GameData.DiffChestHighFrom+GameData.DiffChestHighSplit.Length),
        $"rương {chest} mức {diff} cảnh giới {realm} ra phẩm ngoài trần cảnh giới / Thánh → Đạo");
    Check(g[1]>g[0]&&g[2]>g[1],$"rương {chest} cảnh giới {realm}: phẩm không tăng theo mức ({string.Join(" / ",g.Select(x=>x.ToString("0.000")))})");
}
// phần Thánh → Đạo: Boss > Hiếm > Thường, Ác mộng > Khó, Thường không có
for(int realm=0;realm<GameData.Realms.Count;realm++){
    double High(string chest,int diff){var q=GameData.ChestQuality(chest,realm,diff);return q.Skip(GameData.DiffChestHighFrom).Take(4).Sum()/q.Sum();}
    foreach(var chest in GameData.Chests.Keys)Check(High(chest,2)>High(chest,1)&&High(chest,1)>High(chest,0),$"rương {chest} cảnh giới {realm}: tỉ lệ Thánh → Đạo không tăng theo mức");
    for(int diff=1;diff<3;diff++)Check(High("ruong_boss",diff)>High("ruong_hiem",diff)&&High("ruong_hiem",diff)>High("ruong_thuong",diff),
        $"cảnh giới {realm} mức {diff}: tỉ lệ Thánh → Đạo không cao nhất ở Rương Boss");
}
// mô tả kỹ năng linh thú ghi số thật: khớp đúng khiên / sát thương PetCombat tạo ra (thủ địch 0)
{
    var vn=System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
    var owner=new CombatStats(5000,40000,800);var petSt=new CombatStats(1200,900,300,0,0);
    foreach(var skill in new[]{"turtle","butterfly","fox","cat"}){
        var def=PetData.Definitions.First(x=>x.Skill==skill);
        for(int form=0;form<3;form++){
            var nums=PetData.NumbersOf(petSt,owner,form);
            var text=PetData.SkillDescription(skill,form,0,-1,nums);
            Check(!text.Contains("Linh Uy")&&!text.Contains("Linh Hộ"),$"mô tả {skill} còn chữ Linh Uy / Linh Hộ khi có số thật");
            var pc=new PetCombat(def,form,owner,0,new Random(1),false,petSt);
            long hp=1;var cast=pc.Cast(1,ref hp,1_000_000_000,1_000_000_000,0,1);
            var shown=cast.Guard>0?cast.Guard:cast.Dmg;
            Check(text.Contains(shown.ToString("N0",vn)),$"mô tả {skill} hình thái {form} không khớp trận đánh ({shown}): {text}");
        }
    }
}
db.Database.ExecuteSqlRaw("ALTER TABLE Players DROP COLUMN LearnedManuals");
db.Database.ExecuteSqlRaw("ALTER TABLE Players DROP COLUMN ManualVersion");
SchemaUpgrade.Apply(db);SchemaUpgrade.Apply(db);
db.Database.OpenConnection();
using(var cmd=db.Database.GetDbConnection().CreateCommand()){
    cmd.CommandText="SELECT ManualVersion FROM Players LIMIT 1";
    Check(Convert.ToInt32(cmd.ExecuteScalar())==0,"legacy schema default version incorrect");
    cmd.CommandText="SELECT LearnedManuals FROM Players LIMIT 1";
    Check(Convert.ToString(cmd.ExecuteScalar())=="","legacy schema default unlocks incorrect");
}
// ---- Công pháp vận hành: một hệ, đòn thường theo hệ đó, bị động hệ khác còn 50% ----
{
    var ap=await game.CreateAsync("Active manual");
    CultivationManuals.Unlock(ap,"hoa");CultivationManuals.Unlock(ap,"thuy");
    var lvA=ap.SkillLevels();lvA["hoa_linh_can"]=5;lvA["thuy_linh_can"]=3;lvA["nhu_thuy"]=5;ap.SetSkillLevels(lvA);
    ap.ActiveManual="";CultivationManuals.SyncActive(ap);
    Check(ap.ActiveManual=="thuy"&&ap.Slots()[SkillData.BasicSlot]=="thuy_cau","legacy default should pick highest-level manual and its basic");
    double CpAtk(Player x)=>game.BuildStateAsync(x).Result.StatSources!.First(s=>s.Name=="Công pháp").AtkPct;
    var atkThuy=CpAtk(ap);
    game.ActivateManual(ap,"hoa");
    Check(ap.ActiveManual=="hoa"&&ap.Slots()[SkillData.BasicSlot]=="hoa_cau","activate should switch basic attack");
    var atkHoa=CpAtk(ap);
    Check(atkHoa>0&&Math.Abs(atkThuy-atkHoa*CultivationManuals.InactiveRate)<.0001,$"inactive passives should be halved ({atkThuy} vs {atkHoa})");
    game.SetSkillSlot(ap,SkillData.BasicSlot,"thuy_cau");
    Check(ap.ActiveManual=="thuy","equipping another basic should activate its manual");
    Reject(()=>game.SetSkillSlot(ap,SkillData.BasicSlot,null),"basic slot should not be clearable");
    Reject(()=>game.ActivateManual(ap,"moc"),"cannot activate a manual not learned");
    ap.ActiveManual="moc";CultivationManuals.SyncActive(ap);
    Check(ap.ActiveManual=="thuy","invalid saved manual should fall back to highest learned");
}
// ---- Kỹ năng chủ động hệ Hỏa: 10% × cấp cơ hội thiêu đốt khi tung ----
{
    Check(SkillData.ActiveBurnChance(SkillData.ById["liet_diem"],5)==50&&SkillData.ActiveBurnChance(SkillData.ById["liet_diem"],1)==10,"fire active burn chance should be 10% per level");
    Check(SkillData.ActiveBurnChance(SkillData.ById["hoa_cau"],5)==0&&SkillData.ActiveBurnChance(SkillData.ById["thuy_cau"],5)==0,"basic attacks should not get active burn chance");
    var bp=await game.CreateAsync("Fire burn");
    CultivationManuals.Unlock(bp,"hoa");
    var lvB=bp.SkillLevels();lvB["liet_diem"]=5;bp.SetSkillLevels(lvB);
    bp.SkillSlots="hoa_cau,liet_diem";
    var b=GameService.Battle(bp);
    Check(b.BurnPct>0&&b.Burn>0,"equipped fire active skill should enable burn");
    var ib=new CombatBurn();
    Check(ib.Ignite(1000,false)==50&&ib.TurnsLeft==1,"ignite should burn this turn and one more");
    var ib2=new CombatBurn();ib2.Tick(1000,true);
    Check(ib2.Ignite(1000,true)==0&&ib2.TurnsLeft==1,"ignite after a tick should only refresh");
}
// ---- Nhóm debuff (thiêu đốt · tê liệt · choáng · đóng băng): quái chỉ mang 1, cái mới nhất đè cái cũ ----
{
    var owner=new CombatStats(1000,10000,100);
    var phoenixDef=PetData.ById.Values.First(d=>d.Skill=="phoenix");
    var frostDef=PetData.ById.Values.First(d=>d.Skill=="frost");
    var fire=new PetCombat(phoenixDef,0,owner,random:new FixedRandom(0));
    long hpA=10000;fire.Cast(0,ref hpA,100000,100000,0,1);
    Check(fire.Burning&&fire.NewDebuff=="burn","phoenix should apply burn debuff");
    fire.ClearDebuffs();
    Check(!fire.Burning,"owner debuff should clear pet burn");
    var ice=new PetCombat(frostDef,0,owner,random:new FixedRandom(0));
    ice.Cast(3,ref hpA,100000,100000,0,1);
    Check(ice.NewDebuff=="freeze"&&ice.Paralyzed&&ice.Shatter(4),"frost should freeze");
    ice.ClearDebuffs();
    Check(!ice.Paralyzed&&!ice.Shatter(4),"owner burn should clear freeze and shatter");
}
Console.WriteLine($"PASS {count} manual authorization, consumption, respec, reset, persistence, legacy/schema migration and chest distribution checks.");
class FixedRandom(double value):Random{public override double NextDouble()=>value;}
