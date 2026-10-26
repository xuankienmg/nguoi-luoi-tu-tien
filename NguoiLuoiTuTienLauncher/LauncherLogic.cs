using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace VanDao.Launcher;

public class DownloadProgressReport
{
    public long DownloadedBytes { get; set; }
    public long TotalBytes { get; set; }
    public int Percent { get; set; }
    public double SpeedMBs { get; set; }
    public string Status { get; set; } = "";
}

/// <summary>Một bản phát hành game hiển thị trong Nhật ký cập nhật.</summary>
public class ReleaseNote
{
    public string Version { get; set; } = "";
    public string? Title { get; set; }
    public string? PublishedAt { get; set; }
    public string Changelog { get; set; } = "";
}

public class UpdateCheckResult
{
    // Game update info
    public bool HasUpdate { get; set; }
    public string CurrentVersion { get; set; } = "0.7.0";
    public string? LatestVersion { get; set; }
    public string? DownloadUrl { get; set; }
    public long AssetSize { get; set; }
    public string? Changelog { get; set; }
    public string? ReleaseTitle { get; set; }
    public string? PublishedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string StatusText { get; set; } = "";

    // Launcher update info
    public bool HasLauncherUpdate { get; set; }
    public string CurrentLauncherVersion { get; set; } = "1.0.0";
    public string? LatestLauncherVersion { get; set; }
    public string? LauncherDownloadUrl { get; set; }
    public long LauncherAssetSize { get; set; }
    public string? LauncherChangelog { get; set; }

    // Các bản phát hành game gần nhất (mới → cũ) cho Nhật ký cập nhật
    public List<ReleaseNote> RecentReleases { get; set; } = new();
}

public static class LauncherLogic
{
    /// <summary>Số bản phát hành game hiển thị trong Nhật ký cập nhật.</summary>
    public const int RecentReleaseCount = 2;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static LauncherLogic()
    {
        Http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NguoiLuoiTuTienLauncher", "1.0"));
    }

    /// <summary>
    /// Đọc thông tin tu sĩ từ file SQLite vandao.db trong LocalAppData
    /// </summary>
    public static (string Name, string RealmInfo) GetPlayerInfo()
    {
        try
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VanDao",
                "vandao.db"
            );

            if (!File.Exists(dbPath))
                return ("Đạo hữu", "Chưa tạo nhân vật");

            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Name, Realm, Stage FROM Players ORDER BY CreatedAt DESC LIMIT 1";

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var name = reader.GetString(0);
                var realmIndex = reader.GetInt32(1);
                var stageIndex = reader.GetInt32(2);

                var realmName = realmIndex switch
                {
                    0 => $"Luyện Khí Tầng {stageIndex + 1}",
                    1 => $"Trúc Cơ {StageName(stageIndex)}",
                    2 => $"Kim Đan {StageName(stageIndex)}",
                    3 => $"Nguyên Anh {StageName(stageIndex)}",
                    4 => $"Hóa Thần {StageName(stageIndex)}",
                    5 => $"Luyện Hư {StageName(stageIndex)}",
                    6 => $"Hợp Thể {StageName(stageIndex)}",
                    7 => $"Đại Thừa {StageName(stageIndex)}",
                    8 => $"Độ Kiếp {StageName(stageIndex)}",
                    9 => $"Chân Tiên {StageName(stageIndex)}",
                    _ => $"Cảnh Giới {realmIndex} (Kỳ {stageIndex + 1})"
                };

