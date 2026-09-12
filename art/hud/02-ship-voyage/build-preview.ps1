Add-Type -AssemblyName System.Drawing
$ErrorActionPreference='Stop'
$bmp=[System.Drawing.Bitmap]::new(1400,560);$g=[System.Drawing.Graphics]::FromImage($bmp);$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#23414F'));$g.SmoothingMode='AntiAlias';$g.InterpolationMode='HighQualityBicubic'
function Draw($name,$x,$y,$w,$h){$i=[System.Drawing.Image]::FromFile((Join-Path $PSScriptRoot "$name.png"));$g.DrawImage($i,[System.Drawing.RectangleF]::new($x,$y,$w,$h));$i.Dispose()}
function Text($text,$x,$y,$size){$f=[System.Drawing.Font]::new('Arial',$size,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel);$g.DrawString($text,$f,[System.Drawing.Brushes]::White,$x,$y);$f.Dispose()}
Draw heading-label 535 14 220 66
Text 'SHIP HP' 575 29 32
Draw hp-track 190 84 1024 96
$state=$g.Save();$g.SetClip([System.Drawing.Rectangle]::new(202,96,750,72));Draw hp-fill-green 202 96 1000 72;$g.Restore($state)
Draw hp-frame 190 84 1024 96
Draw icon-ship 95 50 135 135
Text 'VOYAGE' 192 208 28
Draw voyage-track 190 270 1024 64
Draw voyage-delay-fill 555.6 286 292.8 32
Draw voyage-frame 190 270 1024 64
Draw voyage-reference-marker 832.4 246 32 112
Draw icon-ship 503.6 222 104 104
Draw icon-harbour 80 234 110 110
Draw icon-island 1220 226 125 125
Draw timer-label 1200 355 170 51
Text '3:42' 1248 363 32
Draw icon-speed 190 381 70 70
Draw speed-label 269 390 215 64.5
Text '8.5 kn' 324 405 30
Text '02 / SHIP + VOYAGE - SEPARATE PNG ASSETS' 190 503 22
$bmp.Save((Join-Path $PSScriptRoot 'preview.png'),[System.Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
