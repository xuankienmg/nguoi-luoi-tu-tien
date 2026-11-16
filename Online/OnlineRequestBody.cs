using System.IO.Compression;
using System.Text;

namespace VanDao.Online;

/// <summary>
/// Phản hồi lưu trong game."OnlineRequests".Response (để phát lại khi client gửi lại cùng Idempotency-Key).
/// 11/2026: nén gzip + base64 trước khi ghi ("gz1:..."): trạng thái nhân vật ~259 KB còn ~45 KB mỗi hành động,
/// giảm băng thông máy chủ → Supabase. Dòng cũ (JSON thường) vẫn đọc được nên không cần migration.
/// </summary>
public static class OnlineRequestBody
{
    private const string Prefix = "gz1:";

    public static string Pack(ReadOnlySpan<byte> utf8)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(utf8);
        return Prefix + Convert.ToBase64String(output.GetBuffer(), 0, (int)output.Length);
    }

    public static string Unpack(string stored)
    {
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal)) return stored;
        using var input = new MemoryStream(Convert.FromBase64String(stored[Prefix.Length..]));
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
