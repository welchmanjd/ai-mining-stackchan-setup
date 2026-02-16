# =========================================================
# Build distribution firmware (public)
#
# Run (PowerShell):
#   .\tools\build_firmware_public.ps1
#
# For clean metadata (no "-dirty" caused by sample config staging):
#   .\tools\build_firmware_public.ps1 -UseSampleConfig:$false
# =========================================================



param(
    [string]$Env = 'm5stack-core2-dist',
    [string]$FirmwareRepo = '',
    [switch]$SkipBuild,
    [bool]$UseSampleConfig = $false
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Env = $Env.Trim()

function Resolve-AppVersion([string]$repoRoot) {
    $ver = '0.0.0-dev'
    $exactTag = ''
    try {
        $exactTag = (git -C $repoRoot describe --tags --exact-match HEAD).Trim()
    } catch {
        $exactTag = ''
    }
    if (-not [string]::IsNullOrWhiteSpace($exactTag)) {
        $ver = if ($exactTag.StartsWith('v')) { $exactTag.Substring(1) } else { $exactTag }
    } else {
        $nearestTag = ''
        try {
            $nearestTag = (git -C $repoRoot describe --tags --abbrev=0).Trim()
        } catch {
            $nearestTag = ''
        }
        if (-not [string]::IsNullOrWhiteSpace($nearestTag)) {
            $nearestNorm = if ($nearestTag.StartsWith('v')) { $nearestTag.Substring(1) } else { $nearestTag }
            $count = ''
            try {
                $count = (git -C $repoRoot rev-list "$nearestTag..HEAD" --count).Trim()
            } catch {
                $count = ''
            }
            if (-not [string]::IsNullOrWhiteSpace($count) -and $count -ne '0') {
                $ver = "$nearestNorm-dev.$count"
            } else {
                $ver = $nearestNorm
            }
        }
    }
    try {
        $dirty = (git -C $repoRoot status --porcelain)
        if (-not [string]::IsNullOrWhiteSpace($dirty)) { $ver = "$ver-dirty" }
    } catch { }
    return $ver
}

function Resolve-BuildId([string]$repoRoot) {
    $hash = ''
    try {
        $hash = (git -C $repoRoot rev-parse --short=12 HEAD).Trim()
    } catch {
        $hash = ''
    }
    if ([string]::IsNullOrWhiteSpace($hash)) { $hash = 'nogit' }
    try {
        $dirty = (git -C $repoRoot status --porcelain)
        if (-not [string]::IsNullOrWhiteSpace($dirty)) { $hash = "$hash-dirty" }
    } catch { }
    return $hash
}

$setupRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

if ([string]::IsNullOrWhiteSpace($FirmwareRepo)) {
    $FirmwareRepo = Join-Path $setupRoot '..\ai-mining-stackchan-core2'
}
$firmwareRoot = Resolve-Path $FirmwareRepo

if (-not (Test-Path $firmwareRoot)) {
    throw "Firmware repo not found: $firmwareRoot"
}

$configDir = Join-Path $firmwareRoot 'src\config'
$sampleConfig = Join-Path $configDir 'config_private.sample.h'
$privateConfig = Join-Path $configDir 'config_private.h'
$backupConfig = ""
$copiedSample = $false
$resolvedVer = ''
$resolvedBuildId = ''
$destMain = ''

if ($UseSampleConfig) {
    if (-not (Test-Path $sampleConfig)) {
        throw "Sample config not found: $sampleConfig"
    }
    if (Test-Path $privateConfig) {
        $backupConfig = Join-Path $configDir 'config_private.h.bak'
        Copy-Item -Path $privateConfig -Destination $backupConfig -Force
    }
    Copy-Item -Path $sampleConfig -Destination $privateConfig -Force
    $copiedSample = $true
}

try {
    if (-not $SkipBuild) {
        Write-Host "Building public firmware for env '$Env'..."
        Push-Location $firmwareRoot
        try {
            pio run -e $Env
            if ($LASTEXITCODE -ne 0) {
                throw "pio run failed with exit code $LASTEXITCODE"
            }
        }
        finally {
            Pop-Location
        }
    }

    $buildDir = Join-Path $firmwareRoot ".pio\build\$Env"
    if (-not (Test-Path $buildDir)) {
        throw "Build output not found: $buildDir"
    }

    $appBin = Join-Path $buildDir 'firmware.bin'
    $bootloaderBin = Join-Path $buildDir 'bootloader.bin'
    $partitionsBin = Join-Path $buildDir 'partitions.bin'
    $bootApp0Bin = Join-Path $buildDir 'boot_app0.bin'

    if (-not (Test-Path $appBin)) { throw "Missing firmware.bin in $buildDir" }
    if (-not (Test-Path $bootloaderBin)) { throw "Missing bootloader.bin in $buildDir" }
    if (-not (Test-Path $partitionsBin)) { throw "Missing partitions.bin in $buildDir" }

    $pioHome = $env:PLATFORMIO_HOME_DIR
    if ([string]::IsNullOrWhiteSpace($pioHome)) {
        $pioHome = Join-Path $env:USERPROFILE '.platformio'
    }
    $pythonPath = Join-Path $pioHome 'penv\Scripts\python.exe'
    $esptoolPy = Join-Path $pioHome 'packages\tool-esptoolpy\esptool.py'

    if (-not (Test-Path $pythonPath)) { throw "PlatformIO python not found: $pythonPath" }
    if (-not (Test-Path $esptoolPy)) { throw "esptool.py not found: $esptoolPy" }

    $mergedPath = Join-Path $buildDir 'stackchan_core2_public_merged.bin'

    $mergeArgs = @(
        "`"$esptoolPy`"",
        "--chip", "esp32",
        "merge_bin",
        "-o", "`"$mergedPath`"",
        "0x1000", "`"$bootloaderBin`"",
        "0x8000", "`"$partitionsBin`"",
        "0x10000", "`"$appBin`""
    )

    if (Test-Path $bootApp0Bin) {
        $mergeArgs += @("0xe000", "`"$bootApp0Bin`"")
    }

    Write-Host "Merging binaries..."
    & $pythonPath $mergeArgs
    if ($LASTEXITCODE -ne 0) {
        throw "esptool.py merge_bin failed with exit code $LASTEXITCODE"
    }

    $outDir = Join-Path $setupRoot 'firmware'
    $destMain = Join-Path $outDir 'stackchan_core2_public.bin'
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    Copy-Item -Path $mergedPath -Destination $destMain -Force
    $(Get-Item -Path $destMain).LastWriteTime = Get-Date

    # Resolve metadata while build-time working tree state is still intact.
    # This keeps meta.json aligned with the actual compiled firmware identity.
    $resolvedVer = Resolve-AppVersion $firmwareRoot
    $resolvedBuildId = Resolve-BuildId $firmwareRoot

    Write-Host "Copied: $destMain"
}
finally {
    if ($copiedSample) {
        if ($backupConfig -and (Test-Path $backupConfig)) {
            Move-Item -Path $backupConfig -Destination $privateConfig -Force
        } else {
            Remove-Item -Path $privateConfig -Force -ErrorAction SilentlyContinue
        }
    }
}

$metaPath = [System.IO.Path]::ChangeExtension($destMain, '.meta.json')
$meta = @{
    app = 'Mining-Stackchan-Core2'
    ver = if (-not [string]::IsNullOrWhiteSpace($resolvedVer)) { $resolvedVer } else { Resolve-AppVersion $firmwareRoot }
    build_id = if (-not [string]::IsNullOrWhiteSpace($resolvedBuildId)) { $resolvedBuildId } else { Resolve-BuildId $firmwareRoot }
} | ConvertTo-Json -Depth 2
Set-Content -Path $metaPath -Value $meta -Encoding UTF8
Write-Host "Copied: $metaPath"
Write-Host "Done."

