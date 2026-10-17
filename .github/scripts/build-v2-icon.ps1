# Reuse the original mark at its exact size; only change color/background.
Add-Type -AssemblyName System.Drawing
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
function RoundedPath([int]$size) {
    $radius = [Math]::Max(2, [Math]::Round($size * 0.12))
    $diameter = 2 * $radius
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc(0, 0, $diameter, $diameter, 180, 90)
    $path.AddArc($size-$diameter, 0, $diameter, $diameter, 270, 90)
    $path.AddArc($size-$diameter, $size-$diameter, $diameter, $diameter, 0, 90)
    $path.AddArc(0, $size-$diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure(); return $path
}
function WithBackground([Drawing.Bitmap]$mark) {
    $bitmap = [Drawing.Bitmap]::new($mark.Width, $mark.Height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = 'AntiAlias'
    $path = RoundedPath $mark.Width
    $graphics.FillPath([Drawing.Brushes]::Black, $path)
    $graphics.DrawImageUnscaled($mark, 0, 0)
    $path.Dispose(); $graphics.Dispose(); return $bitmap
}
function ReadOriginalFrame([byte[]]$data, [int]$index) {
    $entry = 6 + 16 * $index
    $length = [BitConverter]::ToUInt32($data, $entry + 8)
    $offset = [BitConverter]::ToUInt32($data, $entry + 12)
    $inputStream = [IO.MemoryStream]::new($data, $offset, $length)
    $image = [Drawing.Bitmap]::new($inputStream)
    $copy = [Drawing.Bitmap]::new($image)
    $image.Dispose(); $inputStream.Dispose(); return $copy
}
$sourceBytes = [IO.File]::ReadAllBytes((Join-Path $root 'assets/HomeCamMonitor-Beta.ico'))
$sourceCount = [BitConverter]::ToUInt16($sourceBytes,4)
$original = ReadOriginalFrame $sourceBytes ($sourceCount - 1)
$original.Save((Join-Path $root 'assets/homecam-monitor-logo-original-icon.png'), [Drawing.Imaging.ImageFormat]::Png)
$black = [Drawing.Bitmap]::new($original.Width, $original.Height)
for ($y=0; $y -lt $original.Height; $y++) {
    for ($x=0; $x -lt $original.Width; $x++) {
        $black.SetPixel($x, $y, [Drawing.Color]::FromArgb($original.GetPixel($x,$y).A, 0, 0, 0))
    }
}
$black.Save((Join-Path $root 'assets/homecam-monitor-logo-black.png'), [Drawing.Imaging.ImageFormat]::Png)
$background = WithBackground $original
$background.Save((Join-Path $root 'assets/homecam-monitor-logo-default.png'), [Drawing.Imaging.ImageFormat]::Png)
$black.Dispose(); $background.Dispose(); $original.Dispose()
# Keep every original Windows icon resolution and the original mark geometry.
$sourcePath = Join-Path $root 'assets/HomeCamMonitor-Beta.ico'
$bytes = [IO.File]::ReadAllBytes($sourcePath)
$count = [BitConverter]::ToUInt16($bytes,4)
$sizes = @(); $frames = @()
for($i=0; $i -lt $count; $i++) {
    $size = [int]$bytes[6+16*$i]; if($size -eq 0){$size=256}; $sizes += $size
    $mark = ReadOriginalFrame $bytes $i
    if ($mark.Width -ne $size -or $mark.Height -ne $size) { throw "Original icon frame dimensions do not match its directory entry." }
    $bitmap = WithBackground $mark
    $stream = [IO.MemoryStream]::new(); $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    $frames += ,($stream.ToArray())
    $stream.Dispose(); $bitmap.Dispose(); $mark.Dispose()
}
$writer = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $root 'assets/HomeCamMonitor-V2-Beta.ico')))
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i=0; $i -lt $sizes.Count; $i++) {
    $dimension = if($sizes[$i] -eq 256){0}else{$sizes[$i]}
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset); $offset += $frames[$i].Length
}
foreach($frame in $frames){$writer.Write([byte[]]$frame)}
$writer.Dispose()
