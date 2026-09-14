param([string]$Build='build/battlestation-video-cockpit')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe=[IO.Path]::GetFullPath((Join-Path $root "$Build/video-bridge/Battlestation.VideoBridge.exe"))
if(!$exe.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Video host must remain inside this worktree'}
if(!(Test-Path -LiteralPath $exe)){throw 'Build the video bridge first'}
$data=Join-Path $env:LOCALAPPDATA 'Battlestation'
New-Item -ItemType Directory -Path $data -Force | Out-Null
$manifest=Join-Path $data 'video-native-host.json'
$hostDescription=[ordered]@{name='com.battlestation.video';description='Battlestation local video bridge';path=$exe;type='stdio';allowed_origins=@('chrome-extension://bbbkiomcecimmpndgliccmeagfhbednp/')}
[IO.File]::WriteAllText($manifest,($hostDescription | ConvertTo-Json -Depth 3),[Text.UTF8Encoding]::new($false))
$keys=@('HKCU:\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.battlestation.video','HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.battlestation.video')
# The installed Brave build uses Chromium's Chrome-compatible lookup on Windows.
foreach($key in $keys){if(Test-Path $key){$existing=(Get-Item $key).GetValue('');if($existing -and $existing -ne $manifest){throw 'A different native host registration already exists'}}}
foreach($key in $keys){New-Item -Path $key -Force | Out-Null;Set-Item -Path $key -Value $manifest}
[pscustomobject]@{Extension=(Join-Path $root 'browser/video-dock');Id='bbbkiomcecimmpndgliccmeagfhbednp';Host=$manifest;BrowserPage='brave://extensions'}
