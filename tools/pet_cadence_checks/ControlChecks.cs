using VanDao.Data;
using VanDao.Game;

/// <summary>Khống chế quái theo hệ linh thú: tê liệt (Lôi) · đóng băng (Băng) · choáng (Thổ / Phong).</summary>
public static class ControlChecks
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        var owner = new CombatStats(1000, 10000, 20);
        var expected = new Dictionary<string, (string Kind, double Chance)> {
            ["tu_dien_mieu"] = ("paralyze", .25), ["loi_van_dieu"] = ("paralyze", .2), ["thanh_long"] = ("paralyze", .2),
            ["thai_co_loi_long"] = ("paralyze", .5), ["bang_tinh_tho"] = ("freeze", .3), ["kim_giap_quy"] = ("stun", .25), ["bach_ho"] = ("stun", .25) };
        foreach (var def in PetData.Definitions)
        {
            var (kind, chance) = PetData.ControlOf(def.Skill);
            if (!expected.TryGetValue(def.Id, out var want)) { Check(kind == "" && chance == 0, def.Id + " should have no control"); continue; }
            Check(kind == want.Kind && chance == want.Chance, def.Id + " control kind and chance");
            Check(PetData.SkillDescription(def.Skill, 2, 0).Contains(PetData.ControlName(kind)), def.Id + " description names its control");
            for (var form = 0; form < 3; form++)
            {
                // Tỉ lệ dưới ngưỡng: khống; bằng ngưỡng: không khống (biên chính xác).
                var hit = new PetCombat(def, form, owner, 0, new FixedRandom(chance - 1e-6), false); long hp = 5000;
                var cast = hit.Cast(4, ref hp, 1_000_000, 1_000_000, 0, 1);
                Check(cast.ParalyzeTurns == 1 && cast.ControlKind == kind && hit.Paralyzed && hit.ControlKind == kind, def.Id + " applies " + kind);
                hit.BeginTurn(5, ref hp, 1_000_000, 0, 1);
                Check(!hit.Paralyzed && hit.ControlKind == "", def.Id + " control lasts one turn");
                Check(hit.Shatter(5) == (kind == "freeze") && !hit.Shatter(4) && !hit.Shatter(6), def.Id + " freeze bonus only on the next turn");
                var miss = new PetCombat(def, form, owner, 0, new FixedRandom(chance), false); hp = 5000;
                cast = miss.Cast(4, ref hp, 1_000_000, 1_000_000, 0, 1);
                Check(cast.ParalyzeTurns == 0 && cast.ControlKind == "" && !miss.Paralyzed && !miss.Shatter(5), def.Id + " no control at the chance boundary");
            }
            var wave = new PetCombat(def, 2, owner, 0, new FixedRandom(0), false); long h = 5000;
            wave.Cast(1, ref h, 1_000_000, 1_000_000, 0, 1); wave.NextWave();
            Check(!wave.Paralyzed && !wave.Shatter(2), def.Id + " new wave clears control and freeze");
            if (!PetData.SkillIsSupport(def.Skill))
            {
                // Đòn kết liễu: quái đã chết thì không gieo / không gắn khống.
                var kill = new PetCombat(def, 2, owner, 0, new FixedRandom(0), false); h = 5000;
                var killing = kill.Cast(1, ref h, 1, 1, 0, 1);
                Check(killing.ParalyzeTurns == 0 && !kill.Paralyzed, def.Id + " no control on a killing blow");
            }
        }
        // Chi phí khống trừ vào ngân sách: 0,4 × tỉ lệ.
        Check(Math.Abs(PetData.SkillCoef("cat", 2) - 36 * 4 * (1 - .25 - .4 * .25)) < 1e-9, "Cat budget pays for paralysis");
        Check(Math.Abs(PetData.SkillCoef("frost", 2) - 25 * .15 * 5 * (1 - .25 - .4 * .3)) < 1e-9, "Frost budget pays for freeze");
        Check(Math.Abs(PetData.SkillCoef("ancient_thunder", 2) - 50 * 3 * .8) < 1e-9, "Ancient thunder budget unchanged");
        // Trận thật: mỗi lượt có nhãn khống thì quái thật sự mất lượt; lượt vỡ băng nằm sau lượt đóng băng.
        foreach (var id in new[] { "bang_tinh_tho", "kim_giap_quy", "tu_dien_mieu" })
        {
            var p = new Player { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Realm = 3, Chapter = 3, BodyHp = 1_000_000, Area = 15,
                ExploreReadyAt = new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc) };
            p.Pets.Add(new Pet { DefId = id, Slot = 1, Level = 60, Form = 2, AptAtk = 90, AptDef = 90, AptHp = 90 });
            var a = GameService.BattleAt(p, 15);
            Check(a.MonControl is not null && a.MonControl.Length == a.Hits && a.Shatter!.Length == a.Hits, id + " control arrays follow the sampled turns");
            Check(a.MonControl!.Select((k, i) => k == "" || a.MonSkip[i]).All(x => x), id + " control label only when the monster loses its turn");
            Check(a.MonControl!.All(k => k == "" || k == PetData.ControlOf(PetData.ById[id].Skill).Kind), id + " control label matches the pet element");
        }
        Console.WriteLine($"PASS {checks} monster control checks.");
    }
    private sealed class FixedRandom(double value) : Random { public override double NextDouble() => value; }
}
