using VanDao.Game;
using VanDao.Data;

namespace GemSystemTests;

public class XichDiemTimingTests
{
    [Fact]
    public void FullAnimationExtendsOnlyTheActualUltimateTurn()
    {
        var template = GameService.BattleAt(new Player(), 1, new DateTime(2026,11,5,0,0,0,DateTimeKind.Utc));
        var normal = template with { Hits=2, Waves=null, Extra=null, Pet=null, Casts=new() };
        var ultimate = normal with { Casts=new() { new(0,"xich_diem_thien","Xích Diễm Thiên","ultimate",100) } };
        Assert.Equal(3, BattleTiming.CombatSeconds(normal), 8);
        Assert.Equal(60d/30+.25+2, BattleTiming.CombatSeconds(ultimate), 8);
        var duplicate = ultimate with { Casts=new(ultimate.Casts) { new(0,"xich_diem_thien","","",50) } };
        Assert.Equal(BattleTiming.CombatSeconds(ultimate), BattleTiming.CombatSeconds(duplicate));
        var zero = normal with { Casts=new() { new(0,"xich_diem_thien","","",0) } };
        Assert.Equal(BattleTiming.CombatSeconds(ultimate), BattleTiming.CombatSeconds(zero));
    }
}
