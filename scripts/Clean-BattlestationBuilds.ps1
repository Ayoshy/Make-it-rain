param(
    [string]$Root=(Split-Path $PSScriptRoot -Parent),
    [string]$Data=(Join-Path $env:LOCALAPPDATA 'Battlestation'),
    [Parameter(Mandatory)][string]$LoadedBuild,
    [switch]$Apply
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$Root=[IO.Path]::GetFullPath($Root)
$buildRoot=[IO.Path]::GetFullPath((Join-Path $Root 'build'))
$protected=[Collections.Generic.List[string]]::new()
$errors=[Collections.Generic.List[string]]::new()
$references=[Collections.Generic.List[string]]::new()
$references.Add([IO.Path]::GetFullPath($LoadedBuild))
$pointer=Join-Path $buildRoot 'current.txt'
if(!(Test-Path -LiteralPath $pointer -PathType Leaf)){throw 'Startup selection is missing; cleanup cancelled.'}
$selection=[IO.File]::ReadAllText($pointer).Trim()
if(!$selection){throw 'Startup selection is empty; cleanup cancelled.'}
$selected=[IO.Path]::GetFullPath((Join-Path $Root $selection))
if(!(Test-Path -LiteralPath (Join-Path $selected 'Battlestation.exe'))){throw 'Startup build is missing; cleanup cancelled.'}
$references.Add($selected)
$manifest=Join-Path $Data 'video-native-host.json'
if(Test-Path -LiteralPath $manifest){
    $video=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
    if(!$video.path){throw 'Video bridge reference is unreadable; cleanup cancelled.'}
    $references.Add([string]$video.path)
}
# Persistent launch paths, including older direct shortcuts.
$shell=New-Object -ComObject WScript.Shell
foreach($folder in @([Environment]::GetFolderPath('Desktop'),[Environment]::GetFolderPath('CommonDesktopDirectory'),[Environment]::GetFolderPath('Startup'),[Environment]::GetFolderPath('CommonStartup'))){
    if(!$folder -or !(Test-Path -LiteralPath $folder)){continue}
    foreach($link in Get-ChildItem -LiteralPath $folder -Filter '*.lnk' -File){
        $shortcut=$shell.CreateShortcut($link.FullName)
        $references.Add([string]$shortcut.TargetPath+' '+[string]$shortcut.Arguments)
    }
}
foreach($task in Get-ScheduledTask){foreach($action in $task.Actions){$references.Add([string]$action.Execute+' '+[string]$action.Arguments)}}
function ProcessReferences {
    foreach($process in Get-CimInstance Win32_Process){
        [string]$process.ExecutablePath+' '+[string]$process.CommandLine
    }
}
foreach($reference in ProcessReferences){$references.Add($reference)}
# Include DLLs loaded from an old build by another executable.
foreach($process in Get-Process){
    try {foreach($module in $process.Modules){if($module.FileName.StartsWith($buildRoot+'\',[StringComparison]::OrdinalIgnoreCase)){$references.Add($module.FileName)}}}
    catch [ComponentModel.Win32Exception] { } # Windows system processes can deny inspection.
    catch [InvalidOperationException] { } # Process exited during the snapshot.
    finally {$process.Dispose()}
}
function IsReferenced([string]$directory,$items){
    foreach($item in $items){if($item.IndexOf($directory,[StringComparison]::OrdinalIgnoreCase) -ge 0){return $true}}
    return $false
}
[long]$reclaimable=0;[long]$freed=0;[int]$removed=0
foreach($entry in Get-ChildItem -LiteralPath $buildRoot -Directory){
    $directory=[IO.Path]::GetFullPath($entry.FullName)
    # Only immediate, real compiled-build directories. Never follow a junction.
    if([IO.Path]::GetDirectoryName($directory) -ne $buildRoot -or ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -or !(Test-Path -LiteralPath (Join-Path $directory 'Battlestation.exe'))){
        $protected.Add($entry.Name+' (hors nettoyage)');continue
    }
    if(IsReferenced $directory $references){$protected.Add($entry.Name+' (utilisé ou référencé)');continue}
    $children=@(Get-ChildItem -LiteralPath $directory -Recurse -Force)
    if(@($children | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){$protected.Add($entry.Name+' (lien de fichiers)');continue}
    $files=@($children | Where-Object {!$_.PSIsContainer})
    [long]$bytes=($files | Measure-Object -Property Length -Sum).Sum
    $reclaimable+=$bytes
    if(!$Apply){continue}
    try {
        # Recheck launches and startup selection immediately before deletion.
        $fresh=@(ProcessReferences)+@([IO.Path]::GetFullPath((Join-Path $Root ([IO.File]::ReadAllText($pointer).Trim()))))
        if(IsReferenced $directory $fresh){$protected.Add($entry.Name+' (devenu actif)');$reclaimable-=$bytes;continue}
        # A locked module/file leaves the entire build in place.
        foreach($file in $files){$handle=[IO.File]::Open($file.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None);$handle.Dispose()}
        Remove-Item -LiteralPath $directory -Recurse -Force
        $removed++;$freed+=$bytes;$reclaimable-=$bytes
    }
    catch {$errors.Add($entry.Name+': '+$_.Exception.Message)}
}
[ordered]@{reclaimableBytes=$reclaimable;freedBytes=$freed;removed=$removed;protected=@($protected);errors=@($errors)} | ConvertTo-Json -Depth 4 -Compress
