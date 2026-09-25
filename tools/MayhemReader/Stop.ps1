$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$proof=Join-Path $root 'artifacts/validation/lol-reader'
$run=[IO.Path]::GetFullPath([IO.File]::ReadAllText((Join-Path $proof 'active-run.txt')).Trim())
if(!$run.StartsWith($proof+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected run path'}
[IO.File]::WriteAllText((Join-Path $run 'stop.request'),'Stop requested '+[DateTimeOffset]::UtcNow.ToString('O'))
Write-Output 'Stop requested; status.json records completion. No game or desktop process is closed.'
