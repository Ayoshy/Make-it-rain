param([Parameter(Mandatory=$true)][string]$CodexPath)
# This wrapper lives only in the integrated shell. No account or global Codex config is read.
$env:BATTLESTATION_CODEX_EXE=$CodexPath
[Environment]::SetEnvironmentVariable('NO_COLOR',$null,'Process')
$env:TERM='xterm-256color'
$env:COLORTERM='truecolor'
$env:FORCE_COLOR='1'
$env:CLICOLOR='1'
function global:codex {
    & $env:BATTLESTATION_CODEX_EXE -c 'tui.animations=false' -c 'tui.theme="catppuccin-mocha"' -c "tui.terminal_title=['run-state','activity','thread-name','project-name']" @args
}
$kiloRequestPath=Join-Path $env:LOCALAPPDATA 'Battlestation\kilo-request.json'
if(Test-Path -LiteralPath $kiloRequestPath)
{
    try
    {
        $request=Get-Content -LiteralPath $kiloRequestPath -Raw | ConvertFrom-Json
        Remove-Item -LiteralPath $kiloRequestPath -Force -ErrorAction SilentlyContinue
        $project=[string]$request.project;$command=[string]$request.command
        if((Test-Path -LiteralPath $project -PathType Container)-and((Test-Path -LiteralPath $command -PathType Leaf)-or($command -eq 'kilo')))
        {
            Set-Location -LiteralPath $project
            $Host.UI.RawUI.WindowTitle='Kilo CLI · '+[IO.Path]::GetFileName($project)
            & $command
            if($LASTEXITCODE -ne 0){Write-Host ('Kilo CLI terminé avec le code '+$LASTEXITCODE+'.') -ForegroundColor Yellow}
            return
        }
    }
    catch{}
}
