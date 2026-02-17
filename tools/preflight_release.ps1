param(
    [string]$FirmwareBinPath = '.\firmware\stackchan_core2_public.bin',
    [string]$FirmwareMetaPath = '.\firmware\stackchan_core2_public.meta.json',
    [string]$ChangelogPath = '.\CHANGELOG.md',
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$errors = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

function Add-Error([string]$message) {
    $script:errors.Add($message)
}

function Add-Warning([string]$message) {
    $script:warnings.Add($message)
}

function Assert-FileExists([string]$path, [string]$label) {
    if (-not (Test-Path $path -PathType Leaf)) {
        Add-Error("$label not found: $path")
        return $false
    }
    Write-Host "[OK] ${label}: $path"
    return $true
}

Write-Host '== Release Preflight =='

if (-not $AllowDirty) {
    try {
        $gitStatus = git status --porcelain
        if (-not [string]::IsNullOrWhiteSpace(($gitStatus | Out-String))) {
            Add-Error('Working tree is dirty. Commit/stash changes or run with -AllowDirty.')
            $lines = ($gitStatus -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            if ($lines.Count -gt 0) {
                Write-Host '  Dirty entries (first 20):'
                $lines | Select-Object -First 20 | ForEach-Object { Write-Host "    $_" }
            }
        } else {
            Write-Host '[OK] Working tree is clean.'
        }
    } catch {
        Add-Warning("Unable to read git status: $($_.Exception.Message)")
    }
} else {
    Add-Warning('Dirty working tree check skipped (-AllowDirty).')
}

$hasFirmwareBin = Assert-FileExists $FirmwareBinPath 'Firmware binary'
$hasFirmwareMeta = Assert-FileExists $FirmwareMetaPath 'Firmware metadata'
$hasChangelog = Assert-FileExists $ChangelogPath 'Changelog'

if ($hasFirmwareMeta) {
    try {
        $meta = Get-Content $FirmwareMetaPath -Raw | ConvertFrom-Json
        Write-Host '[OK] Firmware metadata JSON is valid.'

        foreach ($field in @('app', 'ver', 'build_id')) {
            if (-not ($meta.PSObject.Properties.Name -contains $field)) {
                Add-Error("Firmware metadata field missing: $field")
                continue
            }
            $value = [string]$meta.$field
            if ([string]::IsNullOrWhiteSpace($value)) {
                Add-Error("Firmware metadata field is empty: $field")
            } else {
                Write-Host "[OK] meta.$field = $value"
            }
        }

        if (($meta.PSObject.Properties.Name -contains 'build_id') -and ([string]$meta.build_id).Contains('-dirty')) {
            Add-Error("Firmware build_id contains '-dirty': $($meta.build_id)")
        }
    } catch {
        Add-Error("Firmware metadata parse failed: $($_.Exception.Message)")
    }
}

if ($hasChangelog) {
    try {
        $changelog = Get-Content $ChangelogPath -Raw
        if ($changelog -match '(?m)^## \[Unreleased\]') {
            Write-Host '[OK] Changelog has [Unreleased] section.'
        } else {
            Add-Warning('Changelog does not contain [Unreleased] section.')
        }
    } catch {
        Add-Warning("Unable to read changelog: $($_.Exception.Message)")
    }
}

Write-Host ''
if ($warnings.Count -gt 0) {
    Write-Host 'Warnings:'
    $warnings | ForEach-Object { Write-Host "  - $_" }
    Write-Host ''
}

if ($errors.Count -gt 0) {
    Write-Host 'FAIL'
    $errors | ForEach-Object { Write-Host "  - $_" }
    exit 1
}

Write-Host 'PASS'
exit 0
