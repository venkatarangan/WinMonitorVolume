# Publishing WinMonitorVolume to the Microsoft Store

**Status:** version 1.1.8.0 was submitted on 3 November 2026 and has been **approved and published**: <https://apps.microsoft.com/detail/9pc4vlxpk5sw?hl=en-US&gl=IN>. It was installed from the Store and works the same as the zip build. The GitHub zip release (1.1.6) is older than the Store version.

## Why the Store

On Windows 11, an unsigned EXE downloaded from the internet is treated with suspicion:

- **Microsoft Defender SmartScreen** shows *"Windows protected your PC"*. You can only continue through the easy-to-miss **More info → Run anyway** link.
- **Smart App Control**, which is on for many new Windows 11 installs, blocks unsigned apps completely and has no "Run anyway" option.

So for many users the release zip either looked like malware or didn't run at all. Buying a code-signing certificate costs money every year, and a new certificate still triggers SmartScreen until it builds up a reputation. A Store package is signed by Microsoft, so it installs and runs with no warnings. Store installs also update automatically.

## How the Store build differs

The Store takes an **MSIX** package. You upload it **unsigned** and Microsoft signs it. Because this is a classic desktop (WinForms) app, it is packaged as **full trust** (`runFullTrust`).

| File | Purpose |
|---|---|
| `store/store-identity.json` | The product identity from Partner Center (identity name, publisher, publisher display name). `build-msix.ps1` reads it, so you don't need to type the values in. These values are not secret; every package contains them. |
| `store/AppxManifest.template.xml` | MSIX manifest template: identity, logos, `runFullTrust`, and a sign-in `StartupTask`. |
| `store/make-assets.ps1` | Draws the package logos in `store\Assets`, plus the 1080×1080 and 2160×2160 Store box art. |
| `store/build-msix.ps1` | Does a self-contained publish, fills in the manifest, runs `makeappx` and writes `dist\WinMonitorVolume_<ver>_x64.msix`. Bumps `build-number.txt` unless you pass `-NoBump`. |
| `Startup.cs`, `Program.cs` | Inside an MSIX package Windows ignores the `Run` registry key. So the Store build switches the manifest's `StartupTask` on or off, and keeps the chosen mode (`--tray` or `--set N`) in `%LOCALAPPDATA%\WinMonitorVolume\startup.txt`. When Windows starts the app at sign-in, `Program.cs` reads that file. The zip / `Install.ps1` build still uses the `Run` key. |
| `WinMonitorVolume.csproj` | Target framework is `net10.0-windows10.0.19041.0` so the app can call the Windows `StartupTask` API. |
| `PRIVACY.md` | Privacy policy. Partner Center needs one because the app uses `runFullTrust`. |

## Releasing an update

On Windows, in PowerShell in the repo folder:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass   # Windows blocks .ps1 scripts by default
.\store\build-msix.ps1
```

Then go to Partner Center > WinMonitorVolume > **Start update** (a new submission), drag the new `.msix` into **Packages**, update *What's new in this version* and submit. Every upload needs a higher version than the last one. The script handles this by bumping the build number.

## One-time setup (already done)

**Tools.** You need the .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`) and the Windows SDK, which provides `makeappx.exe` and `signtool.exe`. If `winget install Microsoft.WindowsSDK.10.0.22621` fails with exit code `2147942487` (`0x80070057`), first check whether the SDK is already installed:

```powershell
Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe"
```

If it isn't, install `Microsoft.WindowsSDK.10.0.26100` instead, or use the installer from <https://developer.microsoft.com/windows/downloads/windows-sdk/>.

**Partner Center.** Go to **Apps and games → New product → MSIX or PWA app**, reserve the name `WinMonitorVolume`, then copy the three values from **Product management → Product identity** into `store/store-identity.json`.

## Testing a package locally before submitting

You can't install an unsigned MSIX. To test one, build it with a throwaway identity and sign it with a self-signed certificate:

```powershell
.\store\make-assets.ps1
.\store\build-msix.ps1 -NoBump -IdentityName "WinMonitorVolume.Test" -Publisher "CN=WinMonitorVolumeTest" -PublisherDisplayName "Test"

$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=WinMonitorVolumeTest" `
  -KeyUsage DigitalSignature -CertStoreLocation Cert:\CurrentUser\My `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3","2.5.29.19={text}")
Export-Certificate -Cert $cert -FilePath .\test.cer
```

