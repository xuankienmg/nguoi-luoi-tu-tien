using VanDao.Data;
using VanDao.Game;
using Xunit;

namespace GemSystemTests;

/// <summary>Đồng bộ nhẹ: danh sách ải chỉ gửi lại khi client chưa có đúng bản đó (header X-Map-Key).</summary>
public class MapSyncTests
{
    [Fact]
    public void KeyIsStableAndAreasAreSentWithoutClientKey()
    {
        var p = new Player();
        var game = new GameService(null!);
        var a = game.MapStateFor(p);
        var b = game.MapStateFor(p);
        Assert.NotEmpty(a.Areas);
        Assert.False(a.AreasOmitted);
        Assert.Equal(a.AreasKey, b.AreasKey);
        Assert.Equal(24, a.AreasKey.Length);
    }

    [Fact]
    public void MatchingClientKeyOmitsAreasOnly()
    {
        var p = new Player();
        var full = new GameService(null!).MapStateFor(p);
        var lite = new GameService(null!) { ClientMapKey = full.AreasKey }.MapStateFor(p);
        Assert.True(lite.AreasOmitted);
        Assert.Empty(lite.Areas);
        Assert.Equal(full.AreasKey, lite.AreasKey);
        Assert.Equal(full.Area, lite.Area);
        Assert.Equal(full.AreaMax, lite.AreaMax);
    }

    [Fact]
    public void StaleClientKeyGetsFullAreas()
    {
        var p = new Player();
        var map = new GameService(null!) { ClientMapKey = "000000000000000000000000" }.MapStateFor(p);
        Assert.False(map.AreasOmitted);
        Assert.NotEmpty(map.Areas);
    }

    [Fact]
    public void KeyChangesWhenAreasChange()
    {
        var area = new AreaDto(1, "Sói", "Rừng", 10, 2, 1, true, 3, true);
        Assert.NotEqual(GameService.AreasKey([area]), GameService.AreasKey([area with { Win = false }]));
    }
}
