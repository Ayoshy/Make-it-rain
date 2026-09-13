param([int]$Seconds = 20, [string]$Label = 'candidate-observation')
$ErrorActionPreference = 'Stop'
if ($Seconds -lt 2 -or $Seconds -gt 60) { throw 'Use a sample between 2 and 60 seconds' }
$root = Split-Path $PSScriptRoot -Parent
$tree = @(Get-CimInstance Win32_Process)
$rm = @(Get-Process Rainmeter)
if ($rm.Count -ne 1) { throw 'Expected exactly one Rainmeter process' }
$ids = [Collections.Generic.HashSet[int]]::new()
[void]$ids.Add($rm[0].Id)
foreach($p in $tree | Where-Object { $_.Name -eq 'ViceCity.GpuHelper.exe' -and $_.SessionId -eq $rm[0].SessionId }) { [void]$ids.Add($p.ProcessId) }
do {
  $added = $false
  foreach($p in $tree) { if($ids.Contains($p.ParentProcessId) -and $ids.Add($p.ProcessId)) { $added = $true } }
} while ($added)
function Get-Sample {
  $result = @{}
  foreach($processId in $ids) {
    try { $p=Get-Process -Id $processId -ErrorAction Stop; $result[$processId]=[pscustomobject]@{name=$p.ProcessName;cpu=$p.CPU;workingSet=$p.WorkingSet64;privateBytes=$p.PrivateMemorySize64} } catch { }
  }
  return $result
}
$start = Get-Sample
$clock = [Diagnostics.Stopwatch]::StartNew()
Start-Sleep -Seconds $Seconds
$end = Get-Sample
$clock.Stop()
$gpuEngines = @(Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^pid_(\d+)_' -and $ids.Contains([int]$Matches[1]) } | Select-Object Name,UtilizationPercentage)
$gpuMemory = @(Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^pid_(\d+)_' -and $ids.Contains([int]$Matches[1]) } | Select-Object Name,DedicatedUsage,SharedUsage)
$rows = foreach($processId in $ids) {
  if($start.ContainsKey($processId) -and $end.ContainsKey($processId)) {
    $a=$start[$processId]; $b=$end[$processId]
    [pscustomobject]@{pid=$processId;name=$b.name;cpuPercent=100*($b.cpu-$a.cpu)/$clock.Elapsed.TotalSeconds/[Environment]::ProcessorCount;workingSetMiB=$b.workingSet/1MB;privateMiB=$b.privateBytes/1MB}
  }
}
$report=[pscustomobject]@{timestamp=(Get-Date).ToString('o');label=$Label;seconds=$clock.Elapsed.TotalSeconds;logicalProcessors=[Environment]::ProcessorCount;processes=@($rows);totalCpuPercent=($rows|Measure-Object cpuPercent -Sum).Sum;totalWorkingSetMiB=($rows|Measure-Object workingSetMiB -Sum).Sum;totalPrivateMiB=($rows|Measure-Object privateMiB -Sum).Sum;gpuPercent=$null;gpuNote='GPU attribution not sampled. Working sets can include shared pages. Offscreen preview and concurrent original desktop are not a final idle/game benchmark.'}
$report | Add-Member -NotePropertyName gpuEngineSnapshot -NotePropertyValue $gpuEngines
$report | Add-Member -NotePropertyName gpuMemorySnapshot -NotePropertyValue $gpuMemory
$report.gpuNote='GPU per-process/per-engine snapshot at sample end, not a normalized aggregate. Working sets can include shared pages. Offscreen preview and concurrent original desktop are not a final idle/game benchmark.'
$report | ConvertTo-Json -Depth 5 | Set-Content "$root/artifacts/validation/resources-$Label.json"
$report | Select-Object label,totalCpuPercent,totalWorkingSetMiB,totalPrivateMiB
