param([string]$Build,[switch]$AtLogon)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Build){$Build=if(Test-Path (Join-Path $root 'build/current.txt')){[IO.File]::ReadAllText((Join-Path $root 'build/current.txt')).Trim()}else{'build/battlestation'}}
$directory=[IO.Path]::GetFullPath((Join-Path $root $Build))
if(!$directory.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Build must remain inside this project'}
$exe=Join-Path $directory 'Battlestation.exe'
if(!(Test-Path -LiteralPath $exe)){throw 'Build Battlestation first'}
if($AtLogon){
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
    try{if(!$process.HasExited){$process.PriorityClass='High'}}catch [InvalidOperationException]{}
}
