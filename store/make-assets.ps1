# Generates the Store/MSIX logo PNGs in store\Assets from app.ico (largest frame). Windows PowerShell or pwsh on Windows.
# Usage: .\store\make-assets.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $PSScriptRoot 'Assets'
New-Item -ItemType Directory -Force $out | Out-Null

# Read the largest frame straight from the .ico directory. Icon.ToBitmap() fails on PNG-compressed
# frames ("Requested range extends past the end of the array"), and app.ico stores all frames as PNG.
$bytes = [System.IO.File]::ReadAllBytes((Join-Path $root 'app.ico'))
$count = [BitConverter]::ToUInt16($bytes, 4)
$best = $null; $bestSize = -1
for ($i = 0; $i -lt $count; $i++) {
    $e = 6 + 16 * $i
    $w = $bytes[$e]; if ($w -eq 0) { $w = 256 }
    if ($w -gt $bestSize) { $bestSize = $w; $best = $e }
}
$len = [BitConverter]::ToInt32($bytes, $best + 8)
$off = [BitConverter]::ToInt32($bytes, $best + 12)
$ms = New-Object System.IO.MemoryStream -ArgumentList $bytes, $off, $len
$isPng = $bytes[$off] -eq 0x89 -and $bytes[$off + 1] -eq 0x50
if ($isPng) {
    $src = [System.Drawing.Image]::FromStream($ms)
} else {
    $src = (New-Object System.Drawing.Icon (Join-Path $root 'app.ico'), $bestSize, $bestSize).ToBitmap()
}
Write-Host "Using ${bestSize}x${bestSize} frame from app.ico"

function New-Logo([int]$w, [int]$h, [string]$name, [double]$fill = 0.75, [bool]$tile = $false) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    if ($tile) { $g.Clear([System.Drawing.Color]::FromArgb(0x0A, 0x6F, 0xCF)) }
    $side = [int]([Math]::Min($w, $h) * $fill)
    $g.DrawImage($src, [int](($w - $side) / 2), [int](($h - $side) / 2), $side, $side)
    $g.Dispose()
    $bmp.Save((Join-Path $out $name), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}

New-Logo 44  44  'Square44x44Logo.png'  1.0
New-Logo 88  88  'Square44x44Logo.scale-200.png' 1.0
New-Logo 44  44  'Square44x44Logo.targetsize-44_altform-unplated.png' 1.0
New-Logo 50  50  'StoreLogo.png'        1.0
New-Logo 150 150 'Square150x150Logo.png' 0.7
New-Logo 71  71  'SmallTile.png'        0.7
New-Logo 310 150 'Wide310x150Logo.png'  0.8
New-Logo 310 310 'LargeTile.png'        0.7
# 1:1 box art for the Store listing (upload in Partner Center, not part of the package).
# Partner Center wants exactly 1080x1080 or 2160x2160, so redraw it as vectors (same design as
# tools\make-icon.ps1) instead of upscaling the 256px icon frame.
function New-BoxArt([int]$s, [string]$name) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAlias'
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.Point]::new(0, 0)), ([System.Drawing.Point]::new(0, $s)), ([System.Drawing.Color]::FromArgb(0x1A, 0x86, 0xE8)), ([System.Drawing.Color]::FromArgb(0x00, 0x5A, 0xB5))
    $g.FillRectangle($brush, 0, 0, $s, $s)
    $font = New-Object System.Drawing.Font 'Segoe Fluent Icons', ($s * 0.45), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
    $g.DrawString([string][char]0xE767, $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, ($s * 0.03), $s, $s), $fmt)
    $g.Dispose()
    $bmp.Save((Join-Path $out $name), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}
Remove-Item (Join-Path $out 'StoreListing-300x300.png') -ErrorAction SilentlyContinue
New-BoxArt 1080 'StoreListing-1080x1080.png'
New-BoxArt 2160 'StoreListing-2160x2160.png'
Write-Host "Assets written to $out"
