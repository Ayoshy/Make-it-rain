$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$escape=[char]27
1..120 | ForEach-Object { [Console]::WriteLine("SCROLL_FIXTURE_$_") }
[Console]::Write("${escape}[38;2;180;140;240mBATTLESTATION_NATIVE_READY${escape}[0m`r`n")
$line=Read-Host 'INPUT'
if($line -ne 'azerty-42'){throw 'Input roundtrip failed'}
[Console]::Write("${escape}[?1049h${escape}[2J${escape}[HALTERNATE_SCREEN`r`n")
Start-Sleep -Milliseconds 300
[Console]::Write("${escape}[?1049lBATTLESTATION_NATIVE_OK — été 42`r`n")
