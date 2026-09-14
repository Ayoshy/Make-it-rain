$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$build = [IO.File]::ReadAllText((Join-Path $root 'build/current.txt')).Trim()
$directory = [IO.Path]::GetFullPath((Join-Path $root $build))
if (!$directory.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Build must remain inside this project'
}
$exe = Join-Path $directory 'Battlestation.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Build Battlestation first' }
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$service = New-Object -ComObject 'Schedule.Service'
$service.Connect()
$task = $service.NewTask(0)
$task.RegistrationInfo.Description = 'Bureau modulable Battlestation, ouverture de session sans delai.'
$task.Principal.UserId = $sid
$task.Principal.LogonType = 3 # InteractiveToken: desktop user, no password.
$task.Principal.RunLevel = 0 # Keep normal desktop/terminal permissions.
$task.Settings.Enabled = $true
$task.Settings.AllowDemandStart = $true
$task.Settings.DisallowStartIfOnBatteries = $false
$task.Settings.StopIfGoingOnBatteries = $false
$task.Settings.ExecutionTimeLimit = 'PT0S'
$task.Settings.MultipleInstances = 2 # IgnoreNew; never terminate existing sessions.
$task.Settings.Priority = 6 # Normal process priority; desktop effects yield to user work.
$task.Settings.StartWhenAvailable = $true
$trigger = $task.Triggers.Create(9)
$trigger.UserId = $sid
$trigger.Delay = 'PT0S'
$action = $task.Actions.Create(0)
$action.Path = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
$action.Arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + (Join-Path $PSScriptRoot 'Start-Battlestation.ps1') + '" -AtLogon'
$action.WorkingDirectory = $root
$proofDirectory = Join-Path $root 'artifacts/validation'
New-Item -ItemType Directory -Path $proofDirectory -Force | Out-Null
$registered = $service.GetFolder('\').RegisterTaskDefinition('Battlestation', $task, 6, $sid, $null, 3, $null)
$registered.Xml | Set-Content (Join-Path $root 'artifacts/validation/battlestation-startup.xml') -Encoding Unicode
Write-Output "Battlestation registered at logon: $exe (no delay, High priority)."
