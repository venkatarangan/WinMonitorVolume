# Saves a PNG of an open window (by exact title), cropped to the visible frame. Used for docs/screenshots.
# Usage: .\tools\capture-window.ps1 -Title 'WinMonitorVolume' -Out .\docs\screenshots\main-window.png
# Open the About box with Alt+H, A, then capture title 'About WinMonitorVolume'.
param([string]$Title, [string]$Out)
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class Cap {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string t);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
}
'@
[Cap]::SetProcessDPIAware() | Out-Null
$h = [Cap]::FindWindow([NullString]::Value, $Title)
if ($h -eq [IntPtr]::Zero) { throw "window '$Title' not found" }
$wr = New-Object Cap+RECT; [Cap]::GetWindowRect($h, [ref]$wr) | Out-Null
$fr = New-Object Cap+RECT; [Cap]::DwmGetWindowAttribute($h, 9, [ref]$fr, 16) | Out-Null
$bmp = New-Object System.Drawing.Bitmap ($wr.R - $wr.L), ($wr.B - $wr.T)
$g = [System.Drawing.Graphics]::FromImage($bmp); $hdc = $g.GetHdc()
[Cap]::PrintWindow($h, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc); $g.Dispose()
$crop = New-Object System.Drawing.Rectangle ($fr.L - $wr.L), ($fr.T - $wr.T), ($fr.R - $fr.L), ($fr.B - $fr.T)
$bmp.Clone($crop, $bmp.PixelFormat).Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
"saved $Out ($($crop.Width)x$($crop.Height))"
