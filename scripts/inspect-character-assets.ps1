param([string]$Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$files = @(Get-ChildItem -LiteralPath (Join-Path $Root 'assets\character') -Recurse -Filter '*.png' -File | Sort-Object FullName | Where-Object { $_.Name -notmatch 'sheet' })
$out = Join-Path $Root 'tests\asset-audit'
New-Item -ItemType Directory -Force -Path $out | Out-Null
for ($page = 0; $page * 24 -lt $files.Count; $page++) {
    $bmp = New-Object System.Drawing.Bitmap(1536,1120)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(205,219,228))
    $font = New-Object System.Drawing.Font('Consolas',10)
    for ($slot = 0; $slot -lt 24 -and $page * 24 + $slot -lt $files.Count; $slot++) {
        $id = $page * 24 + $slot
        $file = $files[$id]
        $img = [System.Drawing.Image]::FromFile($file.FullName)
        $x = ($slot % 6) * 256
        $y = [Math]::Floor($slot / 6) * 280
        $g.DrawImage($img, [int]$x, [int]($y+24), 256,256)
        $name = $file.FullName.Substring((Join-Path $Root 'assets\character').Length+1)
        $g.DrawString("$id $name",$font,[System.Drawing.Brushes]::Black,[single]$x,[single]$y)
        $img.Dispose()
    }
    $g.Dispose()
    $font.Dispose()
    $bmp.Save((Join-Path $out "page-$page.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
for ($id=0;$id -lt $files.Count;$id++) { "$id $($files[$id].FullName.Substring((Join-Path $Root 'assets\character').Length+1))" }
