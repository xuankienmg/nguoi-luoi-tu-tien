using VanDao.Data;
using VanDao.Game;
using Xunit;

namespace GemSystemTests;

/// <summary>Vòng quay từ Đại Thừa: bỏ ô Phàm, thêm Kim Tiên; bảo hiểm và Rương Thiên Cơ có Kim Tiên.</summary>
public class SpinKimTienTests
{
    private const int Kt = 10;   // chỉ số "kimtien" trong RarityOrder

    [Fact]
    public void BelowDaiThuaWheelIsUnchanged()
    {
        Assert.Same(GameData.SpinWeights, GameData.SpinWeightsFor(GameData.SpinKimTienRealm - 1));
        Assert.Equal(10, GameData.SpinWeights.Length);
    }

    [Fact]
    public void DaiThuaWheelDropsPhamAndAddsRareKimTien()
    {
        var w = GameData.SpinWeightsFor(GameData.SpinKimTienRealm);
        Assert.Equal("kimtien", GameData.RarityOrder[Kt]);
        Assert.Equal(11, w.Length);
        Assert.Equal(0, w[0]);
        Assert.Equal(100, w.Sum(), 6);
        Assert.Equal(.01, w[Kt], 6);
        Assert.True(w[Kt] < w[9]);
        for (var i = 1; i < 10; i++) Assert.Equal(GameData.SpinWeights[i] / GameData.SpinWeights[1], w[i] / w[1], 9);   // giữ tỉ lệ giữa các phẩm
    }

    [Fact]
    public void PityRollsMatchRealm()
    {
        var rng = new Random(7);
        var low = new HashSet<int>(); var high = new HashSet<int>();
        for (var i = 0; i < 4000; i++)
        {
            var p = SpinPity.Threshold - 1; low.Add(SpinPity.Roll(ref p, false, rng, realm: 0));
            p = SpinPity.Threshold - 1; high.Add(SpinPity.Roll(ref p, false, rng, realm: GameData.SpinKimTienRealm));
        }
        Assert.Equal(new[] { 7, 8, 9 }, low.OrderBy(x => x));
        Assert.Equal(new[] { 7, 8, 9, Kt }, high.OrderBy(x => x));
        Assert.Equal(100, GameData.SpinPityWeightsKimTien.Sum(), 6);
    }

    [Fact]
    public void NormalRollsNeverGivePhamFromDaiThua()
    {
        var rng = new Random(3);
        for (var i = 0; i < 5000; i++) { var p = 0; Assert.NotEqual(0, SpinPity.Roll(ref p, false, rng, realm: GameData.SpinKimTienRealm)); }
    }

    [Fact]
    public void EventChestFromDaiThuaGivesDaoToKimTien()
    {
        var player = new Player { Realm = GameData.SpinKimTienRealm };
        player.Add(SpinEvent.ChestItem, 400);
        var result = new GameService(null!).UseItem(player, SpinEvent.ChestItem, 0);
        var got = result.craft!.Outputs!.Select(o => GameData.ItemById[o.Item].Rarity).ToHashSet();
        Assert.Subset(new HashSet<string> { "dao", "supreme", "kimtien" }, got);
        Assert.Contains("dao", got);
    }

    [Fact]
    public void EventChestKimTienOnlyFromDaiThua()
    {
        Assert.Equal(10, GameService.EventChestWeights.Length);   // không có ô Kim Tiên dưới Đại Thừa
        Assert.Equal(100, GameService.EventChestWeights.Sum(), 6);
        Assert.Equal(100, GameService.EventChestHighWeights.Sum(), 6);
        Assert.Equal(5, GameService.EventChestHighWeights[Kt], 6);
        var player = new Player { Realm = GameData.SpinKimTienRealm - 1 };
        player.Add(SpinEvent.ChestItem, 400);
        var result = new GameService(null!).UseItem(player, SpinEvent.ChestItem, 0);
        var got = result.craft!.Outputs!.Select(o => GameData.ItemById[o.Item].Rarity).ToHashSet();
        Assert.Subset(new HashSet<string> { "dao", "supreme" }, got);
    }
}
