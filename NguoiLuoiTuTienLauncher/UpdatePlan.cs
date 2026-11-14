namespace VanDao.Launcher;

/// <summary>Một gói tải trong release (đường dẫn tải + kích thước).</summary>
public sealed record PackAsset(string Name, string Url, long Size);

/// <summary>
/// Chọn gói cần tải khi cập nhật game. Release đăng 3 loại gói:
/// <c>VanDao_vX.Y.Z.zip</c> (đầy đủ, cho Launcher cũ / cài mới),
/// <c>VanDao_core_vX.Y.Z.vdpack</c> (mọi thứ trừ ảnh) và
/// <c>VanDao_assets_&lt;hash&gt;.vdpack</c> (wwwroot/img). Gói ảnh có hash nội dung nên chỉ tải lại khi ảnh đổi.
/// Đuôi .vdpack vẫn là zip nhưng Launcher đời cũ (chỉ tìm *.zip) bỏ qua, không tải nhầm.
/// </summary>
public static class UpdatePlan
{
    public const string CorePrefix = "VanDao_core_";
    public const string AssetsPrefix = "VanDao_assets_";
    public const string PackExtension = ".vdpack";
    /// <summary>File đánh dấu hash ảnh đã cài, nằm trong gói ảnh (và gói đầy đủ) tại wwwroot/img/.</summary>
    public const string HashMarkerRelPath = "wwwroot/img/assets-hash.txt";

    public static bool IsCorePack(string name) =>
        name.StartsWith(CorePrefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(PackExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Hash trong tên gói ảnh (16 ký tự hex), null nếu không phải gói ảnh hợp lệ.</summary>
    public static string? AssetsHashFromName(string name)
    {
        if (!name.StartsWith(AssetsPrefix, StringComparison.OrdinalIgnoreCase) || !name.EndsWith(PackExtension, StringComparison.OrdinalIgnoreCase))
            return null;
        var hash = name[AssetsPrefix.Length..^PackExtension.Length];
        return hash.Length == 16 && hash.All(Uri.IsHexDigit) ? hash.ToLowerInvariant() : null;
    }

    /// <summary>Đọc hash ảnh đã cài trên máy; null nếu chưa có (cài từ bản cũ) hoặc đọc lỗi.</summary>
    public static string? ReadLocalAssetsHash(string gamePath)
    {
        try
        {
            var path = Path.Combine(gamePath, HashMarkerRelPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path).Trim().ToLowerInvariant() : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Danh sách gói cần tải theo thứ tự. Có đủ gói core + ảnh: tải core, và chỉ tải ảnh khi hash trên máy khác.
    /// Thiếu một trong hai (release cũ): tải gói đầy đủ. Không có gì: rỗng.
    /// </summary>
    public static IReadOnlyList<PackAsset> Choose(PackAsset? core, PackAsset? assets, string? assetsHash, PackAsset? full, string? localAssetsHash)
    {
        if (core is null || assets is null || string.IsNullOrEmpty(assetsHash))
            return full is null ? [] : [full];
        var needAssets = !string.Equals(assetsHash, localAssetsHash, StringComparison.OrdinalIgnoreCase);
        return needAssets ? [core, assets] : [core];
    }
}
