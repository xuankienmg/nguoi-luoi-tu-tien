using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Game;

public record DonateRedeemRequest(string? Code, Guid Player);

public record DonateCodeDto(string Code, int Amount, long Vnd, string Note, DateTime CreatedAt, DateTime? UsedAt, Guid? UsedByPlayer, bool Offline);

/// <summary>Code donate (chế độ Server, bảng game.DonateCodes): 1.000đ = 1 Cực Phẩm Linh Thạch, mỗi code dùng một lần.</summary>
public sealed class DonateStore(GameDbContext db)
{
    public const string Prefix = "NLTT-";
    public const long VndPerStone = 1000, MaxVnd = 10_000_000;
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";   // bỏ 0/O/1/I dễ nhầm

    public static bool IsDonateCode(string? code) => (code ?? "").Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    private static string NewCode()
    {
        var s = new char[8];
        for (var i = 0; i < s.Length; i++) s[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return $"{Prefix}{new string(s, 0, 4)}-{new string(s, 4, 4)}";
    }

    /// <summary>Tạo code mới từ số tiền VND (bội của 1.000đ).</summary>
    public async Task<DonateCodeDto> CreateAsync(long vnd, string? note, Guid admin)
    {
        if (vnd <= 0 || vnd % VndPerStone != 0 || vnd > MaxVnd) throw new GameException($"Số tiền phải là bội của 1.000đ, tối đa {MaxVnd.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))}đ.");
        var n = (note ?? "").Trim();
        var row = new DonateCode { Code = NewCode(), Amount = (int)(vnd / VndPerStone), Vnd = vnd, Note = n[..Math.Min(100, n.Length)], CreatedBy = admin, CreatedAt = DateTime.UtcNow };
        db.DonateCodes.Add(row);
        await db.SaveChangesAsync();
        return Dto(row);
    }

    /// <summary>Danh sách code mới nhất trước.</summary>
    public async Task<List<DonateCodeDto>> ListAsync(int take = 50) =>
        (await db.DonateCodes.AsNoTracking().OrderByDescending(c => c.CreatedAt).Take(Math.Clamp(take, 1, 200)).ToListAsync()).Select(Dto).ToList();

    /// <summary>Đánh dấu đã dùng bằng một câu UPDATE có điều kiện (hai người nhập cùng lúc chỉ một người được). Trả về số Cực Phẩm, null = sai hoặc đã dùng.</summary>
    public async Task<int?> ClaimAsync(string? code, Guid player, Guid? owner, bool offline)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        if (c.Length == 0 || c.Length > 20) return null;
        var now = DateTime.UtcNow;
        var n = await db.DonateCodes.Where(x => x.Code == c && x.UsedAt == null).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.UsedAt, now).SetProperty(x => x.UsedByPlayer, player).SetProperty(x => x.UsedByOwner, owner).SetProperty(x => x.Offline, offline));
        if (n == 0)
            // bản offline mất phản hồi (timeout, rớt mạng) rồi nhập lại: trả lại số cũ cho đúng nhân vật đó, save offline tự chặn cộng hai lần
            return offline
                ? await db.DonateCodes.Where(x => x.Code == c && x.Offline && x.UsedByPlayer == player).Select(x => (int?)x.Amount).SingleOrDefaultAsync()
                : null;
        return await db.DonateCodes.Where(x => x.Code == c).Select(x => x.Amount).SingleAsync();
    }

    private static DonateCodeDto Dto(DonateCode c) => new(c.Code, c.Amount, c.Vnd, c.Note, c.CreatedAt, c.UsedAt, c.UsedByPlayer, c.Offline);
}
