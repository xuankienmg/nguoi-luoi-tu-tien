using VanDao.Data;
using VanDao.Game;
namespace GemSystemTests;

public class HangingMapTests
{
    [Theory]
    [InlineData(3, 4)]
    [InlineData(4, 0)]
    [InlineData(2, 3)]
    public void HangingMapKeepsDifficultyEvenWhenClearedMarksAreMissing(int diff, int chapter)
    {
        var p = new Player { MapVer = 2, Diff = diff, Chapter = chapter, ChapterMax = 9, Area = 5, AreaMax = 15, Cleared = "" };
        GameService.ConvertLegacy(p);
        Assert.Equal((diff, chapter), (p.Diff, p.Chapter));
    }
}
