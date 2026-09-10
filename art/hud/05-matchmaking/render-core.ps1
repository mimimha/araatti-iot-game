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
