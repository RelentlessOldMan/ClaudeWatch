# Builds ClaudeWatch as a self-contained Native AOT executable into dist\.
# Usage: .\build.ps1  [-Configuration Release] [-Rid win-x64]

[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Rid = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'

# Native AOT linking shells out to vswhere.exe to locate the MSVC toolset; make sure
# it is reachable even when this script runs outside a Developer prompt.
$vsInstaller = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer'
if ((Test-Path $vsInstaller) -and ($env:PATH -notlike "*$vsInstaller*")) {
    $env:PATH = "$vsInstaller;$env:PATH"
}

Write-Host "Publishing ClaudeWatch (Native AOT, $Rid, $Configuration)..." -ForegroundColor Cyan

dotnet publish (Join-Path $root 'ClaudeWatch.csproj') `
    -c $Configuration `
    -r $Rid `
    --self-contained `
    -o $dist

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

$exe = Join-Path $dist 'claudewatch.exe'
if (Test-Path $exe) {
    $size = '{0:N1} MB' -f ((Get-Item $exe).Length / 1MB)
    Write-Host "Built $exe ($size)" -ForegroundColor Green
} else {
    Write-Warning "Publish reported success but $exe was not found."
}
