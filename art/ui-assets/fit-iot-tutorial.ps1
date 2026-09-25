# Jump is a separately edited compact sprite; do not overwrite it with the old wide layout.
Add-Type -AssemblyName System.Drawing
$assetDir = (Resolve-Path (Join-Path $PSScriptRoot '../../unity/UnderTheSea/Assets/Game/Art/UI/Tutorial')).Path
function New-Canvas($w, $h) {
    return [System.Drawing.Bitmap]::new($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
}
function New-Graphics($bitmap) {
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    return $g
}
function Put-Part($g, $source, $sx, $sy, $sw, $sh, $dx, $dy, $dw, $dh, $fade = 0) {
    $part = $source.Clone([System.Drawing.Rectangle]::new($sx,$sy,$sw,$sh), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    if ($fade -gt 0) {
        for ($y = $sh-$fade; $y -lt $sh; $y++) {
            $factor = [double]($sh-1-$y) / $fade
            for ($x=0; $x -lt $sw; $x++) {
                $c=$part.GetPixel($x,$y)
                $part.SetPixel($x,$y,[System.Drawing.Color]::FromArgb([int]($c.A*$factor),$c.R,$c.G,$c.B))
            }
        }
    }
    $g.DrawImage($part,[System.Drawing.Rectangle]::new($dx,$dy,$dw,$dh))
    $part.Dispose()
}
foreach ($kind in @('move','look')) {
    $source = [System.Drawing.Bitmap]::new((Join-Path $assetDir "icon-iot-$kind.png"))
    $labels = [System.Drawing.Bitmap]::new((Join-Path $assetDir 'icon-iot-move.png'))
    $canvas = New-Canvas $source.Width $source.Height
    $g = New-Graphics $canvas
    switch ($kind) {
        'move' {
            Put-Part $g $source 300 0 815 510 92 8 1182 740 65
            Put-Part $g $source 324 669 89 106 385 707 76 90
            Put-Part $g $source 750 678 88 98 967 707 81 90
        }
        'look' {
            Put-Part $g $source 240 40 690 425 53 8 1069 658 45
            Put-Part $g $labels 324 669 89 106 225 641 67 80
            Put-Part $g $labels 750 678 88 98 789 641 72 80
        }
    }
    $canvas.Save((Join-Path $assetDir "icon-iot-$kind-fit-v2.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $canvas.Dispose(); $source.Dispose(); $labels.Dispose()
}
