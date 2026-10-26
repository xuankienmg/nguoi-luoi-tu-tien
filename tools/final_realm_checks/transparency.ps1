param([string]$Root = '.')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$icons = Get-ChildItem (Join-Path $Root 'wwwroot/img/final_realms/gear') -Filter '*.png'
if ($icons.Count -ne 20) { throw 'Expected twenty final realm gear icons.' }
foreach ($file in $icons) {
    $icon = [System.Drawing.Bitmap]::FromFile($file.FullName)
    try {
        foreach ($corner in @(@(0,0),@(127,0),@(0,127),@(127,127))) {
            if ($icon.GetPixel($corner[0],$corner[1]).A -ne 0) { throw "Opaque background corner: $($file.Name)" }
        }
        $transparent = 0
        $visible = 0
        for ($y=0; $y -lt $icon.Height; $y++) { for ($x=0; $x -lt $icon.Width; $x++) {
            $alpha = $icon.GetPixel($x,$y).A
            if ($alpha -eq 0) { $transparent++ }
            if ($alpha -ge 128) { $visible++ }
        } }
        if ($transparent -lt $icon.Width*$icon.Height*.15) { throw "Background not removed: $($file.Name)" }
        if ($visible -lt $icon.Width*$icon.Height*.05) { throw "Equipment artwork missing: $($file.Name)" }
        $wiki = Join-Path $Root "site/img/wiki/final_realms/gear/$($file.Name)"
        if (!(Test-Path -LiteralPath $wiki) -or (Get-FileHash -LiteralPath $wiki).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw "Wiki icon differs: $($file.Name)" }
        foreach ($runtimeRoot in @('bin/Debug/net8.0-windows','build-check/final-realms')) {
            $runtime = Join-Path $Root "$runtimeRoot/wwwroot/img/final_realms/gear/$($file.Name)"
            if ((Test-Path -LiteralPath $runtime) -and (Get-FileHash -LiteralPath $runtime).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw "Runtime icon differs: $runtime" }
        }
    } finally { $icon.Dispose() }
}
Write-Output 'PASS: 20 transparent gear icons, visible artwork and matching wiki copies.'
