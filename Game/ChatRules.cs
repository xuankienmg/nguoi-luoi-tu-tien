using System.Globalization;
using System.Text;

namespace VanDao.Game;

/// <summary>Kênh Thế giới: luật chung (độ dài, hồi, cảnh giới), không phụ thuộc DB.</summary>
public static class ChatRules
{
    public const int MaxLen = 120;
    public const int SystemMaxLen = 300;
    public const int AnnouncementMaxLen = 280;
    public const string AdminPrefix = "[Admin] ";
    /// <summary>Banner chạy chữ: tin hệ thống thường hiện trong 2 phút sau khi có; thông báo Admin 2 giờ (mỗi người chơi thấy 1 lần).</summary>
    public const int SystemBannerMinutes = 2, AdminBannerMinutes = 120;   // 11/2026: Admin 2 phút → 2 giờ
    public const int CooldownSeconds = 3;
    public const int MinRealm = 1;          // Trúc Cơ
    public const int PageSize = 50;
    public const int RetainDays = 7;
    public const int OnlineMinutes = 5;
    public const string KindPlayer = "player", KindSystem = "system";

    /// <summary>Cắt đầu/cuối, gộp khoảng trắng, bỏ ký tự điều khiển / định dạng (zero-width…), cắt MaxLen ký tự.</summary>
    public static string Clean(string? text, int maxLen = MaxLen)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sb = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text.Trim())
        {
            if (char.IsWhiteSpace(c)) { space = true; continue; }
            if (char.IsControl(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format) continue;
            if (space && sb.Length > 0) sb.Append(' ');
            space = false;
            sb.Append(c);
        }
        if (sb.Length <= maxLen) return sb.ToString();
        var cut = char.IsHighSurrogate(sb[maxLen - 1]) ? maxLen - 1 : maxLen;
        return sb.ToString(0, cut);
    }

    /// <summary>Lý do không được gửi; null = được gửi.</summary>
    public static string? LockReason(int realm, bool blocked) =>
        blocked ? "Nhân vật bị gắn cờ nên không được truyền âm."
        : realm < MinRealm ? "Đạt Trúc Cơ để truyền âm."
        : null;

    /// <summary>Số giây còn phải chờ (làm tròn lên) kể từ tin cuối.</summary>
    public static int CooldownLeft(DateTime? last, DateTime now) =>
        last is null ? 0 : Math.Max(0, (int)Math.Ceiling(CooldownSeconds - (now - last.Value).TotalSeconds));
}
