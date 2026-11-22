<#
.SYNOPSIS
  Deploy backend online lên máy nhà (Docker Desktop + Cloudflare Tunnel) đúng commit của một tag phát hành.
.DESCRIPTION
  Lấy mã nguồn của tag bằng git archive (không dùng thư mục làm việc đang sửa dở), build image vandao-online:<tag>,
  chuyển nhãn current, khởi động lại app và chờ /healthz. Không qua healthz trong 3 phút thì quay về tag trước.
  Xem tools/online_cloud/HOME.md.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File deploy\home\deploy-home.ps1 -Tag v0.15.2
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File deploy\home\deploy-home.ps1 -Tag v0.15.3 -MigrationsApplied
#>
param(
    [Parameter(Mandatory = $true)][string]$Tag,
    [string]$ServerDir = (Join-Path $env:USERPROFILE 'vandao-server'),
    [switch]$MigrationsApplied
)
$ErrorActionPreference = 'Stop'

function Assert-Exit([string]$What) {
    if ($LASTEXITCODE -ne 0) { throw "$What thất bại (mã $LASTEXITCODE)." }
}

# PowerShell 5.1: chuyển stderr của lệnh native về $null khi ErrorActionPreference=Stop sẽ ném lỗi, nên tạm hạ xuống Continue
function Invoke-Quiet([scriptblock]$Cmd) {
    $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { & $Cmd 2>$null } finally { $ErrorActionPreference = $old }
}

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$compose = Join-Path $ServerDir 'docker-compose.yml'
$tagFile = Join-Path $ServerDir 'current-tag'

if ($Tag -notmatch '^v\d+\.\d+\.\d+$') { throw "Tag phải có dạng vX.Y.Z (vd v0.15.2)." }
foreach ($f in @('.env', 'secrets\supabase-ca.crt')) {
    if (-not (Test-Path (Join-Path $ServerDir $f))) { throw "Thiếu $ServerDir\$f (xem tools/online_cloud/HOME.md)." }
}
docker info --format '{{.ServerVersion}}' | Out-Null
Assert-Exit 'Docker Desktop chưa chạy? docker info'

Write-Host "== Lấy tag $Tag"
git -C $repo fetch --tags --force origin; Assert-Exit 'git fetch'
$sha = (git -C $repo rev-parse --verify "refs/tags/$Tag^{commit}"); Assert-Exit "Không có tag $Tag"
git -C $repo merge-base --is-ancestor $sha origin/master
if ($LASTEXITCODE -ne 0) { throw "$Tag không nằm trên master." }

$prev = if (Test-Path $tagFile) { (Get-Content $tagFile -Raw).Trim() } else { '' }
# schema phải có trên Supabase cloud trước khi backend mới chạy (tools/online_cloud/README.md mục 2)
if (-not $MigrationsApplied) {
    $base = $prev
    if (-not $base) { $base = (Invoke-Quiet { git -C $repo describe --tags --abbrev=0 --match 'v[0-9]*' "$Tag^" }) }
    $changed = $true
    if ($base) { Invoke-Quiet { git -C $repo diff --quiet $base $sha -- Online/Migrations supabase/migrations }; $changed = ($LASTEXITCODE -ne 0) }
    if ($changed) {
        throw "$Tag có migration mới so với $(if ($base) { $base } else { 'lịch sử đầu' }). Áp migration lên Supabase trước, rồi chạy lại với -MigrationsApplied."
    }
}

Write-Host "== Build vandao-online:$Tag"
$work = Join-Path $env:TEMP "vandao-src-$Tag"
$tar = "$work.tar"
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory -Path $work | Out-Null
git -C $repo archive --format=tar -o $tar $sha -- Dockerfile.online .dockerignore Program.cs Game Data Online Desktop/AppPaths.cs wwwroot tools/online_host
Assert-Exit 'git archive'
$tarExe = Join-Path $env:SystemRoot 'System32\tar.exe'
if (-not (Test-Path $tarExe)) { $tarExe = 'tar' }
& $tarExe -xf $tar -C $work; Assert-Exit 'Giải nén mã nguồn'
docker build -f (Join-Path $work 'Dockerfile.online') -t "vandao-online:$Tag" $work; Assert-Exit 'docker build'
Remove-Item -Recurse -Force $work, $tar

Copy-Item (Join-Path $repo 'deploy\home\docker-compose.yml') $compose -Force

function Switch-To([string]$t) {
    docker tag "vandao-online:$t" vandao-online:current; Assert-Exit 'docker tag'
    docker compose --project-directory $ServerDir -f $compose up -d --remove-orphans --force-recreate app; Assert-Exit 'docker compose up app'
    docker compose --project-directory $ServerDir -f $compose up -d cloudflared; Assert-Exit 'docker compose up cloudflared'
}

function Test-Healthy {
    for ($i = 0; $i -lt 36; $i++) {
        try {
            $r = Invoke-RestMethod -Uri 'http://127.0.0.1:18080/healthz' -TimeoutSec 5
            if ($r.status -eq 'ok') { return $true }
        } catch { }
        Start-Sleep -Seconds 5
    }
    return $false
}

Write-Host "== Chạy $Tag (trước đó: $(if ($prev) { $prev } else { 'chưa có' }))"
Switch-To $Tag
if (-not (Test-Healthy)) {
    Write-Warning "$Tag không qua /healthz. Log gần nhất:"
    docker compose --project-directory $ServerDir -f $compose logs --tail 80 app
    if ($prev -and $prev -ne $Tag) {
        Invoke-Quiet { docker image inspect "vandao-online:$prev" } | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Warning "Quay về $prev"
            Switch-To $prev
            if (-not (Test-Healthy)) { Write-Warning "$prev cũng không lên. Xem: docker compose --project-directory $ServerDir logs app" }
        }
    }
    throw "Deploy $Tag thất bại."
}
Set-Content -Path $tagFile -Value $Tag -Encoding ascii

# Giữ 3 image gần nhất để quay về nhanh
Write-Host '== Dọn image cũ'
$tags = @(docker images vandao-online --format '{{.Tag}}' | Where-Object { $_ -match '^v\d' })
$tags | Select-Object -Skip 3 | Where-Object { $_ -ne $Tag } | ForEach-Object { docker rmi "vandao-online:$_" | Out-Null }
docker image prune -f | Out-Null

Write-Host "== Đã chạy $Tag. Kiểm tra: http://127.0.0.1:18080/healthz và tên miền public của tunnel."
