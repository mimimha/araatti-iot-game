. "$PSScriptRoot/render-core.ps1"
function Circle($cx,$cy,$r,$fill,$stroke='none',$sw=0,$alpha=255){
 $opacity=($alpha/255.0).ToString('0.###',[Globalization.CultureInfo]::InvariantCulture)
 $script:xml.Add("<circle cx='$cx' cy='$cy' r='$r' fill='$fill' stroke='$stroke' stroke-width='$sw' opacity='$opacity'/>")
 $p=[System.Drawing.Drawing2D.GraphicsPath]::new();$p.AddEllipse([single]($cx-$r),[single]($cy-$r),[single](2*$r),[single](2*$r))
 if($fill -ne 'none'){$c=Color $fill;$b=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb($alpha,$c));$script:g.FillPath($b,$p);$b.Dispose()}
 if($stroke -ne 'none'){$c=Color $stroke;$pen=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb($alpha,$c),$sw);$script:g.DrawPath($pen,$p);$pen.Dispose()};$p.Dispose()
}
StartAsset 'task-background' 512 512
Circle 256 256 242 '#121E26' none 0 235
EndAsset
StartAsset 'task-frame' 512 512
Circle 256 256 242 none '#080F16' 20
Circle 256 256 242 none '#40535F' 5
EndAsset
StartAsset 'task-progress-track' 512 512
Circle 256 256 216 none '#48545A' 32
EndAsset
StartAsset 'task-progress-fill' 512 512
Circle 256 256 216 none '#FFC62A' 32
EndAsset
StartAsset 'task-label' 512 128
Rect 5 5 502 118 59 '#14222B' '#080F16' 8
Line 67 13 445 13 '#475F6B' 3
EndAsset
StartAsset 'world-background' 512 512
Circle 256 256 224 '#173540' none 0 150
EndAsset
StartAsset 'world-ring' 512 512
Circle 256 256 224 none '#10232D' 18 170
Circle 256 256 224 none '#F2F7F3' 10
EndAsset
StartAsset 'world-focus-ring' 512 512
Circle 256 256 247 none '#FFC62A' 8
EndAsset
StartAsset 'icon-repair' 256 256
Poly '50,200 146,99 170,123 78,222 63,224 46,208' '#F0F3EF'
Poly '112,68 137,43 158,54 204,103 207,121 184,146 163,126 151,112 138,107 127,88' '#F4F6F2'
Line 65 201 121 143 '#B4C5C6' 5
EndAsset
StartAsset 'icon-sails' 256 256
Line 124 30 124 219 '#0A1117' 14
Line 124 30 124 219 '#F4F6F2' 5
Poly '111,45 46,180 112,168' '#F4F6F2'
Poly '140,56 205,180 141,168' '#F4F6F2'
Line 45 202 210 202 '#0A1117' 14
Line 45 202 210 202 '#F4F6F2' 5
EndAsset
StartAsset 'icon-helm' 256 256
for($i=0;$i -lt 8;$i++){
 $a=$i*[Math]::PI/4;$x1=128+26*[Math]::Cos($a);$y1=128+26*[Math]::Sin($a);$x2=128+101*[Math]::Cos($a);$y2=128+101*[Math]::Sin($a)
 Line $x1 $y1 $x2 $y2 '#0A1117' 20
 Line $x1 $y1 $x2 $y2 '#F4F6F2' 9
}
Circle 128 128 67 none '#0A1117' 23
Circle 128 128 67 none '#F4F6F2' 11
Circle 128 128 24 '#F4F6F2' '#0A1117' 9
Circle 128 128 7 '#526974' none 0
EndAsset
StartAsset 'icon-cannon' 256 256
Poly '50,153 165,113 183,154 83,188 59,183' '#ECEFEC'
Poly '56,151 51,125 174,80 190,119 78,163' '#F4F6F2'
Poly '164,76 182,69 204,116 187,126' '#C4D0CF'
Poly '62,184 157,154 174,177 76,211' '#B9C7C6'
Circle 75 200 24 '#F4F6F2' '#0A1117' 10
Circle 167 183 22 '#F4F6F2' '#0A1117' 10
Circle 75 200 8 '#263941'
Circle 167 183 8 '#263941'
EndAsset
