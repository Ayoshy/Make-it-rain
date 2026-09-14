$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
foreach($name in @('Core','Layout','Desktop','Terminal','Commands')){
    $project=Join-Path $root "tests/Battlestation.$name.Tests.csproj"
    if($name -eq 'Terminal'){dotnet run --project $project -c Release -- (Join-Path $root 'tests/TerminalFixture.ps1')}
    else{dotnet run --project $project -c Release}
    if($LASTEXITCODE){throw "$name checks failed"}
}
foreach($file in Get-ChildItem $PSScriptRoot -Filter '*.ps1'){
    $tokens=$null;$errors=$null
    [void][Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
    if($errors){throw "Parse errors: $($file.Name)"}
}
& (Join-Path $PSScriptRoot 'Validate-Native.ps1')
Write-Output 'Battlestation checks passed.'
