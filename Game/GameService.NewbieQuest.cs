using VanDao.Data;
namespace VanDao.Game;

/// <summary>Nhiệm vụ tân thủ (NewbieQuestData): chuỗi tuần tự, nhận quà từng bước.</summary>
public partial class GameService
{
    /// <summary>Bước hiện tại đã kẹp vào [0, Total]: dữ liệu hỏng (âm) không làm nhận lại quà nhiệm vụ đầu.</summary>
    private static int NewbieQuestStepOf(Player p) => Math.Clamp(p.NewbieQuestStep, 0, NewbieQuestData.Total);

    private static NewbieQuestData.QuestDef? CurrentNewbieQuest(Player p)
    {
        var step = NewbieQuestStepOf(p);
        return step < NewbieQuestData.Total ? NewbieQuestData.Quests[step] : null;
    }

    private static (int progress, int need) NewbieQuestProgress(Player p, NewbieQuestData.QuestDef q) =>
        q.CountEvent is not null ? (Math.Min(p.NewbieQuestCount, q.CountNeed), q.CountNeed) : (q.Check!(p) ? 1 : 0, 1);

    /// <summary>Trạng thái nhiệm vụ đang mở; null khi đã xong chuỗi. Chỉ đọc, không sửa Player.</summary>
    public NewbieQuestDto? NewbieQuestStateFor(Player p)
    {
        if (CurrentNewbieQuest(p) is not { } q) return null;
        var (progress, need) = NewbieQuestProgress(p, q);
        return new(NewbieQuestStepOf(p), NewbieQuestData.Total, q.Id, q.Title, q.Hint, q.Tab, progress, need, progress >= need,
            q.Rewards.Select(r => new ItemQtyDto(r.Item, r.Qty)).ToList());
    }

    /// <summary>Nhận quà nhiệm vụ đang mở và chuyển sang nhiệm vụ kế tiếp (cùng một lần lưu nên không nhận trùng).</summary>
    public (string, string) ClaimNewbieQuest(Player p)
    {
        if (CurrentNewbieQuest(p) is not { } q) throw new GameException("Đã hoàn thành nhiệm vụ tân thủ.");
        var (progress, need) = NewbieQuestProgress(p, q);
        if (progress < need) throw new GameException("Chưa hoàn thành nhiệm vụ.");
        var bagNeed = q.Rewards.Select(r => r.Item).Distinct().Count(i => !IsCurrency(i) && p.Count(i) == 0);
        if (BagCap(p) - BagUsed(p) < bagNeed) throw new GameException($"Hành trang cần {bagNeed} ô trống để nhận quà.");
        foreach (var r in q.Rewards) p.Add(r.Item, r.Qty);
        var step = NewbieQuestStepOf(p);
        p.NewbieQuestStep = step + 1;
        p.NewbieQuestCount = 0;
        return Log(p, $"Nhiệm vụ tân thủ {step + 1}/{NewbieQuestData.Total} «{q.Title}»: nhận {string.Join(", ", q.Rewards.Select(r => $"{r.Qty} {GameData.ItemById[r.Item].Name}"))}.", "gold");
    }

    /// <summary>Gọi sau khi hành động thành công; chỉ tăng bộ đếm khi nhiệm vụ đang mở cần đúng sự kiện này.</summary>
    public static void RecordQuestEvent(Player p, string ev)
    {
        if (CurrentNewbieQuest(p) is { CountEvent: { } need } && need == ev) p.NewbieQuestCount++;
    }
}
