Add-Type -AssemblyName System.Drawing

function New-IconPng([int]$size) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $scale = $size / 256.0
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $background = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 23, 27, 34))
    $border = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(180, 91, 107, 122), [Math]::Max(1, [int](5 * $scale)))
    $ring = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 101, 199, 138), [Math]::Max(2, [int](20 * $scale)))
    $bar = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 232, 237, 243))
    $rect = [System.Drawing.RectangleF]::new(18*$scale, 18*$scale, 220*$scale, 220*$scale)
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new(); $radius = 58*$scale; $diameter = 2*$radius
    $path.AddArc($rect.X, $rect.Y, $diameter, $diameter, 180, 90); $path.AddArc($rect.Right-$diameter, $rect.Y, $diameter, $diameter, 270, 90); $path.AddArc($rect.Right-$diameter, $rect.Bottom-$diameter, $diameter, $diameter, 0, 90); $path.AddArc($rect.X, $rect.Bottom-$diameter, $diameter, $diameter, 90, 90); $path.CloseFigure()
    $graphics.FillPath($background, $path); $graphics.DrawPath($border, $path)
    $graphics.DrawArc($ring, 56*$scale, 56*$scale, 144*$scale, 144*$scale, 35, 290)
    $graphics.FillRectangle($bar, 112*$scale, 78*$scale, 32*$scale, 100*$scale)
    $memory = [System.IO.MemoryStream]::new(); $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $bitmap.Dispose(); $background.Dispose(); $border.Dispose(); $ring.Dispose(); $bar.Dispose(); $path.Dispose()
    return ,$memory.ToArray()
}

$sizes = 16,24,32,48,64,128,256
$images = @($sizes | ForEach-Object { ,(New-IconPng $_) })
$output = Join-Path $PSScriptRoot '..\src\CodexQuotaFloat\Resources\CodexQuotaFloat.ico'
$stream = [System.IO.File]::Open($output, [System.IO.FileMode]::Create)
$writer = [System.IO.BinaryWriter]::new($stream)
$writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) { $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }; $writer.Write([Byte]$dimension); $writer.Write([Byte]$dimension); $writer.Write([Byte]0); $writer.Write([Byte]0); $writer.Write([UInt16]1); $writer.Write([UInt16]32); $writer.Write([UInt32]$images[$i].Length); $writer.Write([UInt32]$offset); $offset += $images[$i].Length }
foreach ($image in $images) { $writer.Write($image) }
$writer.Dispose(); $stream.Dispose(); Write-Output (Resolve-Path $output)
