param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$cache=Join-Path $root 'vendor\SDL3-3.4.16'
$sdl=Join-Path $cache 'SDL3-3.4.16'
if(!(Test-Path -LiteralPath (Join-Path $sdl 'lib\x64\SDL3.dll'))){
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    $archive=Join-Path $cache 'SDL3-devel-3.4.16-VC.zip'
    if(!(Test-Path -LiteralPath $archive)){
        & curl.exe --fail --location --silent --show-error --output $archive 'https://github.com/libsdl-org/SDL/releases/download/release-3.4.16/SDL3-devel-3.4.16-VC.zip'
        if($LASTEXITCODE){throw 'SDL3 download failed'}
    }
    if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '1a784cb2a5c64d56fe7a62090fe9d242d9865f235e4ea9678f1a6ba4e693e7de'){throw 'SDL3 archive hash mismatch'}
    Expand-Archive -LiteralPath $archive -DestinationPath $cache
}
Write-Output $sdl
