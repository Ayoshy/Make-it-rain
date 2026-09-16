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
# DeepSeek variant: provider, model and catalog are overridden on this command
# line only, never written to the global Codex configuration.
# Ne jamais forcer la methode d'authentification ici
# (`preferred_auth_method`/`forced_login_method`) : le CLI repond alors
# "API key login is required, but ChatGPT is currently being used. Logging out."
# et supprime auth.json, donc la session ChatGPT de codex doit etre refaite.
if([string]::IsNullOrWhiteSpace($env:DEEPSEEK_API_KEY)){$env:DEEPSEEK_API_KEY=[Environment]::GetEnvironmentVariable('DEEPSEEK_API_KEY','User')}
$env:BATTLESTATION_DS_CATALOG=(Join-Path $PSScriptRoot 'codex-deepseek-models.json') -replace '\\','/'
function global:codex-ds {
    & $env:BATTLESTATION_CODEX_EXE -c 'tui.animations=false' -c 'tui.theme="catppuccin-mocha"' -c "tui.terminal_title=['run-state','activity','thread-name','project-name']" -c 'model_provider="deepseek"' -c 'model="deepseek-flash"' -c 'model_reasoning_effort="high"' -c 'web_search="disabled"' -c "model_catalog_json=`"$env:BATTLESTATION_DS_CATALOG`"" -c 'model_providers.deepseek.name="DeepSeek"' -c 'model_providers.deepseek.base_url="https://api.deepseek.com/"' -c 'model_providers.deepseek.wire_api="responses"' -c 'model_providers.deepseek.env_key="DEEPSEEK_API_KEY"' @args
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
$codexDsRequestPath=Join-Path $env:LOCALAPPDATA 'Battlestation\codex-ds-request.json'
if(Test-Path -LiteralPath $codexDsRequestPath)
{
    try
    {
        $request=Get-Content -LiteralPath $codexDsRequestPath -Raw | ConvertFrom-Json
        Remove-Item -LiteralPath $codexDsRequestPath -Force -ErrorAction SilentlyContinue
        $project=[string]$request.project
        if(Test-Path -LiteralPath $project -PathType Container)
        {
            Set-Location -LiteralPath $project
            $Host.UI.RawUI.WindowTitle='Codex CLI (DS) · '+[IO.Path]::GetFileName($project)
            codex-ds -C $project
            if($LASTEXITCODE -ne 0){Write-Host ('Codex CLI (DS) terminé avec le code '+$LASTEXITCODE+'.') -ForegroundColor Yellow}
            return
        }
    }
    catch{}
}
