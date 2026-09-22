Add-Type -AssemblyName System.Drawing

$assetDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePanel = 'C:\Users\SSAFY\.codex\generated_images\01a0beed-d9ed-7d31-bf4f-88b41a75b886\exec-5421ecc4-683a-4390-a187-7116fade12f5.png'
$sourceFill = 'C:\Users\SSAFY\.codex\generated_images\01a0beed-d9ed-7d31-bf4f-88b41a75b886\exec-2ef5cf13-5816-4e16-a2a6-932ccd34373d.png'

function Get-AlphaBounds([System.Drawing.Bitmap]$bitmap) {
    $left = $bitmap.Width
    $top = $bitmap.Height
    $right = -1
    $bottom = -1

    for ($y = 0; $y -lt $bitmap.Height; $y++) {
        for ($x = 0; $x -lt $bitmap.Width; $x++) {
            if ($bitmap.GetPixel($x, $y).A -gt 4) {
                if ($x -lt $left) { $left = $x }
                if ($x -gt $right) { $right = $x }
                if ($y -lt $top) { $top = $y }
                if ($y -gt $bottom) { $bottom = $y }
            }
        }
    }

    return [System.Drawing.Rectangle]::FromLTRB($left, $top, $right + 1, $bottom + 1)
}

function Export-TransparentSprite($source, $destination, $width, $height, $padding) {
    $inputBitmap = [System.Drawing.Bitmap]::FromFile($source)
    try {
        $bounds = Get-AlphaBounds $inputBitmap
        $outputBitmap = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($outputBitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

                $target = [System.Drawing.Rectangle]::new(
                    [int]$padding,
                    [int]$padding,
                    [int]($width - 2 * $padding),
                    [int]($height - 2 * $padding)
                )
                $graphics.DrawImage($inputBitmap, $target, $bounds, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally { $graphics.Dispose() }
            $outputBitmap.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $outputBitmap.Dispose() }
    }
    finally { $inputBitmap.Dispose() }
}

Export-TransparentSprite $sourcePanel (Join-Path $assetDirectory 'island-restoration-panel-anchor-4k.png') 4096 568 16
Export-TransparentSprite $sourceFill (Join-Path $assetDirectory 'island-restoration-progress-fill-4k.png') 4096 512 12
