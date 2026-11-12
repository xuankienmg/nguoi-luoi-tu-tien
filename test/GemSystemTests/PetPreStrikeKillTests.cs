using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>Linh thú đầu lượt (thiêu đốt / kỳ tích) được client diễn sau đòn tu sĩ:
/// đòn tu sĩ đã đủ hạ quái thì lượt đó không còn sát thương linh thú (thanh máu không treo ở phần máu của linh thú).</summary>
public class PetPreStrikeKillTests
{
    [Fact]
    public void HeroKillingBlowLeavesNoPendingPetDamage()
    {
        var p = new Player { Id = Guid.Parse("40000000-0000-0000-0000-0000000000d7"), Realm = 0, Chapter = 9, Diff = 0, BodyHp = 1_000_000_000_000, MapVer = 1,
            LearnedManuals = "hoa", ActiveManual = "hoa", SkillSlots = "hoa_cau," };
        p.SetSkillLevels(SkillData.Skills.Where(s => s.Element == "hoa" && s.Tree != "basic").ToDictionary(s => s.Id, s => s.MaxLv));
        var pet = new Pet { PlayerId = p.Id, DefId = "thai_co_hoa_ky_lan", Level = 80, Form = 2, Slot = 1, AptAtk = 90, AptDef = 90, AptHp = 90 };
        p.Pets.Add(pet);
        int kills = 0, preStrikePet = 0;
        for (var i = 0; i < 120; i++)
        {
            var b = GameService.BattleAt(p, 1, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(i * 13).AddSeconds(i * 37));
            for (var t = 0; t < b.Hits; t++)
            {
                if (b.MonHpLeft[t] > 0) continue;
                var start = b.Waves.LastOrDefault(w => w.Start <= t);
                var before = start?.Start == t || t == 0 ? start?.Hp ?? b.MonsterHp : b.MonHpLeft[t - 1];
                var hero = b.Casts.Where(c => c.Turn == t).Sum(c => c.Dmg) + b.BasicDamage[t] + b.ExtraDamage[t]   // đốt / độc trừ ở lượt địch (sau linh thú), không thuộc đòn tu sĩ
                    + (b.Bolt[t] ? b.BoltDmg : 0) + (t == 0 ? b.FirstHit : 0);
                if (hero < before) continue;
                kills++;
                var pending = b.Pet!.Casts.Where(c => c.Turn == t && !(c.At >= .85)).Sum(c => c.Dmg);
                if (pending > 0) preStrikePet++;
            }
        }
        Assert.True(kills > 0, "không có lượt tu sĩ tự hạ quái");
        Assert.Equal(0, preStrikePet);
    }
}
