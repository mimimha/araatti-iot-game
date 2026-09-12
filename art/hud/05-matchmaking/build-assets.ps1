. "$PSScriptRoot/render-core.ps1"
function AlphaRect($x,$y,$w,$h,$r,$hex,$alpha,$sw=0){
 $opacity=($alpha/255.0).ToString('0.###',[Globalization.CultureInfo]::InvariantCulture)
 $p=RoundPath $x $y $w $h $r;$c=[System.Drawing.Color]::FromArgb($alpha,(Color $hex))
 if($sw -gt 0){$script:xml.Add("<rect x='$x' y='$y' width='$w' height='$h' rx='$r' fill='none' stroke='$hex' stroke-opacity='$opacity' stroke-width='$sw'/>");$pen=[System.Drawing.Pen]::new($c,$sw);$script:g.DrawPath($pen,$p);$pen.Dispose()}
 else{$script:xml.Add("<rect x='$x' y='$y' width='$w' height='$h' rx='$r' fill='$hex' fill-opacity='$opacity'/>");$b=[System.Drawing.SolidBrush]::new($c);$script:g.FillPath($b,$p);$b.Dispose()};$p.Dispose()
}
StartAsset 'slot-occupied-background' 320 480
Rect 8 8 304 464 32 '#F0E1BC'
Rect 16 16 288 448 25 none '#D3B67C' 2
EndAsset
StartAsset 'slot-empty-background' 320 480
AlphaRect 8 8 304 464 32 '#193A4D' 180
EndAsset
StartAsset 'slot-frame' 320 480
Rect 8 8 304 464 32 none '#889A9B' 5
Rect 15 15 290 450 25 none '#47606B' 2
EndAsset
StartAsset 'slot-local-highlight' 320 480
AlphaRect 8 8 304 464 32 '#FFC442' 35 15
Rect 8 8 304 464 32 none '#FFC33B' 6
Rect 15 15 290 450 25 none '#FFF1BD' 3
EndAsset
StartAsset 'slot-nameplate-light' 288 76
Rect 3 3 282 70 18 '#F4E6C5' '#CCAA6E' 3
Line 26 9 262 9 '#FFFAE6' 2
EndAsset
StartAsset 'slot-nameplate-dark' 288 76
AlphaRect 3 3 282 70 18 '#132E3E' 225
Rect 3 3 282 70 18 none '#42606C' 2
EndAsset
foreach($state in @('normal','pressed','disabled')){
 StartAsset "match-button-$state" 512 112
 if($state -eq 'normal'){Gradient 512 112 35 '#FFD469' '#E69B21';Rect 4 4 504 104 31 none '#FBE7AD' 4;Line 34 11 478 11 '#FFF0BE' 3}
 if($state -eq 'pressed'){Gradient 512 112 35 '#C58A27' '#D99F36';Rect 4 4 504 104 31 none '#DDB967' 4}
 if($state -eq 'disabled'){Gradient 512 112 35 '#75817D' '#566765';Rect 4 4 504 104 31 none '#A0ACA3' 4}
 EndAsset
}
StartAsset 'loading-spinner' 256 256
for($i=0;$i -lt 10;$i++){
 $a=($i*36-90)*[Math]::PI/180;$cx=128+89*[Math]::Cos($a);$cy=128+89*[Math]::Sin($a);$alpha=45+21*$i
 $opacity=($alpha/255.0).ToString('0.###',[Globalization.CultureInfo]::InvariantCulture)
 $sx=$cx.ToString('0.###',[Globalization.CultureInfo]::InvariantCulture);$sy=$cy.ToString('0.###',[Globalization.CultureInfo]::InvariantCulture)
 $script:xml.Add("<circle cx='$sx' cy='$sy' r='14' fill='#F5E3AC' opacity='$opacity'/>")
 $dotBrush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb($alpha,(Color '#F5E3AC')));$script:g.FillEllipse($dotBrush,[single]($cx-14),[single]($cy-14),[single]28,[single]28);$dotBrush.Dispose()
}
EndAsset
StartAsset 'notice-divider' 512 32
for($x=8;$x -lt 198;$x+=28){Line $x 16 ($x+14) 16 '#AA915D' 2}
for($x=308;$x -lt 490;$x+=28){Line $x 16 ($x+14) 16 '#AA915D' 2}
Poly '256,2 261,11 274,16 261,21 256,30 251,21 238,16 251,11' '#E2BB6C' none 0
EndAsset
