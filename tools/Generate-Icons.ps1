<#
.SYNOPSIS  Renders the MDEditor app icon (.ico) and the MSIX tile logos from one drawing routine.
.NOTES     Output: MDEditor/Assets/AppIcon.ico and MDEditor/Packaging/Assets/*.png. Re-run after changing the design.
#>
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root 'MDEditor/Assets'
$tiles = Join-Path $root 'MDEditor/Packaging/Assets'
New-Item -ItemType Directory -Force $assets, $tiles | Out-Null

function New-Logo([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = [Math]::Min($w, $h); $m = $s * 0.08
    $x = ($w - $s) / 2 + $m; $y = ($h - $s) / 2 + $m; $d = $s - 2 * $m; $r = $d * 0.22
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($x, $y, $r, $r, 180, 90); $path.AddArc($x + $d - $r, $y, $r, $r, 270, 90)
    $path.AddArc($x + $d - $r, $y + $d - $r, $r, $r, 0, 90); $path.AddArc($x, $y + $d - $r, $r, $r, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 36, 99, 235))), $path)
    $font = New-Object System.Drawing.Font 'Segoe UI', ($d * 0.36), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
    $rect = New-Object System.Drawing.RectangleF $x, $y, $d, $d
    $g.DrawString("M$([char]0x2193)", $font, [System.Drawing.Brushes]::White, $rect, $fmt)
    $g.Dispose(); return $bmp
}

# MSIX tile assets
foreach ($t in @(@('StoreLogo', 50, 50), @('Square44x44Logo', 44, 44), @('Square150x150Logo', 150, 150), @('Wide310x150Logo', 310, 150), @('Square71x71Logo', 71, 71))) {
    $b = New-Logo $t[1] $t[2]; $b.Save((Join-Path $tiles "$($t[0]).png"), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}

# Multi-size .ico (PNG-compressed entries)
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($sz in $sizes) {
    $b = New-Logo $sz $sz; $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose(); ,$ms.ToArray()
}
$out = New-Object System.IO.MemoryStream; $bw = New-Object System.IO.BinaryWriter $out
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
[IO.File]::WriteAllBytes((Join-Path $assets 'AppIcon.ico'), $out.ToArray())
"Wrote AppIcon.ico and $((Get-ChildItem $tiles -Filter *.png).Count) tile logos"
