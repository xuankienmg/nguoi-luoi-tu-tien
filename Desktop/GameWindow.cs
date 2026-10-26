using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace VanDao.Desktop;

/// <summary>
/// Dải game kiểu "TBH: Task Bar Hero": một cửa sổ ngang, không viền, nền đặc,
/// nằm sát ngay trên thanh taskbar và luôn hiển thị khi làm việc khác.
///
/// - Chuột phải (hoặc nút ⋯): menu nhanh — bố cục màn hình, kích thước, ghim.
/// - Kéo ⋮⋮ để đặt tự do; chọn lại một bố cục để neo về taskbar.
/// - Nút "Tu luyện": mở Đột phá / Hành trang / Đan dược / Bản đồ trong cửa sổ bật lên phía trên dải.
/// - Shift+F12: đặt lại vị trí khi bị lạc. Shift+F11: đặt lại kích thước.
/// - Biểu tượng ở khay hệ thống: ẩn/hiện và thoát.
/// </summary>
public sealed class GameWindow : Form
{
    /// <summary>Kích thước 100% (bằng 150% của bản cũ 280×200).</summary>
    private static readonly Size BaseSize = new(420, 300);
    private const int HotkeyResetPos = 1, HotkeyResetScale = 2;

    private readonly string _url;
    private readonly WindowSettings _s = WindowSettings.Load();
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly ContextMenuStrip _menu = new() { Renderer = Theme.Renderer, ShowImageMargin = true };
    private readonly NotifyIcon _tray = new() { Text = "Người Lười Tu Tiên" };
    private readonly System.Windows.Forms.Timer _dockTimer = new() { Interval = 2000 };
    private readonly System.Windows.Forms.Timer _revealTimer = new() { Interval = 10000 };
    private PanelWindow? _panel;
    private PanelWindow? _rankPanel;
    private bool _placing, _revealed;

    public GameWindow(string serverUrl)
    {
        _url = serverUrl.TrimEnd('/') + "/";

        Text = "Người Lười Tu Tiên";
        using (var iconStream = typeof(GameWindow).Assembly.GetManifestResourceStream("VanDao.AppIcon.ico"))
            Icon = iconStream is not null
                ? new Icon(iconStream)
                : Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? (Icon)SystemIcons.Application.Clone();
        _tray.Icon = Icon;
        _tray.Visible = true;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = true;
        Controls.Add(_web);

        BuildMenu();
        _tray.ContextMenuStrip = _menu;
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ToggleVisible(); };

        ApplySize();
        ApplyBackground();
        ApplyPin();
        Opacity = 1.0; // độ trong cố định 100%
        PlaceStrip();

        // ẩn hẳn (region rỗng) tới khi trang có dữ liệu thật và vẽ xong khung đầu,
        // tránh lúc mở hiện khối đen rồi khung "Tầng 1" trống rồi mới nhảy sang trạng thái thật
        SetRegion(new Region(Rectangle.Empty));
        _revealTimer.Tick += (_, _) => Reveal(); // trang lỗi / không báo về thì vẫn hiện
        _revealTimer.Start();

        // người dùng kéo xong → chuyển sang vị trí tự do
        ResizeEnd += (_, _) =>
        {
            if (_placing) return;
            _s.Dock = StripDock.Manual;
            (_s.X, _s.Y) = (Left, Top);
            _s.Screen = Screen.FromControl(this).DeviceName;
            _s.Save();
        };

        // taskbar đổi vị trí, đổi độ phân giải, cắm thêm màn hình → neo lại
        _dockTimer.Tick += (_, _) => { if (_s.Dock != StripDock.Manual) PlaceStrip(); };
        _dockTimer.Start();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;

