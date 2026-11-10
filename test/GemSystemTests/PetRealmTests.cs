using VanDao.Data;
using VanDao.Game;
using Xunit;

namespace GemSystemTests;

/// <summary>Cảnh giới riêng của linh thú: chỉ số và cấp trần theo cảnh giới của chính con, đột phá có tỉ lệ thất bại và cần yêu hạch Boss.</summary>
public class PetRealmTests
{
    private static (Player p, Pet pet) Setup(int ownerRealm, int petRealm, int level)
    {
        var p = new Player { Realm = ownerRealm };
        var pet = new Pet { DefId = PetData.StarterEggs.Values.First(), Realm = petRealm, Level = level, AptAtk = 100, AptDef = 100, AptHp = 100 };
        p.Pets.Add(pet);
        return (p, pet);
    }

    [Fact]
    public void StatsDoNotGrowWhenOwnerBreaksThrough()
    {
        var (p, pet) = Setup(2, 2, 30);
        var before = GameService.PetStats(p, pet);
        p.Realm = 6;
        Assert.Equal(before, GameService.PetStats(p, pet));
    }

    [Fact]
    public void StatsAndLevelCapGrowWithPetRealm()
    {
        var (p, pet) = Setup(6, 2, 20);
        var low = GameService.PetStats(p, pet);
        pet.Realm = 3;
        Assert.True(GameService.PetStats(p, pet).Atk > low.Atk);
        for (var r = 1; r <= PetData.MaxRealm; r++) Assert.True(PetData.LevelCap(r) > PetData.LevelCap(r - 1));
    }

    [Fact]
    public void LegacyPetsKeepOwnerRealmOnce()
    {
        var (p, pet) = Setup(5, -1, 40);
        GameService.MigratePetRealms(p);
        Assert.Equal(5, pet.Realm);
        p.Realm = 8;
        GameService.MigratePetRealms(p);
        Assert.Equal(5, pet.Realm);                    // đã gán, không tự đổi theo chủ nhân
        var (q, old) = Setup(9, -1, 10);
        GameService.MigratePetRealms(q);
        Assert.Equal(PetData.LegacyMaxRealm, old.Realm);
    }

    [Fact]
    public void BreakthroughNeedsCapOwnerRealmAndMaterialsAndBossCore()
    {
        var game = new GameService(null!);
        for (var target = 1; target <= PetData.MaxRealm; target++)
        {
            var cost = PetData.BreakCost(target);
            Assert.Equal("boss", GameData.ItemById[PetData.BreakCore(target)].Type);   // yêu hạch Boss tương ứng
            Assert.True(cost.ContainsKey(PetData.BreakCore(target)) && cost["linh_thach"] > 0 && cost["hoa_hinh_dan"] > 0);
            Assert.Equal(GameData.Realms[target - 1].BreakChance, PetData.BreakChance(target));   // bằng tỉ lệ đột phá nhân vật
        }
        var (p, pet) = Setup(3, 2, 59);
        Fund(p, 3);
        Assert.Throws<GameException>(() => game.PetBreak(p, pet.Id));              // chưa đủ cấp trần
        pet.Level = PetData.LevelCap(2);
        var (poor, poorPet) = Setup(3, 2, PetData.LevelCap(2));
        Assert.Throws<GameException>(() => game.PetBreak(poor, poorPet.Id));       // thiếu nguyên liệu
        var (same, samePet) = Setup(3, 3, PetData.LevelCap(3));
        Fund(same, 4);
        Assert.Throws<GameException>(() => game.PetBreak(same, samePet.Id));       // không vượt cảnh giới chủ nhân
        Assert.Equal(3, samePet.Realm);
        game.PetBreak(p, pet.Id, new Always(0.0));                                  // đủ điều kiện: thành công
        Assert.Equal(3, pet.Realm);
    }

    [Fact]
    public void SuccessPaysEverythingAndFailureKeepsBossCore()
    {
        var game = new GameService(null!);
        var (p, pet) = Setup(4, 1, PetData.LevelCap(1));
        Fund(p, 2);
        var core = PetData.BreakCore(2);
        var coreBefore = p.Count(core);
        game.PetBreak(p, pet.Id, new Always(0.999));                                 // thất bại
        Assert.Equal(1, pet.Realm);
        Assert.Equal(coreBefore, p.Count(core));
        Assert.True(p.Count("linh_thach") < 10_000_000);
        Fund(p, 2);
        game.PetBreak(p, pet.Id, new Always(0.0));                                   // thành công
        Assert.Equal(2, pet.Realm);
        Assert.True(p.Count(core) < coreBefore + 1 || p.Count(core) == coreBefore + 1 - PetData.BreakCost(2)[core]);
    }

    [Fact]
    public void RealmCapsAtChanTienAndStopsAtMax()
    {
        var game = new GameService(null!);
        var (p, pet) = Setup(9, 9, PetData.LevelCap(9));
        Assert.Throws<GameException>(() => game.PetBreak(p, pet.Id));
    }

    private static void Fund(Player p, int target)
    {
        foreach (var (item, need) in PetData.BreakCost(target)) p.Add(item, Math.Max(0, need * 2 - (int)p.Count(item)));
        p.Add("linh_thach", 10_000_000 - p.Count("linh_thach"));
    }

    private sealed class Always(double value) : Random { public override double NextDouble() => value; protected override double Sample() => value; }
}
