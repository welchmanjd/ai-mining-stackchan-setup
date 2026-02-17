# =========================================================
# Release helper (one-command flow)
#
# Run (PowerShell):
#   powershell -ExecutionPolicy Bypass -File .\tools\release.ps1 -ReleaseVersion v0.203
#
# Or from current PowerShell session:
#   .\tools\release.ps1 -ReleaseVersion v0.203
# =========================================================

param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseVersion,
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$preflightScript = Join-Path $PSScriptRoot 'preflight_release.ps1'
$buildScript = Join-Path $repoRoot 'scripts\build_dist.ps1'
$notesScript = Join-Path $PSScriptRoot 'release_notes_auto.ps1'

if (-not (Test-Path $preflightScript -PathType Leaf)) { throw "Missing script: $preflightScript" }
if (-not (Test-Path $buildScript -PathType Leaf)) { throw "Missing script: $buildScript" }
if (-not (Test-Path $notesScript -PathType Leaf)) { throw "Missing script: $notesScript" }

Write-Host '== Release Flow =='
Write-Host "Version: $ReleaseVersion"
Write-Host ''

Write-Host 'Step 1/3: Preflight checks'
if ($AllowDirty) {
    & $preflightScript -AllowDirty
} else {
    & $preflightScript
}
if ($LASTEXITCODE -ne 0) {
    throw "Preflight failed with exit code $LASTEXITCODE"
}

Write-Host ''
Write-Host 'Step 2/3: Build distribution'
& $buildScript
if ($LASTEXITCODE -ne 0) {
    throw "build_dist failed with exit code $LASTEXITCODE"
}

Write-Host ''
Write-Host 'Step 3/3: Generate release notes draft'
& $notesScript -ReleaseVersion $ReleaseVersion
if ($LASTEXITCODE -ne 0) {
    throw "release_notes_auto failed with exit code $LASTEXITCODE"
}

Write-Host ''
Write-Host 'DONE'
Write-Host 'Artifacts:'
Write-Host '  - dist\AiStackchanSetup.zip'
Write-Host '  - dist\RELEASE_NOTES_auto.md'
