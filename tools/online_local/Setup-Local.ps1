param([string]$SupabaseCli='supabase',[string]$Docker='docker')
$ErrorActionPreference='Stop'
$root=[System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location -LiteralPath $root
New-Item -ItemType Directory -Force .local|Out-Null
# Windows PowerShell turns native stderr into terminating errors under Stop; native steps check $LASTEXITCODE instead.
$ErrorActionPreference='Continue'
$network=& $Docker network inspect vandao-online-local --format '{{.Name}}' 2>$null
if($LASTEXITCODE -ne 0){& $Docker network create --driver bridge --opt com.docker.network.bridge.host_binding_ipv4=127.0.0.1 vandao-online-local|Out-Null}
& $SupabaseCli start --workdir tools/online_local --network-id vandao-online-local -x realtime,storage-api,imgproxy,mailpit,edge-runtime,logflare,vector,supavisor *> .local/supabase-start.log
if($LASTEXITCODE -ne 0){throw 'Supabase start failed; inspect the ignored .local/supabase-start.log.'}
& $SupabaseCli status --workdir tools/online_local --output json 2> .local/supabase-status-errors.log|Set-Content .local/supabase-status.json -Encoding utf8
$ErrorActionPreference='Stop'
dotnet build tools/online_checks -p:NuGetAudit=false -v:q
if($LASTEXITCODE -ne 0){throw 'Local check build failed.'}
# Docker Desktop can override the bridge default; enforce explicit host bindings too.
dotnet run --project tools/online_checks --no-build -- bind-loopback $root
if($LASTEXITCODE -ne 0){throw 'Loopback binding failed; keep the local stack stopped.'}
dotnet run --project tools/online_checks --no-build -- prepare $root
if($LASTEXITCODE -ne 0){throw 'Local schema preparation failed.'}
Write-Host 'Prepared isolated local Supabase. No cloud project or existing save was modified.'
