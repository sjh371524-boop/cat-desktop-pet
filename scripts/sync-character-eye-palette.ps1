param([switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'CharacterEyePalette.cs') -ReferencedAssemblies System.Drawing
[CharacterEyePalette]::Run($root, $VerifyOnly.IsPresent)
