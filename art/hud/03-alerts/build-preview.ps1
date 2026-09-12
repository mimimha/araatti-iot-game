Add-Type -AssemblyName System.Drawing
$ErrorActionPreference='Stop'
$bmp=[System.Drawing.Bitmap]::new(1120,650);$g=[System.Drawing.Graphics]::FromImage($bmp);$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#254553'));$g.SmoothingMode='AntiAlias';$g.InterpolationMode='HighQualityBicubic'
function Draw($name,$x,$y,$w,$h){$im=[System.Drawing.Image]::FromFile((Join-Path $PSScriptRoot "$name.png"));$g.DrawImage($im,[System.Drawing.RectangleF]::new($x,$y,$w,$h));$im.Dispose()}
function Text($s,$x,$y,$size,$hex='#F3EFDB'){$f=[System.Drawing.Font]::new('Arial',$size,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel);$b=[System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($hex));$g.DrawString($s,$f,$b,$x,$y);$b.Dispose();$f.Dispose()}
Text '03 / INCIDENT ALERTS' 36 24 26
$icons=@('icon-hull-breach','icon-sail-torn','icon-cannon-jam');$titles=@('HULL BREACH','SAIL TORN','CANNON JAM');$details=@('Repair the hull','Patch the sail','Clear the barrel');$ratios=@(.72,.53,.35)
for($i=0;$i -lt 3;$i++){
 $x=32;$y=84+$i*175;$s=.66
 Draw alert-background $x $y (960*$s) (240*$s);Draw alert-frame $x $y (960*$s) (240*$s)
 Draw alert-accent-red ($x+24*$s) ($y+28*$s) (24*$s) (184*$s)
 Draw alert-icon-slot ($x+60*$s) ($y+24*$s) (192*$s) (192*$s)
 Draw $icons[$i] ($x+76*$s) ($y+40*$s) (160*$s) (160*$s)
 Text $titles[$i] ($x+280*$s) ($y+34*$s) 27
 Text $details[$i] ($x+280*$s) ($y+105*$s) 21 '#CBDADD'
 Draw alert-countdown-track ($x+280*$s) ($y+188*$s) (640*$s) (16*$s)
 $state=$g.Save();$g.SetClip([System.Drawing.RectangleF]::new(($x+280*$s),($y+188*$s),(640*$s*$ratios[$i]),(16*$s)));Draw alert-countdown-fill ($x+280*$s) ($y+188*$s) (640*$s) (16*$s);$g.Restore($state)
 if($i -eq 0){Draw alert-new-highlight $x $y (960*$s) (240*$s)}
}
Text 'SWAPPABLE ICONS' 728 86 23
for($i=0;$i -lt 3;$i++){Draw $icons[$i] 756 (126+$i*137) 116 116}
Text 'NO BAKED TEXT / TIMERS' 704 571 19 '#B6CDD5'
$bmp.Save((Join-Path $PSScriptRoot 'preview.png'),[System.Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
