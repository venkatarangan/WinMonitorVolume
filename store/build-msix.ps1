# Builds an unsigned .msix for Microsoft Store submission (Partner Center signs it for you).
#
# Usage:
#   .\store\build-msix.ps1            # uses the Store identity saved in store\store-identity.json
#   .\store\build-msix.ps1 -NoBump    # rebuild the same version
# To build a local test package, override the identity:
#   .\store\build-msix.ps1 -NoBump -IdentityName "WinMonitorVolume.Test" -Publisher "CN=WinMonitorVolumeTest" -PublisherDisplayName "Test"
# (the real values are on Partner Center > your app > Product management > Product identity)
#
# Needs: .NET 10 SDK and the Windows 10/11 SDK (for makeappx.exe).
param(
    [string]$IdentityName,
    [string]$Publisher,
    [string]$PublisherDisplayName,
    [string]$DisplayName,
    [switch]$NoBump
)
$ErrorActionPreference = 'Stop'
$identityFile = Join-Path $PSScriptRoot 'store-identity.json'
if (Test-Path $identityFile) {
    $id = Get-Content $identityFile -Raw | ConvertFrom-Json
    if (-not $IdentityName) { $IdentityName = $id.IdentityName }
    if (-not $Publisher) { $Publisher = $id.Publisher }
    if (-not $PublisherDisplayName) { $PublisherDisplayName = $id.PublisherDisplayName }
    if (-not $DisplayName) { $DisplayName = $id.DisplayName }
}
if (-not $DisplayName) { $DisplayName = 'WinMonitorVolume' }
if (-not ($IdentityName -and $Publisher -and $PublisherDisplayName)) {
    throw "Missing Store identity. Fill in $identityFile or pass -IdentityName, -Publisher and -PublisherDisplayName."
}
Write-Host "Packaging as $IdentityName ($PublisherDisplayName)"
$root = Split-Path $PSScriptRoot -Parent
$numberFile = Join-Path $root 'build-number.txt'

# Same 1.1.<build> scheme as build.ps1. The Store needs the 4th part to be 0 and each upload to be higher.
$build = [int](Get-Content $numberFile | Select-Object -First 1)
if (-not $NoBump) { $build++; Set-Content $numberFile $build }
$version = "1.1.$build.0"

# 1. Logos
if (-not (Test-Path "$PSScriptRoot\Assets\StoreLogo.png")) { & "$PSScriptRoot\make-assets.ps1" }

# 2. Self-contained publish (the Store package must not depend on a separately installed .NET)
$layout = Join-Path $root 'publish-msix'
Remove-Item $layout -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish "$root\WinMonitorVolume.csproj" -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:BuildNumber=$build -o $layout
if ($LASTEXITCODE) { exit $LASTEXITCODE }
Remove-Item "$layout\*.pdb" -ErrorAction SilentlyContinue

# 3. Manifest + assets into the layout
Copy-Item "$PSScriptRoot\Assets" "$layout\Assets" -Recurse -Force
Remove-Item "$layout\Assets\StoreListing-*.png" -ErrorAction SilentlyContinue
$xml = Get-Content "$PSScriptRoot\AppxManifest.template.xml" -Raw
$xml = $xml.Replace('{{IdentityName}}', $IdentityName).Replace('{{Publisher}}', $Publisher).
    Replace('{{PublisherDisplayName}}', [Security.SecurityElement]::Escape($PublisherDisplayName)).
    Replace('{{DisplayName}}', $DisplayName).Replace('{{Version}}', $version)
Set-Content "$layout\AppxManifest.xml" $xml -Encoding UTF8

# 4. Pack
$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw 'makeappx.exe not found. Install the Windows 10/11 SDK (winget install Microsoft.WindowsSDK.10.0.22621).' }
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$msix = Join-Path $dist "WinMonitorVolume_${version}_x64.msix"
& $makeappx.FullName pack /d $layout /p $msix /o
if ($LASTEXITCODE) { exit $LASTEXITCODE }

Write-Host "`nBuilt $msix (version $version)"
Write-Host 'Upload this file in Partner Center > Packages. It is unsigned on purpose; the Store signs it.'
