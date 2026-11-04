using Microsoft.Web.WebView2.WinForms;

namespace VanDao.Desktop;

/// <summary>
/// Cửa sổ Tu luyện (Đột phá / Hành trang / Đan dược / Bản đồ) bật lên sát dải game khi cần,
/// đóng lại thì game quay về dạng dải gọn trên taskbar.
/// </summary>
public sealed class PanelWindow : Form
{
    private static readonly Dictionary<string, string> TabNames = new()
    {
        ["req"] = "Đột phá", ["inv"] = "Hành trang", ["craft"] = "Luyện chế · Đan dược", ["map"] = "Bản đồ", ["skill"] = "Công pháp", ["pet"] = "Linh thú", ["fuse"] = "Luyện chế · Luyện khí", ["vault"] = "Luyện chế · Khảm Linh Thạch", ["petfuse"] = "Luyện chế · Linh thú", ["sell"] = "Luyện chế · Bán", ["spin"] = "Luyện chế · Vòng quay", ["market"] = "Chợ", ["rank"] = "Bảng xếp hạng", ["kyduyen"] = "Kỳ duyên", ["event"] = "Sự kiện",
    };

    private static readonly Dictionary<string, string> TabNamesEn = new()
    {
        ["req"] = "Breakthrough", ["inv"] = "Inventory", ["craft"] = "Crafting · Alchemy", ["map"] = "Map", ["skill"] = "Techniques", ["pet"] = "Spirit Beasts", ["fuse"] = "Crafting · Forging", ["vault"] = "Crafting · Socketing", ["petfuse"] = "Crafting · Spirit Beast Gear", ["sell"] = "Crafting · Sell", ["spin"] = "Crafting · Wheel", ["market"] = "Market", ["rank"] = "Leaderboard", ["kyduyen"] = "Fortune", ["event"] = "Events",
    };

    private string Title(string tab) => _s.L($"Tu luyện · {TabNames[tab]}", $"Cultivation · {TabNamesEn[tab]}");
    private string? SoloTitle() => _solo switch
    {
        "rank" => _s.L("Thiên bảng · Bảng xếp hạng", "Heaven Ranking · Leaderboard"),
        "event" => _s.L("Sự kiện", "Events"),
        _ => null,
    };

