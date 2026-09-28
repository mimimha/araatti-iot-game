Add-Type -AssemblyName System.Drawing
$ErrorActionPreference='Stop'
$script:out=$PSScriptRoot
function Color($hex){[System.Drawing.ColorTranslator]::FromHtml($hex)}
function RoundPath($x,$y,$w,$h,$r){
 $p=[System.Drawing.Drawing2D.GraphicsPath]::new();$d=2*$r
 $p.AddArc($x,$y,$d,$d,180,90);$p.AddArc(($x+$w-$d),$y,$d,$d,270,90);$p.AddArc(($x+$w-$d),($y+$h-$d),$d,$d,0,90);$p.AddArc($x,($y+$h-$d),$d,$d,90,90);$p.CloseFigure();return ,$p
}
function StartAsset($name,$w,$h){
 $script:name=$name;$script:b=[System.Drawing.Bitmap]::new($w,$h);$script:g=[System.Drawing.Graphics]::FromImage($script:b);$script:g.SmoothingMode='AntiAlias';$script:g.Clear([System.Drawing.Color]::Transparent)
 $script:xml=[System.Collections.Generic.List[string]]::new();$script:xml.Add("<svg xmlns='http://www.w3.org/2000/svg' width='$w' height='$h' viewBox='0 0 $w $h'>")
}
function EndAsset(){
 $script:xml.Add('</svg>');[IO.File]::WriteAllLines((Join-Path $script:out "$script:name.svg"),$script:xml)
 $script:b.Save((Join-Path $script:out "$script:name.png"),[System.Drawing.Imaging.ImageFormat]::Png);$script:g.Dispose();$script:b.Dispose()
}
function Paint($path,$fill,$stroke,$sw){
 if($fill -ne 'none'){$brush=[System.Drawing.SolidBrush]::new((Color $fill));$script:g.FillPath($brush,$path);$brush.Dispose()}
 if($stroke -ne 'none'){$pen=[System.Drawing.Pen]::new((Color $stroke),$sw);$pen.LineJoin='Round';$script:g.DrawPath($pen,$path);$pen.Dispose()};$path.Dispose()
}
function Rect($x,$y,$w,$h,$r,$fill,$stroke='none',$sw=0){
 $script:xml.Add("<rect x='$x' y='$y' width='$w' height='$h' rx='$r' fill='$fill' stroke='$stroke' stroke-width='$sw'/>")
 if($r -gt 0){$p=RoundPath $x $y $w $h $r}else{$p=[System.Drawing.Drawing2D.GraphicsPath]::new();$p.AddRectangle([System.Drawing.RectangleF]::new($x,$y,$w,$h))};Paint $p $fill $stroke $sw
}
function Poly($points,$fill,$stroke='#0A1117',$sw=12){
 $script:xml.Add("<polygon points='$points' fill='$fill' stroke='$stroke' stroke-width='$sw' stroke-linejoin='round'/>")
 $pts=[System.Drawing.PointF[]]@($points.Split(' ')|ForEach-Object{$xy=$_.Split(',');[System.Drawing.PointF]::new([single]$xy[0],[single]$xy[1])})
 $p=[System.Drawing.Drawing2D.GraphicsPath]::new();$p.AddPolygon($pts);Paint $p $fill $stroke $sw
}
function Line($x1,$y1,$x2,$y2,$stroke,$sw){
 $script:xml.Add("<path d='M $x1 $y1 L $x2 $y2' fill='none' stroke='$stroke' stroke-width='$sw' stroke-linecap='round'/>")
 $pen=[System.Drawing.Pen]::new((Color $stroke),$sw);$pen.StartCap='Round';$pen.EndCap='Round';$script:g.DrawLine($pen,[single]$x1,[single]$y1,[single]$x2,[single]$y2);$pen.Dispose()
}
function Gradient($w,$h,$r,$top,$bottom){
 $script:xml.Add("<defs><linearGradient id='fill' x1='0' y1='0' x2='0' y2='1'><stop stop-color='$top'/><stop offset='1' stop-color='$bottom'/></linearGradient></defs><rect width='$w' height='$h' rx='$r' fill='url(#fill)'/>")
 if($r -gt 0){$p=RoundPath 0 0 $w $h $r}else{$p=[System.Drawing.Drawing2D.GraphicsPath]::new();$p.AddRectangle([System.Drawing.RectangleF]::new(0,0,$w,$h))}
 $brush=[System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new(0,0,$w,$h),(Color $top),(Color $bottom),[single]90)
 if($r -eq 0){$saved=$script:g.SmoothingMode;$script:g.SmoothingMode='None';$script:g.FillRectangle($brush,0,0,$w,$h);$script:g.SmoothingMode=$saved}else{$script:g.FillPath($brush,$p)}
 $brush.Dispose();$p.Dispose()
}
StartAsset 'hp-frame' 1024 96
Rect 12 12 1000 72 36 none '#070D12' 24
Rect 12 12 1000 72 36 none '#69757B' 10
Rect 12 12 1000 72 36 none '#BCC7C9' 3
EndAsset
StartAsset 'hp-track' 1024 96
Rect 12 12 1000 72 36 '#10191E'
EndAsset
StartAsset 'hp-fill-green' 1000 72
Gradient 1000 72 36 '#8EF525' '#3CBA06'
Line 38 9 962 9 '#C6FF6C' 3
EndAsset
StartAsset 'hp-mask' 1000 72
Rect 0 0 1000 72 36 '#FFFFFF'
EndAsset
StartAsset 'voyage-frame' 1024 64
Rect 8 8 1008 48 24 none '#080E14' 16
Rect 8 8 1008 48 24 none '#839299' 6
EndAsset
StartAsset 'voyage-track' 1024 64
Rect 8 8 1008 48 24 '#29343B'
EndAsset
StartAsset 'voyage-delay-fill' 32 32
Gradient 32 32 0 '#FF6146' '#ED291F'
EndAsset
StartAsset 'voyage-reference-marker' 32 112
foreach($y in @(4,40,76)){Rect 8 $y 16 26 4 '#FFC637' '#1B2021' 4}
EndAsset
StartAsset 'icon-ship' 256 256
Line 125 37 125 174 '#0A1117' 18
Line 125 37 125 174 '#F7F3E8' 7
Poly '136,43 192,54 136,72' '#F7F3E8' '#0A1117' 10
Poly '112,65 58,148 112,148' '#F7F3E8' '#0A1117' 12
Poly '139,72 139,153 199,153' '#F7F3E8' '#0A1117' 12
Poly '32,164 72,176 193,176 226,157 218,193 195,216 72,216 48,196' '#F7F3E8' '#0A1117' 14
Poly '55,178 79,185 190,185 210,175 204,190 188,202 78,202 62,193' '#D7E4E2' none 0
Line 79 216 192 216 '#0A1117' 9
EndAsset
StartAsset 'icon-harbour' 256 256
Poly '32,205 224,205 216,230 40,230' '#F5F7F3'
Rect 47 132 73 67 4 '#F5F7F3' '#0A1117' 12
Poly '36,132 83,87 131,132' '#F5F7F3'
Rect 150 57 45 144 4 '#F5F7F3' '#0A1117' 12
Poly '139,57 173,28 207,57' '#F5F7F3'
Rect 162 79 21 27 2 '#0A1117'
Rect 74 162 21 37 2 '#0A1117'
Line 139 130 204 130 '#0A1117' 10
EndAsset
StartAsset 'icon-island' 256 256
Poly '29,209 71,181 110,177 148,185 186,188 229,214 217,229 40,229' '#F5F7F3'
Poly '109,182 132,110 155,75 166,80 148,119 137,186' '#F5F7F3'
Poly '155,78 129,45 103,39 81,54 117,59 145,87 100,75 71,83 61,110 103,94 145,95 158,112 183,125 171,91 209,105 225,91 206,69 177,65 194,44 177,29 156,47' '#F5F7F3'
EndAsset
StartAsset 'icon-speed' 256 256
Poly '82,49 220,127 82,205 105,144 35,144 35,110 105,110' '#F5F7F3'
Line 26 64 64 64 '#0A1117' 15
Line 26 64 64 64 '#F5F7F3' 6
Line 26 190 64 190 '#0A1117' 15
Line 26 190 64 190 '#F5F7F3' 6
EndAsset
foreach($name in @('timer-label','speed-label','heading-label')){
 StartAsset $name 320 96
 Rect 5 5 310 86 43 '#131F27' '#080E14' 8
 Line 49 12 271 12 '#455862' 3
 EndAsset
}
Get-ChildItem -LiteralPath $script:out -Filter '*.png' | Select-Object Name,Length
