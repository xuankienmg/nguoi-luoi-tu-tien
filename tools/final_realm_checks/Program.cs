using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

var root = Path.GetFullPath(args.FirstOrDefault() ?? ".");
using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
var game = new GameService(db);
var checks = 0;
var failures = new List<string>();
void Check(bool ok, string message) { checks++; if (!ok) failures.Add(message); }
void Reject(Action action, string message) {
    try { action(); Check(false, message); } catch (GameException) { Check(true, message); }
}
bool Image(string? path) => path is not null && File.Exists(Path.Combine(root, "wwwroot", path));
Player Peak(int realm) => new() { Realm = realm, Stage = GameData.Realms[realm].Stages.Length - 1,
    Qi = GameData.Realms[realm].QiReq(GameData.Realms[realm].Stages.Length - 1) };
void Fund(Player p, Dictionary<string, int> cost) { foreach (var (id, n) in cost) p.Add(id, n); }

Check(GameData.Realms.Count == 10, "Expected ten playable realms");
var hiddenSkins = new[] { "do_kiep", "do_kiep_nu", "chan_tien", "chan_tien_nu" };
Check(hiddenSkins.All(id => !GameData.SkinById.ContainsKey(id)), "Final realm costumes must be hidden and unavailable");
foreach (var realm in new[] { 8, 9 }) {
    var final = GameData.Realms[realm];
    var previous = GameData.Realms[7];
    Check(final.Hero == previous.Hero && final.HeroStand == previous.HeroStand && final.HeroMed == previous.HeroMed && final.HeroCast == previous.HeroCast,
        "Final realms must keep the previous default appearance without granting a new costume");
    foreach (var id in hiddenSkins) {
        var player = new Player { Realm = realm, Skin = id };
        Reject(() => game.SetSkin(player, id), "Hidden skins must reject direct equip requests");
        Check(GameService.PvpFighterOf(player).Hero == previous.Hero, "Old hidden skin selections must fall back to the available appearance");
    }
}
for (var realm = 7; realm <= 8; realm++) {
    var r = GameData.Realms[realm];
    var cost = r.BreakCost!;
    var missing = Peak(realm);
    Fund(missing, cost.Where(x => x.Key != r.BossItem).ToDictionary());
    var before = missing.Inventory.ToDictionary(x => x.ItemId, x => x.Count);
    Reject(() => game.Advance(missing), $"realm {realm}: cannot advance without boss core");
    Check(missing.Realm == realm && before.All(x => missing.Count(x.Key) == x.Value), "Rejected breakthrough must not consume items");

    var failed = Peak(realm);
    Fund(failed, cost);
    failed.Bonus = -2; // Clamp chance to zero: deterministic failure through the real service.
    failed.PillBonus = .2;
    game.Advance(failed);
    Check(failed.Realm == realm && failed.Count(r.BossItem) == 0, "Failed breakthrough must consume boss core");
    Check(failed.PillBonus == .2 && failed.Qi >= 0 && failed.Qi < r.QiReq(failed.Stage), "Failure must preserve pill bonus and reduce qi");
    Check(cost.All(x => failed.Count(x.Key) == 0), "Failure must debit all breakthrough costs");

    var success = Peak(realm);
    Fund(success, cost);
    success.Admin = "break";
    success.PillBonus = .3;
    success.Skin = "kim_dan";
    game.Advance(success);
    Check(success.Realm == realm + 1 && success.Stage == 0 && success.Qi == 0, "Successful breakthrough enters next realm at initial stage");
    Check(success.PillBonus == 0 && success.Bonus == 0 && success.Skin == "kim_dan", "Final realm success resets bonuses and preserves the selected outfit");
    Check(cost.All(x => success.Count(x.Key) == 0), "Success debits all costs including boss core");

    var pill = new Player { Realm = realm };
    var pillId = GameData.BreakPillId(realm);
    pill.Add(pillId, 4);
    for (var i = 0; i < 3; i++) game.Use(pill, pillId);
    Check(pill.PillBonus == .3 && pill.Count(pillId) == 1, "Break pill stacks up to thirty percent");
    Reject(() => game.Use(pill, pillId), "Fourth break pill must be rejected");
    pill.Realm++;
    Reject(() => game.Use(pill, pillId), "Break pill must be restricted to its realm");

    var source = GameData.ChapterDropping(r.BossItem);
    Check(source is not null && GameData.BossItemDifficulty(r.BossItem) == 2 && source.Enabled, "Nightmare boss core must have a reachable source");
}
foreach (var realm in new[] { 8, 9 }) {
    var r = GameData.Realms[realm];
    Check(r.Stages.Length == 3, "Final realms must have three stages");
    foreach (var path in new[] { r.Hero, r.HeroStand, r.HeroMed, r.HeroCast, r.Halo })
        Check(Image(path), $"Missing realm asset: {path} (root: {root})");
    for (var stage = 0; stage < 2; stage++) {
        var p = new Player { Realm = realm, Stage = stage, Qi = r.QiReq(stage) };
        Fund(p, r.StageCost(stage));
        game.Advance(p);
        Check(p.Stage == stage + 1 && p.Qi == 0, "Stage advancement must work");
    }
    var prefix = realm == 8 ? "dk_" : "ct_";
    var gear = GameData.GearBases.Where(x => x.Id.StartsWith(prefix)).ToArray();
    Check(gear.Length == 10, "Each final realm must have ten base gear pieces");
    foreach (var item in gear) {
        Check(Image(item.Icon), $"Missing gear icon: {item.Icon}");
        Check(item.MinRealm == realm && GameData.GearLevelOf(item) == (realm == 8 ? 45 : 50), "Gear realm and level must match");
        var wearer = new Player { Realm = realm };
        var wearableId = GameData.GearId(item, 0);
        wearer.Add(wearableId, 1);
        game.Equip(wearer, wearableId, item.Slot!);
        Check(wearer.Equipment.Any(x => x.ItemId == wearableId), "Gear must be wearable in its realm");
        var early = new Player { Realm = realm - 1 };
        early.Add(wearableId, 1);
        Reject(() => game.Equip(early, wearableId, item.Slot!), "Gear must reject early equip");
        foreach (var quality in Enumerable.Range(0, GameData.QualityMult.Length)) {
            var variant = GameData.ItemById[GameData.GearId(item, quality)];
            Check(variant.MinRealm == realm && variant.Icon == item.Icon && variant.ItemLevel == (realm == 8 ? 45 : 50), "Variant must preserve realm, icon and item level");
        }
    }
    var pBattle = new Player { Realm = realm, Stage = 2, Chapter = 9, Area = 16, Diff = 2 };
    var stats = GameService.Stats(pBattle);
    Check(stats.Atk > 0 && stats.Hp > 0 && stats.Def >= 0, "Final realm stats must remain positive");
    var battle = GameService.Battle(pBattle);
    Check(battle.HeroHp > 0 && battle.HeroHpLeft.All(x => x >= 0) && battle.MonHpLeft.All(x => x >= 0), "Final realm combat health must not overflow");
    foreach (var quality in new[] { 0, 4, 9 }) {
        var equipped = new Player { Realm = realm, Stage = 2, Chapter = 9, Area = 16, Diff = 2,
            LearnedManuals = string.Join(',', SkillData.Elements.Select(x => x.Id)), ActiveManual = "moc" };
        equipped.SetSkillLevels(SkillData.Skills.Where(x => x.Tree != "basic" && x.MinRealm <= realm).ToDictionary(x => x.Id, x => x.MaxLv));
        CultivationManuals.SyncActive(equipped);
        foreach (var item in gear) { var id = GameData.GearId(item, quality); equipped.Add(id, 1); game.Equip(equipped, id, item.Slot!); }
        game.SetSkillSlot(equipped, 1, $"moc_{r.Id}_active");
        var fullStats = GameService.Stats(equipped);
        Check(fullStats.Hp >= stats.Hp && fullStats.Atk >= stats.Atk, "Full gear and passives must never lower stats through overflow");
        var fullBattle = GameService.Battle(equipped);
        Check(fullBattle.HeroHpLeft.All(x => x >= 0 && x <= fullBattle.HeroHp) && fullBattle.MonHpLeft.All(x => x >= 0), "Full gear battle HP must remain within bounds");
        Check(fullBattle.HeroDmg >= 0 && (fullBattle.Heal?.All(x => x >= 0) ?? true) && (fullBattle.GuardLeft?.All(x => x >= 0) ?? true), "Full gear combat damage, recovery and shields must remain nonnegative");
        var fighter = GameService.PvpFighterOf(equipped);
        var pvp = PvpCombat.Run(fighter, fighter, 20261026);
        Check(pvp.Actions.All(x => x.HpA >= 0 && x.HpD >= 0 && x.GuardA >= 0 && x.GuardD >= 0), "Final realm PvP must keep valid health and shields");
    }
}
var max = Peak(9);
Reject(() => game.Advance(max), "Highest realm must reject further advancement");
Check(max.Realm == 9 && max.Stage == 2, "Highest realm rejection must preserve progress");

