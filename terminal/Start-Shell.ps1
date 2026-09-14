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
