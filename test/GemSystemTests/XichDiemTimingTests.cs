using VanDao.Game;
using VanDao.Data;

namespace GemSystemTests;

public class XichDiemTimingTests
{
    static BattleDto Normal()
    {
        var template = GameService.BattleAt(new Player(), 1, new DateTime(2026,11,5,0,0,0,DateTimeKind.Utc));
        return template with { Hits=2, Waves=null, Extra=null, Pet=null, Casts=new() };
    }
    [Fact]
    public void EveryUltimateTurnReservesTwoSecondsIncludingMisses()
    {
        var normal = Normal();
        Assert.Equal(3, BattleTiming.CombatSeconds(normal), 8);
        foreach (var id in new[] { "hu_khong_bang_nguc", "xich_diem_thien", "thien_loi" })
        {
            var ult = normal with { Casts=new() { new(0,id,"","",100, Ultimate: true) } };
            Assert.Equal(4.25, BattleTiming.CombatSeconds(ult), 8);
            Assert.Equal(4.25, BattleTiming.CombatSeconds(ult with { Casts=new() { new(0,id,"","",0, Ultimate: true) } }), 8);
        }
    }
    [Fact]
    public void FullAnimationExtendsOnlyTheActualUltimateTurn()
    {
        var normal = Normal();
        var ultimate = normal with { Casts=new() { new(0,"xich_diem_thien","Xích Diễm Thiên","ultimate",100, Ultimate: true) } };
        Assert.Equal(1 + 2.25 + 1, BattleTiming.CombatSeconds(ultimate), 8);
        // Nhiều tuyệt kỹ cùng lượt chỉ giữ chỗ một lần 3 giây.
        var duplicate = ultimate with { Casts=new(ultimate.Casts) { new(0,"thien_loi","","",50, Ultimate: true) } };
        Assert.Equal(BattleTiming.CombatSeconds(ultimate), BattleTiming.CombatSeconds(duplicate));
        // Chiêu thi triển đầu trận (thủ/khống, không phải tuyệt kỹ) không kéo dài lượt.
        Assert.Equal(3, BattleTiming.CombatSeconds(normal with { Casts=new() { new(0,"huyen_bang","","",0) } }), 8);
    }
}
