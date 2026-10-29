using System.Text.Json;
using VanDao.Game;

var root = Path.GetFullPath(args.FirstOrDefault() ?? ".");
if (!Directory.Exists(Path.Combine(root, "wwwroot", "img")) && Directory.Exists(Path.Combine(root, "../..", "wwwroot", "img")))
    root = Path.GetFullPath(Path.Combine(root, "../.."));
var site = Path.Combine(root, "site");
if (!Directory.Exists(Path.Combine(root, "wwwroot", "img"))) throw new Exception("Run from the game repository root, or pass its path.");
var images = new HashSet<string>();
void Image(string? path) { if (path?.StartsWith("img/") == true) images.Add(path); }
foreach (var item in GameData.AllItems) Image(item.Icon);
foreach (var pet in PetData.Definitions) foreach (var form in pet.Forms) Image(form.Image);
var chapters = GameData.Chapters.Select(c => new {
    c.Index, c.Name, c.Realm, c.Enabled, c.BossLair, c.KeyItem, c.BossItem,
    hardBossItem = GameData.BreakthroughBossItem(c.Index, 1), nightmareBossItem = GameData.BreakthroughBossItem(c.Index, 2),
    difficulties = Enumerable.Range(0, GameData.DiffNames.Length).Select(d => {
      var mapChapter = d == 3 ? HellMapData.Chapter(c.Index) : c;
      return new {
        name = GameData.DiffNames[d],
        chapterName = mapChapter.Name, realm = mapChapter.Realm, bossLair = mapChapter.BossLair, keyItem = mapChapter.KeyItem, bossItem = mapChapter.BossItem,
        drops = GameData.DiffDrops(c, d),
        herbChapter = GameData.DiffHerbChapter(c, d),
        herbs = GameData.HerbDropDefsFor(c, d),   // linh thảo của map (Khó / Ác mộng: chương cuối + linh thảo bổ sung của chương)
        bossMaterials = PetLoot.BossMaterials(c.Index, d),   // vật phẩm rơi khi hạ Boss
        areas = Enumerable.Range(1, GameData.BossAreaFor(d)).Select(a => new {
            index = a, place = GameData.AreaPlace(mapChapter, a, d),
            waves = GameData.AreaWaves(mapChapter, a, d).Select(w => {
                Image(w.Image);
                var stones = GameData.StoneRange(c, a, w.Tier, d);
                return new { w.Id, w.Name, w.Image, w.Tier, w.Hp, w.Atk, w.Def,
                    w.Crit, w.CritResist, w.Accuracy, w.Dodge, w.Combo, w.ComboResist, w.Reflect, w.ReflectResist,
                    stoneMin = stones.Min, stoneMax = stones.Max };
            }).ToArray()
        }).ToArray()
    }; }).ToArray()
}).ToArray();
var data = new {
    combatRules = new { bossBaseRatings = Enumerable.Range(0, GameData.DiffNames.Length).Select(d => MonsterCombatRatings.For("boss_hac_giao", "boss", d).Crit).ToArray(),
        commonShare = MonsterCombatRatings.For("", "normal", 0).Crit, eliteShare = MonsterCombatRatings.For("", "elite", 0).Crit, resistanceBonus = 50, featuredBonus = 50 },
    items = GameData.AllItems, slots = GameData.Slots, brews = GameData.Brews,
    pets = PetData.Definitions, sets = EquipmentEffects.Sets, herbDrops = GameData.HerbDrops,
    realms = GameData.Realms.Select((r, i) => new { index = i, r.Name, r.Desc, r.Stages, r.BagSlots, r.EquipSlots, r.BreakCost, r.BreakChance }),
    chapters, qualityKeys = GameData.RarityOrder, qualityNames = GameData.QualityName, qualityColors = GameData.QualityColor,
    petHatchSeconds = PetData.HatchSeconds, petEggIds = PetData.EggIds,
    gemRatingBudget = GemProgression.Themes.Select(theme => new {
        theme, itemId = GemProgression.Id(theme, 0),
        levels = Enumerable.Range(1, 10).Select(level => new { level, ratings = GemRatingBudget.For(theme, level) }).ToArray()
    }).ToArray(),
    gemUpgradeChances = GemData.UpgradeChances, gemRecipes = GemData.Recipes, herbToGem = GemData.HerbToCuiGemMap,
    gemBossDrops = Enumerable.Range(0, GameData.DiffNames.Length).Select(d => new { difficulty = d, drops = GemData.BossBonusDrops(d) }).ToArray()
};
Directory.CreateDirectory(Path.Combine(site, "data"));
File.WriteAllText(Path.Combine(site, "data", "wiki.json"), JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
var copied = 0;
foreach (var image in images.Order()) {
    var source = Path.Combine(root, "wwwroot", image);
    if (!File.Exists(source)) throw new Exception($"Missing wiki image: {image}");
    var dest = Path.Combine(site, "img", "wiki", image[4..]);
    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
    File.Copy(source, dest, true);
    copied++;
}
Console.WriteLine($"Wiki: {GameData.AllItems.Count} items, {PetData.Definitions.Length} pets, {chapters.Length} chapters, {copied} images.");
