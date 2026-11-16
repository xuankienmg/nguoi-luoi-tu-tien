using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VanDao.Desktop;

/// <summary>Cách vẽ phần bên phải một dòng menu (vùng ShortcutKeyDisplayString).</summary>
internal enum MenuTrail { Text, Value, Keys, Toggle }

/// <summary>Trang trí một dòng menu: biểu tượng (glyph Segoe Fluent/MDL2), kiểu phần phải, màu cảnh báo.</summary>
internal sealed record MenuLook(string? Glyph, MenuTrail Trail = MenuTrail.Text, bool On = false, bool Danger = false);

internal static class Theme
{
    public static readonly Color Ink = Color.FromArgb(14, 20, 22);
    public static readonly Color Bar = Color.FromArgb(20, 29, 32);
    public static readonly Color Line = Color.FromArgb(43, 58, 62);
    public static readonly Color Hover = Color.FromArgb(36, 50, 54);
    public static readonly Color Paper = Color.FromArgb(235, 228, 211);
    public static readonly Color Muted = Color.FromArgb(151, 165, 162);
    public static readonly Color Gold = Color.FromArgb(230, 196, 106);
    public static readonly Color Cinnabar = Color.FromArgb(222, 120, 96);
    private static readonly Color Faint = Color.FromArgb(104, 118, 116);

    // glyph dùng chung cho menu (mã Segoe MDL2 Assets — Segoe Fluent Icons trên Win11 giữ cùng mã)
    public const string GLayout = "", GSize = "", GLang = "", GPin = "",
        GReset = "", GRescale = "", GKeys = "", GMusic = "", GGift = "",
        GHide = "", GSignOut = "", GExit = "";

    public static ToolStripRenderer Renderer { get; } = new DarkRenderer();
    public static readonly Font MenuFont = new("Segoe UI", 9.75f);
    private static readonly Font KeyFont = new("Segoe UI", 7.75f);
    private static readonly Font IconFont = PickIconFont();

    private static readonly ConditionalWeakTable<ToolStripItem, MenuLook> Looks = new();
    private static readonly ConditionalWeakTable<ToolStrip, object> Rounded = new();

    /// <summary>Gắn trang trí cho dòng menu, trả lại chính item để viết liền mạch.</summary>
    public static T Look<T>(this T item, MenuLook look) where T : ToolStripItem
    {
        Looks.AddOrUpdate(item, look);
        if (look.Trail == MenuTrail.Toggle && item is ToolStripMenuItem mi)
            mi.ShortcutKeyDisplayString = "WWWW"; // giữ chỗ cho công tắc, chữ không được vẽ
        return item;
    }

    public static T Look<T>(this T item, string? glyph, MenuTrail trail = MenuTrail.Text, bool on = false, bool danger = false)
        where T : ToolStripItem => item.Look(new MenuLook(glyph, trail, on, danger));

    public static bool IsOn(this ToolStripItem item) => Looks.TryGetValue(item, out var l) && l.On;

    /// <summary>Đổi trạng thái công tắc và vẽ lại ngay (menu vẫn mở).</summary>
    public static void SetOn(this ToolStripItem item, bool on)
    {
        if (Looks.TryGetValue(item, out var l)) Looks.AddOrUpdate(item, l with { On = on });
        item.Invalidate();
    }

    // bấm dòng công tắc → mọi menu trong chuỗi (menu chính + menu con) bỏ qua lần đóng do ItemClicked
    private static bool _keepOpen;
    private static readonly ConditionalWeakTable<ToolStripDropDown, object> Hooked = new();