                return (name, realmName);
            }
        }
        catch { }

        return ("Tu Sĩ Ẩn Danh", "Luyện Khí");
    }

    private static string StageName(int s) => s switch
    {
        0 => "Sơ kỳ",
        1 => "Trung kỳ",
        2 => "Hậu kỳ",
        _ => $"Kỳ {s + 1}"
    };


    /// <summary>
    /// Kiểm tra phiên bản mới từ GitHub Releases (cả Game và Launcher)
    /// </summary>
    public static async Task<UpdateCheckResult> CheckGitHubUpdateAsync(LauncherSettings settings)
    {
        // Đồng bộ lại phiên bản thực tế trên máy từ file NguoiLuoiTuTien.exe trước khi kiểm tra
        var realGameVer = LauncherSettings.DetectGameVersion(settings.GamePath);
        if (!string.IsNullOrEmpty(realGameVer))
        {
            settings.CurrentVersion = realGameVer;
            settings.Save();
        }

        var result = new UpdateCheckResult
        {
            CurrentVersion = settings.CurrentVersion,
            CurrentLauncherVersion = settings.LauncherVersion,
            HasUpdate = false,
            HasLauncherUpdate = false
        };

        if (string.IsNullOrWhiteSpace(settings.GitHubRepo))
        {
            result.StatusText = "Chưa cấu hình GitHub Repo";
            return result;
        }

        try
        {
            var token = string.Empty; // Public releases: never distribute a GitHub credential.
            // Lấy danh sách các releases gần nhất (để nhận diện cả release của Game lẫn Launcher)
            var apiUrl = $"https://api.github.com/repos/{settings.GitHubRepo.Trim()}/releases?per_page=10";
            using var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);

            if (!string.IsNullOrWhiteSpace(token))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var res = await Http.SendAsync(req);

            if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                result.ErrorMessage = "Kho phát hành không tồn tại hoặc đang ở chế độ Riêng tư (Private). Cần dùng repository phát hành Public để người chơi tải được.";
                result.StatusText = "Không thể kiểm tra (Repo Private)";
                return result;
            }

            if (!res.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"GitHub API: {(int)res.StatusCode} {res.ReasonPhrase}";
                result.StatusText = result.ErrorMessage;
                return result;
            }

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                result.StatusText = "Không có release.";
                return result;
            }

            var vCurGame = ParseVersion(settings.CurrentVersion);
            var vCurLauncher = ParseVersion(settings.LauncherVersion);

            Version? maxFoundGameVer = null;
            var gameReleases = new List<(Version Ver, ReleaseNote Note)>();
            Version? maxFoundLauncherVer = null;

            foreach (var release in doc.RootElement.EnumerateArray())
            {
                if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
                if (release.TryGetProperty("prerelease", out var prerelease) && prerelease.GetBoolean()) continue;
                var tagName = release.TryGetProperty("tag_name", out var tn) ? (tn.GetString() ?? "") : "";
                var body = release.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "";

                // Kiểm tra xem release này có dành riêng cho Launcher không (tag bắt đầu bằng launcher-v hoặc launcher-)
                bool isLauncherRelease = tagName.StartsWith("launcher-", StringComparison.OrdinalIgnoreCase);

                if (isLauncherRelease)
                {
                    var verStr = tagName.Substring(9).TrimStart('v', 'V');
                    var ver = ParseVersion(verStr);
                    if (maxFoundLauncherVer == null || ver > maxFoundLauncherVer)
                    {
                        maxFoundLauncherVer = ver;
                        result.LatestLauncherVersion = verStr;
                        result.LauncherChangelog = body;

                        // Tìm asset zip của launcher
                        FindZipAsset(release, !string.IsNullOrWhiteSpace(token), out var url, out var size);
                        result.LauncherDownloadUrl = url;
                        result.LauncherAssetSize = size;
                    }
                }
                else
                {
                    // Release của Game (ví dụ v0.8.0)
                    var verStr = tagName.TrimStart('v', 'V');
                    var ver = ParseVersion(verStr);

                    // Kiểm tra bên trong asset của release game xem có đính kèm file Launcher*.zip không
                    if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assets.EnumerateArray())
                        {
                            var assetName = asset.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                            if (assetName.Contains("Launcher", StringComparison.OrdinalIgnoreCase) && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                // Tìm số phiên bản trong tên file nếu có, ví dụ Launcher-v1.1.0.zip
                                var match = Regex.Match(assetName, @"v?(\d+\.\d+(\.\d+)?)");
                                var lVerStr = match.Success ? match.Groups[1].Value : verStr;
                                var lVer = ParseVersion(lVerStr);

                                if (maxFoundLauncherVer == null || lVer > maxFoundLauncherVer)
                                {
                                    maxFoundLauncherVer = lVer;
                                    result.LatestLauncherVersion = lVerStr;
                                    if (!string.IsNullOrWhiteSpace(token) && asset.TryGetProperty("url", out var au) && !string.IsNullOrEmpty(au.GetString()))
                                    {
                                        result.LauncherDownloadUrl = au.GetString();
                                    }
                                    else
                                    {
                                        result.LauncherDownloadUrl = asset.TryGetProperty("browser_download_url", out var du) ? du.GetString() : null;
                                    }
                                    result.LauncherAssetSize = asset.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                                    result.LauncherChangelog = "Bản cập nhật Launcher mới đính kèm cùng phiên bản game " + tagName;
                                }
                            }
                        }
                    }

                    gameReleases.Add((ver, new ReleaseNote
                    {
                        Version = verStr,
                        Title = release.TryGetProperty("name", out var noteName) ? noteName.GetString() : tagName,
                        PublishedAt = release.TryGetProperty("published_at", out var notePublished) ? notePublished.GetString() : null,
                        Changelog = body
                    }));

                    if (maxFoundGameVer == null || ver > maxFoundGameVer)
                    {
                        maxFoundGameVer = ver;
                        result.LatestVersion = verStr;
                        result.Changelog = body;
                        result.ReleaseTitle = release.TryGetProperty("name", out var name) ? name.GetString() : tagName;
                        result.PublishedAt = release.TryGetProperty("published_at", out var published) ? published.GetString() : null;

                        // Tìm asset zip của game (không phải asset launcher)
                        FindGameZipAsset(release, !string.IsNullOrWhiteSpace(token), out var url, out var size);
                        result.DownloadUrl = url;
                        result.AssetSize = size;
                    }
                }
            }

            // Nhật ký cập nhật: 2 bản game mới nhất theo số phiên bản
            result.RecentReleases = gameReleases.OrderByDescending(r => r.Ver).Take(RecentReleaseCount).Select(r => r.Note).ToList();

            // Kết luận cập nhật Game
            if (maxFoundGameVer != null && maxFoundGameVer > vCurGame)
            {
                // Chỉ kích hoạt HasUpdate khi file zip đã thực sự có mặt trên GitHub
                if (!string.IsNullOrEmpty(result.DownloadUrl))
                {
                    result.HasUpdate = true;
                    result.StatusText = $"Đã có bản game mới: v{result.LatestVersion}";
                }
                else
                {
                    result.HasUpdate = false;
                    result.StatusText = $"Bản v{result.LatestVersion} đang được máy chủ GitHub đóng gói...";
                }

            }
            else
            {
                result.HasUpdate = false;
                result.LatestVersion ??= settings.CurrentVersion;
                result.StatusText = "Trò chơi đang ở phiên bản mới nhất";
            }

            // Kết luận cập nhật Launcher
            if (maxFoundLauncherVer != null && maxFoundLauncherVer > vCurLauncher)
            {
                result.HasLauncherUpdate = true;
            }

            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.StatusText = "Không thể kiểm tra cập nhật (offline)";
            return result;
        }
    }

    private static void FindZipAsset(JsonElement release, bool hasToken, out string? downloadUrl, out long size)
    {
        downloadUrl = null;
        size = 0;

        if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    if (hasToken && asset.TryGetProperty("url", out var u) && !string.IsNullOrEmpty(u.GetString()))
                    {
                        downloadUrl = u.GetString();
                    }
                    else
                    {
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var du) ? du.GetString() : null;
                    }
                    size = asset.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                    return;
                }
            }
        }
    }

    private static void FindGameZipAsset(JsonElement release, bool hasToken, out string? downloadUrl, out long size)
    {
        downloadUrl = null;
        size = 0;

        if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            // Ưu tiên asset .zip của game (không chứa từ Launcher)
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && !name.Contains("Launcher", StringComparison.OrdinalIgnoreCase))
                {
                    if (hasToken && asset.TryGetProperty("url", out var u) && !string.IsNullOrEmpty(u.GetString()))
                    {
                        downloadUrl = u.GetString();
                    }
                    else
                    {
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var du) ? du.GetString() : null;
                    }
                    size = asset.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                    return;
                }
            }

            // Nếu không có file riêng biệt, lấy file .zip bất kỳ trong assets
            FindZipAsset(release, hasToken, out downloadUrl, out size);
        }
    }

    private static Version ParseVersion(string? s)
    {
        var parts = (s ?? "").Trim().TrimStart('v', 'V').Split('-')[0].Split('.');
        int P(int i) => i < parts.Length && int.TryParse(parts[i], out var n) ? n : 0;
        return new Version(P(0), P(1), P(2));
    }

    /// <summary>
    /// Tải file zip với tiến trình % và giải nén đè vào thư mục game.
    /// Hỗ trợ cả download trực tiếp (public repo) lẫn GitHub API redirect S3 (private repo).
    /// </summary>
    public static async Task DownloadAndExtractAsync(
        string downloadUrl,
        string targetDir,
        string? token,
        IProgress<DownloadProgressReport> progress,
        CancellationToken ct = default)
    {
        var tempZip = Path.Combine(Path.GetTempPath(), $"vandao_game_patch_{Guid.NewGuid():N}.zip");
        try
        {
            string actualUrl = downloadUrl;
            bool sendAuthHeader = !string.IsNullOrWhiteSpace(token);

            // Xử lý download asset từ GitHub API (cho repo private)
            if (downloadUrl.Contains("api.github.com") && downloadUrl.Contains("/releases/assets/"))
            {
                using var handler = new HttpClientHandler { AllowAutoRedirect = false };
                using var redirectClient = new HttpClient(handler);
                redirectClient.DefaultRequestHeaders.Add("User-Agent", "NguoiLuoiTuTienLauncher");
                using var apiReq = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                apiReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                if (!string.IsNullOrWhiteSpace(token))
                {
                    apiReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
                }

                using var redirectRes = await redirectClient.SendAsync(apiReq, ct);
                if (redirectRes.StatusCode == System.Net.HttpStatusCode.Found || 
                    redirectRes.StatusCode == System.Net.HttpStatusCode.SeeOther ||
                    redirectRes.StatusCode == System.Net.HttpStatusCode.MovedPermanently)
                {
                    if (redirectRes.Headers.Location != null)
                    {
                        actualUrl = redirectRes.Headers.Location.ToString();
                        // Đường link S3 đã ký URL không được gửi kèm header Authorization của GitHub
                        sendAuthHeader = false;
                    }
                }
            }

            using var req = new HttpRequestMessage(HttpMethod.Get, actualUrl);
            if (sendAuthHeader)
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token!.Trim());
            }

            using var response = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            var stopwatch = Stopwatch.StartNew();
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;

                var seconds = stopwatch.Elapsed.TotalSeconds;
                var speed = seconds > 0 ? (totalRead / (1024.0 * 1024.0)) / seconds : 0;
                var percent = totalBytes > 0 ? (int)(totalRead * 100 / totalBytes) : 50;

                progress.Report(new DownloadProgressReport
                {
                    DownloadedBytes = totalRead,
                    TotalBytes = totalBytes,
                    Percent = percent,
                    SpeedMBs = Math.Round(speed, 1),
                    Status = "Đang tải dữ liệu trò chơi..."
                });
            }

            await fileStream.DisposeAsync();

            progress.Report(new DownloadProgressReport
            {
                DownloadedBytes = totalRead,
                TotalBytes = totalBytes,
                Percent = 100,
                SpeedMBs = 0,
                Status = "Đang giải nén và cập nhật tệp..."
            });

            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            // Đóng game nếu đang chạy để không bị khóa file khi giải nén đè
            try
            {
                foreach (var proc in Process.GetProcessesByName("NguoiLuoiTuTien"))
                {
                    proc.Kill();
                    proc.WaitForExit(3000);
                }
            }
            catch { }

            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(tempZip);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        var dirPath = Path.Combine(targetDir, entry.FullName);
                        Directory.CreateDirectory(dirPath);
                        continue;
                    }

                    // Không ghi đè launcher_settings.json từ gói zip game để tránh làm mất cấu hình / token người chơi
                    if (entry.Name.Equals("launcher_settings.json", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var destPath = Path.Combine(targetDir, entry.FullName);
                    var parentDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                        Directory.CreateDirectory(parentDir);

                    try
                    {
                        entry.ExtractToFile(destPath, overwrite: true);
                    }
                    catch (Exception) { }
                }
            }, ct);
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                try { File.Delete(tempZip); } catch { }
            }
        }
    }

    /// <summary>
    /// Tự động cập nhật Launcher: Tải bản zip Launcher mới, tạo script updater, ghi đè và tự khởi động lại
    /// </summary>
    public static async Task DownloadAndApplyLauncherUpdateAsync(
        string downloadUrl,
        string? token,
        IProgress<DownloadProgressReport> progress,
        CancellationToken ct = default)
    {
        var tempZip = Path.Combine(Path.GetTempPath(), $"vandao_launcher_update_{Guid.NewGuid():N}.zip");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            if (!string.IsNullOrWhiteSpace(token))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            }

            using var response = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            var stopwatch = Stopwatch.StartNew();
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;

                var seconds = stopwatch.Elapsed.TotalSeconds;
                var speed = seconds > 0 ? (totalRead / (1024.0 * 1024.0)) / seconds : 0;
                var percent = totalBytes > 0 ? (int)(totalRead * 100 / totalBytes) : 50;

                progress.Report(new DownloadProgressReport
                {
                    DownloadedBytes = totalRead,
                    TotalBytes = totalBytes,
                    Percent = percent,
                    SpeedMBs = Math.Round(speed, 1),
                    Status = "Đang tải bản cập nhật Launcher..."
                });
            }

            await fileStream.DisposeAsync();

            progress.Report(new DownloadProgressReport
            {
                DownloadedBytes = totalRead,
                TotalBytes = totalBytes,
                Percent = 100,
                SpeedMBs = 0,
                Status = "Đang chuẩn bị khởi động lại để hoàn tất..."
            });

            // Thư mục chứa Launcher hiện tại đang chạy
            var launcherDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            var extractTemp = Path.Combine(Path.GetTempPath(), $"vandao_launcher_ext_{Guid.NewGuid():N}");
            Directory.CreateDirectory(extractTemp);

            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(tempZip, extractTemp, overwriteFiles: true);
            }, ct);

            // Kiểm tra thư mục giải nén
            var sourceDir = extractTemp;
            if (Directory.GetFiles(sourceDir).Length == 0 && Directory.GetDirectories(sourceDir).Length == 1)
            {
                sourceDir = Directory.GetDirectories(sourceDir)[0];
            }

            // Tạo script batch tự động đóng launcher cũ, chép đè, và bật lại launcher mới.
            // Tên exe lấy theo file đang chạy (VanDaoLauncher.exe ở bản cài cũ, NguoiLuoiTuTienLauncher.exe từ khi đổi tên).
            var exeName = Path.GetFileName(Environment.ProcessPath) is { Length: > 0 } running ? running : "NguoiLuoiTuTienLauncher.exe";
            var updaterBat = Path.Combine(Path.GetTempPath(), $"vandao_launcher_patcher_{Guid.NewGuid():N}.bat");
            var script = $@"@echo off
