param(
    [Parameter(Mandatory=$true)][string]$GearAtlas,
    [Parameter(Mandatory=$true)][string]$SkillAtlas,
    [string]$OutputRoot = '',
    [switch]$GearOnly
)
$ErrorActionPreference = 'Stop'
if (!$OutputRoot) { $OutputRoot = Join-Path $PSScriptRoot '../../wwwroot/img/final_realms' }
Add-Type -AssemblyName System.Drawing
# Extract generated atlas cells; never recolor, redraw or replace the original artwork.
function Export-Cells([string]$Source, [int]$Columns, [int]$Rows, [string[]]$Names, [string]$Folder) {
    if ($Names.Count -ne $Columns * $Rows) { throw 'Atlas name count does not match grid.' }
    $null = New-Item -ItemType Directory -Force -Path $Folder
    $atlas = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $Source))
    try {
        for ($i = 0; $i -lt $Names.Count; $i++) {
            $col = $i % $Columns
            $row = [Math]::Floor($i / $Columns)
            $left = [int][Math]::Round($col * $atlas.Width / $Columns)
            $top = [int][Math]::Round($row * $atlas.Height / $Rows)
            $right = [int][Math]::Round(($col + 1) * $atlas.Width / $Columns)
            $bottom = [int][Math]::Round(($row + 1) * $atlas.Height / $Rows)
            $width = $right - $left
            $height = $bottom - $top
            $icon = [System.Drawing.Bitmap]::new(128, 128)
            $graphics = [System.Drawing.Graphics]::FromImage($icon)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $scale = [Math]::Min(128.0 / $width, 128.0 / $height)
                $drawWidth = [float]($width * $scale)
                $drawHeight = [float]($height * $scale)
                $target = [System.Drawing.RectangleF]::new((128-$drawWidth)/2, (128-$drawHeight)/2, $drawWidth, $drawHeight)
                $sourceRect = [System.Drawing.RectangleF]::new($left, $top, $width, $height)
                $graphics.DrawImage($atlas, $target, $sourceRect, [System.Drawing.GraphicsUnit]::Pixel)
                $icon.Save((Join-Path $Folder ($Names[$i] + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
            } finally { $graphics.Dispose(); $icon.Dispose() }
        }
    } finally { $atlas.Dispose() }
}
$pieces = @('kiem','chau','mu','ao','gang','quan','giay','day','phu','nhan')
$gearNames = foreach ($prefix in @('dk','ct')) { foreach ($piece in $pieces) { "${prefix}_${piece}" } }
$skillNames = foreach ($element in @('hoa','thuy','moc','tho','loi','bang','gio')) {
    foreach ($realm in @('do_kiep','chan_tien')) { foreach ($tree in @('active','passive')) { "${element}_${realm}_${tree}" } }
}
Export-Cells $GearAtlas 5 4 $gearNames (Join-Path $OutputRoot 'gear')
if (!$GearOnly) { Export-Cells $SkillAtlas 4 7 $skillNames (Join-Path $OutputRoot 'skills') }
Write-Output 'Extracted equipment icons (128 x 128 PNG), preserving source alpha.'
