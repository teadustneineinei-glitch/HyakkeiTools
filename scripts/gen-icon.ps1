# Generate app icons: gradient rounded square + "bai" (0x767E) glyph.
# Output: src\Hyakkei.App\Assets\logo.png (256px) and multi-size logo.ico
# NOTE: keep this file ASCII-only. PowerShell 5.1 reads BOM-less files as ANSI,
#       so non-ASCII comments would corrupt parsing.
Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot "..\src\Hyakkei.App\Assets"
New-Item -ItemType Directory -Force $outDir | Out-Null

function New-LogoPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    # rounded-rect path
    $r = [int]($size * 0.22); $d = $r * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
    $path.AddArc(0, $size - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    # blue -> violet diagonal gradient
    $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $c1 = [System.Drawing.Color]::FromArgb(255, 84, 118, 255)
    $c2 = [System.Drawing.Color]::FromArgb(255, 148, 88, 255)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $g.FillPath($brush, $path)

    $font = New-Object System.Drawing.Font("Microsoft YaHei UI", [float]($size * 0.5), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $textRect = New-Object System.Drawing.RectangleF(0, [float]($size * 0.02), $size, $size)
    $g.DrawString([string][char]0x767E, $font, [System.Drawing.Brushes]::White, $textRect, $sf)

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

# logo.png
[System.IO.File]::WriteAllBytes((Join-Path $outDir "logo.png"), (New-LogoPng 256))

# logo.ico: multi-size PNG-compressed ICO
$sizes = @(16, 24, 32, 48, 256)
$images = @{}
foreach ($s in $sizes) { $images[$s] = New-LogoPng $s }

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($s in $sizes) {
    $data = $images[$s]
    $b = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$b); $bw.Write([byte]$b)
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$data.Length)
    $bw.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($s in $sizes) { $bw.Write($images[$s]) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $outDir "logo.ico"), $ms.ToArray())

Write-Host "Icons generated -> $outDir"