chcp 65001 >nul
timeout /t 1 /nobreak >nul
:wait_process
tasklist /fi ""imagename eq {exeName}"" 2>nul | findstr /i ""{exeName}"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait_process
)
xcopy /s /e /y ""{sourceDir}\*"" ""{launcherDir}\"" >nul
rmdir /s /q ""{extractTemp}"" 2>nul
if exist ""{tempZip}"" del /f /q ""{tempZip}"" 2>nul
start """" ""{Path.Combine(launcherDir, exeName)}""
(goto) 2>nul & del ""%~f0""
";

            File.WriteAllText(updaterBat, script, System.Text.Encoding.UTF8);

            // Chạy batch script và thoát Launcher hiện tại
            var psi = new ProcessStartInfo
            {
                FileName = updaterBat,
                CreateNoWindow = true,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
            Application.Exit();
        }
        catch (Exception ex)
        {
            throw new Exception(LauncherSettings.L("Lỗi cập nhật Launcher: ", "Launcher update error: ") + ex.Message);
        }
    }

    /// <summary>
    /// Tìm file chạy của game và khởi động
    /// </summary>
    public static (bool Success, string Message) LaunchGame(string gamePath)
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(gamePath, "NguoiLuoiTuTien.exe"),
                Path.Combine(gamePath, "bin", "Release", "net8.0-windows", "NguoiLuoiTuTien.exe"),
                Path.Combine(gamePath, "bin", "Debug", "net8.0-windows", "NguoiLuoiTuTien.exe")
            };

            string? exeToRun = candidates.FirstOrDefault(File.Exists);

            if (exeToRun != null)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exeToRun,
                    WorkingDirectory = gamePath,
                    UseShellExecute = true
                };
                Process.Start(psi);
                return (true, "Khởi chạy thành công");
            }

            if (File.Exists(Path.Combine(gamePath, "VanDao.csproj")))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run",
                    WorkingDirectory = gamePath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
                return (true, "Khởi chạy qua dotnet run thành công");
            }

            return (false, LauncherSettings.L("Không tìm thấy NguoiLuoiTuTien.exe hoặc VanDao.csproj", "Couldn't find NguoiLuoiTuTien.exe or VanDao.csproj"));
        }
        catch (Exception ex)
        {
            return (false, LauncherSettings.L("Lỗi khởi chạy: ", "Launch error: ") + ex.Message);
        }
    }
}
