. "$PSScriptRoot/render-core.ps1"
function AlphaRect($x,$y,$w,$h,$r,$hex,$alpha,$strokeWidth=0){
 $opacity=($alpha/255.0).ToString('0.###',[Globalization.CultureInfo]::InvariantCulture)
 $p=RoundPath $x $y $w $h $r;$c=Color $hex;$c=[System.Drawing.Color]::FromArgb($alpha,$c.R,$c.G,$c.B)
 if($strokeWidth -gt 0){$script:xml.Add("<rect x='$x' y='$y' width='$w' height='$h' rx='$r' fill='none' stroke='$hex' stroke-opacity='$opacity' stroke-width='$strokeWidth'/>");$pen=[System.Drawing.Pen]::new($c,$strokeWidth);$script:g.DrawPath($pen,$p);$pen.Dispose()}
 else{$script:xml.Add("<rect x='$x' y='$y' width='$w' height='$h' rx='$r' fill='$hex' fill-opacity='$opacity'/>");$brush=[System.Drawing.SolidBrush]::new($c);$script:g.FillPath($brush,$p);$brush.Dispose()};$p.Dispose()
}
function Ellipse($x,$y,$w,$h,$fill,$stroke='#0A1117',$sw=10){
 $script:xml.Add("<ellipse cx='$($x+$w/2)' cy='$($y+$h/2)' rx='$($w/2)' ry='$($h/2)' fill='$fill' stroke='$stroke' stroke-width='$sw'/>")
 $p=[System.Drawing.Drawing2D.GraphicsPath]::new();$p.AddEllipse([single]$x,[single]$y,[single]$w,[single]$h);Paint $p $fill $stroke $sw
}
StartAsset 'alert-background' 960 240
AlphaRect 12 12 936 216 28 '#10202B' 224
EndAsset
StartAsset 'alert-frame' 960 240
Rect 12 12 936 216 28 none '#080F15' 8
AlphaRect 12 12 936 216 28 '#82939B' 120 3
EndAsset
StartAsset 'alert-accent-red' 24 184
Rect 2 2 20 180 10 '#F14A46'
EndAsset
StartAsset 'alert-new-highlight' 960 240
foreach($layer in @(@(20,16),@(14,24),@(8,48),@(3,200))){AlphaRect 12 12 936 216 28 '#FFA34A' $layer[1] $layer[0]}
EndAsset
StartAsset 'alert-icon-slot' 192 192
AlphaRect 4 4 184 184 20 '#070E15' 150
EndAsset
StartAsset 'alert-countdown-track' 680 16
Rect 0 0 680 16 8 '#617078'
EndAsset
StartAsset 'alert-countdown-fill' 680 16
Gradient 680 16 8 '#FFCF3D' '#F99A0A'
EndAsset
StartAsset 'icon-hull-breach' 256 256
Poly '38,187 70,211 188,66 158,44 129,79 123,104 101,108' '#B87A3C'
Poly '55,125 77,144 94,132 106,153 128,127 118,107 154,65 131,43' '#E7AB5D'
Poly '89,208 119,221 148,177 153,149 171,148 207,91 182,73 162,113 136,117 141,143' '#D7974E'
Line 68 179 108 133 '#6D452A' 6
Line 130 187 151 161 '#80522D' 6
Poly '191,149 177,179 181,194 195,200 209,194 212,181' '#36CDF7' '#092A3B' 8
Poly '60,48 49,70 53,82 66,83 75,73' '#36CDF7' '#092A3B' 7
Line 202 127 224 114 '#36CDF7' 10
EndAsset
StartAsset 'icon-sail-torn' 256 256
Line 69 30 69 227 '#0A1117' 16
Line 69 30 69 227 '#C39459' 6
Poly '82,40 170,48 183,92 163,110 187,111 180,157 146,179 109,176 122,145 102,159 89,135' '#F4ECD5'
Poly '175,170 155,198 90,201 86,177 108,186 146,189' '#DDD5BE'
Line 117 76 133 102 '#ACA58F' 5
Line 133 102 120 125 '#0A1117' 8
Line 120 125 143 122 '#0A1117' 8
Line 143 122 137 144 '#0A1117' 8
EndAsset
StartAsset 'icon-cannon-jam' 256 256
Poly '50,153 165,113 183,154 83,188 59,183' '#ECEFEC'
Poly '56,151 51,125 174,80 190,119 78,163' '#F4F6F2'
Poly '164,76 182,69 204,116 187,126' '#C4D0CF'
Poly '62,184 157,154 174,177 76,211' '#B9C7C6'
Ellipse 50 176 49 49 '#F4F6F2'
Ellipse 145 161 44 44 '#F4F6F2'
Ellipse 66 192 17 17 '#263941' none 0
Ellipse 159 175 16 16 '#263941' none 0
Poly '194,33 186,54 207,57 200,78 230,51 213,48 225,29' '#FFBC35' '#111B20' 7
EndAsset
