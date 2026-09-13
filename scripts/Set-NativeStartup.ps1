param([switch]$Restore)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$backup=Join-Path $root 'backups/startup-before-native.json'
$key='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$names=@('ConradSensor','CodexMeter','WallpaperEngine')
if($Restore){
  if(!(Test-Path $backup)){throw 'No startup backup available'}
  foreach($item in (Get-Content $backup -Raw|ConvertFrom-Json)){
    if($item.exists){New-ItemProperty -Path $key -Name $item.name -Value $item.value -PropertyType String -Force|Out-Null}
    else{Remove-ItemProperty -Path $key -Name $item.name -ErrorAction SilentlyContinue}
  }
  Write-Output 'Original startup entries restored.';return
}
if(Get-Process ConradSensor,CodexMeter,wallpaper64,webwallpaper64 -ErrorAction SilentlyContinue){throw 'Legacy applications are still running'}
$native=Get-Content "$env:LOCALAPPDATA/ViceCityRainmeter/probe.json" -Raw|ConvertFrom-Json
if(([DateTimeOffset]::Now-[DateTimeOffset]$native.sampledAt).TotalSeconds -gt 15){throw 'Native data is not fresh'}
$link="$env:APPDATA/Microsoft/Windows/Start Menu/Programs/Startup/Rainmeter.lnk"
if(!(Test-Path $link)){throw 'Rainmeter startup shortcut is missing'}
$shortcut=(New-Object -ComObject WScript.Shell).CreateShortcut($link)
if($shortcut.TargetPath -ne 'C:\Program Files\Rainmeter\Rainmeter.exe'){throw 'Unexpected Rainmeter startup target'}
if(!(Test-Path $backup)){
  $values=Get-ItemProperty $key
  @($names|ForEach-Object {$entry=$values.PSObject.Properties[$_];[pscustomobject]@{name=$_;exists=($null -ne $entry);value=$entry.Value}})|ConvertTo-Json|Set-Content $backup
}
foreach($name in $names){Remove-ItemProperty -Path $key -Name $name -ErrorAction SilentlyContinue}
Write-Output 'Only the existing Rainmeter shortcut remains for desktop personalization startup.'
