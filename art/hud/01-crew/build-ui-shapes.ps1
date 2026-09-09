Add-Type -AssemblyName System.Drawing
$outDir = $PSScriptRoot
function RoundedPath([single]$x,[single]$y,[single]$w,[single]$h,[single]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = $r * 2
  $p.AddArc($x,$y,$d,$d,180,90)
  $p.AddArc(($x+$w-$d),$y,$d,$d,270,90)
  $p.AddArc(($x+$w-$d),($y+$h-$d),$d,$d,0,90)
  $p.AddArc($x,($y+$h-$d),$d,$d,90,90)
  $p.CloseFigure()
  return ,$p
}
function RenderShape($name,$width,$height,$svg,$paint) {
  [IO.File]::WriteAllText((Join-Path $outDir "$name.svg"),$svg)
  $bmp = New-Object System.Drawing.Bitmap($width,$height)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear([System.Drawing.Color]::Transparent)
  & $paint $g
  $bmp.Save((Join-Path $outDir "$name.png"),[System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
}
$colors = @{green='#51D82A';red='#F0443E';yellow='#FFC52E';purple='#A85BEB'}
foreach($colorName in @('green','red','yellow','purple')) {
  $c = $colors[$colorName]
  $svg = "<svg xmlns='http://www.w3.org/2000/svg' width='512' height='512' viewBox='0 0 512 512'><rect x='27' y='27' width='458' height='458' rx='88' fill='none' stroke='#080E12' stroke-width='42'/><rect x='27' y='27' width='458' height='458' rx='88' fill='none' stroke='$c' stroke-width='24'/><rect x='27' y='27' width='458' height='458' rx='88' fill='none' stroke='#FFFFFF' stroke-opacity='.3' stroke-width='3'/></svg>"
  RenderShape "frame-$colorName" 512 512 $svg {
    param($g)
    $p = RoundedPath 27 27 458 458 88
    foreach($spec in @(@('#080E12',42),@($c,24),@('#B7D3DB',3))) {
      $pen = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml($spec[0]),[single]$spec[1])
      if($spec[1] -eq 3) {$pen.Color=[System.Drawing.Color]::FromArgb(76,255,255,255)}
      $g.DrawPath($pen,$p); $pen.Dispose()
    }
    $p.Dispose()
  }
}
RenderShape 'portrait-backplate' 512 512 "<svg xmlns='http://www.w3.org/2000/svg' width='512' height='512'><defs><linearGradient id='g' x2='0' y2='1'><stop stop-color='#34434C'/><stop offset='1' stop-color='#111B22'/></linearGradient></defs><rect x='27' y='27' width='458' height='458' rx='88' fill='url(#g)'/></svg>" {
  param($g)
  $p=RoundedPath 27 27 458 458 88
  $brush=New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(27,27,458,458)),[System.Drawing.ColorTranslator]::FromHtml('#34434C'),[System.Drawing.ColorTranslator]::FromHtml('#111B22'),90)
  $g.FillPath($brush,$p); $brush.Dispose(); $p.Dispose()
}
RenderShape 'role-label' 512 144 "<svg xmlns='http://www.w3.org/2000/svg' width='512' height='144'><rect x='6' y='6' width='500' height='132' rx='66' fill='#111B22' fill-opacity='.94' stroke='#080E12' stroke-width='8'/><path d='M 70 15 H 442' stroke='#65757E' stroke-opacity='.55' stroke-width='3'/></svg>" {
  param($g)
  $p=RoundedPath 6 6 500 132 66
  $b=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(240,17,27,34))
  $pen=New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml('#080E12'),8)
  $g.FillPath($b,$p); $g.DrawPath($pen,$p)
  $light=New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(140,101,117,126),3)
  $g.DrawLine($light,70,15,442,15)
  $p.Dispose(); $b.Dispose(); $pen.Dispose(); $light.Dispose()
}
