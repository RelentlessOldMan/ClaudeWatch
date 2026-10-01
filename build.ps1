# Builds ClaudeWatch as a self-contained Native AOT executable into dist\.
# Usage: .\build.ps1  [-Configuration Release] [-Rid win-x64]
#
# Publishes to a staging dir and then swaps the exe into dist\, so the build works
# even while Claude Code is running the current dist\claudewatch.exe as its status
# line (a running exe can't be overwritten on Windows, but it can be renamed aside).

[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Rid = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root  = $PSScriptRoot
$dist  = Join-Path $root 'dist'
$stage = Join-Path $root 'obj\stage'

# Native AOT linking shells out to vswhere.exe to locate the MSVC toolset; make sure
# it is reachable even when this script runs outside a Developer prompt.
$vsInstaller = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer'
if ((Test-Path $vsInstaller) -and ($env:PATH -notlike "*$vsInstaller*")) {
    $env:PATH = "$vsInstaller;$env:PATH"
}

Write-Host "Publishing ClaudeWatch (Native AOT, $Rid, $Configuration)..." -ForegroundColor Cyan

# Publish to staging (never locked), then swap into dist.
dotnet publish (Join-Path $root 'ClaudeWatch.csproj') `
    -c $Configuration `
    -r $Rid `
    --self-contained `
    -o $stage

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null
$src = Join-Path $stage 'claudewatch.exe'
$dst = Join-Path $dist  'claudewatch.exe'
$old = Join-Path $dist  'claudewatch-old.exe'

# Clear any previous leftover; ignore if it's still locked by a running instance.
if (Test-Path $old) { try { Remove-Item $old -Force -ErrorAction Stop } catch {} }

# If the target is locked (status line is live), rename it aside; otherwise overwrite.
if (Test-Path $dst) {
    try { Rename-Item $dst $old -ErrorAction Stop }
    catch { Remove-Item $dst -Force -ErrorAction SilentlyContinue }
}

Copy-Item $src $dst -Force

if (Test-Path $dst) {
    $size = '{0:N1} MB' -f ((Get-Item $dst).Length / 1MB)
    Write-Host "Built $dst ($size)" -ForegroundColor Green
    if (Test-Path $old) {
        Write-Host "Note: previous exe is still running; left as claudewatch-old.exe (auto-cleaned next build)." -ForegroundColor DarkYellow
    }
} else {
    Write-Warning "Build succeeded but $dst was not found."
}
