using VanDao.Data;
using VanDao.Game;
using Xunit;

namespace GemSystemTests;

/// <summary>Mỗi hệ đúng 2 bị động mỗi cảnh giới; từ Luyện Hư một trong hai học từ bí kíp bị động (chỉ bán ở Tạp Hóa VIP).</summary>
public class PassiveBookTests
{
    private static readonly string[] Elements = ["hoa", "thuy", "moc", "tho", "loi", "bang", "gio"];

    [Fact]
    public void EveryElementHasExactlyTwoPassivesPerRealmAndOneBookFromLuyenHu()
    {
        foreach (var el in Elements)
            for (var realm = 0; realm <= 9; realm++)
            {
                var passives = SkillData.Skills.Where(s => s.Element == el && s.Tree == "passive" && s.MinRealm == realm).ToList();
                Assert.True(passives.Count == 2, $"{el} r{realm} có {passives.Count} bị động");
                Assert.Equal(realm >= 5 ? 1 : 0, passives.Count(s => s.Book != ""));
            }
    }

    [Fact]
    public void BooksAreSoldOnlyInVipShop()
    {
        var player = new Player { Realm = 9 };
        var vip = GameService.VipOffersFor(player).Where(o => o.Group == "Bí kíp bị động").Select(o => o.Item).ToHashSet();
        foreach (var book in PassiveBooks.Items)
        {
            Assert.Contains(book.Id, vip);
            Assert.DoesNotContain(GameService.ShopDailyPool, d => d.Id == book.Id);
            Assert.True(GameData.ItemById.ContainsKey(book.Id));
        }
        Assert.Equal(35, PassiveBooks.Items.Length);
    }

    [Fact]
    public void UsingBookLearnsPassiveForFreeThenLevelsWithPoints()
    {
        var game = new GameService(null!);
        var skill = SkillData.Skills.First(s => s.Element == "hoa" && s.Book != "" && s.MinRealm == 5);
        var p = new Player { Realm = 5, AdminPoints = 50 };
        CultivationManuals.Unlock(p, "hoa");
        p.Add(skill.Book, 1);
        Assert.Throws<GameException>(() => game.UpgradeSkill(p, skill.Id));        // chưa có bí kíp
        var spent = GameService.SkillPointsSpent(p);
        game.UseItem(p, skill.Book);
        Assert.Equal(1, GameService.SkillLevels(p)[skill.Id]);
        Assert.Equal(spent, GameService.SkillPointsSpent(p));                      // cấp 1 miễn điểm
        Assert.Equal(0, p.Count(skill.Book));
        game.UpgradeSkill(p, skill.Id);
        Assert.Equal(2, GameService.SkillLevels(p)[skill.Id]);
        Assert.True(GameService.SkillPointsSpent(p) > spent);
    }

    [Fact]
    public void BookIsKeptWhenRealmOrTreeRequirementIsNotMet()
    {
        var game = new GameService(null!);
        var skill = SkillData.Skills.First(s => s.Element == "hoa" && s.Book != "" && s.MinRealm == 6);
        var p = new Player { Realm = 5 };
        CultivationManuals.Unlock(p, "hoa");
        p.Add(skill.Book, 1);
        Assert.Throws<GameException>(() => game.UseItem(p, skill.Book));           // chưa tới Hợp Thể
        Assert.Equal(1, p.Count(skill.Book));
        var locked = new Player { Realm = 9 };
        locked.Add(skill.Book, 1);
        Assert.Throws<GameException>(() => game.UseItem(locked, skill.Book));       // chưa mở cây hệ Hỏa
        Assert.Equal(1, locked.Count(skill.Book));
    }

    [Fact]
    public void LearnedBookSurvivesSkillReset()
    {
        var game = new GameService(null!);
        var skill = SkillData.Skills.First(s => s.Element == "thuy" && s.Book != "" && s.MinRealm == 5);
        var p = new Player { Realm = 5 };
        CultivationManuals.Unlock(p, "thuy");
        p.Add(skill.Book, 1);
        game.UseItem(p, skill.Book);
        p.SetSkillLevels(new());                                                    // như Tẩy Tủy Đan
        Assert.Equal(1, GameService.SkillLevels(p)[skill.Id]);
        Assert.Contains("thuy", CultivationManuals.Unlocked(p));                    // danh sách hệ đã mở không bị lẫn token bí kíp
    }
}
