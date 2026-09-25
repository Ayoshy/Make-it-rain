param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $root ('build/mayhem-reader-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))}
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(!$out.StartsWith((Join-Path $root 'build')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output must be inside build/'}
if(Test-Path -LiteralPath $out){throw 'Choose a fresh build directory'}
New-Item -ItemType Directory -Path $out | Out-Null
$vs=& 'C:/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe' -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$vs){throw 'Visual C++ x64 missing'}
@"
@echo off
call "$vs\VC\Auxiliary\Build\vcvars64.bat" >nul
cl /nologo /O2 /LD /std:c++20 /EHsc /utf-8 /MT "$PSScriptRoot\ReaderNative.cpp" windowsapp.lib D3d11.lib Dwmapi.lib User32.lib /link /OUT:"$out\ReaderNative.dll"
"@ | Set-Content -LiteralPath (Join-Path $out 'compile.cmd') -Encoding Ascii
Push-Location $out
try {& $env:ComSpec /d /c (Join-Path $out 'compile.cmd');if($LASTEXITCODE){throw 'Native build failed'}} finally {Pop-Location}
dotnet publish (Join-Path $PSScriptRoot 'MayhemReader.csproj') -c Release -o $out --nologo
if($LASTEXITCODE){throw 'Reader build failed'}
Copy-Item -LiteralPath (Join-Path $root 'artifacts/validation/lol-direct/cherry-augments.json') -Destination (Join-Path $out 'catalog.json')
Write-Output $out
