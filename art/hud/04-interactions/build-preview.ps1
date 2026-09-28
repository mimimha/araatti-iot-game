Add-Type -AssemblyName System.Drawing
$ErrorActionPreference='Stop'
$bmp=[System.Drawing.Bitmap]::new(1200,610);$g=[System.Drawing.Graphics]::FromImage($bmp);$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#294955'));$g.SmoothingMode='AntiAlias';$g.InterpolationMode='HighQualityBicubic'
function Draw($name,$x,$y,$w,$h){$im=[System.Drawing.Image]::FromFile((Join-Path $PSScriptRoot "$name.png"));$g.DrawImage($im,[System.Drawing.RectangleF]::new($x,$y,$w,$h));$im.Dispose()}
function Text($s,$x,$y,$size){$f=[System.Drawing.Font]::new('Arial',$size,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel);$g.DrawString($s,$f,[System.Drawing.Brushes]::White,$x,$y);$f.Dispose()}
Text '04 / TASK + WORLD INTERACTIONS' 40 28 27
Text 'CURRENT TASK' 70 107 23
$x=64;$y=154;$d=268
Draw task-background $x $y $d $d;Draw task-progress-track $x $y $d $d
$state=$g.Save();$pie=[System.Drawing.Drawing2D.GraphicsPath]::new();$pie.AddPie(($x-40),($y-40),($d+80),($d+80),-90,234);$g.SetClip($pie);Draw task-progress-fill $x $y $d $d;$g.Restore($state);$pie.Dispose()
Draw task-frame $x $y $d $d;Draw icon-repair ($x+$d*.25) ($y+$d*.25) ($d*.5) ($d*.5)
Draw task-label 64 438 268 67;Text 'REPAIRING' 124 457 23
Text 'WORLD BILLBOARDS' 420 107 23
$names=@('repair','sails','helm','cannon')
for($i=0;$i -lt 4;$i++){$wx=414+180*$i;Draw world-background $wx 196 146 146;Draw world-ring $wx 196 146 146;if($i -eq 0){Draw world-focus-ring $wx 196 146 146};Draw "icon-$($names[$i])" ($wx+31) 227 84 84;Text ($names[$i].ToUpper()) ($wx+25) 366 18}
Text 'ONE ICON SET / INDEPENDENT PROGRESS + FOCUS' 420 460 20
Text 'EXAMPLE: 65% - FULL RING SPRITE IS INCLUDED' 64 557 21
$bmp.Save((Join-Path $PSScriptRoot 'preview.png'),[System.Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
