using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace VanDao.Launcher;

public sealed class LauncherForm : Form
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private LauncherSettings _settings = LauncherSettings.Load();
    private CancellationTokenSource? _updateCts;
    private readonly LauncherUpdateRequest? _updateRequest;
    private bool _autoUpdatePending, _restartAfterUpdate, _startupHandled;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    private bool _dwmRoundingApplied;

    public LauncherForm(LauncherUpdateRequest? updateRequest = null)
    {
        _updateRequest = updateRequest;
        _autoUpdatePending = _restartAfterUpdate = updateRequest is not null;
        if (updateRequest is not null) _settings.GamePath = updateRequest.GamePath;
        Text = "Người Lười Tu Tiên Launcher";
        Size = new Size(1024, 640);
        MinimumSize = new Size(960, 600);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(9, 12, 16);

        try
        {
            using var iconStream = typeof(LauncherForm).Assembly.GetManifestResourceStream("VanDao.Launcher.AppIcon.ico");
            Icon = iconStream is not null
                ? new Icon(iconStream)
                : Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? (Icon)SystemIcons.Application.Clone();
        }
        catch { }

        Controls.Add(_web);
        Load += async (_, _) => await InitWebViewAsync();
        Shown += (_, _) => ApplyRoundedCorners();
        Resize += (_, _) => { if (!_dwmRoundingApplied) ApplyRoundedCornersFallback(); };
    }

    private void ApplyRoundedCorners()
    {
        // Try Windows 11 DWM API first (build 22000+)
        try
        {
            int preference = DWMWCP_ROUND;
            int result = DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            if (result == 0)
            {
                _dwmRoundingApplied = true;
                return; // Success on Win11
            }
        }
        catch { }

        // Fallback: use region-based rounding for Win10
        ApplyRoundedCornersFallback();
    }

    private void ApplyRoundedCornersFallback()
    {
        try
        {
            var rgn = CreateRoundRectRgn(0, 0, Width, Height, 16, 16);
            SetWindowRgn(Handle, rgn, true);
        }
        catch { }
    }

    private async Task InitWebViewAsync()
    {
        try
        {
            var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VanDao", "LauncherWebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, dataDir);
            await _web.EnsureCoreWebView2Async(env);

            var s = _web.CoreWebView2.Settings;
            s.IsStatusBarEnabled = false;
            s.IsZoomControlEnabled = false;
            s.AreBrowserAcceleratorKeysEnabled = false;

            _web.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _web.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
            };

            // Tìm thư mục wwwroot cho UI Launcher
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                // Khi chạy từ thư mục gốc của game
                Path.Combine(baseDir, "NguoiLuoiTuTienLauncher", "wwwroot"),
                // Bản cài cũ: giao diện Launcher còn ở thư mục VanDaoLauncher (trước khi đổi tên)
                Path.Combine(baseDir, "VanDaoLauncher", "wwwroot"),
                // Khi chạy trực tiếp bên trong thư mục NguoiLuoiTuTienLauncher
                Path.Combine(baseDir, "wwwroot"),
                // Khi chạy từ Visual Studio / build output
                Path.GetFullPath(Path.Combine(baseDir, "..", "wwwroot")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "wwwroot")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "wwwroot"))
            };

            var wwwrootDir = candidates.FirstOrDefault(Directory.Exists)
                ?? Path.Combine(baseDir, "NguoiLuoiTuTienLauncher", "wwwroot");

            _web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "vandaolauncher.local",
                wwwrootDir,
                CoreWebView2HostResourceAccessKind.Allow
            );

            _web.CoreWebView2.Navigate("https://vandaolauncher.local/index.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show(LauncherSettings.L("Lỗi khởi tạo WebView2: ", "WebView2 failed to start: ") + ex.Message, LauncherSettings.L("Lỗi Launcher", "Launcher error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            switch (type)
            {
                case "drag":
                    Invoke(() =>
                    {
                        ReleaseCapture();
                        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                    });
                    break;

                case "minimize":
                    Invoke(() => WindowState = FormWindowState.Minimized);
                    break;

                case "close":
                    Invoke(Close);
                    break;

                case "get_init_data":
                    SendInitData();
                    break;

                case "check_update":
                    _ = CheckUpdateAsync();
                    break;

                case "start_update":
                    var downloadUrl = root.TryGetProperty("downloadUrl", out var du) ? du.GetString() ?? "" : "";
                    var newVersion = root.TryGetProperty("newVersion", out var nv) ? nv.GetString() : null;
                    _ = StartUpdateAsync(downloadUrl, newVersion);
                    break;

                case "start_launcher_update":
                    var lUrl = root.GetProperty("downloadUrl").GetString() ?? "";
                    _ = StartLauncherUpdateAsync(lUrl);
                    break;

                case "launch_game":
                    LaunchGameProcess();
                    break;

                case "save_settings":
                    SaveSettingsFromJs(root.GetProperty("settings"));
                    break;

                case "browse_folder":
                    BrowseFolder();
                    break;

                case "online_mode":
                    LauncherOnline.SetPlayMode(root.GetProperty("mode").GetString() ?? "Offline");
                    SendOnlineState();
                    break;

                case "online_session":
                    _ = OnlineSessionAsync(
                        root.GetProperty("action").GetString() ?? "",
                        root.GetProperty("email").GetString() ?? "",
                        root.TryGetProperty("password", out var pw) ? pw.GetString() : null,
                        !root.TryGetProperty("remember", out var rm) || rm.ValueKind != JsonValueKind.False);
                    break;

                case "online_signout":
                    _ = SignOutAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lỗi xử lý message: {ex.Message}");
        }
    }

    private void SendInitData()
    {
        var (playerName, realmInfo) = LauncherLogic.GetPlayerInfo();
        var data = new
        {
            type = "init_data",
            settings = _settings,
            player = new { name = playerName, realm = realmInfo }
        };
        SendToJs(data);
        SendOnlineState();

        if (!_startupHandled && (_settings.AutoCheckOnStartup || _autoUpdatePending))
        {
            _startupHandled = true;
            _ = CheckUpdateAsync();
        }
    }

    /// <summary>Thẻ Tài khoản: có máy chủ không, chế độ chơi đang chọn, email đã đăng nhập (không gửi token cho trang).</summary>
    private void SendOnlineState() => SendToJs(new
    {
        type = "online_state",
        available = LauncherOnline.BackendUrl(_settings.GamePath).Length > 0,
        mode = LauncherOnline.PlayMode,
        email = LauncherOnline.SignedInEmail()
    });

    private async Task OnlineSessionAsync(string action, string email, string? password, bool remember)
    {
        if (action is not ("signin" or "signup" or "recover" or "resend")) return;
        var (status, body) = await LauncherOnline.SessionAsync(LauncherOnline.BackendUrl(_settings.GamePath), action, email, password, remember);
        SendToJs(new { type = "online_result", action, status, body });
        SendOnlineState();
    }

    private async Task SignOutAsync()
    {
        await LauncherOnline.SignOutAsync(LauncherOnline.BackendUrl(_settings.GamePath));
        SendOnlineState();
    }

    private async Task CheckUpdateAsync()
    {
        SendToJs(new { type = "status_update", status = "checking", text = "Đang kiểm tra GitHub Releases..." });

        var check = await LauncherLogic.CheckGitHubUpdateAsync(_settings);

        SendToJs(new
        {
            type = "update_checked",
            hasUpdate = check.HasUpdate,
            currentVersion = check.CurrentVersion,
            latestVersion = check.LatestVersion,
            downloadUrl = check.DownloadUrl,
            assetSize = check.AssetSize,
            changelog = check.Changelog,
            releaseTitle = check.ReleaseTitle,
            publishedAt = check.PublishedAt,
            statusText = check.StatusText,
            errorMessage = check.ErrorMessage,

            // Cập nhật Launcher
            hasLauncherUpdate = check.HasLauncherUpdate,
            currentLauncherVersion = check.CurrentLauncherVersion,
            latestLauncherVersion = check.LatestLauncherVersion,
            launcherDownloadUrl = check.LauncherDownloadUrl,
            launcherAssetSize = check.LauncherAssetSize,
            launcherChangelog = check.LauncherChangelog,

            // Nhật ký cập nhật: các bản game gần nhất
            recentReleases = check.RecentReleases
        });
        if (_autoUpdatePending)
        {
            _autoUpdatePending = false;
            if (check.HasUpdate && !string.IsNullOrEmpty(check.DownloadUrl))
                await StartUpdateAsync(check.DownloadUrl, check.LatestVersion);
            // No asset or a failed check: keep the launcher visible so the player can retry.
        }
    }

    private async Task StartUpdateAsync(string downloadUrl, string? newVersion)
    {
        if (_updateCts is not null) return;
        if (string.IsNullOrEmpty(downloadUrl))
        {
            SendToJs(new { type = "update_error", message = "Không tìm thấy đường link tải file zip trong release." });
            return;
        }

        _updateCts = new CancellationTokenSource();
        var progress = new Progress<DownloadProgressReport>(report =>
        {
            SendToJs(new
            {
                type = "download_progress",
                percent = report.Percent,
                speed = report.SpeedMBs,
                status = report.Status,
                downloaded = report.DownloadedBytes,
                total = report.TotalBytes
            });
        });

        try
        {
            // The game closes gracefully after handing off; never overwrite its executable while it is running.
            if (_updateRequest is not null)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await _updateRequest.WaitForGameExitAsync(timeout.Token);
            }
            await LauncherLogic.DownloadAndExtractAsync(
                downloadUrl,
                _settings.GamePath,
                null,
                progress,
                _updateCts.Token
            );

            if (!string.IsNullOrEmpty(newVersion))
            {
                _settings.CurrentVersion = newVersion;
                _settings.Save();
            }

            SendToJs(new
            {
                type = "update_finished",
                success = true,
                currentVersion = _settings.CurrentVersion,
                message = "Cập nhật game hoàn tất! Giờ bạn có thể vào game."
            });
            if (_restartAfterUpdate)
            {
                _restartAfterUpdate = false;
                LaunchGameProcess();
            }
        }
        catch (Exception ex)
        {
            SendToJs(new
            {
                type = "update_finished",
                success = false,
                message = $"Lỗi khi tải hoặc giải nén game: {ex.Message}"
            });
        }
        finally
        {
            _updateCts.Dispose();
            _updateCts = null;
        }
    }

    private async Task StartLauncherUpdateAsync(string downloadUrl)
    {
        if (string.IsNullOrEmpty(downloadUrl))
        {
            SendToJs(new { type = "launcher_update_error", message = "Không tìm thấy đường link tải bản cập nhật Launcher." });
            return;
        }

        _updateCts = new CancellationTokenSource();
        var progress = new Progress<DownloadProgressReport>(report =>
        {
            SendToJs(new
            {
                type = "launcher_download_progress",
                percent = report.Percent,
                speed = report.SpeedMBs,
                status = report.Status
            });
        });

        try
        {
            await LauncherLogic.DownloadAndApplyLauncherUpdateAsync(
                downloadUrl,
                null,
                progress,
                _updateCts.Token
            );
        }
        catch (Exception ex)
        {
            SendToJs(new
            {
                type = "launcher_update_error",
                message = ex.Message
            });
            MessageBox.Show(ex.Message, LauncherSettings.L("Lỗi cập nhật Launcher", "Launcher update error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LaunchGameProcess()
    {
        var (success, msg) = LauncherLogic.LaunchGame(_settings.GamePath);
        if (success)
        {
            SendToJs(new { type = "game_launched", success = true });
            if (_settings.CloseLauncherOnPlay)
            {
                Hide();
                Application.Exit();
            }
            else
            {
                WindowState = FormWindowState.Minimized;
            }
        }
        else
        {
            SendToJs(new { type = "game_launch_failed", message = msg });
            MessageBox.Show(msg, LauncherSettings.L("Lỗi khởi chạy game", "Game launch error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SaveSettingsFromJs(JsonElement s)
    {
        if (s.TryGetProperty("gamePath", out var gp)) _settings.GamePath = gp.GetString() ?? _settings.GamePath;
        if (s.TryGetProperty("gitHubRepo", out var gr)) _settings.GitHubRepo = gr.GetString() ?? _settings.GitHubRepo;
        if (s.TryGetProperty("autoCheckOnStartup", out var ac)) _settings.AutoCheckOnStartup = ac.GetBoolean();
        if (s.TryGetProperty("closeLauncherOnPlay", out var cl)) _settings.CloseLauncherOnPlay = cl.GetBoolean();
        if (s.TryGetProperty("currentVersion", out var cv)) _settings.CurrentVersion = cv.GetString() ?? _settings.CurrentVersion;
        if (s.TryGetProperty("launcherVersion", out var lv)) _settings.LauncherVersion = lv.GetString() ?? _settings.LauncherVersion;
        if (s.TryGetProperty("theme", out var th)) _settings.Theme = th.GetString() ?? _settings.Theme;
        if (s.TryGetProperty("language", out var lg) && lg.GetString() is { } lang) _settings.SetLanguage(lang);

        _settings.Save();
        SendToJs(new { type = "settings_saved", settings = _settings });
        SendOnlineState();   // đổi thư mục game có thể đổi máy chủ online
    }

    private void BrowseFolder()
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = LauncherSettings.L("Chọn thư mục cài đặt game Người Lười Tu Tiên", "Choose the Người Lười Tu Tiên game folder"),
            SelectedPath = _settings.GamePath
        };

        if (fbd.ShowDialog() == DialogResult.OK)
        {
            _settings.GamePath = fbd.SelectedPath;
            _settings.Save();
            SendToJs(new { type = "settings_saved", settings = _settings });
            SendOnlineState();   // đổi thư mục game có thể đổi máy chủ online
        }
    }

    private void SendToJs(object obj)
    {
        try
        {
            if (_web.CoreWebView2 != null)
            {
                var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                Invoke(() => _web.CoreWebView2.PostWebMessageAsJson(json));
            }
        }
        catch { }
    }
}
