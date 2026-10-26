param(
    [Parameter(Mandatory=$true)][string]$BossAtlas,
    [Parameter(Mandatory=$true)][string]$MonsterAtlas,
    [string]$OutputRoot = ''
)
$ErrorActionPreference = 'Stop'
if (!$OutputRoot) { $OutputRoot = Join-Path $PSScriptRoot '../../wwwroot/img/hell' }
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices;
public static class HellAlpha {
    // Bounding box of visible pixels inside one atlas cell; empty when the cell is fully transparent.
    public static Rectangle Bounds(Bitmap bmp, Rectangle cell) {
        var data = bmp.LockBits(cell, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try {
            // Stride spans the whole atlas row; copy only this cell's bytes per row to stay inside the bitmap.
            var row = cell.Width * 4; var bytes = new byte[row * cell.Height];
            for (var y = 0; y < cell.Height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), bytes, y * row, row);
            int l = cell.Width, t = cell.Height, r = -1, b = -1;
            for (var y = 0; y < cell.Height; y++) for (var x = 0; x < cell.Width; x++)
                if (bytes[y * row + x * 4 + 3] > 16) { l = Math.Min(l, x); t = Math.Min(t, y); r = Math.Max(r, x); b = Math.Max(b, y); }
            return r < 0 ? Rectangle.Empty : new Rectangle(cell.X + l, cell.Y + t, r - l + 1, b - t + 1);
        } finally { bmp.UnlockBits(data); }
    }
    public static bool Opaque(Bitmap bmp, int x, int y) { return bmp.GetPixel(x, y).A > 16; }
}
'@
# Extract generated atlas cells; never recolor, redraw or replace the original artwork.
# Each monster gets the four runtime variants used by map.js / battle-fx.js: x.png 320x221, x_hd.png 240x240, x_48.png, x_24.png.
function Save-Fit($Atlas, $Source, [int]$Width, [int]$Height, [double]$Margin, [string]$Path) {
    $canvas = [System.Drawing.Bitmap]::new($Width, $Height)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $scale = [Math]::Min($Width * (1 - 2 * $Margin) / $Source.Width, $Height * (1 - 2 * $Margin) / $Source.Height)
        $w = [float]($Source.Width * $scale); $h = [float]($Source.Height * $scale)
        $target = [System.Drawing.RectangleF]::new(($Width - $w) / 2, ($Height - $h) / 2, $w, $h)
        $graphics.DrawImage($Atlas, $target, [System.Drawing.RectangleF]$Source, [System.Drawing.GraphicsUnit]::Pixel)
        $canvas.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $canvas.Dispose() }
}
function Export-Cells([string]$Source, [int]$Columns, [int]$Rows, [string[]]$Names) {
    if ($Names.Count -ne $Columns * $Rows) { throw 'Atlas name count does not match grid.' }
    $null = New-Item -ItemType Directory -Force -Path $OutputRoot
    $atlas = [System.Drawing.Bitmap]::new((Resolve-Path -LiteralPath $Source).Path)
    try {
        foreach ($corner in @(@(0, 0), @(($atlas.Width - 1), 0), @(0, ($atlas.Height - 1)), @(($atlas.Width - 1), ($atlas.Height - 1)))) {
            if ([HellAlpha]::Opaque($atlas, $corner[0], $corner[1])) { throw "$Source must have a transparent background (corner $($corner -join ',') is opaque)." }
        }
        for ($i = 0; $i -lt $Names.Count; $i++) {
            $col = $i % $Columns; $row = [Math]::Floor($i / $Columns)
            $left = [int][Math]::Round($col * $atlas.Width / $Columns); $top = [int][Math]::Round($row * $atlas.Height / $Rows)
            $cell = [System.Drawing.Rectangle]::new($left, $top,
                [int][Math]::Round(($col + 1) * $atlas.Width / $Columns) - $left, [int][Math]::Round(($row + 1) * $atlas.Height / $Rows) - $top)
            $body = [HellAlpha]::Bounds($atlas, $cell)
            if ($body.IsEmpty) { throw "Atlas cell $i ($($Names[$i])) is empty." }
            $base = Join-Path $OutputRoot $Names[$i]
            Save-Fit $atlas $body 320 221 0.03 "$base.png"
            Save-Fit $atlas $body 240 240 0.03 "${base}_hd.png"
            Save-Fit $atlas $body 48 48 0.02 "${base}_48.png"
            Save-Fit $atlas $body 24 24 0.0 "${base}_24.png"
        }
    } finally { $atlas.Dispose() }
}
$bosses = @('boss_thoi_phan_quan', 'boss_hac_hung_tinh', 'boss_thanh_anh_dai_vuong', 'boss_thiet_phien_cong_chua', 'boss_nguu_ma_vuong', 'boss_diem_la_vuong')
$monsters = @('quai_nguu_dau', 'quai_ma_dien', 'quai_hac_vo_thuong', 'quai_bach_vo_thuong', 'quai_hoa_diem_tieu_yeu', 'tinh_bach_cot_tinh')
Export-Cells $BossAtlas 3 2 $bosses
Export-Cells $MonsterAtlas 3 2 $monsters
Write-Output "Extracted $($bosses.Count + $monsters.Count) Hell monsters x 4 sizes into $OutputRoot, preserving source alpha."
