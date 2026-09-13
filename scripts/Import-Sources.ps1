$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$dest = Join-Path $root "backups/$stamp"
New-Item -ItemType Directory -Path $dest -Force | Out-Null
$sources = [ordered]@{
  conrad = 'C:\Users\Ayo\Documents\Project\Conrad Sensor'
  codex = 'C:\Users\Ayo\Documents\Project\Codex Meter'
  wallpaper = 'C:\Program Files (x86)\Steam\steamapps\common\wallpaper_engine\projects\myprojects\gta-vi-wallpaper'
}
$manifest = [Collections.Generic.List[object]]::new()
foreach ($name in $sources.Keys) {
  $source = $sources[$name]
  $target = Join-Path $dest $name
  New-Item -ItemType Directory -Path $target -Force | Out-Null
  foreach ($file in Get-ChildItem -LiteralPath $source -File -Recurse -Force) {
    $relative = $file.FullName.Substring($source.Length + 1)
    if ($relative -match '(^|[\\/])(\.git|bin|obj|node_modules)([\\/]|$)' -or
        $file.Name -match '^(conrad-connection\.js|codex-connection\.js|wallpaper-token\.txt|auth\.json)$') { continue }
    # Compiled application distributions are reproducible, not source inputs.
    if ($relative -match '^artifacts[\\/]' -and $file.Extension -notin '.png','.jpg','.jpeg') { continue }
    $copy = Join-Path $target $relative
    New-Item -ItemType Directory -Path (Split-Path $copy -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $copy
    $a = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $b = (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash
    if ($a -ne $b) { throw "Source changed during copy: $relative" }
    $manifest.Add([pscustomobject]@{source=$name;path=$relative;sha256=$b;bytes=$file.Length})
  }
  if (Test-Path -LiteralPath (Join-Path $source '.git')) {
    git -C $source status --porcelain=v1 | Set-Content (Join-Path $dest "$name-git-status.txt")
    git -C $source rev-parse HEAD | Set-Content (Join-Path $dest "$name-head.txt")
  }
}
$we = Get-Content 'C:\Program Files (x86)\Steam\steamapps\common\wallpaper_engine\config.json' -Raw | ConvertFrom-Json
$key = 'C:/Program Files (x86)/Steam/steamapps/common/wallpaper_engine/projects/myprojects/gta-vi-wallpaper/index.html'
$we.Ayo.wproperties.$key | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $dest 'wallpaper-effective-overrides.json')
$we.Ayo.general.wallpaperconfig | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $dest 'wallpaper-layout.json')
Copy-Item -LiteralPath "$env:APPDATA\Rainmeter\Rainmeter.ini" -Destination (Join-Path $dest 'Rainmeter.ini')
Copy-Item -LiteralPath "$env:LOCALAPPDATA\ConradSensor\settings.json" -Destination (Join-Path $dest 'conrad-settings.json')
$manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $dest 'manifest.json')
$sources | ConvertTo-Json | Set-Content (Join-Path $dest 'provenance.json')
Set-Content (Join-Path $root 'backups/latest.txt') $stamp
Write-Output "Verified $($manifest.Count) files; backup: $dest"
