param([switch]$Capture,[string]$Label='desktop',[int]$WatchSeconds=0)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if($Label -notmatch '^[a-zA-Z0-9-]+$'){throw 'Use a simple artifact label'}
if(!('StationWindowAudit' -as [type])){
Add-Type @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class StationWindowAudit {
 public delegate bool EnumFn(IntPtr w,IntPtr data);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumFn f,IntPtr data);
 [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p,EnumFn f,IntPtr data);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr w,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr w,StringBuilder b,int c);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr w,out Rect r);
 [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr w);
 [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr w,int i);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr w);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr w);
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr w,int attribute,out int value,int size);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
 [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;}
 public class Entry {public long Hwnd,Parent,Style,ExStyle;public uint Pid;public int Z,X,Y,Width,Height,Cloaked;public string Class;public bool Visible,Foreground,Minimized;}
 static Entry Read(IntPtr w,int z) {uint pid;GetWindowThreadProcessId(w,out pid);var b=new StringBuilder(128);GetClassName(w,b,128);Rect r;GetWindowRect(w,out r);int cloak;DwmGetWindowAttribute(w,14,out cloak,4);return new Entry{Hwnd=w.ToInt64(),Parent=GetParent(w).ToInt64(),Style=GetWindowLongPtr(w,-16).ToInt64(),ExStyle=GetWindowLongPtr(w,-20).ToInt64(),Pid=pid,Z=z,Class=b.ToString(),Visible=IsWindowVisible(w),Foreground=w==GetForegroundWindow(),Minimized=IsIconic(w),Cloaked=cloak,X=r.Left,Y=r.Top,Width=r.Right-r.Left,Height=r.Bottom-r.Top};}
 public static Entry[] Snapshot(uint[] ids) {var rows=new List<Entry>();int z=0;EnumWindows((w,d)=>{var row=Read(w,z++);bool selected=Array.IndexOf(ids,row.Pid)>=0;if(selected||row.Class=="Progman"||row.Class=="WorkerW"||row.Foreground)rows.Add(row);EnumChildWindows(w,(child,data)=>{var entry=Read(child,-1);if(Array.IndexOf(ids,entry.Pid)>=0)rows.Add(entry);return true;},IntPtr.Zero);return true;},IntPtr.Zero);return rows.ToArray();}
}
'@
}
$processes=@(Get-CimInstance Win32_Process)
$selected=@($processes | Where-Object {$_.Name -match '^(Battlestation|ConEmu64|Battlestation.GpuHelper|ConradSensor)\.exe$'})
$ids=[uint32[]]@($selected.ProcessId)
$dpi=[StationWindowAudit]::SetThreadDpiAwarenessContext([IntPtr](-4))
try {
    $windows=[StationWindowAudit]::Snapshot($ids)
    $report=[pscustomobject]@{timestamp=(Get-Date).ToString('o');processes=@($selected | Select-Object Name,ProcessId,ParentProcessId,ExecutablePath);windows=$windows}
$dir=Join-Path $env:LOCALAPPDATA 'Battlestation/inspection'
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $dir "dotnet-$Label-windows.json") -Encoding UTF8
    if($WatchSeconds -gt 0){
        $watchPath=Join-Path $dir "dotnet-$Label-watch.jsonl"
        $until=(Get-Date).AddSeconds($WatchSeconds)
        $stream=[IO.File]::Open($watchPath,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::ReadWrite)
        $writer=[IO.StreamWriter]::new($stream,[Text.UTF8Encoding]::new($false))
        try {
            $writer.AutoFlush=$true
            while((Get-Date) -lt $until){
                $sample=[pscustomobject]@{timestamp=(Get-Date).ToString('o');windows=[StationWindowAudit]::Snapshot($ids)}
                $writer.WriteLine(($sample | ConvertTo-Json -Depth 6 -Compress))
                Start-Sleep -Milliseconds 200
            }
        }finally{$writer.Dispose();$stream.Dispose()}
    }
    $windows | Where-Object {$_.Visible} | Format-Table Pid,Z,Hwnd,Parent,Class,X,Y,Width,Height -AutoSize
    if($Capture){
        Add-Type -AssemblyName System.Drawing
        $x=[StationWindowAudit]::GetSystemMetrics(76);$y=[StationWindowAudit]::GetSystemMetrics(77)
        $w=[StationWindowAudit]::GetSystemMetrics(78);$h=[StationWindowAudit]::GetSystemMetrics(79)
        $image=New-Object Drawing.Bitmap($w,$h);$graphics=[Drawing.Graphics]::FromImage($image)
        try {$graphics.CopyFromScreen($x,$y,0,0,$image.Size);$image.Save((Join-Path $dir "dotnet-$Label-screen.png"),[Drawing.Imaging.ImageFormat]::Png)}finally{$graphics.Dispose();$image.Dispose()}
    }
}finally{[void][StationWindowAudit]::SetThreadDpiAwarenessContext($dpi)}
