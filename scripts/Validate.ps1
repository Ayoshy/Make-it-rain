$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
dotnet run --project "$root/tests/ViceCity.Tests.csproj" -c Release --nologo
if ($LASTEXITCODE) { throw 'Contract tests failed' }
foreach ($f in Get-ChildItem "$root/web" -Filter '*.js') { node --check $f.FullName; if ($LASTEXITCODE) { throw "JS syntax: $($f.Name)" } }
$forbidden = @(Get-ChildItem "$root/src","$root/web","$root/skins" -File -Recurse | Where-Object Name -in 'conrad-connection.js','codex-connection.js','wallpaper-token.txt','auth.json')
if ($forbidden.Count) { throw 'Private association files must not be in source' }
$bridge = rg -l '127\.0\.0\.1:1918[78]|X-Conrad-Token|X-Codex-Meter-Token' "$root/web" "$root/src" -g '!obj/**' -g '!bin/**'
if ($bridge) { throw 'Legacy HTTP bridge reference remains in the implementation' }
if ($LASTEXITCODE -gt 1) { throw 'Source scan failed' }
$global:LASTEXITCODE = 0
Write-Output 'Source checks passed. Does not certify real clicks, GPU writes, desktop placement, game mode, or startup.'
