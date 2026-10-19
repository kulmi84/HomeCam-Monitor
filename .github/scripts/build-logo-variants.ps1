# All variants derive from the confirmed Beta37 default artwork, without changing geometry.
Add-Type -AssemblyName System.Drawing
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source = [Drawing.Bitmap]::new((Join-Path $root 'assets/homecam-monitor-logo-default.png'))
try {
    foreach ($variant in @('White','Black','Default')) {
        $mark = [Drawing.Bitmap]::new($source.Width,$source.Height)
        for ($y=0;$y -lt $source.Height;$y++) { for ($x=0;$x -lt $source.Width;$x++) {
            $pixel=$source.GetPixel($x,$y)
            if ($variant -eq 'Default') { $color=$pixel } else {
                $alpha=[int][Math]::Round($pixel.A * [Math]::Max($pixel.R,[Math]::Max($pixel.G,$pixel.B)) / 255)
                $level=if($variant -eq 'White'){255}else{0}
                $color=[Drawing.Color]::FromArgb($alpha,$level,$level,$level)
            }
            $mark.SetPixel($x,$y,$color)
        } }
        $mark.Save((Join-Path $root "assets/homecam-logo-$variant.png"),[Drawing.Imaging.ImageFormat]::Png)
        $frames=@(); $sizes=@(16,24,32,48,64,128,256)
        foreach($size in $sizes) {
            $frame=[Drawing.Bitmap]::new($size,$size); $g=[Drawing.Graphics]::FromImage($frame)
            $g.InterpolationMode='HighQualityBicubic'; $g.CompositingMode='SourceCopy'
            $g.DrawImage($mark,0,0,$size,$size)
            $stream=[IO.MemoryStream]::new(); $frame.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
            $frames+=,($stream.ToArray()); $stream.Dispose(); $g.Dispose(); $frame.Dispose()
        }
        $writer=[IO.BinaryWriter]::new([IO.File]::Create((Join-Path $root "assets/homecam-icon-$variant.ico")))
        try {
            $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
            $offset=6+16*$sizes.Count
            for($i=0;$i -lt $sizes.Count;$i++) {
                $dim=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
                $writer.Write([byte]$dim);$writer.Write([byte]$dim);$writer.Write([byte]0);$writer.Write([byte]0)
                $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frames[$i].Length);$writer.Write([uint32]$offset)
                $offset+=$frames[$i].Length
            }
            foreach($frame in $frames){$writer.Write([byte[]]$frame)}
        } finally {$writer.Dispose();$mark.Dispose()}
    }
} finally {$source.Dispose()}
Copy-Item (Join-Path $root 'assets/homecam-icon-Default.ico') (Join-Path $root 'assets/HomeCamMonitor-V2-Beta.ico') -Force
