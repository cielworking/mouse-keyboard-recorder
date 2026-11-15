[CmdletBinding()]
param(
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

if (-not $OutputPath) {
    $OutputPath = Join-Path $PSScriptRoot 'dist\SimpleRecorder.exe'
}

$source = Join-Path $PSScriptRoot 'Recorder.cs'
if (-not (Test-Path -LiteralPath $source)) {
    throw "Source file not found: $source"
}

$candidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)

$csc = $null
foreach ($candidate in $candidates) {
    if (Test-Path -LiteralPath $candidate) {
        $csc = $candidate
        break
    }
}

if (-not $csc) {
    throw 'C# compiler not found. Install the .NET Framework 4.x or run the csc.exe command from the README.'
}

$outputDir = Split-Path -Parent $OutputPath
if ($outputDir -and -not (Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

Write-Host "Compiler: $csc"
Write-Host "Source:   $source"
Write-Host "Output:   $OutputPath"

& $csc /nologo /target:winexe /platform:anycpu /optimize+ "/out:$OutputPath" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll $source

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

$file = Get-Item -LiteralPath $OutputPath
Write-Host ("Built {0} ({1} bytes)" -f $file.FullName, $file.Length)
