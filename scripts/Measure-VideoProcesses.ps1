param([ValidateRange(1,30)][int]$Seconds=3,[string]$Output='artifacts/validation/video-smooth/process-metrics.json')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$destination=[IO.Path]::GetFullPath((Join-Path $root $Output))
if(!$destination.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Metrics must stay inside this project'}
# Metadata and counters only. Never request command lines or read process memory.
$all=@(Get-CimInstance Win32_Process -Property Name,ProcessId,ParentProcessId,ExecutablePath)
$ids=[Collections.Generic.HashSet[int]]::new()
foreach($entry in $all){if($entry.Name -match '^(Battlestation.*|ConradSensor|brave|stremio.*|Discord.*)\.exe$'){[void]$ids.Add([int]$entry.ProcessId)}}
do{$added=$false;foreach($entry in $all){if($ids.Contains([int]$entry.ParentProcessId)-and $ids.Add([int]$entry.ProcessId)){$added=$true}}}while($added)
$before=@{}
foreach($number in $ids){try{$process=Get-Process -Id $number -ErrorAction Stop;$before[$number]=$process.CPU;$process.Dispose()}catch [Microsoft.PowerShell.Commands.ProcessCommandException]{}}
$watch=[Diagnostics.Stopwatch]::StartNew();Start-Sleep -Seconds $Seconds;$elapsed=$watch.Elapsed.TotalSeconds
$rows=foreach($entry in $all | Where-Object {$ids.Contains([int]$_.ProcessId)}){
    $number=[int]$entry.ProcessId
    try{
        $process=Get-Process -Id $number -ErrorAction Stop
        [pscustomobject]@{Name=$entry.Name;Pid=$number;ParentPid=$entry.ParentProcessId;Path=$entry.ExecutablePath;Status='running';CpuOneCorePercent=if($before.ContainsKey($number)){[math]::Round(($process.CPU-$before[$number])/$elapsed*100,2)}else{$null};PrivateMB=[math]::Round($process.PrivateMemorySize64/1MB,1);WorkingSetMB=[math]::Round($process.WorkingSet64/1MB,1)}
        $process.Dispose()
    }catch [Microsoft.PowerShell.Commands.ProcessCommandException]{[pscustomobject]@{Name=$entry.Name;Pid=$number;ParentPid=$entry.ParentProcessId;Path=$entry.ExecutablePath;Status='exited';CpuOneCorePercent=$null;PrivateMB=$null;WorkingSetMB=$null}}
}
$report=[pscustomobject]@{At=[DateTimeOffset]::Now.ToString('o');Seconds=$elapsed;LogicalProcessors=[Environment]::ProcessorCount;Note='All Battlestation hosts/helpers and descendants, plus all Brave/Stremio/Discord processes. Source apps include unrelated user activity; working sets share pages. This is not a comparative benchmark.';Processes=@($rows)}
New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $destination -Encoding UTF8
$rows | Where-Object Name -like 'Battlestation*' | Format-Table Name,Pid,Status,CpuOneCorePercent,PrivateMB,WorkingSetMB
