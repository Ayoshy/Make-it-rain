$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe='C:\Program Files\Rainmeter\Rainmeter.exe'
$plugins="$env:APPDATA/Rainmeter/Plugins"
$skinRoot="$env:USERPROFILE/Documents/Rainmeter/Skins"
$instances=@(Get-Process Rainmeter -ErrorAction SilentlyContinue)
if($instances.Count -gt 1){throw 'Multiple Rainmeter instances'}
if($instances.Count -eq 1){
  & $exe !Quit
  if(!$instances[0].WaitForExit(15000)){throw 'Rainmeter did not exit cleanly'}
}
Copy-Item "$root/build/native/ViceCityNative.dll" $plugins -Force
Copy-Item "$root/build/native/ViceCityGlass.dll" $plugins -Force
New-Item -ItemType Directory "$plugins/ViceCityNative" -Force | Out-Null
Copy-Item "$root/build/native/ViceCityNative/*" "$plugins/ViceCityNative/" -Recurse -Force
Copy-Item "$root/skins/ViceCity/@Resources" "$skinRoot/ViceCity/" -Recurse -Force
foreach($name in @('Background','Dashboard','Probe')){Copy-Item "$root/skins/ViceCity/$name" "$skinRoot/ViceCity/" -Recurse -Force}
$started=Start-Process $exe -WindowStyle Hidden -PassThru
Start-Sleep -Milliseconds 1000
& $exe !DeactivateConfig 'ViceCity\Probe'
& $exe !DeactivateConfig 'ViceCity\Desktop'
& $exe !RefreshApp
Start-Sleep -Milliseconds 1000
& $exe !ActivateConfig 'ViceCity\Background' 'Background.ini'
& $exe !ActivateConfig 'ViceCity\Dashboard' 'Dashboard.ini'
$deadline=(Get-Date).AddSeconds(15)
do {
  Start-Sleep -Milliseconds 500
  $statePath="$env:LOCALAPPDATA/ViceCityRainmeter/native-renderer-state.json"
  if(Test-Path $statePath){try{$state=Get-Content $statePath -Raw | ConvertFrom-Json}catch{$state=$null};if($state.pid -eq $started.Id){Write-Output "Native Direct2D background active in Rainmeter $($started.Id). No WebView2 or legacy-app launch.";return}}
}while((Get-Date)-lt $deadline)
throw 'Installed, but native renderer activity was not verified'
