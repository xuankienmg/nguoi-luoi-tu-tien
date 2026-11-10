using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

/// <summary>Kim Tiên (11/2026): cảnh giới thứ 11 sau Chân Tiên.</summary>
public class KimTienRealmTests
{
    private const int Kt = GameData.KimTienRealm;

    [Fact]
    public void RealmFollowsChanTienWithBossCoreGate()
    {
        Assert.Equal(Kt + 1, GameData.Realms.Count);
        var chanTien = GameData.Realms[Kt - 1];
        var kimTien = GameData.Realms[Kt];
        Assert.Equal("Kim Tiên", kimTien.Name);
        Assert.Equal(3, kimTien.Stages.Length);
        Assert.Null(kimTien.BreakCost);
        Assert.Equal(GameData.KimTienCore, chanTien.BossItem);
        Assert.Equal(1, chanTien.BreakCost![GameData.KimTienCore]);
        Assert.Equal(1, chanTien.BreakCost[GameData.KimTienPill]);
        Assert.Equal(5 * chanTien.StatMult, kimTien.StatMult);
        Assert.True(kimTien.QiReq(0) > chanTien.QiReq(2));
    }

    [Fact]
    public void CoreDropsOnlyFromHell2Chapter5Boss()
    {
        Assert.Equal(GameData.KimTienCore, GameData.BreakthroughBossItem(4, 4));
        Assert.Contains(PetLoot.BossMaterials(4, 4), d => d.Item == GameData.KimTienCore && d.Chance == 1);
        for (var ch = 0; ch < 10; ch++)
            for (var d = 0; d <= 4; d++)
                if ((ch, d) != (4, 4)) Assert.DoesNotContain(PetLoot.BossMaterials(ch, d), x => x.Item == GameData.KimTienCore);
        var chapter = GameData.ChapterDropping(GameData.KimTienCore);
        Assert.NotNull(chapter);
        Assert.Equal(4, chapter!.Index);
        Assert.Equal("Hỏa Long", chapter.Boss.Name);
    }

    [Fact]
    public void Hell2LateChaptersAreKimTienContent()
    {
        var labels = Enumerable.Range(0, 10).Select(i => HellMapData.Chapter(4, i).Realm).ToArray();
        Assert.All(labels.Take(5), l => Assert.Equal("Chân Tiên Hậu kỳ", l));
        Assert.All(labels.Skip(5), l => Assert.StartsWith("Kim Tiên", l));
        var tune = HellMapData.TuneFor(4);
        for (var i = 1; i < tune.Length; i++) Assert.True(tune[i] > tune[i - 1]);
        Assert.True(tune[5] > 2 * tune[4]);   // chương 6 là bậc chặn Chân Tiên
    }

    [Fact]
    public void GearSetHasTenPiecesAtLevel55()
    {
        var set = GameData.GearBases.Where(b => b.Set == "Kim Tiên").ToArray();
        Assert.Equal(10, set.Length);
        Assert.All(set, b => Assert.Equal(Kt, b.MinRealm));
        Assert.All(set, b => Assert.Equal(55, GameData.GearLevelOf(b)));
        Assert.Contains(EquipmentEffects.Sets, s => s.Name == "Kim Tiên");
        // Huyền Tiên: chỉ số từng món vẫn nằm trong int, chỉ phép cộng cả bộ mới cần decimal.
        Assert.All(set, b => { var it = GameData.ItemById[GameData.GearId(b, 9)];
            Assert.True(it.Atk >= 0 && it.Def >= 0 && it.Hp >= 0 && (long)it.Atk + it.Def + it.Hp > 0); });
        Assert.Equal(set.Select(b => b.Slot).OrderBy(x => x), GameData.SpinPool(Kt).Select(b => b.Slot).OrderBy(x => x));
    }

    [Fact]
    public void EveryElementGetsActiveAndPassiveSkill()
    {
        var skills = SkillData.Skills.Where(s => s.MinRealm == Kt && s.Book == "").ToArray();
        var elements = SkillData.Skills.Select(s => s.Element).Distinct().Count();
        Assert.Equal(elements * 2, skills.Length);
        // + 1 bị động học từ bí kíp mỗi hệ, bán ở Tạp Hóa VIP
        var books = SkillData.Skills.Where(s => s.MinRealm == Kt && s.Book != "").ToArray();
        Assert.Equal(elements, books.Length);
        Assert.All(books, b => Assert.True(GameData.ItemById.ContainsKey(b.Book)));
        var vip = GameService.VipOffersFor(new Player { Realm = Kt }).Where(o => o.Group == "Bí kíp bị động").ToArray();
        Assert.All(books, b => Assert.Contains(vip, o => o.Item == b.Book && o.Price > vip.Where(x => SkillData.Skills.First(s => s.Book == x.Item).MinRealm == Kt - 1).Max(x => x.Price)));
        foreach (var s in skills)
        {
            var prev = SkillData.ById[s.Req.Single().Split(':')[0]];
            Assert.Equal(Kt - 1, prev.MinRealm);
            if (s.Tree == "active") Assert.True(s.DmgBase > prev.DmgBase);
        }
    }

    [Fact]
    public void PetsCanBreakThroughToKimTien()
    {
        var def = PetData.Definitions.First();
        Assert.Equal(Kt, PetData.MaxRealm);
        Assert.Equal(5 * PetData.StatScale(Kt - 1, def), PetData.StatScale(Kt, def), 6);
        Assert.Equal(900, PetData.LevelCap(Kt - 1));   // 11/2026: 90 cấp mỗi cảnh giới
        Assert.Equal(999, PetData.LevelCap(Kt));
        Assert.Equal(GameData.Realms[Kt - 1].BreakChance, PetData.BreakChance(Kt));
        var cost = PetData.BreakCost(Kt);
        Assert.Equal(4, cost[GameData.KimTienCore]);
        Assert.Equal(GameData.Realms[Kt - 1].BreakCost!["linh_thach"] * 5, cost["linh_thach"]);
    }

    [Fact]
    public void StatSourcesHandleFullHighGradeSet()
    {
        var p = new Player { Realm = Kt };
        foreach (var b in GameData.GearBases.Where(b => b.Set == "Kim Tiên"))
            p.Equipment.Add(new EquippedItem { Slot = b.Slot!, ItemId = GameData.GearId(b, 9) });
        var method = typeof(GameService).GetMethod("StatSources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var gear = ((StatSourceDto[])method.Invoke(null, [p])!).First(s => s.Name == "Trang bị trực tiếp");   // từng tràn int (OverflowException)
        Assert.True(gear.Hp > int.MaxValue);
    }
}
