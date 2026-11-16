using System.Drawing.Drawing2D;

namespace VanDao.Desktop;

/// <summary>Thanh trượt âm lượng: số bên trái, rãnh mảnh, phần đã chọn và núm tròn có quầng màu vàng.
/// Bấm/kéo để đặt, lăn chuột ±5.</summary>
internal sealed class VolumeSlider : Control
{
    private static readonly Color Accent = Theme.Gold; // cùng màu công tắc trong menu
    private const int NumW = 30, Knob = 12, Halo = 20, TrackH = 3;
    private int _value = 25;
    private bool _hot, _drag;

    public event EventHandler? ValueChanged;

    public VolumeSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bar;
        Size = new Size(220, 28);
        Cursor = Cursors.Hand;
    }

    /// <summary>0–100.</summary>
    public int Value
    {
        get => _value;
        set
        {
            value = Math.Clamp(value, 0, 100);
            if (value == _value) return;
            _value = value;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // rãnh chạy từ sau ô số tới mép phải, chừa nửa quầng để núm không bị cắt
    private float TrackLeft => NumW + 8 + Halo / 2f;
    private float TrackRight => Width - Halo / 2f - 1;

    private void SetFromX(int x) =>
        Value = (int)Math.Round((x - TrackLeft) / (TrackRight - TrackLeft) * 100);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && e.X >= NumW) { _drag = true; Capture = true; SetFromX(e.X); }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_drag) SetFromX(e.X);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _drag = false;
        Capture = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnMouseEnter(EventArgs e) { _hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hot = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        Value += Math.Sign(e.Delta) * 5;
        base.OnMouseWheel(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        TextRenderer.DrawText(g, _value.ToString(), Theme.MenuFont, new Rectangle(0, 0, NumW, Height), Theme.Paper,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var cy = Height / 2f;
        var x = TrackLeft + (TrackRight - TrackLeft) * _value / 100f;
        using (var rest = new SolidBrush(Theme.Line))
            g.FillRectangle(rest, x, cy - TrackH / 2f, TrackRight - x, TrackH);
        using (var fill = new SolidBrush(Accent))
        {
            g.FillRectangle(fill, TrackLeft - Halo / 2f + 2, cy - TrackH / 2f, x - TrackLeft + Halo / 2f - 2, TrackH);
            // quầng mờ quanh núm, đậm hơn khi rê chuột / đang kéo
            using var halo = new SolidBrush(Color.FromArgb(_drag ? 110 : _hot ? 85 : 60, Accent));
            g.FillEllipse(halo, x - Halo / 2f, cy - Halo / 2f, Halo, Halo);
            g.FillEllipse(fill, x - Knob / 2f, cy - Knob / 2f, Knob, Knob);
        }
    }
}
