param([string]$Build)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$proof=Join-Path $root 'artifacts/validation/lol-reader'
if(!$Build){$Build=[IO.File]::ReadAllText((Join-Path $proof 'validated-build.txt')).Trim()}
$buildPath=[IO.Path]::GetFullPath($Build)
if(!$buildPath.StartsWith((Join-Path $root 'build')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Reader build must be under this repository build/'}
if(Get-Process -Name MayhemReader -ErrorAction SilentlyContinue){throw 'A Mayhem reader is already open; stop/close it first'}
$exe=Join-Path $buildPath 'MayhemReader.exe'
if(!(Test-Path -LiteralPath $exe)){throw 'Reader executable missing'}
$run=Join-Path $proof ('run-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$p=Start-Process -FilePath $exe -ArgumentList @('--run',('"'+$run+'"')) -WindowStyle Hidden -PassThru
[IO.File]::WriteAllText((Join-Path $proof 'active-run.txt'),$run)
Write-Output "Reader PID $($p.Id); inspect status.json before claiming capture is active."
Write-Output $run
