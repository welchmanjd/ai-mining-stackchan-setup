# =========================================================
# Release helper (one-command flow)
#
# Run (PowerShell):
#   powershell -ExecutionPolicy Bypass -File .\tools\release.ps1 -ReleaseVersion v0.203
#   powershell -ExecutionPolicy Bypass -File .\tools\release.ps1 -ReleaseVersion v0.203 -PublishGitHubRelease
#
# Or from current PowerShell session:
#   .\tools\release.ps1 -ReleaseVersion v0.203
# =========================================================

param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseVersion,
    [switch]$AllowDirty,
    [ValidateSet('ja', 'en')]
    [string]$ReleaseNotesLanguage = 'ja',
    [switch]$PublishGitHubRelease,
    [string]$GitHubRepo = 'welchmanjd/ai-mining-stackchan-setup'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$preflightScript = Join-Path $PSScriptRoot 'preflight_release.ps1'
$buildScript = Join-Path $repoRoot 'scripts\build_dist.ps1'
$notesScript = Join-Path $PSScriptRoot 'release_notes_auto.ps1'
$zipPath = Join-Path $repoRoot 'dist\AiStackchanSetup.zip'
$notesEnPath = Join-Path $repoRoot 'dist\RELEASE_NOTES_auto.md'
$notesJaPath = Join-Path $repoRoot 'dist\RELEASE_NOTES_auto_ja.md'

if (-not (Test-Path $preflightScript -PathType Leaf)) { throw "Missing script: $preflightScript" }
if (-not (Test-Path $buildScript -PathType Leaf)) { throw "Missing script: $buildScript" }
if (-not (Test-Path $notesScript -PathType Leaf)) { throw "Missing script: $notesScript" }

Write-Host '== Release Flow =='
Write-Host "Version: $ReleaseVersion"
Write-Host "Release notes language: $ReleaseNotesLanguage"
Write-Host "Publish GitHub release: $PublishGitHubRelease"
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
Write-Host 'Step 3/4: Generate release notes draft'
& $notesScript -ReleaseVersion $ReleaseVersion
if ($LASTEXITCODE -ne 0) {
    throw "release_notes_auto failed with exit code $LASTEXITCODE"
}

$notesFile = if ($ReleaseNotesLanguage -eq 'ja') { $notesJaPath } else { $notesEnPath }
if (-not (Test-Path $notesFile -PathType Leaf)) {
    throw "Notes file not found: $notesFile"
}

if ($PublishGitHubRelease) {
    Write-Host ''
    Write-Host 'Step 4/4: Create or update GitHub release'
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw 'GitHub CLI (gh) is not available in PATH.'
    }
    if (-not (Test-Path $zipPath -PathType Leaf)) {
        throw "Release asset not found: $zipPath"
    }

    & gh release view $ReleaseVersion --repo $GitHubRepo *> $null
    if ($LASTEXITCODE -eq 0) {
        & gh release edit $ReleaseVersion --repo $GitHubRepo --notes-file $notesFile
        if ($LASTEXITCODE -ne 0) { throw "gh release edit failed with exit code $LASTEXITCODE" }
        & gh release upload $ReleaseVersion $zipPath --repo $GitHubRepo --clobber
        if ($LASTEXITCODE -ne 0) { throw "gh release upload failed with exit code $LASTEXITCODE" }
    } else {
        & gh release create $ReleaseVersion $zipPath --repo $GitHubRepo --title $ReleaseVersion --notes-file $notesFile
        if ($LASTEXITCODE -ne 0) { throw "gh release create failed with exit code $LASTEXITCODE" }
    }
}

Write-Host ''
Write-Host 'DONE'
Write-Host 'Artifacts:'
Write-Host '  - dist\AiStackchanSetup.zip'
Write-Host '  - dist\RELEASE_NOTES_auto.md'
Write-Host '  - dist\RELEASE_NOTES_auto_ja.md'
Write-Host "Release notes selected: $notesFile"
