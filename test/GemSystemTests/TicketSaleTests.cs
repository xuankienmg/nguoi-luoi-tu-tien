using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class TicketSaleTests
{
    [Theory]
    [InlineData("ve_tai_xiu")]
    [InlineData("ve_linh_thu")]
    public void SpinTicketsCannotBeSoldIndividuallyOrInBulk(string id)
    {
        var p = new Player();
        p.Add(id, 3);
        var game = new GameService(null!);
        Assert.Equal(0, GameData.ItemById[id].Sell);
        Assert.Throws<GameException>(() => game.Sell(p, id, 3));
        Assert.Throws<GameException>(() => game.SellBulk(p, 9, items: id + ":3", pick: true));
        Assert.Equal(3, p.Count(id));
        Assert.Equal(0, p.Count("linh_thach"));
    }
}
