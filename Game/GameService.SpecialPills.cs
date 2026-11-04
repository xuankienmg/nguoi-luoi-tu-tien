using VanDao.Data;

namespace VanDao.Game;

/// <summary>Dùng Hộ Đạo Đan và Bàn Đào Trường Sinh Đan (dữ liệu: SpecialPillData).</summary>
public partial class GameService
{
    private (string msg, string kind) UseSpecialPill(Player p, ItemDef def, int n)
    {
        if (p.Realm < def.MinRealm)
            throw new GameException($"{def.Name} chỉ dùng được từ cảnh giới {GameData.Realms[def.MinRealm].Name}.");
        if (n < 0) throw new GameException("Số lượng không hợp lệ.");
        if (def.Id == SpecialPillData.HoDao)
        {
            if (SpecialPillData.HoDaoActive(p)) throw new GameException("Hộ Đạo Đan đang có hiệu lực, chưa cần dùng thêm.");
            SpecialPillData.SetHoDao(p, true);
            p.Add(def.Id, -1);
            return Log(p, $"Dùng {def.Name}: lần đột phá thất bại kế tiếp sẽ không tổn tu vi và không tẩu hỏa nhập ma.", "good");
        }
        var used = SpecialPillData.BanDaoUsed(p);
        var room = SpecialPillData.MaxBanDao - used;
        if (room <= 0) throw new GameException($"Đã dùng tối đa {SpecialPillData.MaxBanDao} viên {def.Name}.");
        var take = Math.Min(room, n == 0 ? p.CountInt(def.Id) : Math.Min(n, p.CountInt(def.Id)));
        SpecialPillData.SetBanDao(p, used + take);
        p.Add(def.Id, -take);
        return Log(p, $"Dùng {(take > 1 ? $"{take} " : "")}{def.Name}: máu và công +{SpecialPillData.BanDaoBonus(p):0.#}% vĩnh viễn ({used + take}/{SpecialPillData.MaxBanDao} viên).", "gold");
    }
}
