# Builds both release EXEs with the same, newly incremented version number, plus release zips.
#   publish\WinMonitorVolume.exe             small; needs the .NET 10 Desktop Runtime
#   publish-standalone\WinMonitorVolume.exe  larger; runs on any 64-bit Windows 10/11 PC, nothing to install
#   dist\WinMonitorVolume-<version>-win-x64[-needs-dotnet].zip   EXE + README.md + Install.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$numberFile = Join-Path $root 'build-number.txt'
$build = [int](Get-Content $numberFile -ErrorAction SilentlyContinue | Select-Object -First 1) + 1
Set-Content $numberFile $build

$common = @("$root\WinMonitorVolume.csproj", '-c', 'Release', '-r', 'win-x64', '-p:PublishSingleFile=true', "-p:BuildNumber=$build")

dotnet publish @common --self-contained false -o "$root\publish"
if ($LASTEXITCODE) { exit $LASTEXITCODE }

dotnet publish @common --self-contained true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "$root\publish-standalone"
if ($LASTEXITCODE) { exit $LASTEXITCODE }

$version = (Get-Item "$root\publish\WinMonitorVolume.exe").VersionInfo.ProductVersion

# Release zips for GitHub: the standalone one is what most people should download.
$dist = "$root\dist"
New-Item -ItemType Directory -Force $dist | Out-Null
$zipStandalone = "$dist\WinMonitorVolume-$version-win-x64.zip"
$zipSmall = "$dist\WinMonitorVolume-$version-win-x64-needs-dotnet.zip"
Compress-Archive -Force -DestinationPath $zipStandalone -Path "$root\publish-standalone\WinMonitorVolume.exe", "$root\README.md", "$root\Install.ps1"
Compress-Archive -Force -DestinationPath $zipSmall -Path "$root\publish\WinMonitorVolume.exe", "$root\README.md", "$root\Install.ps1"

Write-Host "`nBuilt version $version"
Get-Item "$root\publish\WinMonitorVolume.exe", "$root\publish-standalone\WinMonitorVolume.exe", $zipStandalone, $zipSmall |
    ForEach-Object { '{0,-70} {1,6:N1} MB' -f $_.FullName.Substring($root.Length + 1), ($_.Length / 1MB) }
