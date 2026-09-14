param([Parameter(Mandatory=$true)][string]$CodexPath,[Parameter(Mandatory=$true)][string]$ProjectPath)
. "$PSScriptRoot/Start-Shell.ps1" -CodexPath $CodexPath
Set-Location -LiteralPath $ProjectPath
codex -C $ProjectPath
exit $LASTEXITCODE
