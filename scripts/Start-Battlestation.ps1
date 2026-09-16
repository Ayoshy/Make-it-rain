param([string]$Build,[switch]$AtLogon)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Build){
    $pointer=Join-Path $root 'build/current.txt'
    if(!(Test-Path -LiteralPath $pointer)){throw 'No current build selected. Restore build/current.txt to the validated build; no fallback will be launched.'}
    $Build=[IO.File]::ReadAllText($pointer).Trim()
    if(!$Build){throw 'The current build selection is empty'}
}
$directory=[IO.Path]::GetFullPath((Join-Path $root $Build))
$buildRoot=Join-Path $root 'build'
if(!$directory.StartsWith($buildRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Only builds inside build/ can be launched; archived builds must be restored explicitly first'}
$exe=Join-Path $directory 'Battlestation.exe'
if(!(Test-Path -LiteralPath $exe)){throw 'Build Battlestation first'}
if($AtLogon){
    # Existing administrator-owned tasks may still request High. Lower the
    # launcher itself immediately, without changing that task's permissions.
    [Diagnostics.Process]::GetCurrentProcess().PriorityClass='Normal'
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class BattlestationLogonShell {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    public static extern IntPtr FindWindow(string name, string title);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string name, string title);
    delegate bool EnumWindow(IntPtr window, IntPtr data);
    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindow callback, IntPtr data);
    public static bool Ready() {
        if (FindWindow("Progman", null) == IntPtr.Zero) return false;
        bool ready = false;
        EnumWindows((window, data) => {
            ready = FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero;
            return !ready;
        }, IntPtr.Zero);
        return ready;
    }
}
'@
    $deadline=[DateTime]::UtcNow.AddSeconds(60)
    while(![BattlestationLogonShell]::Ready()){
        if([DateTime]::UtcNow -ge $deadline){throw 'Windows desktop did not become available'}
        Start-Sleep -Milliseconds 100
    }
}
$process=Start-Process -FilePath $exe -WorkingDirectory $root -ArgumentList @('--root',('"'+$root+'"')) -WindowStyle Hidden -PassThru
if($AtLogon){
    try{if(!$process.HasExited){$process.PriorityClass='Normal'}}catch [InvalidOperationException]{}
}
