using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

var count = 0;
void Check(bool ok, string why) { if (!ok) throw new Exception(why); count++; }
void Reject(Action action, string why) { try { action(); } catch (GameException) { count++; return; } throw new Exception(why); }
var elements = new[] { "tho", "loi", "bang", "gio" };
Check(CultivationManuals.Available.Length == 7, "all seven manuals must be playable");
Check(GameData.ItemById[GameData.BossChest].Use?.Contains("7 hệ") == true, "boss chest tooltip still advertises a three-element pool");
Check(SkillData.Skills.Select(s => s.Id).Distinct().Count() == SkillData.Skills.Count, "duplicate skill ID");
Check(SkillData.Skills.Select(s => s.Code).Where(c => c.Length > 0).Distinct().Count() == SkillData.Skills.Count, "missing/duplicate code");
foreach (var element in elements) {
    var skills = SkillData.Skills.Where(s => s.Element == element).ToArray();
    Check(skills.Length == 19, element + " must have 19 nodes");
    Check(skills.Count(s => s.Tree == "basic") == 1 && skills.Count(s => s.Tree == "active") == 7 && skills.Count(s => s.Tree == "passive") == 11, element + " tree composition");
    var basic = CultivationManuals.BasicOf(element)!;
    Check(basic.DmgBase == 115 && basic.Cost == 0 && basic.MaxLv == 0, "basic must be free and deal 115% ATK");
    foreach (var s in skills) {
        foreach (var r in s.Req) {
            var parts = r.Split(':');
            Check(SkillData.ById.TryGetValue(parts[0], out var parent) && parent.Element == element && int.Parse(parts[1]) <= parent.MaxLv && parent.MinRealm <= s.MinRealm, "invalid prerequisite " + s.Id);
        }
        var path = new HashSet<string>();
        void Visit(SkillDef current) { Check(path.Add(current.Id), "cycle at " + current.Id); foreach (var r in current.Req) Visit(SkillData.ById[r.Split(':')[0]]); path.Remove(current.Id); }
        Visit(s);
        if (s.Tree != "passive") Check(s.AnimId != "NONE" && s.DmgBase > 0, "cast missing damage/animation: " + s.Id);
        if (s.Tree == "active") Check(s.MaxLv == 5 && SkillData.Damage(s, 5) > SkillData.Damage(s, 1), "active does not scale: " + s.Id);
    }
}
foreach (var id in new[] { "thach_dan", "tho_tuong", "tho_giap", "son_nhac", "loi_kich", "loi_bo", "lien_loi", "thien_loi", "bang_tien", "bang_thuan", "dong_dia", "tuyet_doi", "phong_nhan", "phong_bo", "cuong_phong", "thien_phong" })
    Check(SkillData.ById[id].Tree == "active", "legacy equipped skill changed tree " + id);

using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
db.Database.OpenConnection(); db.Database.EnsureCreated();
var game = new GameService(db);
foreach (var element in elements) {
    var manual = CultivationManuals.All.Single(m => m.Element == element);
    var p = await game.CreateAsync("Four element " + element);
    p.Realm = 6; p.AdminPoints = 10000;
    // Boss survives four basic hits, so the real rage/cast path is exercised.
    p.Chapter = 6; p.Area = GameData.BossArea; p.Diff = 2; p.Admin = "god";
    var active = SkillData.Skills.Where(s => s.Element == element && s.Tree == "active").OrderBy(s => s.MinRealm).ToArray();
    var basic = CultivationManuals.BasicOf(element)!;
    Reject(() => game.UpgradeSkill(p, active[0].Id), "learned locked tree");
    Reject(() => game.ActivateManual(p, element), "activated unread manual");
    p.Add(manual.Item, 2); game.UseItem(p, manual.Item);
    Check(p.Count(manual.Item) == 1 && CultivationManuals.IsUnlocked(p, element), "manual did not consume exactly one");
    Reject(() => game.UseItem(p, manual.Item), "duplicate manual accepted");
    Check(p.Count(manual.Item) == 1, "duplicate manual consumed");
    game.ActivateManual(p, element);
    Check(p.Slots()[0] == basic.Id && p.ActiveManual == element, "manual basic not synced");
    Reject(() => game.UpgradeSkill(p, basic.Id), "free basic can spend points");
    Reject(() => game.UpgradeSkill(p, active[1].Id), "missing prerequisite accepted");
    p.Realm = 0;
    Reject(() => game.UpgradeSkill(p, active[6].Id), "realm condition bypassed");
    p.Realm = 6;
    foreach (var skill in active) {
        for (var level = 0; level < 5; level++) game.UpgradeSkill(p, skill.Id);
        game.SetSkillSlot(p, 1, skill.Id);
        Check(p.Slots()[1] == skill.Id, "active cannot equip");
        var battle = GameService.Battle(p);
        Check(battle.Basic == basic.Id && battle.Casts.Any(c => c.Skill == skill.Id && c.Dmg > 0), "active does not cast in battle: " + skill.Id);
    }
    foreach (var skill in SkillData.Skills.Where(s => s.Element == element && s.Tree == "passive").OrderBy(s => s.MinRealm).ThenBy(s => s.Row))
        for (var level = 0; level < 5; level++) game.UpgradeSkill(p, skill.Id);
    var before = await game.BuildStateAsync(p);
    Check(before.StatSources!.Single(s => s.Name == "Công pháp") is { } source && (source.AtkPct > 0 || source.DefPct > 0 || source.HpPct > 0), "passives missing from stats");
    await db.SaveChangesAsync(); var id = p.Id; db.ChangeTracker.Clear();
    p = await db.Players.Include(x => x.Inventory).Include(x => x.Pets).SingleAsync(x => x.Id == id);
    Check(CultivationManuals.IsUnlocked(p, element) && p.Slots()[0] == basic.Id && GameService.SkillLevels(p).GetValueOrDefault(active[6].Id) == 5, "save/reload lost tree");
    p.Add("tay_tuy_dan", 1); game.UseItem(p, "tay_tuy_dan");
    Check(CultivationManuals.IsUnlocked(p, element) && !GameService.SkillLevels(p).Any(kv => SkillData.ById[kv.Key].Element == element), "respec lost manual or kept levels");
    Check(p.Slots()[0] == basic.Id && p.Slots()[1] == "", "respec slots incorrect");
}
foreach (var realm in new[] { 0, 3, 6 }) {
    var rewards = ChestRewards.Table(realm, GameData.BossChest).Where(r => r.Group == "manual").ToArray();
    Check(rewards.Length == 7 && Math.Abs(rewards.Sum(r => r.Probability) - .01) < 1e-10, "manual reward budget changed");
    Check(rewards.All(r => Math.Abs(r.Probability - .01 / 7) < 1e-10), "unequal manual odds");
}
Console.WriteLine($"PASS {count} four-element checks: tree integrity, prerequisites, learning, casts, stats, respec, persistence and rewards.");
var repo = new DirectoryInfo(AppContext.BaseDirectory);
while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "VanDao.csproj"))) repo = repo.Parent;
if (repo is null) throw new Exception("repository root not found");
File.WriteAllText(Path.Combine(repo.FullName, "docs/four-elements-skills.json"), System.Text.Json.JsonSerializer.Serialize(
    SkillData.Skills.Where(s => elements.Contains(s.Element)), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
