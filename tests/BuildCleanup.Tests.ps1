$ErrorActionPreference='Stop'
$script=Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts/Clean-BattlestationBuilds.ps1'
$testRoot=Join-Path $env:TEMP ('Battlestation-cleanup-'+[guid]::NewGuid().ToString('N'))
$data=Join-Path $testRoot 'data'
New-Item -ItemType Directory -Path $data -Force | Out-Null
try {
    foreach($name in @('loaded','startup','bridge','active','unused','locked')){
        $dir=Join-Path $testRoot ('build/'+$name);New-Item -ItemType Directory -Path $dir -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $dir 'Battlestation.exe'),'fixture')
    }
    [IO.File]::WriteAllText((Join-Path $testRoot 'build/current.txt'),'build/startup')
    @{path=(Join-Path $testRoot 'build/bridge/Battlestation.exe')} | ConvertTo-Json | Set-Content (Join-Path $data 'video-native-host.json')
    $loaded=Join-Path $testRoot 'build/loaded'
    $runningScript=Join-Path $testRoot 'build/active/session.ps1'
    [IO.File]::WriteAllText($runningScript,'Start-Sleep -Seconds 60')
    $running=Start-Process powershell.exe -ArgumentList @('-NoProfile','-File',('"'+$runningScript+'"')) -WindowStyle Hidden -PassThru
    $preview=& $script -Root $testRoot -Data $data -LoadedBuild $loaded | ConvertFrom-Json
    if($preview.removed -ne 0 -or $preview.reclaimableBytes -ne 14 -or $preview.protected.Count -ne 4){throw 'Preview mismatch'}
    $lock=[IO.File]::Open((Join-Path $testRoot 'build/locked/Battlestation.exe'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    try {$result=& $script -Root $testRoot -Data $data -LoadedBuild $loaded -Apply | ConvertFrom-Json}finally{$lock.Dispose()}
    if($result.removed -ne 1 -or $result.freedBytes -ne 7 -or $result.errors.Count -ne 1){throw 'Cleanup result mismatch'}
    foreach($name in @('loaded','startup','bridge','active','locked')){if(!(Test-Path -LiteralPath (Join-Path $testRoot ('build/'+$name+'/Battlestation.exe')))){throw ('Protected build removed: '+$name)}}
    if(Test-Path -LiteralPath (Join-Path $testRoot 'build/unused')){throw 'Unused build still present'}
    if($running.HasExited){throw 'The active fixture session was interrupted'}
    'BUILD_CLEANUP_CHECKS_PASS: preview, exact bytes, current/startup/bridge/process protection, locked build left intact'
}finally{
    if($running -and !$running.HasExited){$running.Kill();$running.WaitForExit()}
    $resolved=[IO.Path]::GetFullPath($testRoot)
    if(!$resolved.StartsWith([IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')+'\Battlestation-cleanup-',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected test cleanup path'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
