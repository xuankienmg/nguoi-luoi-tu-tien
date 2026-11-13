using VanDao.Data;
namespace VanDao.Game;

/// <summary>Bảo vật Lò Rèn: máy đổi trang bị → Điểm Luyện Khí, kệ hôm nay (giờ VN), kệ bảo vật 4 ô.</summary>
public partial class GameService
{
    /// <summary>Sang ngày mới (giờ VN): số lần làm mới và món đã mua về 0.</summary>
    private static string ForgeShopRoll(ForgeData.ForgeSave s)
    {
        var day = LoginDayOf(DateTimeOffset.UtcNow);
        if (s.ShopDay != day) { s.ShopDay = day; s.Rerolls = 0; s.Bought.Clear(); }
        return day;
    }

    private static List<string> ForgeShelfOf(Player p, ForgeData.ForgeSave s) =>
        ForgeRelicData.Shelf(p.Id, s.ShopDay, s.Rerolls, s.Relics, s.Bought);

    /// <summary>items: "id" hoặc "id:số lượng" cách nhau dấu phẩy (id lặp lại thì cộng dồn). Kiểm tra hết rồi mới trừ.</summary>
    public (string msg, string kind) ForgeSalvage(Player p, string? items)
    {
        var want = new Dictionary<string, int>();
        foreach (var tok in (items ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = tok.Split(':');
            var n = 1;
            if (parts.Length > 2 || (parts.Length == 2 && (!int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out n) || n < 1)))
                throw new GameException("Số lượng đổi không hợp lệ.");
            want[parts[0]] = (int)Math.Min(int.MaxValue, (long)want.GetValueOrDefault(parts[0]) + n);
        }
        if (want.Count == 0) throw new GameException("Chưa chọn trang bị để đổi.");
        long points = 0, total = 0;
        foreach (var (id, n) in want)
        {
            var d = GameData.ItemById.GetValueOrDefault(id);
            var each = d is null ? 0 : ForgeRelicData.SalvagePoints(d);
            if (each <= 0) throw new GameException("Máy đổi chỉ nhận trang bị.");
            if (p.Count(id) < n) throw new GameException($"Không đủ {d!.Name} trong hành trang.");
            for (var i = 0; i < n; i++)   // Add(-n) sẽ bỏ các bản 0..n-1: không để mất Tinh Thạch đang khảm
                if (GemData.BagCopy(p, id, i).Sockets.Any(x => !string.IsNullOrEmpty(x)))
                    throw new GameException($"{d!.Name} đang khảm Tinh Thạch: tháo ra trước khi đổi.");
            points += each * n; total += n;
        }
        foreach (var (id, n) in want) p.Add(id, -n);
        var s = ForgeData.Load(p);
        s.Points += points;
        ForgeData.Store(p, s);
        return Log(p, $"Máy đổi: {total:N0} trang bị → +{points:N0} Điểm Luyện Khí (còn {s.Points:N0}).", "good");
    }

    public (string msg, string kind) ForgeRelicBuy(Player p, string relicId)
    {
        var def = ForgeRelicData.ById.GetValueOrDefault(relicId) ?? throw new GameException("Bảo vật không tồn tại.");
        var s = ForgeData.Load(p);
        ForgeShopRoll(s);
        if (!ForgeShelfOf(p, s).Contains(relicId)) throw new GameException("Kệ hôm nay không có bảo vật này.");
        if (s.Bought.Contains(relicId)) throw new GameException("Hôm nay đã mua bảo vật này.");
        if (s.Relics.Contains(relicId)) throw new GameException($"Kệ bảo vật đã có {def.Name}.");
        if (s.Relics.Count >= ForgeRelicData.Slots) throw new GameException($"Kệ bảo vật đã đủ {ForgeRelicData.Slots} món: bỏ bớt 1 món trước.");
        if (s.Points < def.Price) throw new GameException($"Cần {def.Price:N0} Điểm Luyện Khí (đang có {s.Points:N0}).");
        s.Points -= def.Price;
        s.Relics.Add(relicId);
        s.Bought.Add(relicId);
        ForgeData.Store(p, s);
        return Log(p, $"Lò Rèn: đặt {def.Name} lên kệ bảo vật ({def.Desc})", "gold");
    }

    public (string msg, string kind) ForgeRelicDiscard(Player p, string relicId)
    {
        var s = ForgeData.Load(p);
        if (!s.Relics.Remove(relicId)) throw new GameException("Kệ bảo vật không có món này.");
        ForgeData.Store(p, s);
        return ($"Đã bỏ {ForgeRelicData.ById.GetValueOrDefault(relicId)?.Name ?? relicId} khỏi kệ bảo vật.", "");
    }

    public (string msg, string kind) ForgeShelfReroll(Player p)
    {
        if (p.Count("linh_thach") < ForgeRelicData.RerollStone)
            throw new GameException($"Cần {ForgeRelicData.RerollStone:N0} linh thạch để làm mới kệ.");
        var s = ForgeData.Load(p);
        ForgeShopRoll(s);
        p.Add("linh_thach", -ForgeRelicData.RerollStone);
        s.Rerolls++;
        s.Bought.Clear();
        ForgeData.Store(p, s);
        return ($"Làm mới kệ bảo vật (−{ForgeRelicData.RerollStone:N0} linh thạch).", "");
    }
}
