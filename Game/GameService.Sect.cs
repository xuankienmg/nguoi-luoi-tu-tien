using VanDao.Data;

namespace VanDao.Game;

/// <summary>Tông Môn: các hành động có đụng linh thạch / vật phẩm của nhân vật. Luật tông nằm ở ServerSect.</summary>
public partial class GameService
{
    public ServerSect Sect => new(db);

    public Task<SectPageDto> SectPageAsync(Guid id) => Sect.PageAsync(id, DateTime.UtcNow);

    public async Task<string> SectCreateAsync(Player p, string? name)
    {
        var cost = SectData.CreateCost(p.Realm);
        if (p.Count("linh_thach") < cost) throw new GameException($"Lập tông cần {cost:N0} linh thạch.");
        var clean = await Sect.CreateAsync(p.Id, name, DateTime.UtcNow);
        p.Add("linh_thach", -cost);
        AddLog(p, $"Khai sơn lập phái: Tông Môn {clean} ra đời (−{cost:N0} linh thạch).", "gold");
        return $"Đã lập Tông Môn {clean}.";
    }

    public async Task<string> SectDonateAsync(Player p, int tier)
    {
        if (tier < 0 || tier >= SectData.Donates.Length) throw new GameException("Mức quyên góp không hợp lệ.");
        var cost = SectData.DonateCost(tier, p.Realm);
        if (p.Count("linh_thach") < cost) throw new GameException($"Cần {cost:N0} linh thạch.");
        var pts = await Sect.DonateAsync(p.Id, tier, DateTime.UtcNow);
        p.Add("linh_thach", -cost);
        return $"Quyên {cost:N0} linh thạch, nhận {pts} cống hiến.";
    }

    public async Task<string> SectCheckinAsync(Player p)
    {
        var pts = await Sect.CheckinAsync(p.Id, DateTime.UtcNow);
        return $"Điểm danh tông môn, nhận {pts} cống hiến.";
    }

    public async Task<string> SectBuyAsync(Player p, string itemId, int n)
    {
        var price = await Sect.BuyAsync(p.Id, itemId, n, DateTime.UtcNow);
        p.Add(itemId, n);
        var name = GameData.ItemById.TryGetValue(itemId, out var def) ? def.Name : itemId;
        return $"Đổi {n} {name} (−{price:N0} cống hiến).";
    }
}
