$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$ini = Get-Content "$env:APPDATA/Rainmeter/Rainmeter.ini"
$skinRoot = ($ini | Where-Object { $_ -match '^SkinPath=' } | Select-Object -First 1) -replace '^SkinPath=',''
if (!$skinRoot) { throw 'Rainmeter SkinPath absent' }
$plugins = "$env:APPDATA/Rainmeter/Plugins"
if (Test-Path "$plugins/ViceCity.dll") { throw 'Probe already installed. Unload and restart Rainmeter before replacing the plugin.' }
New-Item -ItemType Directory -Path $plugins -Force | Out-Null
Copy-Item "$root/build/ViceCity.dll" $plugins
Copy-Item "$root/build/ViceCity" $plugins -Recurse
Copy-Item "$root/skins/ViceCity" $skinRoot -Recurse
& 'C:\Program Files\Rainmeter\Rainmeter.exe' !RefreshApp
Start-Sleep -Milliseconds 800
& 'C:\Program Files\Rainmeter\Rainmeter.exe' !ActivateConfig 'ViceCity\Probe' 'Probe.ini'
$deadline = (Get-Date).AddSeconds(40)
do {
  Start-Sleep -Milliseconds 500
  $statePath = "$env:LOCALAPPDATA/ViceCityRainmeter/probe.json"
  if (Test-Path $statePath) {
    $state = Get-Content $statePath -Raw | ConvertFrom-Json
    if (([DateTimeOffset]::Now - [DateTimeOffset]$state.sampledAt).TotalSeconds -lt 5) {
      Write-Output "Probe loaded in process $($state.processId). Legacy applications and startup untouched."
      return
    }
  }
} while ((Get-Date) -lt $deadline)
throw 'Files installed, but no fresh probe result. Installation is not validated.'
