. "$PSScriptRoot/render-core.ps1"
StartAsset 'slot-question-mark' 200 300
$script:xml.Add("<path d='M 32 84 C 32 9 169 6 169 84 C 169 125 105 135 105 178 L 105 188' fill='none' stroke='#7F9094' stroke-width='44' stroke-linecap='round' stroke-linejoin='round'/><circle cx='105' cy='252' r='23' fill='#7F9094'/>")
$p=[System.Drawing.Drawing2D.GraphicsPath]::new();$p.AddBezier(32,84,32,9,169,6,169,84);$p.AddBezier(169,84,169,125,105,135,105,178);$p.AddLine(105,178,105,188)
$pen=[System.Drawing.Pen]::new((Color '#7F9094'),44);$pen.StartCap='Round';$pen.EndCap='Round';$pen.LineJoin='Round';$script:g.DrawPath($pen,$p)
$brush=[System.Drawing.SolidBrush]::new((Color '#7F9094'));$script:g.FillEllipse($brush,82,229,46,46);$brush.Dispose();$pen.Dispose();$p.Dispose();EndAsset
