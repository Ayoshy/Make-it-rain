$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
foreach($name in @('Core','Layout','Responsive','Desktop','Terminal','TerminalTabs','Commands','Video','BrowserVideo','Audio','AudioWorker','Experiences')){
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
dotnet run --project (Join-Path $root 'tests/Battlestation.Consolidation.Tests.csproj') -c Release -- (Join-Path $root 'build/native-tests/DeskFixture.dll') (Join-Path $root 'build/native-tests/GraphicsContracts.dll') (Join-Path $root 'assets/Images')
if($LASTEXITCODE){throw 'Recovery and suspension checks failed'}
if(!(Test-Path (Join-Path $root 'node_modules/playwright-core/package.json'))){throw 'Browser test dependency missing. Run npm ci in this project.'}
$previousPreference=$ErrorActionPreference
try {
    # Windows PowerShell wraps native stderr warnings as ErrorRecords; the
    # program's exit code, not a Node color warning, decides test success.
    $ErrorActionPreference='Continue'
    node (Join-Path $root 'tests/browser-extension.test.cjs')
    if($LASTEXITCODE){throw 'Minimized browser extension checks failed'}
}finally{$ErrorActionPreference=$previousPreference}
Write-Output 'Battlestation checks passed.'
