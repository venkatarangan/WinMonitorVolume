<#
.SYNOPSIS
    Installs WinMonitorVolume for the current user. No admin rights needed.

.DESCRIPTION
    Copies WinMonitorVolume.exe and README.md to %LOCALAPPDATA%\Programs\WinMonitorVolume
    (the standard per-user install location), adds a Start menu shortcut and launches the app.
    Running it again updates an existing install.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Install.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Install.ps1 -Uninstall
#>
param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'
$appName    = 'WinMonitorVolume'
$installDir = Join-Path $env:LOCALAPPDATA "Programs\$appName"
$exePath    = Join-Path $installDir "$appName.exe"
$shortcut   = Join-Path ([Environment]::GetFolderPath('Programs')) "$appName.lnk"
$runKey     = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

# Close a running copy so its files can be replaced or removed.
Get-Process $appName -ErrorAction SilentlyContinue | ForEach-Object { $_ | Stop-Process -Force; $_.WaitForExit(5000) | Out-Null }

if ($Uninstall) {
    Remove-ItemProperty $runKey -Name $appName -ErrorAction SilentlyContinue
    Remove-Item $shortcut -ErrorAction SilentlyContinue
    Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "$appName has been removed."
    return
}

# Find the files next to this script (release zip), or in the build output (source checkout).
function Find-File([string]$name, [string[]]$folders) {
    foreach ($folder in $folders) {
        $path = Join-Path $folder $name
        if (Test-Path $path) { return $path }
    }
    throw "Can't find $name next to this script."
}
$exeSource    = Find-File "$appName.exe" @($PSScriptRoot, (Join-Path $PSScriptRoot 'publish-standalone'))
$readmeSource = Find-File 'README.md' @($PSScriptRoot)

if ((Get-Item $exeSource).Length -lt 10MB) {
    Write-Warning "This is the small build of $appName; it needs the .NET 10 Desktop Runtime to run."
}

# Copy the files.
New-Item -ItemType Directory -Force $installDir | Out-Null
Copy-Item $exeSource $exePath -Force
Copy-Item $readmeSource (Join-Path $installDir 'README.md') -Force
Unblock-File $exePath   # drop the "downloaded from the internet" mark so Windows doesn't warn every launch

# Start menu shortcut.
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut)
$link.TargetPath = $exePath
$link.WorkingDirectory = $installDir
$link.IconLocation = "$exePath,0"
$link.Description = "Control your monitor's speaker volume"
$link.Save()

# If a start-with-Windows option was already chosen, point it at the installed copy (keeping its options).
$existing = (Get-ItemProperty $runKey -Name $appName -ErrorAction SilentlyContinue).$appName
if ($existing) {
    $options = ($existing -replace '^\s*"[^"]*"\s*', '').Trim()
    Set-ItemProperty $runKey -Name $appName -Value ("`"$exePath`" $options").Trim()
}

Write-Host "$appName installed to $installDir"
Write-Host "Start menu shortcut added. Launching..."
Start-Process $exePath
