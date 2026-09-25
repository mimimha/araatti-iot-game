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
foreach ($kind in @('move','look','jump')) {
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
        'jump' {
            foreach ($stroke in @(@(36,50,255,183,0),@(20,200,255,178,0),@(10,255,255,224,72),@(4,255,255,251,206))) {
                $pen=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb($stroke[1],$stroke[2],$stroke[3],$stroke[4]),[single]$stroke[0])
                $g.DrawLine($pen,400,315,1380,315)
                $pen.Dispose()
            }
            for($y=175;$y -lt 231;$y++) {
                for($x=765;$x -lt 845;$x++) { $source.SetPixel($x,$y,[System.Drawing.Color]::Transparent) }
            }
            Put-Part $g $source 520 10 325 320 80 8 508 500 45
            $button = $source.Clone([System.Drawing.Rectangle]::new(975,115,310,310),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            for ($y=0; $y -lt $button.Height; $y++) {
                for ($x=0; $x -lt $button.Width; $x++) {
                    $distance=[Math]::Sqrt([Math]::Pow(($x-155)/154.0,2)+[Math]::Pow(($y-155)/154.0,2))
                    if ($distance -gt 0.99) {
                        $c=$button.GetPixel($x,$y)
                        $a=[int]($c.A*[Math]::Max(0,[Math]::Min(1,(1.005-$distance)/0.015)))
                        $button.SetPixel($x,$y,[System.Drawing.Color]::FromArgb($a,$c.R,$c.G,$c.B))
                    }
                }
            }
            $g.DrawImage($button,[System.Drawing.Rectangle]::new(1280,15,494,494))
            $button.Dispose()
            Put-Part $g $labels 750 678 82 98 635 351 100 120
        }
    }
    $canvas.Save((Join-Path $assetDir "icon-iot-$kind-fit-v2.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $canvas.Dispose(); $source.Dispose(); $labels.Dispose()
}
