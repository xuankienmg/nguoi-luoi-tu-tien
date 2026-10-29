using System.Text.Json;
using VanDao.Data;
using VanDao.Game;

// Same loadout at all difficulties. Deterministic regression fixture, not a recommendation for live progression.
var root = Path.GetFullPath(args.FirstOrDefault() ?? ".");
var results = new List<object>();
foreach (var difficulty in Enumerable.Range(0, 4))
foreach (var chapter in new[] { 0, 9 })
foreach (var boss in new[] { false, true })
{
    var wins = 0; long turns = 0, heroCombo = 0, enemyCombo = 0, heroCounter = 0, enemyCounter = 0;
    const int runs = 32;
    for (var sample = 0; sample < runs; sample++)
    {
        var p = new Player { Id = new Guid(sample, 0, 0, new byte[8]), Realm = 9, Stage = 2, Diff = difficulty, Chapter = chapter, MapVer = 1 };
        foreach (var slot in GameData.Realms[9].EquipSlots)
            p.Equipment.Add(new() { Slot = slot, ItemId = GameData.AllItems.First(i => i.Slot == slot && i.MinRealm == 9 && i.Rarity == "supreme").Id });
        var skills = SkillData.Skills.Where(s => s.Element == "hoa").ToDictionary(s => s.Id, s => s.MaxLv);
        p.SetSkillLevels(skills);
        var ultimate = SkillData.Skills.Where(s => s.Element == "hoa" && s.Tree == "active" && s.DmgBase > 0).OrderByDescending(s => s.MinRealm).First();
        p.SkillSlots = "hoa_cau," + ultimate.Id;
        var area = boss ? GameData.BossAreaFor(difficulty) : 10;
        var battle = GameService.BattleAt(p, area, new DateTime(2026, 10, 29, 0, 0, 0, DateTimeKind.Utc));
        if (battle.Win) wins++;
        turns += battle.TimingHits ?? battle.Hits;
        heroCombo += battle.ExtraDamage?.Count(x => x > 0) ?? 0;
        enemyCombo += battle.MonsterExtraDamage?.Count(x => x > 0) ?? 0;
        heroCounter += battle.Reflect?.Count(x => x > 0) ?? 0;
        enemyCounter += battle.MonsterReflectDamage?.Count(x => x > 0) ?? 0;
        if (battle.HeroHpLeft.Any(x => x < 0) || battle.MonHpLeft.Any(x => x < 0)) throw new Exception("Negative replay HP");
        if (battle.MonsterExtraDamage?.Length != battle.Hits || battle.MonsterReflectDamage?.Length != battle.Hits) throw new Exception("Misaligned reaction replay");
    }
    results.Add(new { difficulty = GameData.DiffNames[difficulty], chapter = chapter + 1, boss, runs, wins, winRate = wins * 100d / runs,
        averageTurns = turns / (double)runs, heroCombo, enemyCombo, heroCounter, enemyCounter });
    Console.WriteLine($"{GameData.DiffNames[difficulty]} / chapter {chapter + 1} / {(boss ? "boss" : "elite stage")}: {wins}/{runs}, {turns / (double)runs:0.0} turns");
}
Directory.CreateDirectory(Path.Combine(root, "output"));
File.WriteAllText(Path.Combine(root, "output", "combat-ratings-balance.json"), JsonSerializer.Serialize(new {
    fixture = "Realm 9 stage 2, full supreme realm-9 gear, max fire skills, no gems/pets; 32 seeds per scenario",
    bossBaseRatings = Enumerable.Range(0, 4).Select(d => MonsterCombatRatings.For("boss_hac_giao", "boss", d).Crit).ToArray(),
    commonNormalRatings = MonsterCombatRatings.For("", "normal", 0), eliteNormalRatings = MonsterCombatRatings.For("", "elite", 0), results
}, new JsonSerializerOptions { WriteIndented = true }));