Trusting the certificate needs an **admin** PowerShell. Without admin rights it fails with `Access is denied`:

```powershell
Import-Certificate -FilePath .\test.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

Back in the normal window:

```powershell
$signtool = (Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" | Sort-Object FullName -Descending)[0].FullName
$msix = (Get-ChildItem .\dist\WinMonitorVolume_*_x64.msix | Sort-Object LastWriteTime -Descending)[0].FullName
& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $msix
Add-AppxPackage $msix
```

Then check:

- The volume slider works.
- "Start this app in the system tray" shows as **Enabled** in Task Manager → Startup apps.
- After you sign out and back in, the app goes straight to the tray.
- With "Set the volume to 50%" chosen, the volume changes at sign-in and no window appears.

All of these passed on 3 November 2026.

Clean up afterwards. Run the last line in an admin window, and delete the test-signed `.msix` too, because the Store needs an unsigned one:

```powershell
Get-AppxPackage *WinMonitorVolume.Test* | Remove-AppxPackage
Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -eq "CN=WinMonitorVolumeTest" | Remove-Item
Remove-Item .\test.cer, .\dist\WinMonitorVolume_*_x64.msix
Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -eq "CN=WinMonitorVolumeTest" | Remove-Item
```

## What was entered in the first submission

- **Pricing and availability:** all markets (no market groups), discoverable in the Store, released as soon as it passes certification, **Free**, no free trial.
- **Properties:** category **Utilities & tools**; no special hardware declared.
- **Age ratings:** questionnaire with no violence, no user-generated content and no online features.
- **Store listing language:** **English (United States)**, which matches the package's `en-us`. Users in other English locales fall back to it.
- **Description:**
  > Control your monitor's built-in speaker volume right from Windows — no reaching for the knob behind the screen. WinMonitorVolume talks to your monitor over DDC/CI and sets its speaker volume directly. Use the slider or the Set to MAX button, the system tray icon with 0% / 50% / 100% presets, and optional sign-in actions (start in the tray, or just set the volume and exit). Also works from the command line: `--max`, `--set 50`, `--tray`. Requires a monitor that supports DDC/CI volume control, with DDC/CI turned on in its on-screen menu. Free and open source (MIT).
- **Keywords:** monitor, volume, DDC/CI, speaker, display, tray.
- **Screenshots:** `docs/screenshots/main-window.png`, `about-dialog.png`.
- **Store logo (1:1 box art):** `store\Assets\StoreListing-2160x2160.png`. Partner Center only accepts exactly 1080×1080 or 2160×2160.
- **Website / support:** <https://github.com/venkatarangan/WinMonitorVolume>
- **Privacy policy URL:** <https://github.com/venkatarangan/WinMonitorVolume/blob/main/PRIVACY.md>
- **Copyright:** © Mangoidiots.com
- **Notes for certification:**
  > This is a classic Win32/WinForms desktop app packaged with the Desktop Bridge. It needs full trust to call dxva2.dll (Monitor Configuration API, DDC/CI) to set the external monitor's speaker volume. To test: connect a monitor with DDC/CI enabled, open the app and move the slider. Without a DDC/CI monitor the app shows "This monitor did not answer", which is expected. The app collects no data and makes no network connections.

## Problems hit along the way, and the fixes

- **`.\build.ps1 cannot be loaded because running scripts is disabled`:** run `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass` first. It only affects that PowerShell window.
- **`make-assets.ps1`: `ToBitmap ... Requested range extends past the end of the array`:** every frame in `app.ico` is stored as PNG data, and `Icon.ToBitmap()` can't read that. The script now reads the PNG bytes of the largest frame directly.
- **Store logo rejected (`Invalid size`):** Partner Center needs 1080×1080 or 2160×2160. `make-assets.ps1` now draws both sizes as vectors.
- **Privacy policy required:** `runFullTrust` apps need one. The policy is `PRIVACY.md` in this repo.

## Things to know

- **Certification reviewers may not have a DDC/CI monitor.** The certification notes explain the "This monitor did not answer" message they will see.
- **Sign-in options in the Store build** use Windows' own Startup apps switch. If the user turns the app off there, the app tells them to turn it back on in Settings > Apps > Startup.
- **Detecting a sign-in launch** relies on `AppInstance.GetActivatedEventArgs()` in `Startup.LaunchedByStartupTask()`. It was verified by signing out and in with the test package.
