$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe='C:\Program Files\Rainmeter\Rainmeter.exe'
$installed="$env:USERPROFILE/Documents/Rainmeter/Skins/ViceCity"
function Command([string]$command){
  $p=Start-Process $exe -ArgumentList @('!CommandMeasure','Layout',$command,'ViceCity\Dashboard') -WindowStyle Hidden -PassThru
  if(!$p.WaitForExit(5000)){throw "Unresponsive command: $command"}
}
Command 'Inspect()'
$current=Get-Content "$installed/@Resources/layout-state.json" -Raw|ConvertFrom-Json
if($current.drawer -ne 0){Command "Toggle($($current.drawer))";Start-Sleep -Milliseconds 500}
Command 'Inspect()'
$baseline=Get-Content "$installed/@Resources/layout-state.json" -Raw|ConvertFrom-Json
$results=@()
foreach($index in 1..4){
  $watch=[Diagnostics.Stopwatch]::StartNew();Command "Toggle($index)";$watch.Stop()
  Start-Sleep -Milliseconds 500
  Command 'Inspect()'
  $state=Get-Content "$installed/@Resources/layout-state.json" -Raw|ConvertFrom-Json
  foreach($name in @('Logo','Timedays','Timehours','Timeminutes','Timeseconds')){
    $a=$baseline.meters.$name;$b=$state.meters.$name
    if($a.x -ne $b.x -or $a.y -ne $b.y -or ($name -eq 'Logo' -and ($a.w -ne $b.w -or $a.h -ne $b.h))){throw "Header moved in drawer $index : $name"}
  }
  $panel=if($index -le 2){$state.meters.ConradPanel}else{$state.meters.CodexPanel}
  if($panel.y -lt 706 -or $panel.y+$panel.h -gt 1150){throw "Panel overflow: $index"}
  $results+=[pscustomobject]@{drawer=$index;commandMs=$watch.Elapsed.TotalMilliseconds;headerFixed=$true;meters=$state.meters}
  Command "Toggle($index)";Start-Sleep -Milliseconds 500
}
$results|ConvertTo-Json -Depth 6|Set-Content "$root/artifacts/validation/native-fixed-layout-checks.json"
Write-Output 'All four native drawer states preserve logo/timer coordinates and fit the fixed panel zone.'
