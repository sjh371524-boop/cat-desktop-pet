$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot 'src\CodexCat'
$outputRoot = Join-Path $projectRoot 'dist'
$frameworkRoot = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $frameworkRoot 'csc.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'Windows C# compiler was not found. Enable .NET Framework 4.x.'
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$outputExe = Join-Path $outputRoot 'CodexCat.exe'

$references = @(
    (Join-Path $frameworkRoot 'System.dll'),
    (Join-Path $frameworkRoot 'System.Core.dll'),
    (Join-Path $frameworkRoot 'System.Xaml.dll'),
    (Join-Path $frameworkRoot 'System.Net.Http.dll'),
    (Join-Path $frameworkRoot 'System.Web.Extensions.dll'),
    (Join-Path $frameworkRoot 'System.Windows.Forms.dll'),
    (Join-Path $frameworkRoot 'System.Drawing.dll'),
    (Join-Path $frameworkRoot 'WPF\WindowsBase.dll'),
    (Join-Path $frameworkRoot 'WPF\PresentationCore.dll'),
    (Join-Path $frameworkRoot 'WPF\PresentationFramework.dll')
)

foreach ($reference in $references) {
    if (-not (Test-Path -LiteralPath $reference)) {
        throw "Missing compiler reference: $reference"
    }
}

$sourceFiles = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName
if (-not $sourceFiles) {
    throw "No C# sources were found under $sourceRoot."
}

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:x64',
    '/optimize+',
    '/warn:4',
    '/codepage:65001',
    "/out:$outputExe"
)
$arguments += $references | ForEach-Object { "/reference:$_" }
$arguments += $sourceFiles

& $compiler @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Copy-Item -LiteralPath (Join-Path $projectRoot 'config') -Destination $outputRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets') -Destination $outputRoot -Recurse -Force

Write-Host "Build completed: $outputExe" -ForegroundColor Green
