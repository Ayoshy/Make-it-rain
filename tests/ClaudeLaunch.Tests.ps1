$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('battlestation-claude-'+[Guid]::NewGuid())
$originalLocal=$env:LOCALAPPDATA
$originalLocation=Get-Location
try
{
    $env:LOCALAPPDATA=$fixture
    $data=New-Item -ItemType Directory -Path (Join-Path $fixture Battlestation)
    $project=New-Item -ItemType Directory -Path (Join-Path $fixture "Projet d'Ayo")
    $stub=Join-Path $fixture 'claude-stub.ps1'
    Set-Content -LiteralPath $stub -Value '$global:claudeLaunchDirectory=$PWD.Path; $global:claudeLaunchArgs=@($args); $global:LASTEXITCODE=0'
    $request=Join-Path $data.FullName 'claude-request.json'
    @{project=$project.FullName;command=$stub} | ConvertTo-Json | Set-Content -LiteralPath $request
    & (Join-Path $root 'terminal/Start-Shell.ps1') -CodexPath 'unused-codex.exe'
    if($global:claudeLaunchDirectory -ne $project.FullName){throw 'Wrong project directory'}
    if($global:claudeLaunchArgs.Count -ne 0){throw 'Unexpected prompt or CLI arguments'}
    if(Test-Path -LiteralPath $request){throw 'Launch request was not consumed'}
    $global:claudeLaunchDirectory=$null
    & (Join-Path $root 'terminal/Start-Shell.ps1') -CodexPath 'unused-codex.exe'
    if($null -ne $global:claudeLaunchDirectory){throw 'A new shell replayed the Claude request'}
    Write-Output 'PASS Claude launch: selected project, spaces/apostrophe, no prompt, one-shot request. Stub only; no model call.'
}
finally
{
    Set-Location -LiteralPath $originalLocation.Path
    $env:LOCALAPPDATA=$originalLocal
    $resolved=[IO.Path]::GetFullPath($fixture)
    $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if(!$resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase)-or!(Split-Path $resolved -Leaf).StartsWith('battlestation-claude-')){throw 'Unexpected fixture path'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
