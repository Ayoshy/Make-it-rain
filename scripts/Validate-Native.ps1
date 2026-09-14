$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'build/native-tests'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$vs=& 'C:/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe' -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$vs){throw 'Visual C++ x64 required'}
@"
@echo off
call "$vs\VC\Auxiliary\Build\vcvars64.bat" >nul
cl /nologo /std:c++20 /EHsc /utf-8 /MT "$root\tests\DeskTests.cpp" /link /OUT:"$out\DeskTests.exe"
if errorlevel 1 exit /b 1
cl /nologo /std:c++20 /EHsc /utf-8 /MT /DDESK_PROBE "$root\src\Battlestation.Native\Desk.cpp" windowsapp.lib Shell32.lib Winhttp.lib User32.lib Gdi32.lib Gdiplus.lib Shlwapi.lib Ole32.lib /link /OUT:"$out\DeskContracts.exe"
"@ | Set-Content "$out/compile.cmd" -Encoding Ascii
Push-Location $out
try {
    & $env:ComSpec /d /c "$out/compile.cmd"
    if($LASTEXITCODE){throw 'Native test build failed'}
    & "$out/DeskTests.exe"
    if($LASTEXITCODE){throw 'Clock checks failed'}
    & "$out/DeskContracts.exe" --contracts
    if($LASTEXITCODE){throw 'Weather/cover checks failed'}
}finally{Pop-Location}
