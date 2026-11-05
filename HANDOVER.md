# Handover: WinMonitorVolume

Notes for picking this project up in a new Claude Code session, on any PC. Point Claude at this file first.

## What this is

A small Windows 11 desktop app (C# WinForms, .NET 10) that sets an external monitor's **built-in speaker volume** over **DDC/CI**, so the owner doesn't have to turn the monitor's physical knob. It's the Windows equivalent of the macOS app MonitorControl. It was built for the LG 32U720A-B (connected by USB-C) and tested on that monitor and on an LG 34WQ75C-B (34 inch curved, over DisplayPort). Windows' own volume only works inside the limit set on the monitor; this app changes that limit.

The project folder was originally named `lgmonitor` and has since been renamed to `WinMonitorVolume`. Nothing in the code or scripts depends on the folder name.

## Files

| File | Purpose |
|---|---|
| `WinMonitorVolume.csproj` | Project. Contains the version scheme (see below). |
| `Program.cs` | Entry point. Command-line modes, single-instance handling, app icon and version helpers. |
| `Ddc.cs` | DDC/CI through `dxva2.dll` (Monitor Configuration API). VCP `0x62` = speaker volume, `0x8D` = mute (2 = unmute). Retries each call 3 times. |
| `MainForm.cs` | Main window, tray icon, menus, start-with-Windows options. |
| `AboutForm.cs` | About dialog (version, description, credits with links). |
| `Startup.cs` | Start-with-Windows setting. Unpackaged: the `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry. Microsoft Store (MSIX): the manifest `StartupTask` plus `%LOCALAPPDATA%\WinMonitorVolume\startup.txt`. |
| `app.ico` | App icon (embedded as a resource too; used for the window and tray). |
| `build.ps1` | Release build: bumps the version once, builds both EXEs and the release zips. |
| `Install.ps1` | End-user installer: per-user, no admin. `-Uninstall` removes it. |
| `build-number.txt` | Last build number. Commit it. |
| `README.md` | GitHub readme (Microsoft Store first, then the zip install options, usage, build). |
| `PRIVACY.md` | Privacy policy, linked from the Store listing. |
| `store/` | Microsoft Store packaging: `store-identity.json` (Partner Center identity), `AppxManifest.template.xml`, `make-assets.ps1`, `build-msix.ps1`, `Assets/` and `STORE-SUBMISSION.md` (the full guide and submission record). |
| `docs/screenshots/` | `main-window.png`, `about-dialog.png` (used by the README). |
| `tools/make-icon.ps1` | Regenerates `app.ico`. |
| `tools/capture-window.ps1` | Captures a window by title to PNG, for refreshing screenshots. |

Ignored by git: `bin/`, `obj/`, `publish/`, `publish-standalone/`, `publish-msix/`, `dist/`.

## How it behaves

- **Window:** a monitor picker (auto-selects the first monitor that answers DDC/CI), a volume slider, a **Set to MAX** button, and *When I sign in to Windows* options. Menus: App (Refresh monitors, Exit) and Help (About).
- **Command line:**
  - `--max` sets volume to 100%, then exits. `--set N` sets it to N% of the monitor's maximum, then exits. Neither shows a window. Both apply to every monitor that supports volume, and retry for about 30 seconds in case the display isn't ready right after sign-in.
  - `--tray` starts hidden in the system tray.
- **Tray:** click to open the window. The right-click menu has Volume 100% / 50% / 0%, About and Exit. Closing the window hides it to the tray when the tray startup option is chosen or the app was started with `--tray`; otherwise closing exits.
- **Single instance:** uses a named mutex (`Local\WinMonitorVolume.SingleInstance`). Launching the app a second time signals an event (`Local\WinMonitorVolume.Show`) that brings the first copy's window up, unless the second launch used `--tray`.
- **Start with Windows (zip build):** writes `"<exe path>" --tray` or `"<exe path>" --set N` to the user's `Run` registry value `WinMonitorVolume`. Choosing *Don't start automatically* deletes the value. The entry stores the EXE's full path, so moving the EXE breaks it. `Install.ps1` rewrites an existing entry to point at the installed copy.
- **Start with Windows (Store build):** Windows ignores the `Run` key inside an MSIX package, so the app enables or disables the manifest's `StartupTask` (`WinMonitorVolumeStartup`) and writes `--tray` or `--set N` to `%LOCALAPPDATA%\WinMonitorVolume\startup.txt`. At sign-in Windows starts the app with no arguments. `Program.cs` sees the `StartupTask` activation (`Startup.LaunchedByStartupTask()`) and applies the saved mode.

## Build and versioning

- Requires the **.NET 10 SDK** (any recent 10.0.x). Windows blocks `.ps1` scripts by default, so run `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass` in each new PowerShell window.
- The project targets `net10.0-windows10.0.19041.0`, which is needed for the Windows `StartupTask` API.
- Run `.\build.ps1`. It produces:
  - `publish\WinMonitorVolume.exe`: about 0.2 MB, needs the .NET 10 Desktop Runtime.
  - `publish-standalone\WinMonitorVolume.exe`: about 49 MB, self-contained with .NET built in and compressed. Runs with nothing installed.
  - `dist\WinMonitorVolume-<ver>-win-x64.zip` (standalone) and `dist\WinMonitorVolume-<ver>-win-x64-needs-dotnet.zip` (small). Each contains the EXE, `README.md` and `Install.ps1`.
- **Version** is `1.1.<build>`. A Release build without `-p:BuildNumber` bumps `build-number.txt` by itself. `build.ps1` bumps it once and passes `-p:BuildNumber`, so both EXEs share a version. The last zip release on GitHub is **1.1.6** (older than the Store's 1.1.8.0). The build number is now **8**: build 7 was a local zip build, and **1.1.8.0** is the first Microsoft Store package. MSIX versions are `1.1.<build>.0`. To start a new minor version, change `VersionMajorMinor` in the csproj.
- WinForms doesn't support trimming, so the standalone EXE stays around 49 MB.
- After renaming or copying the folder, delete `bin/` and `obj/` before the first build. They cache absolute paths.

## Microsoft Store

- **Status:** 1.1.8.0 was submitted on 3 November 2026, **approved and live** in the Store: https://apps.microsoft.com/detail/9pc4vlxpk5sw?hl=en-US&gl=IN. The owner installed it and confirmed it works, with no difference from the zip build. The README links to it and calls it the recommended way to run the app.
- **Running without the Store:** the zip and local builds are unsigned, so SmartScreen warns and Smart App Control blocks them. The README's "For developers" section lists the workarounds (Run anyway, Unblock-File, turning SmartScreen or Smart App Control off, or building locally).
- **Why:** on Windows 11, SmartScreen warns about the unsigned zip build and Smart App Control blocks it outright. The Store build is signed by Microsoft and runs without warnings.
- **Identity:** saved in `store/store-identity.json` (publisher: Little Feet Services Pvt Ltd). `build-msix.ps1` reads it, so there's no need to ask for the values again.
- **Release an update:** run `.\store\build-msix.ps1` on Windows (needs the Windows SDK), then upload `dist\WinMonitorVolume_<ver>_x64.msix` unsigned in a new Partner Center submission. Details and the full submission record are in `store/STORE-SUBMISSION.md`.

## Install script

`Install.ps1` copies the EXE and README to `%LOCALAPPDATA%\Programs\WinMonitorVolume`, removes the downloaded-file mark, creates a Start menu shortcut in the user's Programs folder, fixes up any existing start-with-Windows entry, and launches the app. It stops a running copy first. It looks for the EXE next to itself (release zip) or in `publish-standalone\` (source checkout). It warns when it finds the small build. It's written for Windows PowerShell 5.1 so it works on a stock Windows 11.

## Tested so far

- Reading and setting volume on two LG monitors: the 32U720A-B (over USB-C) and the 34WQ75C-B (34 inch curved, over DisplayPort); the owner confirmed it works on both.
- `--max` exits with code 0. `--tray` starts hidden. A second launch brings up the tray copy's window.
- The standalone EXE loads nothing from an installed .NET.
- `Install.ps1` from an extracted release zip, run with PowerShell 5.1: files copied, shortcut created, startup entry repointed, app launched.
- Microsoft Store package (test-signed): volume, tray, and both sign-in options after a real sign-out and sign-in.

## Not yet tested / ideas

- An actual sign-in with each startup option in the zip build.
- Every tray menu item, clicked by hand.
- `Install.ps1 -Uninstall`.
- The standalone EXE on a PC that has no .NET at all.
- **Future to-do (no rebuild just for this):** the About dialog text in `AboutForm.cs` line 42 still says "Tested on Windows 11 with an LG monitor connected by a USB-C cable." It should say two LG monitors, the 32U720A-B over USB-C and the 34WQ75C-B over DisplayPort. Fix it in the next release build, together with whatever else changes then.
- Windows on ARM (the x64 EXE should run under emulation). A `win-arm64` build could be added to `build.ps1`.
- Possible future work:
  - A global hotkey.
  - Restoring the volume after the PC wakes from sleep.

## Credits to keep

The About dialog and README say: written by Mangoidiots.com (https://mangoidiots.com) using Claude AI Opus, inspired by MonitorControl (https://github.com/MonitorControl/).
