using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace VanDao.Launcher;

public class LauncherSettings
{
    public string GamePath { get; set; } = "";
    public string GitHubRepo { get; set; } = "xuankienmg/nguoi-luoi-tu-tien";
    public bool AutoCheckOnStartup { get; set; } = true;
    public bool CloseLauncherOnPlay { get; set; } = true;
    public string CurrentVersion { get; set; } = "0.7.0";
    public string LauncherVersion { get; set; } = "1.0.0";
    public string Theme { get; set; } = "dark";

    /// <summary>Ngôn ngữ giao diện "vi" | "en", dùng chung với game: nguồn chính là Language trong %LocalAppData%\VanDao\window.json.</summary>
    public string Language { get; set; } = "vi";

    /// <summary>Ngôn ngữ đang dùng cho chữ native (hộp thoại, tiêu đề lỗi).</summary>
    public static string Lang { get; private set; } = "vi";

    /// <summary>Chọn chữ theo ngôn ngữ đang dùng.</summary>
    public static string L(string vi, string en) => Lang == "en" ? en : vi;

    private static string GameWindowFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VanDao", "window.json");

    /// <summary>Một cài đặt trong window.json của game, vd. Language, PlayMode (null nếu game chưa lưu cài đặt cửa sổ).</summary>
    internal static string? ReadGameSetting(string key)
    {
        try
        {
            if (!File.Exists(GameWindowFile)) return null;
            return JsonNode.Parse(File.ReadAllText(GameWindowFile))?[key]?.GetValue<string>();
        }
        catch { return null; }
    }

    private static string? ReadGameLanguage() => ReadGameSetting("Language");

    /// <summary>Ghi một cài đặt vào window.json của game, giữ nguyên các cài đặt cửa sổ khác.</summary>
    internal static void WriteGameSetting(string key, string value)
    {
        try
        {
            var root = File.Exists(GameWindowFile) ? JsonNode.Parse(File.ReadAllText(GameWindowFile)) as JsonObject : null;
            root ??= new JsonObject();
            if (root[key]?.GetValue<string>() == value) return;
            root[key] = value;
            Directory.CreateDirectory(Path.GetDirectoryName(GameWindowFile)!);
            File.WriteAllText(GameWindowFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* không ghi được cũng không chặn Launcher */ }
    }

    /// <summary>Đổi ngôn ngữ cho cả Launcher và game.</summary>
    public void SetLanguage(string lang)
    {
        Language = Lang = lang == "en" ? "en" : "vi";
        WriteGameSetting("Language", Language);
    }

    public static string GetSettingsFile()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var inSubDir = Path.Combine(baseDir, "NguoiLuoiTuTienLauncher", "launcher_settings.json");
        var inBaseDir = Path.Combine(baseDir, "launcher_settings.json");

        if (File.Exists(inSubDir)) return inSubDir;
        // Bản cài cũ (thư mục VanDaoLauncher, trước khi đổi tên): chép cài đặt sang thư mục mới để không mất đường dẫn game
        var legacy = Path.Combine(baseDir, "VanDaoLauncher", "launcher_settings.json");
        if (File.Exists(legacy))
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(inSubDir)!); File.Copy(legacy, inSubDir); return inSubDir; }
            catch { return legacy; }
        }
        if (Directory.Exists(Path.Combine(baseDir, "NguoiLuoiTuTienLauncher"))) return inSubDir;
        if (File.Exists(inBaseDir)) return inBaseDir;

        return inSubDir;
    }

    public static LauncherSettings Load()
    {
        var settingsFile = GetSettingsFile();
        try
        {
            if (File.Exists(settingsFile))
            {
                var json = File.ReadAllText(settingsFile);
                var s = JsonSerializer.Deserialize<LauncherSettings>(json);
                if (s != null)
                {
                    if (string.IsNullOrEmpty(s.GamePath))
                        s.GamePath = DetectGamePath();
                    if (string.IsNullOrEmpty(s.LauncherVersion))
                        s.LauncherVersion = "1.0.0";

                    // Luôn tự động đồng bộ phiên bản từ file exe NguoiLuoiTuTien.exe trên máy
                    var detected = DetectGameVersion(s.GamePath);
                    if (!string.IsNullOrEmpty(detected))
                    {
                        s.CurrentVersion = detected;
                        s.Save();
                    }

                    s.Language = Lang = ReadGameLanguage() ?? (s.Language == "en" ? "en" : "vi");

                    // Rewrite legacy settings without any stored GitHub token.
                    s.Save();
                    return s;
                }
            }
        }
        catch { }

        var settings = new LauncherSettings { GamePath = DetectGamePath() };
        settings.CurrentVersion = DetectGameVersion(settings.GamePath);
        settings.Language = Lang = ReadGameLanguage() ?? "vi";
        settings.Save();
        return settings;
    }

    public void Save()
    {
        try
        {
            var settingsFile = GetSettingsFile();
            var dir = Path.GetDirectoryName(settingsFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsFile, json);
        }
        catch { }
    }

    public static string DetectGameVersion(string gamePath)
    {
        try
        {
            // 1. Kiểm tra trực tiếp file exe NguoiLuoiTuTien.exe (chính xác nhất cho người chơi)
            var exeCandidates = new[]
            {
                Path.Combine(gamePath, "NguoiLuoiTuTien.exe"),
                Path.Combine(gamePath, "bin", "Release", "net8.0-windows", "NguoiLuoiTuTien.exe"),
                Path.Combine(gamePath, "bin", "Debug", "net8.0-windows", "NguoiLuoiTuTien.exe")
            };

            foreach (var exe in exeCandidates)
            {
                if (File.Exists(exe))
                {
                    var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);
                    if (!string.IsNullOrWhiteSpace(vi.ProductVersion))
                    {
                        var clean = vi.ProductVersion.Split('+')[0].Trim();
                        if (!string.IsNullOrEmpty(clean)) return clean;
                    }
                    if (!string.IsNullOrWhiteSpace(vi.FileVersion))
                    {
                        var parts = vi.FileVersion.Split('.');
                        if (parts.Length >= 3)
                        {
                            return $"{parts[0]}.{parts[1]}.{parts[2]}";
                        }
                        return vi.FileVersion.Trim();
                    }
                }
            }

            // 2. Fallback kiểm tra VanDao.csproj nếu ở môi trường dev
            var csproj = Path.Combine(gamePath, "VanDao.csproj");
            if (File.Exists(csproj))
            {
                var content = File.ReadAllText(csproj);
                var match = Regex.Match(content, @"<Version>(.*?)</Version>");
                if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                {
                    return match.Groups[1].Value.Trim();
                }
            }
        }
        catch { }
        return "0.7.2";
    }

    public static string DetectGamePath()
    {
        // 1. Tìm ngược lên từ thư mục chạy của Launcher
        var current = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(current);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NguoiLuoiTuTien.exe")) ||
                File.Exists(Path.Combine(dir.FullName, "VanDao.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        // 2. Dự phòng các đường dẫn mặc định
        var candidates = new[]
        {
            @"D:\Game VanDao",
            @"C:\Game VanDao",
            Path.Combine(Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory) ?? @"D:\", "Game VanDao")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(Path.Combine(path, "NguoiLuoiTuTien.exe")) ||
                File.Exists(Path.Combine(path, "VanDao.csproj")))
                return path;
        }

        return @"D:\Game VanDao";
    }
}
