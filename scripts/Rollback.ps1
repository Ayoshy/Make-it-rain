param([switch]$RestoreInitialGpu)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe='C:\Program Files\Rainmeter\Rainmeter.exe'
$statePath="$env:LOCALAPPDATA/ViceCityRainmeter/probe.json"
$state=if(Test-Path $statePath){Get-Content $statePath -Raw|ConvertFrom-Json}else{$null}
if(Get-Process 'ViceCity.GpuHelper' -ErrorAction SilentlyContinue){
  & $exe !CommandMeasure Mcpu GpuRead 'ViceCity\Dashboard'
  Start-Sleep -Seconds 2
  $state=Get-Content $statePath -Raw|ConvertFrom-Json
  $state|Select-Object sampledAt,gpuControl,heatwaveActive|ConvertTo-Json -Depth 4|Set-Content "$root/artifacts/validation/gpu-before-rollback.json"
  if(!$state.gpuControl -or !$state.gpuControl.fanAuto -or $state.heatwaveActive -or $state.gpuControl.thermalLimitCelsius -ne 83){
    if(!$RestoreInitialGpu){throw 'GPU override saved. Use -RestoreInitialGpu only to deliberately restore the pre-session GPU settings before returning to the old desktop.'}
  }
}
foreach($skin in @('ViceCity\Dashboard','ViceCity\Background','ViceCity\Probe','ViceCity\Desktop')){& $exe !DeactivateConfig $skin}
Start-Sleep -Seconds 3
if(Get-Process 'ViceCity.GpuHelper' -ErrorAction SilentlyContinue){throw 'GPU helper has not exited; no forced termination performed'}
$r=Get-Process -Id $state.processId -ErrorAction SilentlyContinue
if($r -and $r.ProcessName -eq 'Rainmeter'){& $exe !Quit;if(!$r.WaitForExit(15000)){throw 'Rainmeter did not exit cleanly'}}
if(Test-Path "$root/backups/startup-before-native.json"){& "$PSScriptRoot/Set-NativeStartup.ps1" -Restore}
$apps=@{
 ConradSensor='C:\Users\Ayo\Documents\Project\Conrad Sensor\artifacts\wallpaper-win-x64\ConradSensor.exe'
 CodexMeter='C:\Users\Ayo\Documents\Project\Codex Meter\artifacts\wallpaper-win-x64\CodexMeter.exe'
}
foreach($name in $apps.Keys){if(!(Get-Process $name -ErrorAction SilentlyContinue)){Start-Process $apps[$name] -WindowStyle Hidden}}
if(!(Get-Process wallpaper64 -ErrorAction SilentlyContinue)){Start-Process 'C:\Program Files (x86)\Steam\steamapps\common\wallpaper_engine\wallpaper64.exe' -ArgumentList '-silent' -WindowStyle Hidden}
Start-Process $exe -WindowStyle Hidden
Write-Output 'Original desktop applications restarted; native skins disabled. Projects were preserved.'
