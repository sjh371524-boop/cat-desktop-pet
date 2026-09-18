$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$uiRoot = Join-Path $projectRoot 'assets\ui'
$processor = Join-Path $PSScriptRoot 'prepare-chroma-sprite-sheet.ps1'

& $processor `
    -InputPath (Join-Path $uiRoot 'soothing-hand-sheet-v2-chroma.png') `
    -OutputDirectory (Join-Path $uiRoot 'soothing_hand') `
    -KeepLargestComponent

Write-Host "Prepared soothing hand frames. Lowering reuses the existing sleep transition." -ForegroundColor Green
