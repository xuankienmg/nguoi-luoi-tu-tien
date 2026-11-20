<#
Gọi Codex CLI (bản desktop OpenAI Codex) để tạo ảnh bằng image_generation,
rồi chép ảnh mới sinh từ ~/.codex/generated_images vào thư mục output.

Ví dụ:
  powershell -ExecutionPolicy Bypass -File tools/codex-imagegen/codex-image.ps1 `
    -Prompt "Pháp tướng phong hệ, nền trong suốt..." -OutDir output/imagegen/phong-v1 -Ref output/x/ref.png -Count 2
#>
param(
  [Parameter(Mandatory = $true)][string]$Prompt,
  [Parameter(Mandatory = $true)][string]$OutDir,
  [string[]]$Ref = @(),
  [int]$Count = 1,
  [string]$Model = "",
  [int]$TimeoutMin = 15
)
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path

# Tìm codex.exe: PATH trước, sau đó thư mục bin của app desktop (tên thư mục hash đổi theo bản cập nhật).
$codex = (Get-Command codex -ErrorAction SilentlyContinue).Source
if (-not $codex) {
  $codex = Get-ChildItem "$env:LOCALAPPDATA\OpenAI\Codex\bin\*\codex.exe" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $codex) { throw "Không tìm thấy codex.exe. Cài/mở app OpenAI Codex hoặc thêm codex vào PATH." }

$genRoot = Join-Path $env:USERPROFILE ".codex\generated_images"
if (-not [IO.Path]::IsPathRooted($OutDir)) { $OutDir = Join-Path $repo $OutDir }
New-Item -ItemType Directory -Force $OutDir | Out-Null
$start = Get-Date

$full = @"
Dùng công cụ tạo ảnh (image_generation) để tạo $Count ảnh theo mô tả dưới đây. Chỉ tạo ảnh, không sửa file nào trong repo.
Nếu có ảnh tham chiếu đính kèm, giữ nhận diện/tạo hình/màu của chúng. Trả lời ngắn: số ảnh đã tạo và mô tả mỗi ảnh một dòng.

MÔ TẢ:
$Prompt
"@
Set-Content -Path (Join-Path $OutDir "prompt.txt") -Value $Prompt -Encoding utf8

$cargs = @("exec", "-C", $repo, "--sandbox", "read-only", "--skip-git-repo-check",
          "-o", (Join-Path $OutDir "codex-reply.txt"))
if ($Model) { $cargs += @("-m", $Model) }
foreach ($r in $Ref) {
  $p = if ([IO.Path]::IsPathRooted($r)) { $r } else { Join-Path $repo $r }
  $cargs += @("-i", (Resolve-Path $p).Path)
}
$cargs += "-"

Write-Host "codex: $codex"
$job = Start-Job { param($exe, $a, $text) $text | & $exe @a 2>&1 } -ArgumentList $codex, $cargs, $full
if (-not (Wait-Job $job -Timeout ($TimeoutMin * 60))) { Stop-Job $job; throw "Codex quá $TimeoutMin phút, đã dừng." }
Receive-Job $job | Select-Object -Last 20 | ForEach-Object { Write-Host $_ }
Remove-Job $job

$new = Get-ChildItem $genRoot -Recurse -File -ErrorAction SilentlyContinue |
  Where-Object { $_.LastWriteTime -ge $start } | Sort-Object LastWriteTime
if (-not $new) { throw "Codex chạy xong nhưng không có ảnh mới trong $genRoot. Xem $OutDir\codex-reply.txt." }
$i = 0
foreach ($f in $new) {
  $i++
  $dest = Join-Path $OutDir ("codex-{0:D2}{1}" -f $i, $f.Extension)
  Copy-Item $f.FullName $dest -Force
  Write-Output $dest
}

