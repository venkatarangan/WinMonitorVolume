# Regenerates app.ico (white speaker glyph on a blue rounded square, sizes 16-256 px).
# Usage: .\tools\make-icon.ps1 -Out .\app.ico
param([string]$Out)
Add-Type -AssemblyName System.Drawing
$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.Clear([System.Drawing.Color]::Transparent)
    $r = [Math]::Max(2, $s * 0.22); $d = $r * 2; $w = $s - 1
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90); $path.AddArc($w - $d, 0, $d, $d, 270, 90)
    $path.AddArc($w - $d, $w - $d, $d, $d, 0, 90); $path.AddArc(0, $w - $d, $d, $d, 90, 90); $path.CloseFigure()
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.Point]::new(0, 0)), ([System.Drawing.Point]::new(0, $s)), ([System.Drawing.Color]::FromArgb(0x1A, 0x86, 0xE8)), ([System.Drawing.Color]::FromArgb(0x00, 0x5A, 0xB5))
    $g.FillPath($brush, $path)
    $font = New-Object System.Drawing.Font 'Segoe Fluent Icons', ($s * 0.58), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
    $g.DrawString([string][char]0xE767, $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, ($s * 0.03), $s, $s), $fmt)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream; $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $pngs += , $ms.ToArray(); $bmp.Dispose()
}
$fs = [System.IO.File]::Create($Out); $bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$b); $bw.Write([byte]$b); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close()
