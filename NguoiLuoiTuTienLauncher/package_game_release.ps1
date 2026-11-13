param(
    [string]$Version = "0.7.2",
    [string]$GameRoot = (Resolve-Path "$PSScriptRoot\..").Path
)

Write-Host "=== Dong goi ban phat hanh v$Version ===" -ForegroundColor Cyan

# 1. Build game ban Release gom toan bo DLL vao 1 file EXE duy nhat
Write-Host "1. Dang bien dich Game (Release Single-File)..." -ForegroundColor Yellow
$publishDir = "$GameRoot\bin\Publish_Game"
if (Test-Path $publishDir) {
    $resolvedPublish = (Resolve-Path -LiteralPath $publishDir).Path
    $expectedPublish = [IO.Path]::GetFullPath((Join-Path $GameRoot 'bin\Publish_Game'))
    if ($resolvedPublish -ne $expectedPublish) { throw 'Unexpected publish directory.' }
    Remove-Item -LiteralPath $resolvedPublish -Recurse -Force
}

dotnet publish "$GameRoot\VanDao.csproj" -c Release -r win-x64 -o $publishDir -p:VersionFromTag=false -p:Version=$Version -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true --self-contained true
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build game that bai!" -ForegroundColor Red
    exit 1
}

# 2. Xoa cac file khong can thiet
Get-ChildItem -Path $publishDir -Filter "*.pdb" | Remove-Item -Force
Get-ChildItem -Path $publishDir -Filter "*.xml" | Remove-Item -Force
if (Test-Path "$publishDir\web.config") { Remove-Item "$publishDir\web.config" -Force }
if (Test-Path "$publishDir\docs") { Remove-Item "$publishDir\docs" -Recurse -Force }
if (Test-Path "$publishDir\NguoiLuoiTuTienLauncher") { Remove-Item "$publishDir\NguoiLuoiTuTienLauncher" -Recurse -Force }
if (Test-Path "$publishDir\launcher_settings.json") { Remove-Item "$publishDir\launcher_settings.json" -Force }

# 3. Nen thanh file zip
$zipName = "VanDao_v$Version.zip"
$zipPath = "$GameRoot\$zipName"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "2. Dang nen thanh $zipName..." -ForegroundColor Yellow
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force

Write-Host "`n=== HOAN TAT ===" -ForegroundColor Green
$s = "{0:N2} MB" -f ((Get-Item $zipPath).Length / 1MB)
Write-Host "Goi game sach dep chi gom NguoiLuoiTuTien.exe va wwwroot da san sang tai:" -ForegroundColor Cyan
Write-Host "  $zipPath ($s)" -ForegroundColor White
