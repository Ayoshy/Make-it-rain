param([string]$OutputDirectory='build/battlestation',[switch]$NoActivate)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=[IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
if(!$out.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Build output must remain inside this repository'}
New-Item -ItemType Directory -Path $out -Force | Out-Null
$vs=& 'C:/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe' -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$vs){throw 'Visual C++ x64 required'}
@"
@echo off
call "$vs\VC\Auxiliary\Build\vcvars64.bat" >nul
cl /nologo /O2 /LD /std:c++20 /EHsc /utf-8 /MT "$root\src\Battlestation.Native\Background.cpp" "$root\src\Battlestation.Native\NativeBackground.cpp" User32.lib D2d1.lib Windowscodecs.lib Ole32.lib /link /OUT:"$out\Battlestation.Graphics.dll"
if errorlevel 1 exit /b 1
cl /nologo /O2 /LD /std:c++20 /EHsc /utf-8 /MT "$root\src\Battlestation.Native\Desk.cpp" "$root\src\Battlestation.Native\Bluetooth.cpp" "$root\src\Battlestation.Native\BudsBattery.cpp" windowsapp.lib Shell32.lib Winhttp.lib User32.lib Gdi32.lib Gdiplus.lib Shlwapi.lib Ole32.lib Uuid.lib Bthprops.lib /link /OUT:"$out\Battlestation.Desk.dll"
if errorlevel 1 exit /b 1
cl /nologo /O2 /LD /std:c++20 /EHsc /utf-8 /MT "$root\src\Battlestation.Native\VideoCapture.cpp" windowsapp.lib D3d11.lib D3DCompiler.lib Dwmapi.lib User32.lib /link /OUT:"$out\Battlestation.Video.dll"
"@ | Set-Content "$out/compile.cmd" -Encoding Ascii
Push-Location $out
try { & $env:ComSpec /d /c "$out/compile.cmd"; if($LASTEXITCODE){throw 'Native build failed'} } finally { Pop-Location }
dotnet publish "$root/src/Battlestation/Battlestation.csproj" -c Release -o $out --nologo
if($LASTEXITCODE){throw 'Battlestation build failed'}
dotnet publish "$root/src/Battlestation.GpuHelper/Battlestation.GpuHelper.csproj" -c Release -o "$out/helper" --nologo
if($LASTEXITCODE){throw 'GPU helper build failed'}
Copy-Item "$out/helper/Battlestation.GpuHelper*" $out -Force
dotnet publish "$root/src/Battlestation.VideoBridge/Battlestation.VideoBridge.csproj" -c Release -o "$out/video-bridge" --nologo
if($LASTEXITCODE){throw 'Video bridge build failed'}
if(!$NoActivate){[IO.File]::WriteAllText((Join-Path $root 'build/current.txt'),$OutputDirectory,[Text.UTF8Encoding]::new($false))}
Write-Output "Built $out/Battlestation.exe"
