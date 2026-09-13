param([switch]$DashboardOnly)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe='C:\Program Files\Rainmeter\Rainmeter.exe'
$installed="$env:USERPROFILE/Documents/Rainmeter/Skins/ViceCity"
$plugins="$env:APPDATA/Rainmeter/Plugins"
$originalPid=(Get-Process Rainmeter).Id
$helperBefore=@(Get-Process 'ViceCity.GpuHelper' -ErrorAction SilentlyContinue).Id
# Hold one data measure while replacing the dashboard: GPU session stays alive.
$probe=@'
[Rainmeter]
Update=1000
OnRefreshAction=[!KeepOnScreen 0][!Move -10 -10][!ClickThrough 1]
[Hold]
Measure=Plugin
Plugin=ViceCityNative
Metric=summary
[Anchor]
Meter=Image
W=1
H=1
SolidColor=0,0,0,1
'@
Copy-Item "$installed/Probe/Probe.ini" "$root/artifacts/validation/probe-before-visual-update.ini" -Force
Set-Content "$installed/Probe/Probe.ini" $probe -Encoding Unicode
& $exe !ActivateConfig 'ViceCity\Probe' 'Probe.ini'
Start-Sleep -Milliseconds 750
try {
  $reloadAt=Get-Date
  if(!$DashboardOnly){
    & $exe !DeactivateConfig 'ViceCity\Background'
    Start-Sleep -Milliseconds 750
    Copy-Item "$root/build/native/ViceCityGlass.dll" $plugins -Force
    Copy-Item "$root/skins/ViceCity/Background/Background.ini" "$installed/Background/Background.ini" -Force
  }
  Copy-Item "$root/skins/ViceCity/Dashboard/Dashboard.ini" "$installed/Dashboard/Dashboard.ini" -Force
  Copy-Item "$root/skins/ViceCity/@Resources/Dashboard.lua","$root/skins/ViceCity/@Resources/RevealMeters.lua" "$installed/@Resources/" -Force
  if(!$DashboardOnly){& $exe !ActivateConfig 'ViceCity\Background' 'Background.ini'}
  & $exe !Refresh 'ViceCity\Dashboard'
  $deadline=(Get-Date).AddSeconds(15)
  do {
    Start-Sleep -Milliseconds 250
    $path="$env:LOCALAPPDATA/ViceCityRainmeter/native-renderer-state.json"
    $ready=(Test-Path $path) -and (Get-Item $path).LastWriteTime -gt $reloadAt
  }while(!$ready -and (Get-Date) -lt $deadline)
  if(!$ready){throw 'New native renderer frame was not verified'}
  if((Get-Process Rainmeter).Id -notcontains $originalPid){throw 'Rainmeter process changed'}
  $helperAfter=@(Get-Process 'ViceCity.GpuHelper' -ErrorAction SilentlyContinue).Id
  if($helperBefore -and @(Compare-Object @($helperBefore) @($helperAfter)).Count){throw 'GPU helper session changed'}
  Write-Output "Visuals updated in Rainmeter $originalPid; GPU helper session preserved."
} finally {
  & $exe !DeactivateConfig 'ViceCity\Probe'
}
