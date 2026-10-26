using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

var root = Path.GetFullPath(args.FirstOrDefault() ?? ".");
using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
await db.Database.OpenConnectionAsync();
await db.Database.EnsureCreatedAsync();
var game = new GameService(db);
var checks = 0;
void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
void Reject(Action action, string message) { try { action(); } catch (GameException) { checks++; return; } throw new Exception(message); }
Player Fixture(int diff = 3) => new() { Realm = 9, Stage = 2, Diff = diff, Chapter = 0, ChapterMax = 9,
    MapVer = 1, Cleared = "2.9", DiffProg = "3.0=30", ExploreReadyAt = DateTime.UtcNow.AddHours(1), LastTickAt = DateTime.UtcNow };
var locked = Fixture(2); locked.Cleared = "";
Reject(() => game.SelectDiff(locked, 3), "Hell must be locked before Nightmare final boss");
var p = Fixture(2); db.Players.Add(p);
game.SelectDiff(p, 3);
Check(p.Diff == 3 && p.Chapter == 0 && p.Area == 30, "Unlocked Hell must restore its own progress");
var map = game.PreviewMap(p, 0);
Check(map.Areas.Count == 31 && map.Areas[15].Waves!.All(w => w.Tier != "boss"), "Hell stage 16 must remain normal");
Check(map.Areas[30].Waves!.All(w => w.Tier == "boss"), "Hell stage 31 must be boss");
Check(map.Areas[29].Open && map.Areas[30].Open, "Stage 30 unlocks boss 31");
game.SelectArea(p, 16);
Reject(() => game.SelectArea(p, 31), "Boss requires key");
p.Add(map.KeyItem, 1); game.SelectArea(p, 31);
Check(p.Area == 31, "Boss can be selected with key");
for (var d = 0; d < 3; d++) {
    var old = Fixture(d); old.Admin = "map";
    var oldMap = game.PreviewMap(old, 0);
    Check(oldMap.Areas.Count == 16 && oldMap.Areas[15].Waves!.All(w => w.Tier == "boss"), "Old maps must retain boss 16");
}
foreach (var a in new[] { 16, 30, 31 }) {
    var saved = Fixture(); saved.Area = a;
    db.Players.Add(saved); await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    var loaded = await game.LoadAsync(saved.Id);
    Check(loaded.Diff == 3 && loaded.Area == a, $"Reload must preserve Hell stage {a}");
}
Check(GameService.LeaderboardProgress(p) == 30029, "Leaderboard must include completed Hell stages beyond 15");
var combatType = typeof(GameData).Assembly.GetType("VanDao.Game.HellMonsterCombat");
Check(combatType is not null, "Hell monsters need a real combat skill state");
MonsterDef Mob(string image, string tier="normal") => new("Test", image, 1000, 100, 0, Tier: tier);
var stone = new HellMonsterCombat(Mob("thach_nhan"), 0, 0);
stone.BeginTurn(0,0); Check(stone.Absorb(60)==0 && stone.Absorb(60)==20, "Armor absorbs only its remaining capacity");
stone.BeginTurn(1,1); stone.BeginTurn(2,2); Check(stone.Absorb(100)==100, "Armor expires after two turns");
var chain = new HellMonsterCombat(Mob("giap_binh"),0,0); chain.BeginTurn(0,0);
Check(chain.OwnerDamage(100)==80 && chain.OwnerDamage(100)==100,"Chains reduce one owner hit, never all actions");
var fire = new HellMonsterCombat(Mob("cot_khuyen"),0,0);
fire.CounterLanded(); fire.CounterLanded(); Check(fire.TickBurn(1)==0,"Burn needs three landed hits");
fire.CounterLanded(); Check(fire.TickBurn(2)==5 && fire.TickBurn(3)==5 && fire.TickBurn(4)==0,"Burn lasts two ticks without stacking");
var lava = new HellMonsterCombat(Mob("thach_tich"),0,0);
lava.BeginTurn(2,2);Check(lava.CounterMultiplier(2,2)==1,"Eruption must announce before damage");
lava.BeginTurn(3,3);Check(lava.CounterMultiplier(3,3)==1.5,"Eruption releases next turn");
var interrupted = new HellMonsterCombat(Mob("thach_tich"),0,0);interrupted.BeginTurn(2,2);interrupted.CounterMultiplier(2,2);
interrupted.BeginTurn(3,3);interrupted.BeginTurn(4,4);Check(interrupted.CounterMultiplier(4,4)==1,"A missed release cannot fire later");
var hellBattle = GameService.BattleAt(p,31,new DateTime(2026,10,26,0,0,0,DateTimeKind.Utc));
var eventsProperty = hellBattle.GetType().GetProperty("MonsterSkills");
Check(eventsProperty?.GetValue(hellBattle) is System.Collections.IEnumerable ev && ev.Cast<object>().Any(),"Real battle must include Hell skill events");
foreach(var d in new[]{0,1,2}) {
    var old = Fixture(d);old.Admin="map";
    Check(GameService.BattleAt(old,16).MonsterSkills is null,"Old difficulties have no Hell skills");
}
var seedTime=new DateTime(2026,10,26,1,0,0,DateTimeKind.Utc);
if(args.Contains("--balance"))Console.WriteLine("Nightmare anchor: "+string.Join(";",GameData.AreaWaves(GameData.Chapters[9],15,2).Select(w=>$"{w.Hp}/{w.Atk}/{w.Def}")));
var fixtures = new List<(string Name,Player P)>();
foreach(var realm in new[]{8,9})foreach(var quality in new[]{4,7,9}) {
    var equipped=Fixture();equipped.Id=Guid.Parse("10000000-0000-0000-0000-000000000001");equipped.Realm=realm;
    equipped.LearnedManuals=string.Join(',',SkillData.Elements.Select(e=>e.Id));equipped.ActiveManual="moc";
    equipped.SetSkillLevels(SkillData.Skills.Where(x=>x.Tree!="basic"&&x.MinRealm<=realm).ToDictionary(x=>x.Id,x=>x.MaxLv));
    CultivationManuals.SyncActive(equipped);
    foreach(var item in GameData.GearBases.Where(x=>x.MinRealm==realm&&x.Slot is not null)) {
        var id=GameData.GearId(item,quality);equipped.Add(id,1);game.Equip(equipped,id,item.Slot!);
    }
    game.SetSkillSlot(equipped,1,$"moc_{GameData.Realms[realm].Id}_active");
    fixtures.Add(($"{GameData.Realms[realm].Name}/q{quality}",equipped));
}
foreach(var (name,hero) in fixtures) {
    var results=new List<string>();
    foreach(var chapter in new[]{0,4,9})foreach(var area in new[]{1,10,20,30,31}) {
        hero.Chapter=chapter;var battle=GameService.BattleAt(hero,area,seedTime);
        Check(battle.HeroHpLeft.All(h=>h>=0&&h<=battle.HeroHp)&&battle.MonHpLeft.All(h=>h>=0),"Balanced combat health must remain in range");
        Check(battle.MonsterSkills!.All(e=>e.Turn>=0&&e.Turn<battle.Hits&&e.Wave<battle.Waves!.Count),"Skill event must belong to a real replay turn and wave");
        results.Add($"{chapter+1}-{area}:{(battle.Win?"W":"L")}/{GameService.LastTurns}");
    }
    if(args.Contains("--balance"))Console.WriteLine(name+" stats="+GameService.Stats(hero)+" "+string.Join(" ",results));
}
var tick=typeof(GameService).GetMethod("Tick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
var now=new DateTime(2026,10,26,0,0,0,DateTimeKind.Utc);
var bossWin=fixtures.Last().P;bossWin.Chapter=0;bossWin.Admin="god,map";bossWin.Area=31;bossWin.ExploreReadyAt=now;bossWin.LastTickAt=now;bossWin.Add("hoa_nguc_lenh",1);
tick.Invoke(game,[bossWin,now]);
Check(bossWin.BossKills==1&&bossWin.Count("ruong_boss_dia_nguc_1")==1,"One real Hell boss win awards one chest");
Check(bossWin.Cleared.Split(';').Count(x=>x=="3.0")==1,"Boss completion is recorded once");
bossWin.Admin="";game.SelectMap(bossWin,1);Check(bossWin.Chapter==1&&bossWin.Area==1,"Cleared boss opens next Hell chapter");
Reject(()=>game.SelectMap(bossWin,2),"Cannot skip Hell chapter");

var same=Fixture();same.Area=16;
var online=GameService.BattleAt(same,16,seedTime);var offline=GameService.BattleAt(same,16,seedTime);
Check(System.Text.Json.JsonSerializer.Serialize(online)==System.Text.Json.JsonSerializer.Serialize(offline),"Same trip must replay all combat and skill events identically");

for(var ch=0;ch<10;ch++) {
    var c=GameData.ChapterFor(3,ch);
    foreach(var look in c.Commons.Concat(c.Elites).Append(new(c.Boss.Name,c.Boss.Image)))
    foreach(var image in new[]{"","_24","_48","_hd"}.Select(s=>look.Image.Replace(".png",s+".png")))
        Check(File.Exists(Path.Combine(root,"wwwroot",image)),"Missing reused asset: "+image);
    for(var a=1;a<=31;a++) {
        var waves=GameData.AreaWaves(c,a,3);
        Check(waves.All(w=>w.Hp>0&&w.Hp<int.MaxValue&&w.Atk>0&&w.Def>=0),$"Hell stats must not saturate or overflow: chapter {ch+1} area {a}: {string.Join(";",waves.Select(w=>$"{w.Hp}/{w.Atk}/{w.Def}"))}");
        Check(waves.Length==(a==31?1:3)&&waves.Any(w=>w.Tier=="boss")== (a==31),"Only stage 31 is a boss");
        Check(GameData.AreaPlace(c,a,3).Contains(a==31?"Vương điện":a<=10?"Ngoại vực":a<=20?"Nham hà":"Nội ngục"),"Place must follow thirty-stage layout");
        foreach(var w in waves)Check(HellMonsterSkills.For(w,ch).Length<=(w.Tier=="boss"?3:w.Tier=="elite"?2:1),"Skill limit by monster tier");
    }
}
var hellChapters=Enumerable.Range(0,10).Select(i=>GameData.ChapterFor(3,i)).ToArray();
var usedArt=hellChapters.SelectMany(c=>c.Commons.Concat(c.Elites).Select(m=>m.Image).Append(c.Boss.Image)).ToHashSet();
var hellArt=Directory.GetFiles(Path.Combine(root,"wwwroot","img","hell"),"*.png").Select(Path.GetFileName)
    .Where(n=>!System.Text.RegularExpressions.Regex.IsMatch(n!,"_(24|48|hd)\\.png$")).ToArray();
Check(hellArt.Length==12,"Twelve new Tây Du Ký monsters are extracted");
(int W,int H) PngSize(string path){var b=File.ReadAllBytes(path);return((b[16]<<24)|(b[17]<<16)|(b[18]<<8)|b[19],(b[20]<<24)|(b[21]<<16)|(b[22]<<8)|b[23]);}
foreach(var art in hellArt) {
    Check(usedArt.Contains("img/hell/"+art),"New art must appear in a Hell roster: "+art);
    var dir=Path.Combine(root,"wwwroot","img","hell");var stem=Path.GetFileNameWithoutExtension(art);
    Check(PngSize(Path.Combine(dir,art))==(320,221)&&PngSize(Path.Combine(dir,stem+"_hd.png"))==(240,240)
        &&PngSize(Path.Combine(dir,stem+"_48.png"))==(48,48)&&PngSize(Path.Combine(dir,stem+"_24.png"))==(24,24),"Runtime variant sizes: "+art);
}
Check(hellChapters.Count(c=>c.Boss.Image.StartsWith("img/hell/"))==6,"Six Hell bosses use the new art");
Check(HellMonsterSkills.For(Mob("img/hell/quai_nguu_dau.png"),0).Single().Id=="dung_giap"
    &&HellMonsterSkills.For(Mob("img/hell/quai_hoa_diem_tieu_yeu.png"),0).Single().Id=="nham_boc"
    &&HellMonsterSkills.For(Mob("img/hell/quai_ma_dien.png"),0).Single().Id=="xieng_nguc"
    &&HellMonsterSkills.For(Mob("img/hell/tinh_bach_cot_tinh.png","elite"),0).Select(s=>s.Id).SequenceEqual(["thieu_hon","xieng_nguc"]),"New species carry their themed skills");
foreach(var chest in GameData.Chests.Keys)for(var r=0;r<10;r++) {
    var id=GameData.ChestId(chest,3);var table=ChestRewards.Table(r,id);
    Check(GameData.ItemById.ContainsKey(id)&&GameData.ChestDiff(id)==3,"Hell chests must retain difficulty identity");
    Check(Math.Abs(table.Sum(x=>x.Probability)-1)<1e-8&&table.All(x=>x.Probability>=0),"Hell chest odds must be normalized");
    Check(table.All(x=>GameData.ItemById[x.Item].Rarity!="supreme"),"Hell ordinary chests cannot introduce Huyền Tiên");
    Check(table.SequenceEqual(ChestRewards.Table(r,id,0)),"Changing active difficulty cannot change chest rewards");
}

Console.WriteLine($"PASS: {checks} Hell map checks");




if(args.Contains("--seed-qa")) {
    var qaPath=Path.Combine(root,".superpowers","sdd","2026-10-26-dia-nguc-1","qa-map.db");
    using var qaDb=new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source="+qaPath).Options);
    await qaDb.Database.EnsureCreatedAsync();
    var qaId=Guid.Parse("20000000-0000-0000-0000-000000000003");
    if(!qaDb.Players.Any(x=>x.Id==qaId)) {
        var qa=Fixture();qa.Id=qaId;qa.Name="QA Địa Ngục 1";qa.Area=30;qa.Admin="map,god";
        qa.Skills=fixtures.Last().P.Skills;qa.SkillSlots=fixtures.Last().P.SkillSlots;
        qa.LearnedManuals=fixtures.Last().P.LearnedManuals;qa.ActiveManual="moc";qa.ManualVersion=1;
        var qaGame=new GameService(qaDb);qaDb.Players.Add(qa);
        foreach(var item in GameData.GearBases.Where(x=>x.MinRealm==9&&x.Slot is not null)) {
            var id=GameData.GearId(item,9);qa.Add(id,1);qaGame.Equip(qa,id,item.Slot!);
        }
        qa.Add("hoa_nguc_lenh",10);await qaDb.SaveChangesAsync();
    }
    Console.WriteLine("QA fixture ready in isolated database.");
}