    private readonly GameWindow _strip;
    private readonly WindowSettings _s;
    private readonly string _url;
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Theme.Ink };
    private bool _ready;
    private string _tab = "req";
    /// <summary>Tab gửi cho trang (openTab): thường trùng _tab; cửa sổ riêng có thể mở mục con, vd "spin" trong Sự kiện.</summary>
    private string _page = "req";
    /// <summary>Cửa sổ riêng một bảng ("rank" Thiên bảng · "event" Sự kiện): cỡ và tiêu đề cố định, không đổi tab.</summary>
    private readonly string? _solo;
    private bool _ranking => _solo == "rank";

    public PanelWindow(GameWindow strip, WindowSettings settings, string url, string? solo = null)
    {
        _strip = strip;
        _s = settings;
        _url = url;
        _solo = solo;
        if (solo is not null) _tab = _page = solo;

        // Không đặt Owner: cửa sổ được sở hữu luôn nằm trên chủ của nó,
        // khiến click vào dải game không thể đưa dải lên trước Tu luyện.
        Icon = strip.Icon;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Ink;
        KeyPreview = true;
        MinimumSize = LogicalToDeviceUnits(MinPanel);   // dưới ~640px giao diện Tu luyện tự xếp dọc, nên không cho kéo hẹp hơn
        Size = SizeFor(_tab);
        Controls.Add(_web);

        Load += async (_, _) =>
        {
            await WebHost.InitAsync(_web, OnMessage, _url);
            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false; // không hiện menu chuột phải của trình duyệt
            _web.CoreWebView2.NavigationCompleted += (_, _) => _ready = true;
            _web.Source = new Uri($"{_url}?mode=panel&tab={_page}&lang={_s.Language}");
        };
        ResizeEnd += (_, _) =>
        {
            if (_solo is not null) return;
            _s.InvW = Width; _s.InvH = Height;
            _s.Save();
        };
        FormClosing += (_, e) =>
        {
            if (e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true; // giữ lại để mở lần sau cho nhanh
            Hide();
        };
    }

    public async void ShowTab(string tab)
    {
        _tab = _solo ?? (TabNames.ContainsKey(tab) ? tab : "req");
        _page = _solo is not null && TabNames.ContainsKey(tab) ? tab : _tab;   // cửa sổ Sự kiện: "spin" mở thẳng mục Vòng quay
        Text = SoloTitle() ?? Title(_tab);
        TopMost = _s.Pinned;
        Size = SizeFor(_tab);
        if (!Visible)
        {
            PlaceNearStrip();
            Show();
        }
        Activate();
        if (_ready)
        {
            await _web.ExecuteScriptAsync($"window.openTab && window.openTab('{_page}')");
            await WebHost.SyncAsync(_web);
        }
    }

    /// <summary>Cỡ nhỏ nhất của bảng Tu luyện (đơn vị logic): đủ rộng để các tab giữ bố cục ngang.</summary>
    private static readonly Size MinPanel = new(700, 560);

    /// <summary>Mọi tab Tu luyện dùng chung một kích thước: kích thước người dùng đã kéo (không nhỏ hơn MinPanel), chưa có thì mặc định.</summary>
    private Size SizeFor(string _)
    {
        if (_ranking) return LogicalToDeviceUnits(new Size(900, 740));
        if (_solo == "event") return LogicalToDeviceUnits(new Size(880, 760));   // đủ chỗ cho Vòng quay (bánh xe + bảng mốc) không phải cuộn nhiều
        var min = LogicalToDeviceUnits(MinPanel);
        return _s.InvW is int w && _s.InvH is int h
            ? new Size(Math.Max(w, min.Width), Math.Max(h, min.Height))
            : LogicalToDeviceUnits(new Size(740, 680));
    }

    /// <summary>Ngôn ngữ vừa đổi: đổi tiêu đề và tải lại trang (js gọi window.setLang).</summary>
    public void SetLanguage(string js)
    {
        Text = SoloTitle() ?? Title(_tab);
        if (_web.CoreWebView2 is not null) _ = _web.ExecuteScriptAsync(js);
    }

    public Task SyncAsync() => Visible ? WebHost.SyncAsync(_web) : Task.CompletedTask;

    /// <summary>Đặt bảng ngay phía trên dải (hoặc phía dưới nếu dải nằm ở mép trên màn hình).</summary>
    private void PlaceNearStrip()
    {
        var area = Screen.FromControl(_strip).WorkingArea;
        var gap = LogicalToDeviceUnits(6);
        var s = _strip.Bounds;
        var above = s.Top - area.Top >= area.Bottom - s.Bottom;
        var y = above ? s.Top - Height - gap : s.Bottom + gap;
        var x = s.Right - Width;
        Location = new Point(
            Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - Width)),
            Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - Height)));
    }

    private void OnMessage(string type, System.Text.Json.JsonElement msg)
    {
        switch (type)
        {
            case "changed": _ = _strip.SyncAsync(); break;
            case "online-login-required": _strip.ShowOnlineLogin(); break;
            case "close-panel": Hide(); break;
            case "open-panel" when msg.TryGetProperty("tab", out var op): _strip.OpenPanel(op.GetString() ?? "req"); break;   // vd bảng Sự kiện → Vòng quay
            case "open-launcher": _strip.OpenLauncher(msg.TryGetProperty("url", out var u) ? u.GetString() : null); break;
            case "quit": _strip.Close(); break;
            case "tab" when _solo is null && msg.TryGetProperty("tab", out var t) && TabNames.ContainsKey(t.GetString() ?? ""):
                var tab = t.GetString()!;
                if (tab == _tab) break;
                _tab = tab;
                Text = Title(_tab);
                var size = SizeFor(_tab);
                if (size != Size) { Size = size; PlaceNearStrip(); }
                break;
            case "break": _strip.PlayBreak(msg.TryGetProperty("kind", out var k) ? k.GetString() ?? "good" : "good"); break;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.UseDarkTitleBar(Handle);
    }
}
