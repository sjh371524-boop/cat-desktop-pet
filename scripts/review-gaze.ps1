$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
$snapshotRoot = Join-Path $projectRoot 'tests\snapshots'
New-Item -ItemType Directory -Force -Path $snapshotRoot | Out-Null
$canvas = New-Object System.Drawing.Bitmap(990,690)
$graphics = [System.Drawing.Graphics]::FromImage($canvas)
$graphics.Clear([System.Drawing.Color]::FromArgb(225,239,248))
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$font = New-Object System.Drawing.Font('Microsoft YaHei',11)
$names = @('up-left','up','up-right','left','center','right','down-left','down','down-right')
try {
    for ($i = 0; $i -lt $names.Count; $i++) {
        $path = Join-Path $snapshotRoot ('17-gaze-' + $names[$i] + '.png')
        $isPreview = Test-Path -LiteralPath $path
        if (-not $isPreview) { $path = Join-Path $projectRoot 'assets\character\stand\000.png' }
        $image = [System.Drawing.Image]::FromFile($path)
        try {
            $x = ($i % 3) * 330
            $y = [int][Math]::Floor($i / 3) * 230
            $source = if ($isPreview) { New-Object System.Drawing.RectangleF(121,17,117,117) } else { New-Object System.Drawing.RectangleF(101,13,104,104) }
            $destination = New-Object System.Drawing.RectangleF(($x+67),($y+28),195,195)
            $graphics.DrawImage($image,$destination,$source,[System.Drawing.GraphicsUnit]::Pixel)
            $graphics.DrawString($names[$i],$font,[System.Drawing.Brushes]::MidnightBlue,($x+10),($y+4))
        } finally { $image.Dispose() }
    }
} finally { $font.Dispose(); $graphics.Dispose() }
$canvas.Save((Join-Path $snapshotRoot 'gaze-review.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$canvas.Dispose()

# Magnify the original eye pixels for authoring/verification; never overwrite art.
$image = [System.Drawing.Image]::FromFile((Join-Path $projectRoot 'assets\character\stand\000.png'))
$eyeDetail = New-Object System.Drawing.Bitmap(660,280)
$graphics = [System.Drawing.Graphics]::FromImage($eyeDetail)
try {
    $graphics.Clear([System.Drawing.Color]::FromArgb(225,239,248))
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.DrawImage($image,(New-Object System.Drawing.RectangleF(0,0,660,280)),(New-Object System.Drawing.RectangleF(120,63,66,28)),[System.Drawing.GraphicsUnit]::Pixel)
} finally { $graphics.Dispose(); $image.Dispose() }
$eyeDetail.Save((Join-Path $snapshotRoot 'gaze-reference-detail.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$eyeDetail.Dispose()

$canvas = New-Object System.Drawing.Bitmap(1185,630)
$graphics = [System.Drawing.Graphics]::FromImage($canvas)
$graphics.Clear([System.Drawing.Color]::FromArgb(225,239,248))
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$font = New-Object System.Drawing.Font('Microsoft YaHei',11)
try {
    for ($i = 0; $i -lt $names.Count; $i++) {
        $path = Join-Path $snapshotRoot ('17-gaze-' + $names[$i] + '.png')
        if (-not (Test-Path -LiteralPath $path)) { continue }
        $image = [System.Drawing.Image]::FromFile($path)
        try {
            $x = ($i % 3) * 395
            $y = [int][Math]::Floor($i / 3) * 210
            $graphics.DrawImage($image,(New-Object System.Drawing.RectangleF($x,($y+30),395,180)),(New-Object System.Drawing.RectangleF(138,70,79,36)),[System.Drawing.GraphicsUnit]::Pixel)
            $graphics.DrawString($names[$i],$font,[System.Drawing.Brushes]::MidnightBlue,($x+10),($y+4))
        } finally { $image.Dispose() }
    }
} finally { $font.Dispose(); $graphics.Dispose() }
$canvas.Save((Join-Path $snapshotRoot 'gaze-eye-detail.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$canvas.Dispose()
