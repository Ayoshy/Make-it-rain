$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'build/native'
New-Item -ItemType Directory -Path $out -Force | Out-Null
dotnet publish "$root/src/ViceCity.Core/ViceCity.Core.csproj" -c Release -o "$out/ViceCityNative" --nologo
if ($LASTEXITCODE) { throw 'Managed build failed' }
dotnet publish "$root/src/ViceCity.GpuHelper/ViceCity.GpuHelper.csproj" -c Release -o "$out/GpuHelper" --nologo
if ($LASTEXITCODE) { throw 'GPU helper build failed' }
Copy-Item "$out/GpuHelper/ViceCity.GpuHelper*" "$out/ViceCityNative/"
$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$vs) { throw 'Visual C++ x64 tools required' }
$native = Get-ChildItem 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Host.win-x64' -Directory |
  Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$headers = Join-Path $native.FullName 'runtimes/win-x64/native'
$batch = @"
@echo off
call "$vs\VC\Auxiliary\Build\vcvars64.bat" >nul
cl /nologo /LD /std:c++17 /EHsc /utf-8 /MT /I"$headers" "$root\src\ViceCity.Plugin\Plugin.cpp" "$root\src\ViceCity.Plugin\NativeBackground.cpp" "$headers\libnethost.lib" Advapi32.lib User32.lib Gdi32.lib D2d1.lib Windowscodecs.lib Ole32.lib /link /OUT:"$out\ViceCityNative.dll"
if errorlevel 1 exit /b 1
cl /nologo /LD /std:c++17 /EHsc /utf-8 /MT "$root\src\ViceCity.Plugin\GlassPlugin.cpp" "$root\src\ViceCity.Plugin\NativeBackground.cpp" User32.lib D2d1.lib Windowscodecs.lib Ole32.lib /link /OUT:"$out\ViceCityGlass.dll"
"@
# Fixed build commands only; no file deletion or moving through another shell.
$batch | Set-Content "$out/compile.cmd" -Encoding Ascii
Push-Location $out
try { & $env:ComSpec /d /c "$out\compile.cmd"; if ($LASTEXITCODE) { throw 'Native build failed' } }
finally { Pop-Location }