    private static void HookKeepOpen(ToolStripDropDown dd)
    {
        if (Hooked.TryGetValue(dd, out _)) return;
        Hooked.Add(dd, new object());
        dd.ItemClicked += (_, e) =>
        {
            if (!Looks.TryGetValue(e.ClickedItem!, out var l) || l.Trail != MenuTrail.Toggle) return;
            _keepOpen = true;
            dd.BeginInvoke(() => _keepOpen = false); // hết chuỗi đóng của lần bấm này
        };
        dd.Closing += (_, e) =>
        {
            if (_keepOpen && e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true;
        };
    }

    /// <summary>Hiện giá trị hiện tại (mờ) ở bên phải dòng có menu con, vd "100%".</summary>
    public static ToolStripMenuItem Value(this ToolStripMenuItem item, string glyph, string value)
    {
        item.ShortcutKeyDisplayString = value;
        return item.Look(glyph, MenuTrail.Value);
    }

    /// <summary>Áp phong cách menu: font, khoảng đệm dòng, bo góc Win11 cho menu và mọi menu con.</summary>
    public static void StyleMenu(ToolStripDropDownMenu menu)
    {
        menu.Font = MenuFont;
        menu.ImageScalingSize = new Size(20, 20);
        menu.Padding = new Padding(4, 6, 4, 6);
        menu.ShowImageMargin = true;
        HookKeepOpen(menu);
        // dòng có menu con: WinForms không vẽ ShortcutKeyDisplayString → giá trị tự vẽ cạnh mũi tên, cần menu đủ rộng
        if (menu.Items.OfType<ToolStripMenuItem>().Any(i => i.HasDropDownItems && Looks.TryGetValue(i, out var l) && l.Trail == MenuTrail.Value))
            menu.MinimumSize = new Size(290, 0);
        foreach (ToolStripItem item in menu.Items)
        {
            if (item is ToolStripMenuItem mi)
            {
                mi.ForeColor = Paper;
                mi.Padding = new Padding(0, 4, 0, 4);
                mi.ShowShortcutKeys = true;
                if (mi.HasDropDownItems && mi.DropDown is ToolStripDropDownMenu sub)
                {
                    sub.Renderer = Renderer;
                    StyleMenu(sub);
                }
            }
            else if (item is ToolStripSeparator sep) sep.Margin = new Padding(0, 3, 0, 3);
        }
    }

    private static Font PickIconFont()
    {
        using var fonts = new System.Drawing.Text.InstalledFontCollection();
        var fluent = fonts.Families.Any(f => f.Name == "Segoe Fluent Icons");
        return new Font(fluent ? "Segoe Fluent Icons" : "Segoe MDL2 Assets", 10.5f);
    }

    private static GraphicsPath RoundRect(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        var d = rad * 2;
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Win11: góc bo nhỏ + viền DWM cùng màu Line. Win10 bỏ qua lệnh, vẫn có viền tự vẽ.</summary>
    private static void RoundCorners(ToolStrip strip)
    {
        if (Rounded.TryGetValue(strip, out _) || !strip.IsHandleCreated) return;
        Rounded.Add(strip, new object());
        int round = 3; // DWMWCP_ROUNDSMALL
        DwmSetWindowAttribute(strip.Handle, 33, ref round, sizeof(int));
        int border = Line.R | Line.G << 8 | Line.B << 16; // COLORREF
        DwmSetWindowAttribute(strip.Handle, 34, ref border, sizeof(int));
    }

    private sealed class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) => RoundedEdges = false;

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ToolStripDropDown) RoundCorners(e.ToolStrip);
            using var b = new SolidBrush(Bar);
            e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Line);
            var r = new Rectangle(Point.Empty, e.ToolStrip.Size);
            r.Width--; r.Height--;
            e.Graphics.DrawRectangle(pen, r);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { } // cùng nền, không kẻ cột

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using var pen = new Pen(Line);
            e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var item = e.Item;
            Looks.TryGetValue(item, out var look);
            var hot = item.Selected && item.Enabled;
            if (hot)
            {
                var r = new RectangleF(3, 1, item.Width - 6, item.Height - 2);
                using (var path = RoundRect(r, 4))
                using (var b = new SolidBrush(Hover))
                    g.FillPath(b, path);
                // vạch vàng bên trái: dấu hiệu duy nhất của dòng đang chọn
                using var rail = new SolidBrush(look?.Danger == true ? Cinnabar : Gold);
                using var railPath = RoundRect(new RectangleF(3, item.Height / 2f - 7, 3, 14), 1.5f);
                g.FillPath(rail, railPath);
            }

            if (look?.Glyph is { } glyph)
            {
                var color = look.Danger ? Cinnabar : hot ? Gold : item.Enabled ? Muted : Faint;
                var box = new Rectangle(6, 0, 28, item.Height);
                TextRenderer.DrawText(g, glyph, IconFont, box, color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            Looks.TryGetValue(e.Item, out var look);
            var isMain = e.Text == e.Item.Text;
            if (e.Item is not ToolStripMenuItem)
            {
                base.OnRenderItemText(e); // nhãn (vd "Âm lượng") giữ màu ForeColor riêng
                return;
            }
            if (isMain)
            {
                e.TextColor = !e.Item.Enabled ? Faint : look?.Danger == true ? Cinnabar : Paper;
                // WinForms đặt chữ hơi cao khi có padding dọc → căn giữa theo cả dòng, thẳng hàng với icon/giá trị
                // (-2: dòng chữ tiếng Việt có chỗ cho dấu phía trên, kéo lên để thân chữ nằm giữa)
                e.TextRectangle = new Rectangle(e.TextRectangle.X, -2, e.TextRectangle.Width, e.Item.Height);
                e.TextFormat = (e.TextFormat & ~(TextFormatFlags.Top | TextFormatFlags.Bottom)) | TextFormatFlags.VerticalCenter;
                base.OnRenderItemText(e);
                return;
            }

            // phần bên phải (vùng phím tắt)
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = e.TextRectangle;
            switch (look?.Trail ?? MenuTrail.Keys)
            {
                case MenuTrail.Toggle:
                    DrawToggle(g, new RectangleF(r.Right - 30, r.Top + r.Height / 2f - 8, 30, 16), look!.On);
                    break;
                case MenuTrail.Value:
                case MenuTrail.Text:
                    TextRenderer.DrawText(g, e.Text, e.TextFont, r, e.Item.Selected ? Paper : Muted,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    break;
                default:
                    DrawKeys(g, e.Text, r, e.Item.Enabled);
                    break;
            }
        }

        private static void DrawToggle(Graphics g, RectangleF r, bool on)
        {
            using var track = RoundRect(r, r.Height / 2);
            if (on)
            {
                using var b = new SolidBrush(Gold);
                g.FillPath(b, track);
            }
            else
            {
                using var pen = new Pen(Muted, 1.2f);
                g.DrawPath(pen, track);
            }
            var d = r.Height - 6;
            var x = on ? r.Right - d - 3 : r.Left + 3;
            using var knob = new SolidBrush(on ? Ink : Muted);
            g.FillEllipse(knob, x, r.Top + 3, d, d);
        }

        /// <summary>"Shift+F12" → các phím bấm nhỏ, căn phải.</summary>
        private static void DrawKeys(Graphics g, string text, Rectangle r, bool enabled)
        {
            var keys = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var x = (float)r.Right;
            var h = 17f;
            var y = r.Top + (r.Height - h) / 2f;
            using var pen = new Pen(Line);
            using var fill = new SolidBrush(Ink);
            for (var i = keys.Length - 1; i >= 0; i--)
            {
                var w = Math.Max(h, TextRenderer.MeasureText(keys[i], KeyFont, Size.Empty, TextFormatFlags.NoPadding).Width + 10);
                var box = new RectangleF(x - w, y, w, h);
                using (var path = RoundRect(box, 3))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }
                TextRenderer.DrawText(g, keys[i], KeyFont, Rectangle.Round(box), enabled ? Muted : Faint,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                x -= w + 3;
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            var hot = e.Item?.Selected == true;
            e.ArrowColor = hot ? Gold : Muted;
            base.OnRenderArrow(e);
            // giá trị hiện tại (vd "100%") ngay trước mũi tên
            if (e.Item is ToolStripMenuItem { HasDropDownItems: true } mi && !string.IsNullOrEmpty(mi.ShortcutKeyDisplayString)
                && Looks.TryGetValue(mi, out var look) && look.Trail == MenuTrail.Value)
            {
                var box = new Rectangle(0, 0, e.ArrowRectangle.Left - 2, mi.Height);
                TextRenderer.DrawText(e.Graphics, mi.ShortcutKeyDisplayString, mi.Font, box, hot ? Paper : Muted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // dấu tích vàng, dễ thấy trên nền tối
            var r = new Rectangle(8, (e.Item.Height - 16) / 2, 16, 16);
            using var pen = new Pen(Gold, 2f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawLines(pen, new[]
            {
                new PointF(r.Left + r.Width * .2f, r.Top + r.Height * .55f),
                new PointF(r.Left + r.Width * .42f, r.Top + r.Height * .75f),
                new PointF(r.Left + r.Width * .8f, r.Top + r.Height * .3f),
            });
        }
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Bar;
        public override Color ImageMarginGradientBegin => Bar;
        public override Color ImageMarginGradientMiddle => Bar;
        public override Color ImageMarginGradientEnd => Bar;
        public override Color MenuBorder => Line;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Hover;
        public override Color MenuItemPressedGradientEnd => Hover;
        public override Color CheckBackground => Bar;
        public override Color CheckSelectedBackground => Hover;
        public override Color CheckPressedBackground => Hover;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Bar;
    }
}
