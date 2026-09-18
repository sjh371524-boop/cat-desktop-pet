$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$names = @('01-stand','07ea-soothe-ear-start-right','07eb-soothe-ear-start-left','13-tail-right-09','13-tail-left-09','02ba-drag-left-blue-eye')
$canvas = New-Object System.Drawing.Bitmap(900,600)
$g = [System.Drawing.Graphics]::FromImage($canvas)
$g.Clear([System.Drawing.Color]::FromArgb(220,235,245))
for($i=0;$i -lt $names.Count;$i++) {
    $img = [System.Drawing.Image]::FromFile((Join-Path $root ('tests\snapshots\'+$names[$i]+'.png')))
    $g.DrawImageUnscaled($img,($i%3)*300,[int]([Math]::Floor($i/3)*300))
    $img.Dispose()
}
$g.Dispose()
$canvas.Save((Join-Path $root 'tests\snapshots\interaction-review.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$canvas.Dispose()
