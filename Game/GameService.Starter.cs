using VanDao.Data;

namespace VanDao.Game;

public partial class GameService
{
    private static StarterChoiceDto StarterChoiceFor(Player p) => new(!p.StarterChosen,
        CultivationManuals.Available,
        PetData.StarterEggs.Where(kv => PetData.EggQuality(kv.Key) is >= 0 and <= 1).Select(kv => new StarterPetOption(kv.Key, kv.Value,
            PetData.HatchSeconds[PetData.EggQuality(kv.Key)])).ToArray());

    public (string msg, string kind) ChooseStarter(Player p, string element, string egg)
    {
        if (p.StarterChosen) throw new GameException("Nhân vật đã chọn công pháp và linh thú khởi đầu.");
        var manual = CultivationManuals.Available.FirstOrDefault(m => m.Element == element)
            ?? throw new GameException("Công pháp khởi đầu không hợp lệ.");
        if (!PetData.StarterEggs.ContainsKey(egg) || PetData.EggQuality(egg) is < 0 or > 1)
            throw new GameException("Chỉ được chọn linh thú khởi đầu phẩm Phàm hoặc Linh.");
        // Validate both choices before changing inventory or permanent tree access.
        CultivationManuals.Unlock(p, element);
        p.ActiveManual = element;
        CultivationManuals.SyncActive(p);
        p.Add(egg, 1);
        p.StarterChosen = true;
        p.LastTickAt = DateTime.UtcNow;
        p.ExploreReadyAt = DateTime.UtcNow.AddSeconds(ExploreSeconds(p));
        return Log(p, $"Khởi đầu với {manual.Name} và {GameData.ItemById[egg].Name}. Đã mở cả cây công pháp; nâng kỹ năng vẫn cần điểm và cảnh giới. Đem trứng đến Linh thú để ấp.", "gold");
    }
}