        Load += async (_, _) => await InitWebAsync();
        FormClosed += (_, _) =>
        {
            _panel?.Dispose();
            _rankPanel?.Dispose();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            _tray.Visible = false;
            _tray.Dispose();
        };
    }

    public Task SyncAsync() => WebHost.SyncAsync(_web);

    /// <summary>Đột phá ở cửa sổ Tu luyện → phát hiệu ứng trên dải game.</summary>
    public void PlayBreak(string kind)
    {
        if (_web.CoreWebView2 is null || kind is not ("good" or "gold" or "bad")) return;
        _ = _web.ExecuteScriptAsync($"window.playBreak && window.playBreak('{kind}', true)");
    }

    // ================= Kích thước, vị trí =================

    private void ApplySize()
    {
        var size = LogicalToDeviceUnits(new Size((int)(BaseSize.Width * _s.Scale), (int)(BaseSize.Height * _s.Scale)));
        // ×3 / ×4 có thể lớn hơn màn hình: thu lại vừa vùng làm việc, giữ tỉ lệ khung
        var area = TargetScreen().WorkingArea;
        var fit = Math.Min(1.0, Math.Min((double)area.Width / size.Width, (double)area.Height / size.Height));
        if (fit < 1) size = new Size((int)(size.Width * fit), (int)(size.Height * fit));
        _placing = true;
        Size = size;
        _placing = false;
        PlaceStrip();
    }

    private Screen TargetScreen() =>
        Screen.AllScreens.FirstOrDefault(sc => sc.DeviceName == _s.Screen) ?? Screen.PrimaryScreen!;

    /// <summary>Tính vị trí theo bố cục đã chọn, sát mép thanh taskbar.</summary>
    private void PlaceStrip()
    {
        var sc = TargetScreen();
        var area = sc.WorkingArea;
        var dock = _s.Dock;

        if (dock == StripDock.Manual && _s.X is int mx && _s.Y is int my)
        {
            var r = new Rectangle(mx, my, Width, Height);
            if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(r))) { MoveTo(new Point(mx, my)); return; }
            dock = StripDock.Auto; // vị trí cũ nằm ngoài màn hình
        }

        if (dock is StripDock.Auto or StripDock.Manual)
        {
            // taskbar ở trên thì neo dưới nó, còn lại neo phía trên taskbar
            var taskbarOnTop = area.Top > sc.Bounds.Top;
            dock = taskbarOnTop ? StripDock.TopRight : StripDock.BottomRight;
        }

        var m = LogicalToDeviceUnits(8);
        var top = dock is StripDock.TopLeft or StripDock.TopCenter or StripDock.TopRight;
        var y = top ? area.Top : area.Bottom - Height;
        var x = dock switch
        {
            StripDock.BottomLeft or StripDock.TopLeft => area.Left + m,
            StripDock.BottomCenter or StripDock.TopCenter => area.Left + (area.Width - Width) / 2,
            _ => area.Right - Width - m,
        };
        MoveTo(new Point(x, y));
    }

    private void MoveTo(Point p)
    {
        if (Location == p) return;
        _placing = true;
        Location = p;
        _placing = false;
    }

    private void OnDisplayChanged(object? sender, EventArgs e) => BeginInvoke(PlaceStrip);

    private void SetDock(StripDock dock)
    {
        _s.Dock = dock;
        _s.Save();
        PlaceStrip();
    }

    private void ResetPosition()
    {
        _s.Dock = StripDock.Auto;
        _s.X = _s.Y = null;
        _s.Screen = null;
        _s.Save();
        ShowStrip();
        PlaceStrip();
    }

    // ================= Ghim, nền =================

    private void ApplyPin()
    {
        TopMost = _s.Pinned;
        if (_panel is not null) _panel.TopMost = _s.Pinned;
        if (_rankPanel is not null) _rankPanel.TopMost = _s.Pinned;
    }

    /// <summary>Nền đặc: cửa sổ hiện đủ hình chữ nhật, khung game phủ kín (đã bỏ tuỳ chọn nền trong suốt).</summary>
    private void ApplyBackground()
    {
        BackColor = Theme.Ink;
        _web.DefaultBackgroundColor = Theme.Ink;
        if (_web.CoreWebView2 is null) return;
        _ = _web.ExecuteScriptAsync("window.setOpaque && window.setOpaque(true)");
    }

    /// <summary>Hiện dải game lần đầu: cả khung chữ nhật.</summary>
    private void Reveal()
    {
        _revealTimer.Stop();
        if (_revealed) return;
        _revealed = true;
        SetRegion(null);
    }

    private void SetRegion(Region? region)
    {
        var old = Region;
        Region = region;
        old?.Dispose();
    }

    // ================= Menu nhanh (chuột phải / khay hệ thống) =================

    private void BuildMenu()
    {
        _menu.AutoClose = true;
        _menu.Opening += (_, _) => RefreshMenu();
    }

    private void RefreshMenu()
    {
        _menu.Items.Clear();

        var layout = new ToolStripMenuItem(_s.L("Bố cục màn hình", "Screen layout"));
        foreach (var (dock, name) in new[]
        {
            (StripDock.Auto, _s.L("Tự động theo thanh taskbar", "Auto (follow taskbar)")),
            (StripDock.BottomLeft, _s.L("Trên taskbar · trái", "Above taskbar · left")),
            (StripDock.BottomCenter, _s.L("Trên taskbar · giữa", "Above taskbar · center")),
            (StripDock.BottomRight, _s.L("Trên taskbar · phải", "Above taskbar · right")),
            (StripDock.TopLeft, _s.L("Mép trên · trái", "Top edge · left")),
            (StripDock.TopCenter, _s.L("Mép trên · giữa", "Top edge · center")),
            (StripDock.TopRight, _s.L("Mép trên · phải", "Top edge · right")),
        })
        {
            var d = dock;
            layout.DropDownItems.Add(new ToolStripMenuItem(name, null, (_, _) => SetDock(d)) { Checked = _s.Dock == d });
        }
        layout.DropDownItems.Add(new ToolStripSeparator());
        layout.DropDownItems.Add(new ToolStripMenuItem(_s.L("Tự do (kéo thanh tiêu đề để di chuyển)", "Free (drag the title bar to move)")) { Checked = _s.Dock == StripDock.Manual, Enabled = false });
        _menu.Items.Add(layout);

        var size = new ToolStripMenuItem(_s.L("Kích thước", "Size"));
        foreach (var k in new[] { 0.75, 1.0, 1.25, 1.5, 2.0, 3.0, 4.0 })
        {
            var v = k;
            size.DropDownItems.Add(new ToolStripMenuItem(k >= 2 ? $"×{k:0}" : $"{k * 100:0}%", null, (_, _) => { _s.Scale = v; _s.Save(); ApplySize(); })
                { Checked = Math.Abs(_s.Scale - k) < 0.001 });
        }
        _menu.Items.Add(size);

        _menu.Items.Add(new ToolStripSeparator());

        var lang = new ToolStripMenuItem(_s.L("Ngôn ngữ · Language", "Language · Ngôn ngữ"));
        foreach (var (code, name) in new[] { ("vi", "Tiếng Việt"), ("en", "English") })
        {
            var c = code;
            lang.DropDownItems.Add(new ToolStripMenuItem(name, null, (_, _) => SetLanguage(c)) { Checked = _s.Language == c });
        }
        _menu.Items.Add(lang);

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem(_s.L("Ghim luôn hiển thị", "Always on top"), null, (_, _) => { _s.Pinned = !_s.Pinned; _s.Save(); ApplyPin(); })
            { Checked = _s.Pinned });
        _menu.Items.Add(new ToolStripMenuItem(_s.L("Đặt lại vị trí", "Reset position"), null, (_, _) => ResetPosition()) { ShortcutKeyDisplayString = "Shift+F12" });
        _menu.Items.Add(new ToolStripMenuItem(_s.L("Đặt lại kích thước", "Reset size"), null, (_, _) => { _s.Scale = 1; _s.Save(); ApplySize(); }) { ShortcutKeyDisplayString = "Shift+F11" });
        var shortcuts = new ToolStripMenuItem(_s.L("Phím tắt", "Keyboard shortcuts"));
        foreach (var (key, tab, vi, en) in new[]
        {
            ("I", "inv", "Hành trang", "Inventory"), ("M", "map", "Bản đồ", "Map"),
            ("D", "req", "Đột phá", "Breakthrough"), ("P", "market", "Chợ", "Market"),
            ("K", "skill", "Công pháp", "Techniques"), ("L", "craft", "Luyện chế", "Crafting"),
        })
            shortcuts.DropDownItems.Add(new ToolStripMenuItem(_s.L(vi, en), null, (_, _) => OpenPanel(tab)) { ShortcutKeyDisplayString = key });
        shortcuts.DropDownItems.Add(new ToolStripSeparator());
        shortcuts.DropDownItems.Add(new ToolStripMenuItem(_s.L("Dùng khi đang chọn cửa sổ game, không áp dụng khi nhập văn bản.", "Active while the game is focused; disabled while typing.")) { Enabled = false });
        _menu.Items.Add(shortcuts);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem(_s.L("Nhập code quà tặng…", "Enter gift code…"), null, (_, _) => ShowGiftCode()));
        _menu.Items.Add(new ToolStripSeparator());

        _menu.Items.Add(Visible ? _s.L("Ẩn xuống khay hệ thống", "Hide to system tray") : _s.L("Hiện dải game", "Show game strip"), null, (_, _) => ToggleVisible());
        _menu.Items.Add(_s.L("Thoát", "Exit"), null, (_, _) => Close());

        foreach (ToolStripItem item in _menu.Items) item.ForeColor = Theme.Paper;
    }

    private void ShowMenuAtCursor() => _menu.Show(Cursor.Position);

    /// <summary>Đổi ngôn ngữ: lưu cài đặt, menu dựng lại theo ngôn ngữ mới, các trang game tải lại (window.setLang).</summary>
    private void SetLanguage(string lang)
    {
        if (_s.Language == lang) return;
        _s.Language = lang;
        _s.Save();
        var js = $"window.setLang && window.setLang('{lang}')";
        if (_web.CoreWebView2 is not null) _ = _web.ExecuteScriptAsync(js);
        _panel?.SetLanguage(js);
        _rankPanel?.SetLanguage(js);
    }

    /// <summary>Hộp nhập code quà tặng; trang game gửi code lên server (window.redeemCode) và báo kết quả trên dải game.</summary>
    private void ShowGiftCode()
    {
        using var f = new Form
        {
            Text = _s.L("Nhập code quà tặng", "Enter gift code"), FormBorderStyle = FormBorderStyle.FixedToolWindow, StartPosition = FormStartPosition.CenterScreen,
            TopMost = true, ShowInTaskbar = false, MinimizeBox = false, MaximizeBox = false,
            BackColor = Theme.Bar, ForeColor = Theme.Paper, Font = new Font("Segoe UI", 10f),
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        var lbl = new Label { Text = _s.L("Nhập code để nhận quà (mỗi nhân vật nhận 1 lần):", "Enter a code to claim a gift (once per character):"), AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        var box = new TextBox { Width = LogicalToDeviceUnits(280), CharacterCasing = CharacterCasing.Upper, MaxLength = 32,
            BackColor = Theme.Ink, ForeColor = Theme.Paper, BorderStyle = BorderStyle.FixedSingle };
        var ok = new Button { Text = _s.L("Nhận", "Claim"), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Theme.Hover, ForeColor = Theme.Gold, AutoSize = true };
        var cancel = new Button { Text = _s.L("Hủy", "Cancel"), DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, ForeColor = Theme.Paper, AutoSize = true };
        ok.FlatAppearance.BorderColor = cancel.FlatAppearance.BorderColor = Theme.Line;
        var btns = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
        btns.Controls.AddRange(new Control[] { cancel, ok });
        var grid = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(12) };
        grid.Controls.Add(lbl); grid.Controls.Add(box); grid.Controls.Add(btns);
        f.Controls.Add(grid);
        f.AcceptButton = ok; f.CancelButton = cancel;
        if (f.ShowDialog(this) != DialogResult.OK) return;
        var code = box.Text.Trim();
        if (code.Length == 0 || _web.CoreWebView2 is null) return;
        _ = _web.ExecuteScriptAsync($"window.redeemCode && window.redeemCode({JsonSerializer.Serialize(code)})");
    }

    /// <summary>Cập nhật game: mở Launcher cạnh game (bản cài cũ: VanDaoLauncher.exe) rồi thoát để Launcher ghi đè file được.
    /// Không có Launcher (bản dev) thì mở link tải bằng trình duyệt.</summary>
    public void OpenLauncher(string? fallbackUrl)
    {
        var launcher = new[] { "NguoiLuoiTuTienLauncher.exe", "VanDaoLauncher.exe" }
            .Select(n => Path.Combine(AppContext.BaseDirectory, n))
            .FirstOrDefault(File.Exists);
        try
        {
            if (launcher is not null)
            {
                var start = new ProcessStartInfo(launcher) { UseShellExecute = true, WorkingDirectory = AppContext.BaseDirectory };
                start.ArgumentList.Add("--update-game");
                start.ArgumentList.Add("--game-path");
                start.ArgumentList.Add(AppContext.BaseDirectory);
                start.ArgumentList.Add("--wait-pid");
                start.ArgumentList.Add(System.Environment.ProcessId.ToString());
                if (Process.Start(start) is null) throw new InvalidOperationException("Không khởi động được Launcher.");
                Close();
                return;
            }
            if (!string.IsNullOrEmpty(fallbackUrl) && Uri.TryCreate(fallbackUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, _s.L("Không mở được Launcher", "Couldn't open the Launcher"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ToggleVisible()
    {
        if (Visible) { _panel?.Hide(); _rankPanel?.Hide(); Hide(); }
        else ShowStrip();
    }

    private void ShowStrip()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    private void OpenPanel(string tab)
    {
        if (tab == "rank")
        {
            _rankPanel ??= new PanelWindow(this, _s, _url, ranking: true);
            _rankPanel.ShowTab("rank");
            return;
        }
        _panel ??= new PanelWindow(this, _s, _url);
        _panel.ShowTab(tab);
    }

    // ================= WebView2 =================

    private async Task InitWebAsync()
    {
        try
        {
            await WebHost.InitAsync(_web, OnMessage);
            _web.CoreWebView2.ContextMenuRequested += (_, e) =>
            {
                e.Handled = true; // thay menu trình duyệt bằng menu của game
                ShowMenuAtCursor();
            };
            _web.Source = new Uri($"{_url}?mode=strip&opaque=1&lang={_s.Language}");
        }
        catch (WebView2RuntimeNotFoundException)
        {
            Hide();
            _tray.ShowBalloonTip(8000, "Người Lười Tu Tiên",
                _s.L("Máy chưa cài Microsoft Edge WebView2 Runtime nên game mở trên trình duyệt. Thoát game từ biểu tượng ở khay hệ thống.",
                    "Microsoft Edge WebView2 Runtime is not installed, so the game opened in your browser. Exit the game from the system tray icon."),
                ToolTipIcon.Info);
            Process.Start(new ProcessStartInfo($"{_url}?lang={_s.Language}") { UseShellExecute = true });
        }
    }

    private void OnMessage(string type, JsonElement msg)
    {
        switch (type)
        {
            case "drag": Native.BeginDrag(this); break;
            case "menu": ShowMenuAtCursor(); break;
            case "open-panel": OpenPanel(msg.TryGetProperty("tab", out var t) ? t.GetString() ?? "req" : "req"); break;
            case "changed": _ = _panel?.SyncAsync(); _ = _rankPanel?.SyncAsync(); break;
            case "shape": Reveal(); break;          // trang đã vẽ xong khung đầu → hiện dải
            case "open-launcher": OpenLauncher(msg.TryGetProperty("url", out var u) ? u.GetString() : null); break;
        }
    }

    // ================= Phím tắt toàn cục =================

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.RegisterHotKey(Handle, HotkeyResetPos, Native.MOD_SHIFT | Native.MOD_NOREPEAT, Native.VK_F12);
        Native.RegisterHotKey(Handle, HotkeyResetScale, Native.MOD_SHIFT | Native.MOD_NOREPEAT, Native.VK_F11);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Native.UnregisterHotKey(Handle, HotkeyResetPos);
        Native.UnregisterHotKey(Handle, HotkeyResetScale);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY)
        {
            if (m.WParam == HotkeyResetPos) ResetPosition();
            else if (m.WParam == HotkeyResetScale) { _s.Scale = 1; _s.Save(); ApplySize(); ShowStrip(); }
        }
        base.WndProc(ref m);
    }

    // Không giành focus khi vừa hiện; click vẫn kích hoạt cửa sổ bình thường.
    protected override bool ShowWithoutActivation => true;
}
