$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
$snapshotRoot = Join-Path $projectRoot 'tests\snapshots'
$items = @(
    @('18-feed-right-0120','fish descends'),
    @('18-feed-right-0250','direct descent'),
    @('18-feed-right-0350','neutral cat'),
    @('18-feed-right-0500','mouth opens only'),
    @('18-feed-right-0550','line breaks at mouth'),
    @('18-feed-right-0650','ordinary eating'),
    @('18-feed-right-0760','last tail passes lips'),
    @('18-feed-right-0810','lips closing'),
    @('18-feed-right-0910','settling'),
    @('18-feed-left-0350','same path from left bubble')
)
$canvas = New-Object System.Drawing.Bitmap(1500,660)
$graphics = [System.Drawing.Graphics]::FromImage($canvas)
$font = New-Object System.Drawing.Font('Microsoft YaHei',14,[System.Drawing.FontStyle]::Bold)
try {
    $graphics.Clear([System.Drawing.Color]::FromArgb(220,235,245))
    for ($i = 0; $i -lt $items.Count; $i++) {
        $image = [System.Drawing.Image]::FromFile((Join-Path $snapshotRoot ($items[$i][0] + '.png')))
        try {
            $x = ($i % 5) * 300
            $y = [int][Math]::Floor($i / 5) * 330
            $graphics.DrawString($items[$i][1],$font,[System.Drawing.Brushes]::MidnightBlue,($x+10),($y+3))
            $graphics.DrawImageUnscaled($image,$x,($y+30))
        } finally { $image.Dispose() }
    }
} finally {
    $font.Dispose()
    $graphics.Dispose()
}
$outputPath = Join-Path $snapshotRoot 'feeding-review.png'
$canvas.Save($outputPath,[System.Drawing.Imaging.ImageFormat]::Png)
$canvas.Dispose()
Write-Output $outputPath
