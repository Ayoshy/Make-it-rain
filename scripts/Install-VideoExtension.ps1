param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$extension=Join-Path $root 'browser/video-dock'
if(!(Test-Path (Join-Path $extension 'manifest.json'))){throw 'Extension Battlestation introuvable dans le dépôt principal'}
Start-Process 'brave.exe' 'brave://extensions'
Write-Output "Dans Brave, clique Charger l extension non empaquetee puis selectionne : $extension"