foreach (var (id, realm) in new[] { ("do_kiep_dan", 7), ("chan_tien_dan", 8), ("pha_chuong_dan_7", 7), ("pha_chuong_dan_8", 8) }) {
    var b = GameData.Brews.Single(x => x.Out == id);
    var p = new Player { Realm = realm };
    Fund(p, b.In);
    game.Brew(p, id, 1);
    Check(p.Count(id) == 1 && b.In.All(x => p.Count(x.Key) == 0), "New pill recipes must brew and debit ingredients");
    var early = new Player { Realm = realm - 1 };
    Fund(early, b.In);
    Reject(() => game.Brew(early, id, 1), $"Recipe {id} must reject brewing before its realm");
}
foreach (var element in SkillData.Elements) foreach (var realm in new[] { 8, 9 }) foreach (var tree in new[] { "active", "passive" }) {
    var id = $"{element.Id}_{(realm == 8 ? "do_kiep" : "chan_tien")}_{tree}";
    var s = SkillData.ById[id];
    Check(s.MinRealm == realm && s.Tree == tree && s.Req.All(x => SkillData.ById.ContainsKey(x.Split(':')[0])), "Final skill tree must retain valid realm and prerequisites");
    Check(Image($"img/final_realms/skills/{id}.png"), $"Missing skill icon: {id}");
    var p = new Player { Realm = realm, AdminPoints = 10000, LearnedManuals = element.Id, ActiveManual = element.Id };
    p.SetSkillLevels(s.Req.ToDictionary(x => x.Split(':')[0], x => int.Parse(x.Split(':')[1])));
    var early = new Player { Realm = realm - 1, AdminPoints = 10000, LearnedManuals = element.Id, Skills = p.Skills };
    Reject(() => game.UpgradeSkill(early, id), "Skill must reject learning before required realm");
    var statsBefore = GameService.Stats(p);
    game.UpgradeSkill(p, id);
    Check(GameService.SkillLevels(p).GetValueOrDefault(id) == 1, $"Skill {id} must be learnable");
    if (tree == "active") {
        game.SetSkillSlot(p, 1, id);
        Check(p.Slots()[1] == id && SkillData.Damage(s, 1) > 0 && s.SecondaryEffect is not null, "Active skill must equip and deal elemental damage");
    } else {
        var statsAfter = GameService.Stats(p);
        Check(statsAfter.Atk > statsBefore.Atk && statsAfter.Hp > statsBefore.Hp, "New passive must improve combat stats");
        Reject(() => game.SetSkillSlot(p, 1, id), "Passive must not occupy active slot");
    }
}
// One completed boss battle yields at most one chest at every difficulty.
foreach (var difficulty in new[] { 0, 1, 2 }) {
    var now = DateTime.UtcNow;
    var boss = new Player { Realm = 9, Stage = 2, Chapter = 0, ChapterMax = 9, Area = GameData.BossArea,
        AreaMax = GameData.BossArea, Diff = difficulty, Cleared = "0.0;1.0;2.0", Admin = "god,map",
        LastTickAt = now, ExploreReadyAt = now };
    boss.Add(GameData.Chapters[0].KeyItem, 1);
    typeof(GameService).GetMethod("Tick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(game, [boss, now]);
    Check(boss.BossKills == 1, "Boss drop check must execute exactly one winning battle");
    Check(boss.Count(GameData.ChestId(GameData.BossChest, difficulty)) == 1, "Each boss win must award exactly one chest when the bag has room");
}
foreach (var realm in Enumerable.Range(0, GameData.Realms.Count)) foreach (var chest in GameData.Chests.Keys) foreach (var difficulty in new[] { 0, 1, 2 }) {
    var table = ChestRewards.Table(realm, GameData.ChestId(chest, difficulty));
    Check(table.All(x => GameData.ItemById[x.Item].Rarity != "supreme"), "Regular chests must never drop Huyền Tiên");
    Check(Math.Abs(table.Sum(x => x.Probability) - 1) < 1e-8, "Regular chest odds must stay normalized");
}
Check(SpinEvent.ChestOdds.All(x => x.Rarity is "immortal" or "dao" or "supreme"), "Event chest must contain only Tiên, Đạo and Huyền Tiên");
Check(Math.Abs(SpinEvent.ChestOdds.Sum(x => x.Chance) - 1) < 1e-8, "Event chest odds must stay normalized");
Check(SpinEvent.ChestOdds.SingleOrDefault(x => x.Rarity == "immortal").Chance == .40
    && SpinEvent.ChestOdds.SingleOrDefault(x => x.Rarity == "dao").Chance == .30
    && SpinEvent.ChestOdds.SingleOrDefault(x => x.Rarity == "supreme").Chance == .30, "Event chest odds must be 40% Tiên, 30% Đạo and 30% Huyền Tiên");
var eventOpener = new Player { Realm = 9 };
eventOpener.Add(SpinEvent.ChestItem, 100);
game.UseItem(eventOpener, SpinEvent.ChestItem, 0);
var eventGear = eventOpener.Inventory.Where(x => GameData.ItemById[x.ItemId].Slot is not null).ToArray();
Check(eventGear.Sum(x => x.Count) == 100 && eventGear.All(x => GameData.ItemById[x.ItemId].Rarity is "immortal" or "dao" or "supreme"), "Opening event chests must award only the revised grades");
foreach (var pet in new[] { false, true }) {
    var progress = 0;
    for (var i = 1; i <= 250; i++) {
        var quality = SpinPity.Roll(ref progress, pet, new Random(i));
        Check(progress == i % 100, "Pity must reset immediately on every hundredth spin");
        if (i % 100 == 0) Check(pet ? quality == PetData.EggIds.Length - 1 : quality >= 7, "The hundredth spin must grant the guaranteed reward");
    }
    var legacy = 100;
    var legacyReward = SpinPity.Roll(ref legacy, pet, new Random(1));
    Check(legacy == 0 && (pet ? legacyReward == PetData.EggIds.Length - 1 : legacyReward >= 7), "An old full pity bar must still grant its pending guarantee");
}
var spinner = new Player { Realm = 9, SpinPityUsed = 99, PetSpinPityUsed = 99 };
spinner.Add(GameData.SpinTicket, 1); spinner.Add(GameData.PetSpinTicket, 1);
game.Spin(spinner, 1);
Check(spinner.SpinPityUsed == 0 && spinner.PetSpinPityUsed == 99, "Gear pity resets independently at full");
game.PetSpin(spinner, 1);
Check(spinner.SpinPityUsed == 0 && spinner.PetSpinPityUsed == 0, "Pet pity resets independently at full");
var activeEvent = SpinEvent.At(DateTimeOffset.UtcNow)!;
var finalMilestone = activeEvent.Milestones.Length - 1;
var eventCap = activeEvent.Milestones[finalMilestone].Points;
var completedEvent = new Player { Realm = 9, EventKey = activeEvent.Key, EventPoints = eventCap + 17 };
game.ClaimEvent(completedEvent, -1);
Check(completedEvent.EventPoints == 17 && completedEvent.EventClaimed == "", "Claiming all completed event milestones must start a new cycle and preserve overflow points");
Check(activeEvent.Milestones.SelectMany(x => x.Rewards).GroupBy(x => x.Item).All(g => completedEvent.Count(g.Key) == g.Sum(x => x.Qty)), "Event reset must deliver every earned reward before resetting");
var partialEvent = new Player { Realm = 9, EventKey = activeEvent.Key, EventPoints = eventCap };
game.ClaimEvent(partialEvent, finalMilestone);
Check(partialEvent.EventPoints == eventCap && partialEvent.EventClaimed == finalMilestone.ToString(), "Receiving only the final reward must retain earlier unclaimed milestones");
game.ClaimEvent(partialEvent, -1);
Check(partialEvent.EventPoints == 0 && partialEvent.EventClaimed == "", "Receiving the remaining earned milestones must reset the full event");
completedEvent.EventPoints = eventCap;
game.ClaimEvent(completedEvent, -1);
Check(completedEvent.EventPoints == 0 && completedEvent.EventClaimed == "", "The next event cycle must be claimable again");
var completedLegacy = new Player { Realm = 9, EventKey = activeEvent.Key, EventPoints = eventCap,
    EventClaimed = string.Join(',', Enumerable.Range(0, activeEvent.Milestones.Length)) };
typeof(GameService).GetMethod("SyncEvent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(game, [completedLegacy]);
Check(completedLegacy.EventPoints == 0 && completedLegacy.EventClaimed == "" && completedLegacy.Inventory.Count == 0, "Old completed event saves must reset without duplicating rewards");
if (SpinEvent.At(activeEvent.Start.AddTicks(-1)) is { } previousEvent) {
    var previousCap = previousEvent.Milestones[^1].Points;
    var rollover = new Player { Realm = 9, EventKey = previousEvent.Key,
        EventPoints = previousCap * 2 + previousEvent.Milestones[0].Points, EventClaimed = "1" };
    var expectedRewards = previousEvent.Milestones.SelectMany((m, i) => m.Rewards.Select(r =>
        new { r.Item, Qty = r.Qty * (2 + (i == 0 ? 1 : 0) - (i == 1 ? 1 : 0)) }))
        .GroupBy(r => r.Item).ToDictionary(g => g.Key, g => g.Sum(r => r.Qty));
    typeof(GameService).GetMethod("SyncEvent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(game, [rollover]);
    Check(expectedRewards.All(x => rollover.Count(x.Key) == x.Value), "Weekly rollover must pay every overflow cycle and partial milestone without duplicating claimed gifts");
    Check(rollover.EventKey == activeEvent.Key && rollover.EventPoints == 0 && rollover.EventClaimed == "", "Weekly rollover must then begin the new event");
}
// Bí kíp: mỗi cuốn +10%, đủ 10 cuốn mới mở hệ; hệ đã mở theo luật cũ giữ nguyên.
var reader = new Player { ManualVersion = 1, LearnedManuals = "hoa" };
var waterManual = CultivationManuals.All.First(m => m.Element == "thuy");
reader.Add(waterManual.Item, CultivationManuals.Required + 1);
for (var i = 1; i < CultivationManuals.Required; i++) {
    var (note, _, _) = game.UseItem(reader, waterManual.Item);
    Check(!CultivationManuals.IsUnlocked(reader, "thuy") && CultivationManuals.Progress(reader)["thuy"] == i, $"manual {i}/10 must only add progress");
    Check(note.Contains($"{i}/{CultivationManuals.Required}") && note.Contains($"{i * 10}%"), $"manual {i}/10 must notify progress");
}
var (unlockNote, _, _) = game.UseItem(reader, waterManual.Item);
Check(CultivationManuals.IsUnlocked(reader, "thuy") && !CultivationManuals.Progress(reader).ContainsKey("thuy") && unlockNote.Contains("mở vĩnh viễn"), "tenth manual must unlock the element");
Check(reader.Count(waterManual.Item) == 1, "each comprehension consumes exactly one manual");
Reject(() => game.UseItem(reader, waterManual.Item), "unlocked element must reject extra manuals");
Check(CultivationManuals.ProgressAll(reader)["hoa"] == CultivationManuals.Required, "legacy unlocked element reports full progress");

if (failures.Count > 0) {
    foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
    throw new Exception($"{failures.Count}/{checks} checks failed");
}
Console.WriteLine($"PASS: {checks} final realm checks (progression, recipes, gear, skills, combat, assets).");
