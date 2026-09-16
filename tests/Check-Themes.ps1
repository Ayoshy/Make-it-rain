param([string]$BaselineRef,[string]$Layout)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root ('artifacts/themes-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $out | Out-Null
$vs=& 'C:/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe' -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$compile=@('@echo off',('call "'+$vs+'\VC\Auxiliary\Build\vcvars64.bat" >nul'))
$common='/nologo /O2 /LD /std:c++20 /EHsc /utf-8 /MT /I"'+$root+'\src\Battlestation.Native" /I"'+$out+'" "'+$root+'\tests\NativeThemeChecks.cpp" User32.lib D2d1.lib Windowscodecs.lib Ole32.lib'
$compile+=('cl '+$common+' /link /OUT:"'+$out+'\theme-preview.dll"')
$compile+='if errorlevel 1 exit /b 1'
if($BaselineRef){
    $source=git -C $root show ($BaselineRef+':src/Battlestation.Native/NativeBackground.cpp')
    if($LASTEXITCODE){throw 'Cannot read the requested baseline'}
    [IO.File]::WriteAllLines((Join-Path $out 'NativeBackground-baseline.cpp'),$source,[Text.UTF8Encoding]::new($false))
    $compile+=('cl /DTHEME_BASELINE '+$common+' /link /OUT:"'+$out+'\baseline-preview.dll"')
    $compile+='if errorlevel 1 exit /b 1'
}
$compile | Set-Content -LiteralPath (Join-Path $out 'compile.cmd') -Encoding Ascii
Push-Location $out
try{& $env:ComSpec /d /c (Join-Path $out 'compile.cmd');if($LASTEXITCODE){throw 'Native preview compilation failed'}}finally{Pop-Location}
python (Join-Path $PSScriptRoot 'NativeThemeChecks.py') $root $out $Layout
if($LASTEXITCODE){throw 'Native theme checks failed'}
Write-Output $out
