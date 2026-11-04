using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;
namespace GemSystemTests;
public class SkillAutoMaxTests
{
    [Fact]
    public void MaxSpendsOnlyOnEligibleSkillsAndStopsWhenNoUpgradeRemains()
    {
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);


        var game = new GameService(db);
        var p = new Player { LearnedManuals = "hoa", AdminPoints = 10000 };

        p.AdminPoints = 10000;
        var before = GameService.SkillPoints(p);
        game.UpgradeSkillsMax(p, "hoa");
        Assert.InRange(GameService.SkillPoints(p), 0, before - 1);
        foreach (var s in SkillData.ById.Values.Where(s => s.Element == "hoa" && s.Tree != "basic" && s.MinRealm <= p.Realm))
            Assert.Equal(s.MaxLv, GameService.SkillLevels(p).GetValueOrDefault(s.Id));
        foreach (var s in SkillData.ById.Values.Where(s => s.Tree != "basic" && (s.Element != "hoa" || s.MinRealm > p.Realm)))
            Assert.Equal(0, GameService.SkillLevels(p).GetValueOrDefault(s.Id));
        var points = GameService.SkillPoints(p);
        Assert.Throws<GameException>(() => game.UpgradeSkillsMax(p, "hoa"));
        Assert.Throws<GameException>(() => game.UpgradeSkillsMax(p, "invalid"));
        Assert.Equal(points, GameService.SkillPoints(p));
    }
}