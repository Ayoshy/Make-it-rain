$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$exe = 'C:\Program Files\Rainmeter\Rainmeter.exe'
$plugins = "$env:APPDATA/Rainmeter/Plugins"
if (!(Test-Path "$plugins/ViceCity.dll")) { throw 'Install the probe first' }
$instances = @(Get-Process Rainmeter)
if ($instances.Count -ne 1) { throw 'Expected exactly one Rainmeter instance' }
Copy-Item "$env:APPDATA/Rainmeter/Rainmeter.ini" "$root/artifacts/validation/Rainmeter-before-update.ini" -Force
& $exe !Quit
if (!$instances[0].WaitForExit(15000)) { throw 'Rainmeter did not exit cleanly. No files replaced.' }
Copy-Item "$root/build/ViceCity.dll" $plugins -Force
Copy-Item "$root/build/ViceCity/*" "$plugins/ViceCity/" -Recurse -Force
$started = Start-Process -FilePath $exe -WindowStyle Hidden -PassThru
$deadline = (Get-Date).AddSeconds(30)
do {
  Start-Sleep -Milliseconds 500
  $statePath = "$env:LOCALAPPDATA/ViceCityRainmeter/probe.json"
  if (Test-Path $statePath) {
    $state = Get-Content $statePath -Raw | ConvertFrom-Json
    if ($state.processId -eq $started.Id -and ([DateTimeOffset]::Now - [DateTimeOffset]$state.sampledAt).TotalSeconds -lt 5) {
      Write-Output "Fresh plugin data verified in Rainmeter process $($started.Id); original desktop applications untouched."
      return
    }
  }
} while ((Get-Date) -lt $deadline)
throw 'Rainmeter restarted, but an active fresh probe was not verified.'
