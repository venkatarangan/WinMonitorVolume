# Handover: WinMonitorVolume

Notes for picking this project up in a new Claude Code session, on any PC. Point Claude at this file first.

## What this is

A small Windows 11 desktop app (C# WinForms, .NET 10) that sets an external monitor's **built-in speaker volume** over **DDC/CI**, so the owner doesn't have to turn the monitor's physical knob. It's the Windows equivalent of the macOS app MonitorControl. It was built for and tested on an LG 32U720A-B connected by USB-C. Windows' own volume only works inside the limit set on the monitor; this app changes that limit.

The project folder was originally named `lgmonitor` and has since been renamed to `WinMonitorVolume`. Nothing in the code or scripts depends on the folder name.

## Files

| File | Purpose |
|---|---|
| `WinMonitorVolume.csproj` | Project. Contains the version scheme (see below). |
| `Program.cs` | Entry point. Command-line modes, single-instance handling, app icon and version helpers. |
| `Ddc.cs` | DDC/CI through `dxva2.dll` (Monitor Configuration API). VCP `0x62` = speaker volume, `0x8D` = mute (2 = unmute). Retries each call 3 times. |
| `MainForm.cs` | Main window, tray icon, menus, start-with-Windows options. |
| `AboutForm.cs` | About dialog (version, description, credits with links). |
| `Startup.cs` | Reads and writes the start-with-Windows entry in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. |
| `app.ico` | App icon (embedded as a resource too; used for the window and tray). |
| `build.ps1` | Release build: bumps the version once, builds both EXEs and the release zips. |
| `Install.ps1` | End-user installer: per-user, no admin. `-Uninstall` removes it. |
| `build-number.txt` | Last build number. Commit it. |
| `README.md` | GitHub readme (install options with the easiest first, usage, build). |
| `docs/screenshots/` | `main-window.png`, `about-dialog.png` (used by the README). |
| `tools/make-icon.ps1` | Regenerates `app.ico`. |
| `tools/capture-window.ps1` | Captures a window by title to PNG, for refreshing screenshots. |

Ignored by git: `bin/`, `obj/`, `publish/`, `publish-standalone/`, `dist/`.

## How it behaves

- **Window:** a monitor picker (auto-selects the first monitor that answers DDC/CI), a volume slider, a **Set to MAX** button, and *When I sign in to Windows* options. Menus: App (Refresh monitors, Exit) and Help (About).
- **Command line:**
  - `--max` sets volume to 100%, then exits. `--set N` sets it to N% of the monitor's maximum, then exits. Neither shows a window. Both apply to every monitor that supports volume, and retry for about 30 seconds in case the display isn't ready right after sign-in.
  - `--tray` starts hidden in the system tray.
- **Tray:** click to open the window. The right-click menu has Volume 100% / 50% / 0%, About and Exit. Closing the window hides it to the tray when the tray startup option is chosen or the app was started with `--tray`; otherwise closing exits.
- **Single instance:** uses a named mutex (`Local\WinMonitorVolume.SingleInstance`). Launching the app a second time signals an event (`Local\WinMonitorVolume.Show`) that brings the first copy's window up, unless the second launch used `--tray`.
- **Start with Windows:** writes `"<exe path>" --tray` or `"<exe path>" --set N` to the user's `Run` registry value `WinMonitorVolume`. Choosing *Don't start automatically* deletes the value. The entry stores the EXE's full path, so moving the EXE breaks it. `Install.ps1` rewrites an existing entry to point at the installed copy.

## Build and versioning

- Requires the **.NET 10 SDK** (any recent 10.0.x).
- Run `.\build.ps1`. It produces:
  - `publish\WinMonitorVolume.exe`: about 0.2 MB, needs the .NET 10 Desktop Runtime.
  - `publish-standalone\WinMonitorVolume.exe`: about 49 MB, self-contained with .NET built in and compressed. Runs with nothing installed.
  - `dist\WinMonitorVolume-<ver>-win-x64.zip` (standalone) and `dist\WinMonitorVolume-<ver>-win-x64-needs-dotnet.zip` (small). Each contains the EXE, `README.md` and `Install.ps1`.
- **Version** is `1.1.<build>`. A Release build without `-p:BuildNumber` bumps `build-number.txt` by itself. `build.ps1` bumps it once and passes `-p:BuildNumber`, so both EXEs share a version. The last build was **1.1.6**. To start a new minor version, change `VersionMajorMinor` in the csproj.
- WinForms doesn't support trimming, so the standalone EXE stays around 49 MB.
- After renaming or copying the folder, delete `bin/` and `obj/` before the first build. They cache absolute paths.

## Install script

`Install.ps1` copies the EXE and README to `%LOCALAPPDATA%\Programs\WinMonitorVolume`, removes the downloaded-file mark, creates a Start menu shortcut in the user's Programs folder, fixes up any existing start-with-Windows entry, and launches the app. It stops a running copy first. It looks for the EXE next to itself (release zip) or in `publish-standalone\` (source checkout). It warns when it finds the small build. It's written for Windows PowerShell 5.1 so it works on a stock Windows 11.

## Tested so far

- Reading and setting volume on the LG monitor over USB-C; the user confirmed it works.
- `--max` exits with code 0. `--tray` starts hidden. A second launch brings up the tray copy's window.
- The standalone EXE loads nothing from an installed .NET.
- `Install.ps1` from an extracted release zip, run with PowerShell 5.1: files copied, shortcut created, startup entry repointed, app launched.

## Not yet tested / ideas

- An actual sign-in with each startup option.
- Every tray menu item, clicked by hand.
- `Install.ps1 -Uninstall`.
- The standalone EXE on a PC that has no .NET at all.
- Windows on ARM (the x64 EXE should run under emulation). A `win-arm64` build could be added to `build.ps1`.
- Possible future work:
  - Code signing, to avoid the SmartScreen warning.
  - A GitHub repository and release (not a git repo yet).
  - A license file (none chosen yet).
  - A global hotkey.
  - Restoring the volume after the PC wakes from sleep.

## Credits to keep

The About dialog and README say: written by Mangoidiots.com (https://mangoidiots.com) using Claude AI Opus, inspired by MonitorControl (https://github.com/MonitorControl/).
