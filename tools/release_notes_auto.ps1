param(
    [string]$DistRoot = '.\dist\AiStackchanSetup',
    [string]$OutputPath = '.\dist\RELEASE_NOTES_auto.md',
    [string]$FirmwareMetaPath = '.\firmware\stackchan_core2_public.meta.json',
    [string]$ChangelogPath = '.\CHANGELOG.md',
    [string]$ReleaseVersion = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Utf8NoBom([string]$path, [string]$content) {
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $content, $encoding)
}

function Get-ArtifactInfo([string]$path) {
    if (-not (Test-Path $path -PathType Leaf)) {
        return [PSCustomObject]@{
            Path = $path
            Exists = $false
            SHA256 = ''
            VirusTotal = ''
        }
    }

    $hash = (Get-FileHash $path -Algorithm SHA256).Hash.ToUpperInvariant()
    $vt = "https://www.virustotal.com/gui/file/$($hash.ToLowerInvariant())?nocache=1"
    return [PSCustomObject]@{
        Path = $path
        Exists = $true
        SHA256 = $hash
        VirusTotal = $vt
    }
}

function Resolve-UnreleasedBullets([string]$path) {
    if (-not (Test-Path $path -PathType Leaf)) {
        return @()
    }

    $lines = Get-Content $path
    $start = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^## \[Unreleased\]') {
            $start = $i
            break
        }
    }
    if ($start -lt 0) {
        return @()
    }

    $bullets = New-Object System.Collections.Generic.List[string]
    for ($j = $start + 1; $j -lt $lines.Count; $j++) {
        if ($lines[$j] -match '^## \[') {
            break
        }
        if ($lines[$j] -match '^- ') {
            $bullets.Add($lines[$j])
        }
    }
    return $bullets
}

$zipPath = "$DistRoot.zip"
$exePath = Join-Path $DistRoot 'app\AiStackchanSetup.exe'

$firmwareBin = Join-Path $DistRoot 'firmware\stackchan_core2_public.bin'
if (-not (Test-Path $firmwareBin -PathType Leaf)) {
    $firstBin = Get-ChildItem (Join-Path $DistRoot 'firmware') -Filter *.bin -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($firstBin) {
        $firmwareBin = $firstBin.FullName
    }
}

$metaPathResolved = $FirmwareMetaPath
$distMeta = Join-Path $DistRoot 'firmware\stackchan_core2_public.meta.json'
if (Test-Path $distMeta -PathType Leaf) {
    $metaPathResolved = $distMeta
}

$meta = $null
if (Test-Path $metaPathResolved -PathType Leaf) {
    try {
        $meta = Get-Content $metaPathResolved -Raw | ConvertFrom-Json
    } catch {
        $meta = $null
    }
}

$versionText = $ReleaseVersion
if ([string]::IsNullOrWhiteSpace($versionText)) {
    $versionText = '<set release version>'
}

$zipInfo = Get-ArtifactInfo $zipPath
$exeInfo = Get-ArtifactInfo $exePath
$fwInfo = Get-ArtifactInfo $firmwareBin
$today = Get-Date -Format 'yyyy-MM-dd'
$changes = Resolve-UnreleasedBullets $ChangelogPath

$changesBlock = if ($changes.Count -gt 0) {
    ($changes -join "`r`n")
} else {
    "- <add user-visible change 1>`r`n- <add user-visible change 2>`r`n- <add user-visible change 3>"
}

$appName = if ($meta -and ($meta.PSObject.Properties.Name -contains 'app')) { [string]$meta.app } else { '<unknown>' }
$appVer = if ($meta -and ($meta.PSObject.Properties.Name -contains 'ver')) { [string]$meta.ver } else { '<unknown>' }
$buildId = if ($meta -and ($meta.PSObject.Properties.Name -contains 'build_id')) { [string]$meta.build_id } else { '<unknown>' }

$zipHash = if ($zipInfo.Exists) { $zipInfo.SHA256 } else { '<missing>' }
$exeHash = if ($exeInfo.Exists) { $exeInfo.SHA256 } else { '<missing>' }
$fwHash = if ($fwInfo.Exists) { $fwInfo.SHA256 } else { '<missing>' }

$zipVt = if ($zipInfo.Exists) { $zipInfo.VirusTotal } else { '<missing>' }
$exeVt = if ($exeInfo.Exists) { $exeInfo.VirusTotal } else { '<missing>' }
$fwVt = if ($fwInfo.Exists) { $fwInfo.VirusTotal } else { '<missing>' }

$content = @"
## Summary
- Release: $versionText
- Date: $today
- This release updates the setup package and bundled firmware for end users.

## User-visible changes
$changesBlock

## Bundled firmware
- app: $appName
- ver: $appVer
- build_id: $buildId

## Quickstart
- Japanese: https://github.com/welchmanjd/ai-mining-stackchan-setup/blob/main/docs/quickstart-ja.md
- Latest release: https://github.com/welchmanjd/ai-mining-stackchan-setup/releases/latest

## Integrity
- SHA256 (`AiStackchanSetup.zip`): $zipHash
- SHA256 (`AiStackchanSetup.exe`): $exeHash
- SHA256 (`stackchan_core2_public.bin`): $fwHash

## Security scan
- VirusTotal (`AiStackchanSetup.zip`): $zipVt
- VirusTotal (`AiStackchanSetup.exe`): $exeVt
- VirusTotal (`stackchan_core2_public.bin`): $fwVt

## Notes
- If a VirusTotal URL is new and has no analysis yet, upload that artifact once.
"@

$outDir = Split-Path $OutputPath -Parent
if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

Write-Utf8NoBom $OutputPath $content

Write-Host "Generated: $OutputPath"
Write-Host "  ZIP: $zipPath"
Write-Host "  EXE: $exePath"
Write-Host "  BIN: $firmwareBin"
