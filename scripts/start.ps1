$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$outputExe = Join-Path $projectRoot 'dist\CodexCat.exe'

$runningPet = Get-Process -Name 'CodexCat' -ErrorAction SilentlyContinue | Where-Object {
    try { $_.Path -eq $outputExe } catch { $false }
} | Select-Object -First 1

if ($null -ne $runningPet) {
    # Starting the tiny second instance signals the existing process to show its hidden window.
    Start-Process -FilePath $outputExe -WorkingDirectory (Split-Path -Parent $outputExe)
    exit 0
}

& (Join-Path $PSScriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Start-Process -FilePath $outputExe -WorkingDirectory (Split-Path -Parent $outputExe)
