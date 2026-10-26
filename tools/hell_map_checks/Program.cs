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
{   // Hero fixtures and chapter gates always run; the flags below only add reports.
    // Late Nightmare (chapters 5–10) into Hell (chapters 1–10).
    Player Hero(int realm,int stage,int quality,int copy=0) {
        // Battle randomness follows the player id: pin it per fixture so runs are reproducible.
        var h=Fixture();h.Id=Guid.Parse($"3000000{copy}-0000-0000-0000-{realm:D4}{stage:D4}{quality:D4}");h.Realm=realm;h.Stage=stage;
        h.LearnedManuals=string.Join(',',SkillData.Elements.Select(e=>e.Id));h.ActiveManual="moc";
        h.SetSkillLevels(SkillData.Skills.Where(x=>x.Tree!="basic"&&x.MinRealm<=realm).ToDictionary(x=>x.Id,x=>x.MaxLv));
        CultivationManuals.SyncActive(h);
        foreach(var item in GameData.GearBases.Where(x=>x.MinRealm==realm&&x.Slot is not null)) {
            var id=GameData.GearId(item,quality);h.Add(id,1);game.Equip(h,id,item.Slot!);
        }
        var active=$"moc_{GameData.Realms[realm].Id}_active";
        if(SkillData.Skills.Any(x=>x.Id==active))game.SetSkillSlot(h,1,active);
        return h;
    }
    // Late-game fights are short: the turn-3 skill crit decides most of them, so a hero wins when it lives
    // that long. Above the two-hit threshold wins fall to dodge luck (~60%) for every hero, not to zero; "clears" therefore
    // means reliably (21 of 24). Randomness follows player id and time: three ids × eight times per hero.
    var seeds=Enumerable.Range(0,8).Select(i=>seedTime.AddHours(i*31+i*i)).ToArray();
    Player[] Squad(int realm,int stage,int quality)=>Enumerable.Range(0,3).Select(i=>Hero(realm,stage,quality,i)).ToArray();
    bool Wins(Player[] squad,int d,int c,int a)=>squad.Sum(h=>{h.Diff=d;h.Chapter=c;return seeds.Count(t=>GameService.BattleAt(h,a,t).Win);})>=21;
    var hellStages=new[]{1,5,10,15,20,25,30,31};
    // Frontier: the first stage (Nightmare ch5 → Hell ch10, bosses included) a hero cannot reliably clear.
    string Frontier(Player[] h) {
        var route=Enumerable.Range(4,6).SelectMany(c=>Enumerable.Range(1,16).Select(a=>(D:2,C:c,A:a)))
            .Concat(Enumerable.Range(0,10).SelectMany(c=>hellStages.Select(a=>(D:3,C:c,A:a))));
        foreach(var (d,c,a) in route)if(!Wins(h,d,c,a))return $"{(d==2?"ÁM":"ĐN")} {c+1}-{a}";
        return "clears ĐN 10";
    }
    if(args.Contains("--tune")) {
        // Largest HellMapData.StatTune[c] each row still clears (every sampled stage, boss included) for Hell chapters 4–10.
        // One worker per chapter: it alone writes StatTune[c] and owns its squads.
        var rows=new[]{(8,2,9)}.Concat(new[]{0,1,2}.SelectMany(k=>new[]{5,6,7,8,9}.Select(q=>(9,k,q)))).ToArray();
        var chapters=Enumerable.Range(3,7).ToArray();
        var squads=chapters.ToDictionary(c=>c,c=>rows.Select(r=>Squad(r.Item1,r.Item2,r.Item3)).ToArray());
        var limit=new double[chapters.Length,rows.Length];
        var saved=HellMapData.StatTune.ToArray();
        Parallel.For(0,chapters.Length,ci=>{
            var c=chapters[ci];
            bool Clears(Player[] s,double k){HellMapData.StatTune[c]=k;return hellStages.All(a=>Wins(s,3,c,a));}
            for(var ri=0;ri<rows.Length;ri++){
                double lo=Math.Log2(.25),hi=Math.Log2(256);
                if(!Clears(squads[c][ri],Math.Pow(2,lo))){limit[ci,ri]=0;continue;}
                for(var i=0;i<11;i++){var mid=(lo+hi)/2;if(Clears(squads[c][ri],Math.Pow(2,mid)))lo=mid;else hi=mid;}
                limit[ci,ri]=Math.Pow(2,lo);
            }
            HellMapData.StatTune[c]=saved[c];
        });
        Console.WriteLine("Row           "+string.Join("",chapters.Select(c=>$"  ĐN {c+1,2}")));
        for(var ri=0;ri<rows.Length;ri++)
            Console.WriteLine($"{GameData.Realms[rows[ri].Item1].Name,-9} k{rows[ri].Item2} q{rows[ri].Item3}"+string.Concat(chapters.Select((c,ci)=>$" {limit[ci,ri],6:0.00}")));
        return;
    }
    if(args.Contains("--curve")) {
        // Monster stat curve: strongest wave per stage, Nightmare ch5–10 then Hell ch1–10.
        foreach(var (d,c,a) in Enumerable.Range(4,6).SelectMany(c=>new[]{1,8,15,16}.Select(a=>(2,c,a)))
            .Concat(Enumerable.Range(0,10).SelectMany(c=>new[]{1,10,20,30,31}.Select(a=>(3,c,a))))) {
            var w=GameData.AreaWaves(GameData.ChapterFor(d,c),a,d).MaxBy(x=>(double)x.Hp*x.Atk)!;
            Console.WriteLine($"{(d==2?"ÁM":"ĐN")} {c+1,2}-{a,-2} {w.Tier,-6} hp {w.Hp/1e6,6:0}M atk {w.Atk/1e6,6:0}M def {w.Def/1e6,6:0}M");
        }
        return;
    }
    if(args.Contains("--debug")) {
        // Battles in full: turns, casts, dodges and remaining health per turn.
        // --debug realm,stage,q,diff,chapter,area (chapter 1-based) picks one fight; default: three late-Nightmare samples.
        var pick=args.SkipWhile(x=>x!="--debug").Skip(1).FirstOrDefault()?.Split(',').Select(int.Parse).ToArray();
        var cases=pick is {Length:6}?new[]{(pick[0],pick[1],pick[2],pick[3],pick[4]-1,pick[5])}:new[]{(8,0,7,2,7,16),(8,2,7,2,7,16),(8,0,5,2,5,8)};
        foreach(var (realm,stage,q,d,c,a) in cases) {
            var h=Hero(realm,stage,q);h.Diff=d;h.Chapter=c;var s=GameService.Stats(h);
            Console.WriteLine($"== {GameData.Realms[realm].Name} k{stage} q{q} vs {(d==2?"ÁM":"ĐN")} {c+1}-{a}: {s}");
            foreach(var t in seeds) {
                var b=GameService.BattleAt(h,a,t);
                Console.WriteLine($"  win {b.Win,-5} turns {b.Hits,2} heroHp {b.HeroHp/1e6:0}M monHp {b.MonsterHp/1e6:0}M monAtk {b.MonsterAtk/1e6,5:0}M heroDmg {b.HeroDmg/1e6,5:0}M monDmg {b.MonsterDmg/1e6,5:0}M"
                    +$" casts {string.Join(",",b.Casts?.Select(x=>$"t{x.Turn}:{x.Dmg/1e6:0}M{(x.Critical?"!":"")}")??[])} rage {string.Join("/",b.Rage)} dodged {string.Join("",b.Dodged!.Select(x=>x?"D":"."))}"
                    +$" heroLeft {string.Join("/",b.HeroHpLeft.Select(x=>(x/1e6).ToString("0")))} monLeft {string.Join("/",b.MonHpLeft.Select(x=>(x/1e6).ToString("0")))}");
            }
        }
        return;
    }
    if(args.Contains("--frontier")) {
        var rows=new[]{(7,2),(8,0),(8,2)}.SelectMany(r=>new[]{5,7,8,9}.Select(q=>(r.Item1,r.Item2,q)))
            .Concat(new[]{0,1,2}.SelectMany(k=>new[]{5,6,7,8,9}.Select(q=>(9,k,q)))).ToArray();
        var squads=rows.Select(r=>Squad(r.Item1,r.Item2,r.q)).ToArray();   // built in order: fixtures share the db context
        var stops=new string[rows.Length];
        Parallel.For(0,rows.Length,i=>stops[i]=Frontier(squads[i]));
        for(var i=0;i<rows.Length;i++){var (realm,stage,q)=rows[i];var s=GameService.Stats(squads[i][0]);
            Console.WriteLine($"{GameData.Realms[realm].Name,-9} k{stage} q{q}: stops at {stops[i],-12} atk {s.Atk/1e6,6:0}M hp {s.Hp/1e6,6:0}M def {s.Def/1e6,5:0}M");}
        return;
    }
    // Chapter gates from 64-bit combat (--frontier): ch.4 needs Chân Tiên, ch.10 needs Chân Tiên q9 past Sơ kỳ.
    Check(!Wins(Squad(8,2,9),3,3,31)&&Wins(Squad(9,0,5),3,3,31),"Hell chapter 4 boss needs Chân Tiên Sơ kỳ q5, not Độ Kiếp q9");
    Check(!Wins(Squad(9,0,9),3,9,31)&&Wins(Squad(9,2,9),3,9,31),"Hell chapter 10 boss needs Chân Tiên Hậu kỳ q9, not Sơ kỳ q9");
    Check(!Wins(Squad(9,2,5),3,6,31),"Chân Tiên q5 must not clear Hell chapter 7");
    string Run(Player h,int diff,int chapter,int area){h.Diff=diff;h.Chapter=chapter;return seeds.Count(t=>GameService.BattleAt(h,area,t).Win).ToString();}
    if(args.Contains("--transition"))Console.WriteLine("Hero            | Ác Mộng ch5..10 (ải 1/8/15/Boss)                    | Địa Ngục ch1..4 (ải 1/10/20/30/Boss)");
    if(args.Contains("--transition"))foreach(var (realm,stage) in new[]{(7,2),(8,0),(8,2),(9,0),(9,2)})foreach(var q in new[]{5,7,9}) {
        var h=Hero(realm,stage,q);
        var nm=string.Join(" ",Enumerable.Range(4,6).Select(c=>string.Concat(new[]{1,8,15,16}.Select(a=>Run(h,2,c,a)))));
        var hell=string.Join(" ",Enumerable.Range(0,4).Select(c=>string.Concat(new[]{1,10,20,30,31}.Select(a=>Run(h,3,c,a)))));
        Console.WriteLine($"{GameData.Realms[realm].Name,-8} k{stage} q{q} | {nm} | {hell}");
    }
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
        Check(waves.All(w=>w.Hp>0&&w.Hp<CombatNumber.Max&&w.Atk>0&&w.Def>=0),$"Hell stats must not saturate or overflow: chapter {ch+1} area {a}: {string.Join(";",waves.Select(w=>$"{w.Hp}/{w.Atk}/{w.Def}"))}");
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
// Difficulty curve from late Nightmare into Hell: bosses never get easier, the Hell boss is its chapter's hardest wave,
// and Hell opens below the Nightmare final boss instead of above it.
long TopAtk(int d,int c,int a)=>GameData.AreaWaves(GameData.ChapterFor(d,c),a,d).Max(w=>w.Atk);
var bossCurve=Enumerable.Range(5,5).Select(c=>TopAtk(2,c,16)).Concat(Enumerable.Range(0,10).Select(c=>TopAtk(3,c,31))).ToArray();
Check(bossCurve.Zip(bossCurve.Skip(1)).All(x=>x.Second>=x.First),"Boss attack must not drop from Nightmare 6 to Hell 10: "+string.Join(",",bossCurve));
for(var c=0;c<10;c++)Check(TopAtk(3,c,31)>TopAtk(3,c,30),"Hell boss must out-hit its stage-30 elite");
Check(TopAtk(3,0,1)<TopAtk(2,9,16),"Hell stage 1 opens below the Nightmare final boss");
// Chapter 6 jumps on purpose (Đại Thừa → Độ Kiếp); from chapter 7 on growth is even.
for(var c=6;c<10;c++)Check(TopAtk(2,c,16)<=TopAtk(2,c-1,16)*1.5,"Late Nightmare bosses grow evenly within Độ Kiếp");
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
