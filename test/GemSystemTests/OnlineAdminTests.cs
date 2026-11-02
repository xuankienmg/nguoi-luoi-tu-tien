using VanDao.Data;
using VanDao.Online;

namespace GemSystemTests;

public class OnlineAdminTests
{
    private static readonly Guid Admin = Guid.Parse("2154fbb6-776a-42ae-868f-f0b31edf5e65");

    [Fact]
    public void AdminUsersListsSupabaseIds()
    {
        var options = new OnlineOptions { AdminUsers = $" {Admin} , 11111111-1111-1111-1111-111111111111" };
        Assert.True(options.IsAdminUser(Admin));
        Assert.False(options.IsAdminUser(Guid.NewGuid()));
        Assert.False(new OnlineOptions().IsAdminUser(Admin));
    }

    [Fact]
    public async Task RequestAdminStaysInItsOwnRequest()
    {
        // không đổi giá trị chung: các test khác chạy song song dùng nó
        var shared = Player.AdminEnabled;
        var inRequest = await Task.Run(async () => { Player.SetRequestAdmin(!shared); await Task.Yield(); return Player.AdminEnabled; });
        Assert.Equal(!shared, inRequest);
        Assert.Equal(shared, Player.AdminEnabled);
    }
}
