using System.Globalization;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>Kỳ duyên Thiên Thư: mỗi ngày 6 khung giờ cố định (giờ Việt Nam), mỗi khung mở 30 phút một kỳ duyên.
/// Bỏ lỡ khung nào thì mất kỳ duyên khung đó. Chọn một trong hai lối để nhận trang Thiên Thư, vật phẩm và Đạo Tâm.</summary>
public static partial class KyDuyen
{
    public static readonly TimeSpan Vn = TimeSpan.FromHours(7);
    // cân bằng 11/2026: 4 → 6 khung/ngày, mỗi khung 10 → 30 phút (các khung không chồng nhau)
    public static readonly int[] Hours = [8, 12, 15, 17, 19, 22];
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(30);

    public record Slot(string Key, DateTimeOffset Start, DateTimeOffset End);

    /// <summary>Khung bắt đầu lúc hour:minute ngày day (giờ Việt Nam). Khung thật luôn ở phút 0; phút khác chỉ dùng cho khung admin.</summary>
    public static Slot Make(DateTime day, int hour, int minute = 0)
    {
        var start = new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, 0, Vn);
        return new($"{start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}:{hour:00}{(minute > 0 ? $":{minute:00}" : "")}", start, start + Window);
    }

    /// <summary>Khung đang mở lúc now (null = ngoài khung).</summary>
    public static Slot? At(DateTimeOffset now)
    {
        var day = now.ToOffset(Vn).Date;
        foreach (var h in Hours)
        {
            var s = Make(day, h);
            if (now >= s.Start && now < s.End) return s;
        }
        return null;
    }

    /// <summary>Khung kế tiếp bắt đầu sau now.</summary>
    public static Slot Next(DateTimeOffset now)
    {
        var day = now.ToOffset(Vn).Date;
        foreach (var h in Hours) { var s = Make(day, h); if (s.Start > now) return s; }
        return Make(day.AddDays(1), Hours[0]);
    }

    /// <summary>Đơn vị linh thạch theo cảnh giới (giá và thưởng của kỳ duyên nhân với số này).</summary>
    public static long StoneUnit(int realm) => 50L * (long)Math.Pow(5, Math.Clamp(realm, 0, Books.Length - 1));

    /// <summary>Kỳ duyên của nhân vật ở khung này: cố định theo Id + giờ bắt đầu khung, tải lại vẫn ra cùng kỳ duyên.</summary>
    public static Encounter Pick(Guid id, Slot s) =>
        Encounters[new Random(unchecked(id.GetHashCode() * 31 + (int)(s.Start.ToUnixTimeSeconds() / 3600))).Next(Encounters.Length)];

    /// <summary>Lối chọn có thành công không: cố định theo Id + khung + lối.</summary>
    public static bool Roll(Guid id, Slot s, int choice, double chance) =>
        chance >= 1 || new Random(unchecked(id.GetHashCode() * 17 + (int)(s.Start.ToUnixTimeSeconds() / 60) + choice)).NextDouble() < chance;

    public static int[] Pages(Player p)
    {
        var a = new int[Books.Length];
        var parts = p.ThienThu.Split(',');
        for (var i = 0; i < a.Length && i < parts.Length; i++)
            a[i] = int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? Math.Clamp(n, 0, PagesPerBook) : 0;
        return a;
    }
    public static void SetPages(Player p, int[] pages) => p.ThienThu = string.Join(',', pages);

    /// <summary>Quyển nhận trang: quyển thấp nhất chưa đủ trong các quyển đã mở (quyển i mở khi cảnh giới ≥ i). -1 = không còn quyển nào nhận.</summary>
    public static int Target(int[] pages, int realm)
    {
        for (var i = 0; i <= Math.Min(realm, pages.Length - 1); i++) if (pages[i] < PagesPerBook) return i;
        return -1;
    }

    /// <summary>Cộng n trang, dư sang quyển kế. Trả về số trang đã vào sách.</summary>
    public static int AddPages(int[] pages, int realm, int n)
    {
        var added = 0;
        while (n > 0 && Target(pages, realm) is var i and >= 0)
        {
            var take = Math.Min(n, PagesPerBook - pages[i]);
            pages[i] += take; n -= take; added += take;
        }
        return added;
    }

    public static bool Complete(int[] pages) => pages.All(n => n >= PagesPerBook);

    /// <summary>Kết thúc khi đủ 10 quyển, theo Đạo Tâm hiện tại (null = chưa đủ).</summary>
    public static Ending? EndingFor(Player p) =>
        !Complete(Pages(p)) ? null : p.DaoTam >= PathAt ? Endings[0] : p.DaoTam <= -PathAt ? Endings[1] : Endings[2];

    /// <summary>Tổng chỉ số vĩnh viễn: mọi quyển đã đủ + kết thúc (nếu có).</summary>
    public static Bonus Total(Player p)
    {
        var pages = Pages(p);
        var sum = Books.Where((_, i) => pages[i] >= PagesPerBook).Aggregate(Bonus.None, (a, b) => a + b.Bonus);
        return EndingFor(p) is { } e ? sum + e.Bonus : sum;
    }
}
