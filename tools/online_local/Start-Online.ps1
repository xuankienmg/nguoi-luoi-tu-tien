param([ValidateSet('Server','Client')][string]$Mode='Server',[switch]$NoBuild)
$ErrorActionPreference='Stop'
$root=[System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location -LiteralPath $root
if(-not $NoBuild){dotnet build tools/online_host -p:NuGetAudit=false -v:q;if($LASTEXITCODE -ne 0){throw 'Online host build failed.'}}
if($Mode -eq 'Server'){
    $path=Join-Path $root '.local/online-server.env.json'
    if(-not(Test-Path -LiteralPath $path)){throw 'Run the local Supabase setup and online_checks prepare first.'}
    $settings=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
    foreach($property in $settings.PSObject.Properties){[Environment]::SetEnvironmentVariable($property.Name,$property.Value,'Process')}
}else{
    $env:Online__Mode='Client';$env:Online__BackendUrl='http://127.0.0.1:5188';$env:Online__LocalPort='5189'
    [Environment]::SetEnvironmentVariable('ConnectionStrings__Game',$null,'Process')
}
[Environment]::SetEnvironmentVariable('Logging__LogLevel__Default','Warning','Process')
[Environment]::SetEnvironmentVariable('Logging__LogLevel__Microsoft.Hosting.Lifetime','Information','Process')
dotnet (Join-Path $root 'tools/online_host/bin/Debug/net8.0/online_host.dll') --web
