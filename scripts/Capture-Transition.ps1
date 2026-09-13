param([int]$Drawer=1,[string]$Name='native-reveal')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root "artifacts/validation/$Name"
New-Item -ItemType Directory $out -Force|Out-Null
$frames=[Collections.Generic.List[Drawing.Bitmap]]::new()
$times=[Collections.Generic.List[double]]::new()
$watch=[Diagnostics.Stopwatch]::StartNew()
try {
  for($i=0;$i -lt 30;$i++){
    if($i -eq 3){Start-Process 'C:\Program Files\Rainmeter\Rainmeter.exe' -ArgumentList @('!CommandMeasure','Layout',"Toggle($Drawer)",'ViceCity\Dashboard') -WindowStyle Hidden}
    $bmp=New-Object Drawing.Bitmap 900,1400
    $g=[Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(3920,0,0,0,$bmp.Size)
    $g.Dispose();$frames.Add($bmp);$times.Add($watch.Elapsed.TotalMilliseconds)
    $delay=[int](($i+1)*33-$watch.Elapsed.TotalMilliseconds)
    if($delay -gt 0){Start-Sleep -Milliseconds $delay}
  }
  for($i=0;$i -lt $frames.Count;$i++){$frames[$i].Save((Join-Path $out ('frame-{0:00}.png' -f $i)))}
  $times|ConvertTo-Json|Set-Content (Join-Path $out 'timing-ms.json')
}finally{foreach($frame in $frames){$frame.Dispose()}}
Write-Output $out
