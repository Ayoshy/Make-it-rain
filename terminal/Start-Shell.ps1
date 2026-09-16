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
if([string]::IsNullOrWhiteSpace($env:DEEPSEEK_API_KEY)){$env:DEEPSEEK_API_KEY=[Environment]::GetEnvironmentVariable('DEEPSEEK_API_KEY','User')}
if([string]::IsNullOrWhiteSpace($env:DEEPSEEK_MODEL)){$env:DEEPSEEK_MODEL='deepseek-flash'}
$deepSeekRequestPath=Join-Path $env:LOCALAPPDATA 'Battlestation\deepseek-request.json'
if(Test-Path -LiteralPath $deepSeekRequestPath)
{
    try
    {
        $request=Get-Content -LiteralPath $deepSeekRequestPath -Raw | ConvertFrom-Json
        Remove-Item -LiteralPath $deepSeekRequestPath -Force -ErrorAction SilentlyContinue
        $project=[string]$request.project;$command=[string]$request.command
        if((Test-Path -LiteralPath $project -PathType Container)-and((Test-Path -LiteralPath $command -PathType Leaf)-or($command -eq 'deepseek-cli')))
        {
            Set-Location -LiteralPath $project
            $Host.UI.RawUI.WindowTitle='DeepSeek CLI · '+[IO.Path]::GetFileName($project)
            & $command
            if($LASTEXITCODE -ne 0){Write-Host ('DeepSeek CLI terminé avec le code '+$LASTEXITCODE+'.') -ForegroundColor Yellow}
            return
        }
    }
    catch{}
}
