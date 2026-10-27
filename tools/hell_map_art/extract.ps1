param(
    [Parameter(Mandatory=$true)][string]$BossAtlas,
    [Parameter(Mandatory=$true)][string]$MonsterAtlas,
    [string]$OutputRoot = '',
    # The generator painted a gray checkerboard instead of real alpha: flood it away from each cell edge.
    [switch]$KeyChecker,
    # Cells whose character faces right; mirrored so every monster faces the player on the left.
    [string[]]$Flip = @('boss_thanh_anh_dai_vuong'),
    # Cells with a translucent glow aura over the painted checkerboard; peeled deeper with -KeyChecker.
    [string[]]$Halo = @('boss_thoi_phan_quan', 'boss_hac_hung_tinh', 'boss_thanh_anh_dai_vuong', 'boss_thiet_phien_cong_chua',
        'boss_nguu_ma_vuong', 'boss_diem_la_vuong', 'tinh_bach_cot_tinh')
)
$ErrorActionPreference = 'Stop'
if (!$OutputRoot) { $OutputRoot = Join-Path $PSScriptRoot '../../wwwroot/img/hell' }
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices;
public static class HellAtlas {
    static byte[] Read(Bitmap bmp) {
        var d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try { var row = bmp.Width * 4; var px = new byte[row * bmp.Height];
            for (var y = 0; y < bmp.Height; y++) Marshal.Copy(IntPtr.Add(d.Scan0, y * d.Stride), px, y * row, row);
            return px; } finally { bmp.UnlockBits(d); }
    }
    static void Write(Bitmap bmp, byte[] px) {
        var d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try { var row = bmp.Width * 4; for (var y = 0; y < bmp.Height; y++) Marshal.Copy(px, y * row, IntPtr.Add(d.Scan0, y * d.Stride), row); }
        finally { bmp.UnlockBits(d); }
    }
    // Copies one cell (inset to skip painted grid lines) into its own ARGB bitmap.
    public static Bitmap Cell(Bitmap atlas, Rectangle cell, bool flip) {
        var bmp = new Bitmap(cell.Width, cell.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp)) {
            g.Clear(Color.Transparent);
            g.DrawImage(atlas, new Rectangle(0, 0, cell.Width, cell.Height), cell, GraphicsUnit.Pixel);
        }
        if (flip) bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
        return bmp;
    }
    static bool Neutral(byte[] p, int i, int spread) {
        int b = p[i], g = p[i + 1], r = p[i + 2];
        return Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) <= spread;
    }
    // Removes the painted checkerboard: flood from the cell edge through neutral grays inside the checker's tone
    // range, so the fill stops at the character's dark outline. Two peel passes clear blended fringe pixels.
    public static int KeyChecker(Bitmap bmp, bool halo) {
        var px = Read(bmp); int w = bmp.Width, h = bmp.Height;
        var edge = new List<int>();
        for (var x = 0; x < w; x++) { edge.Add(x); edge.Add((h - 1) * w + x); }
        for (var y = 0; y < h; y++) { edge.Add(y * w); edge.Add(y * w + w - 1); }
        var tones = new List<int>();
        foreach (var e in edge) if (Neutral(px, e * 4, 6)) tones.Add(px[e * 4 + 1]);
        tones.Sort();
        int lo = tones[tones.Count / 50] - 6, hi = tones[tones.Count - 1 - tones.Count / 50] + 6;
        var bg = new bool[w * h]; var queue = new Queue<int>();
        Func<int, bool> candidate = k => Neutral(px, k * 4, 8) && px[k * 4 + 1] >= lo && px[k * 4 + 1] <= hi;
        foreach (var e in edge) if (!bg[e] && candidate(e)) { bg[e] = true; queue.Enqueue(e); }
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        // Checker pockets enclosed by the art (inside swirls, between limbs): seed from flat blocks matching a
        // checker tone exactly. A checker square is ~290 px; shaded art has no flat gray block that large.
        int dark = tones[tones.Count / 50], light = tones[tones.Count - 1 - tones.Count / 50];
        // JPEG blurs square borders into in-between grays, so accept the whole span between the two tones.
        Func<int, bool> flat = k => Neutral(px, k * 4, 14) && px[k * 4 + 1] >= dark - 10 && px[k * 4 + 1] <= light + 10;
        var seen = new bool[w * h];
        for (var s = 0; s < w * h; s++) {
            if (seen[s] || bg[s] || !flat(s)) continue;
            var block = new List<int> { s }; seen[s] = true;
            for (var b = 0; b < block.Count; b++) {
                int x = block[b] % w, y = block[b] / w;
                for (var n = 0; n < 4; n++) {
                    int nx = x + dx[n], ny = y + dy[n]; if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    var nk = ny * w + nx; if (!seen[nk] && !bg[nk] && flat(nk)) { seen[nk] = true; block.Add(nk); }
                }
            }
            // A pocket qualifies only when both tones appear: the alternating squares are checker evidence,
            // a single flat light patch may be a white robe highlight.
            int darkCount = 0, lightCount = 0;
            foreach (var k in block) {
                if (Math.Abs(px[k * 4 + 1] - dark) <= 8) darkCount++;
                else if (Math.Abs(px[k * 4 + 1] - light) <= 8) lightCount++;
            }
            if (block.Count >= 30 && darkCount >= 8 && lightCount >= 8)
                foreach (var k in block) { bg[k] = true; queue.Enqueue(k); }
        }
        while (queue.Count > 0) {
            var k = queue.Dequeue(); int x = k % w, y = k / w;
            for (var n = 0; n < 4; n++) {
                int nx = x + dx[n], ny = y + dy[n]; if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                var nk = ny * w + nx; if (!bg[nk] && candidate(nk)) { bg[nk] = true; queue.Enqueue(nk); }
            }
        }
        // Glow auras were blended over the checker: those cells keep peeling desaturated pixels until stable,
        // saturated flame and dark outlines stop the peel.
        // Halo test uses relative saturation, so tinted glow goes while gold and flame (saturation > .5) stay.
        Func<int, bool> fringe = k => Neutral(px, k * 4, 20) && px[k * 4 + 1] >= lo - 15 && px[k * 4 + 1] <= hi + 15;
        Func<int, bool> glow = k => {
            int b = px[k * 4], g = px[k * 4 + 1], r = px[k * 4 + 2], max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            return max >= lo - 15 && max <= hi + 40 && (max - min) <= max * .5;
        };
        var passes = halo ? 400 : 2;
        for (var pass = 0; pass < passes; pass++) {
            var peel = new List<int>();
            for (var k = 0; k < w * h; k++) {
                if (bg[k] || !(halo ? glow(k) : fringe(k))) continue;
                int x = k % w, y = k / w;
                if ((x > 0 && bg[k - 1]) || (x < w - 1 && bg[k + 1]) || (y > 0 && bg[k - w]) || (y < h - 1 && bg[k + w])) peel.Add(k);
            }
            if (peel.Count == 0) break;
            foreach (var k in peel) bg[k] = true;
        }
        // Weapons of neighbouring cells reach across the cell border: drop opaque pieces touching the edge
        // that are small next to the main body. Detached flames and shards inside the cell are kept.
        var label = new int[w * h]; var sizes = new List<int> { 0 }; var touches = new List<bool> { false };
        for (var s = 0; s < w * h; s++) {
            if (bg[s] || label[s] != 0) continue;
            var id = sizes.Count; var part = new List<int> { s }; label[s] = id; var edgeHit = false;
            for (var b = 0; b < part.Count; b++) {
                int x = part[b] % w, y = part[b] / w;
                if (x == 0 || y == 0 || x == w - 1 || y == h - 1) edgeHit = true;
                for (var n = 0; n < 4; n++) {
                    int nx = x + dx[n], ny = y + dy[n]; if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    var nk = ny * w + nx; if (!bg[nk] && label[nk] == 0) { label[nk] = id; part.Add(nk); }
                }
            }
            sizes.Add(part.Count); touches.Add(edgeHit);
        }
        var largest = 0; foreach (var size in sizes) largest = Math.Max(largest, size);
        for (var k = 0; k < w * h; k++) if (!bg[k] && touches[label[k]] && sizes[label[k]] < largest * .15) bg[k] = true;
        var cleared = 0;
        for (var k = 0; k < w * h; k++) if (bg[k]) { px[k * 4 + 3] = 0; cleared++; }
        Write(bmp, px);
        return cleared * 100 / (w * h);
    }
    // Bounding box of visible pixels; empty when the cell is fully transparent.
    public static Rectangle Bounds(Bitmap bmp) {
        var px = Read(bmp); int l = bmp.Width, t = bmp.Height, r = -1, b = -1;
        for (var y = 0; y < bmp.Height; y++) for (var x = 0; x < bmp.Width; x++)
            if (px[(y * bmp.Width + x) * 4 + 3] > 16) { l = Math.Min(l, x); t = Math.Min(t, y); r = Math.Max(r, x); b = Math.Max(b, y); }
        return r < 0 ? Rectangle.Empty : new Rectangle(l, t, r - l + 1, b - t + 1);
    }
    public static bool Opaque(Bitmap bmp, int x, int y) { return bmp.GetPixel(x, y).A > 16; }
}
'@
# Extract generated atlas cells; the art itself is never recolored or redrawn (only background removal and mirroring).
# Each monster gets the four runtime variants used by map.js / battle-fx.js: x.png 320x221, x_hd.png 240x240, x_48.png, x_24.png.
function Save-Fit($Image, $Source, [int]$Width, [int]$Height, [double]$Margin, [string]$Path) {
    $canvas = [System.Drawing.Bitmap]::new($Width, $Height)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $scale = [Math]::Min($Width * (1 - 2 * $Margin) / $Source.Width, $Height * (1 - 2 * $Margin) / $Source.Height)
        $w = [float]($Source.Width * $scale); $h = [float]($Source.Height * $scale)
        $target = [System.Drawing.RectangleF]::new(($Width - $w) / 2, ($Height - $h) / 2, $w, $h)
        $graphics.DrawImage($Image, $target, [System.Drawing.RectangleF]$Source, [System.Drawing.GraphicsUnit]::Pixel)
        $canvas.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $canvas.Dispose() }
}
function Export-Cells([string]$Source, [int]$Columns, [int]$Rows, [string[]]$Names) {
    if ($Names.Count -ne $Columns * $Rows) { throw 'Atlas name count does not match grid.' }
    $null = New-Item -ItemType Directory -Force -Path $OutputRoot
    $atlas = [System.Drawing.Bitmap]::new((Resolve-Path -LiteralPath $Source).Path)
    try {
        if (!$KeyChecker) {
            foreach ($corner in @(@(0, 0), @(($atlas.Width - 1), 0), @(0, ($atlas.Height - 1)), @(($atlas.Width - 1), ($atlas.Height - 1)))) {
                if ([HellAtlas]::Opaque($atlas, $corner[0], $corner[1])) { throw "$Source has an opaque background (corner $($corner -join ',')); use -KeyChecker for a painted checkerboard." }
            }
        }
        # Painted grid lines sit on the cell borders; an inset keeps them out of every cell.
        $inset = if ($KeyChecker) { 6 } else { 0 }
        for ($i = 0; $i -lt $Names.Count; $i++) {
            $col = $i % $Columns; $row = [Math]::Floor($i / $Columns)
            $left = [int][Math]::Round($col * $atlas.Width / $Columns); $top = [int][Math]::Round($row * $atlas.Height / $Rows)
            $right = [int][Math]::Round(($col + 1) * $atlas.Width / $Columns); $bottom = [int][Math]::Round(($row + 1) * $atlas.Height / $Rows)
            $rect = [System.Drawing.Rectangle]::new($left + $inset, $top + $inset, $right - $left - 2 * $inset, $bottom - $top - 2 * $inset)
            $cell = [HellAtlas]::Cell($atlas, $rect, $Flip -contains $Names[$i])
            try {
                if ($KeyChecker) { $pct = [HellAtlas]::KeyChecker($cell, $Halo -contains $Names[$i]); Write-Output "$($Names[$i]): cleared $pct% background" }
                $body = [HellAtlas]::Bounds($cell)
                if ($body.IsEmpty) { throw "Atlas cell $i ($($Names[$i])) is empty." }
                $base = Join-Path $OutputRoot $Names[$i]
                Save-Fit $cell $body 320 221 0.03 "$base.png"
                Save-Fit $cell $body 240 240 0.03 "${base}_hd.png"
                Save-Fit $cell $body 48 48 0.02 "${base}_48.png"
                Save-Fit $cell $body 24 24 0.0 "${base}_24.png"
            } finally { $cell.Dispose() }
        }
    } finally { $atlas.Dispose() }
}
$bosses = @('boss_thoi_phan_quan', 'boss_hac_hung_tinh', 'boss_thanh_anh_dai_vuong', 'boss_thiet_phien_cong_chua', 'boss_nguu_ma_vuong', 'boss_diem_la_vuong')
$monsters = @('quai_nguu_dau', 'quai_ma_dien', 'quai_hac_vo_thuong', 'quai_bach_vo_thuong', 'quai_hoa_diem_tieu_yeu', 'tinh_bach_cot_tinh')
Export-Cells $BossAtlas 3 2 $bosses
Export-Cells $MonsterAtlas 3 2 $monsters
Write-Output "Extracted $($bosses.Count + $monsters.Count) Hell monsters x 4 sizes into $OutputRoot."
