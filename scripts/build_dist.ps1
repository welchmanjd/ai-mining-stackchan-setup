# =========================================================
# Build & zip distribution
#
# Run (PowerShell):
#   powershell -ExecutionPolicy Bypass -File .\scripts\build_dist.ps1
# =========================================================

param(
  [string]$Project = ".\AiStackchanSetup.csproj",
  [string]$Runtime = "win-x64",
  [string]$Configuration = "Release",
  [string]$DistRoot = ".\dist\AiStackchanSetup"
)


$ErrorActionPreference = "Stop"

function Ensure-Dir([string]$p) {
  if (!(Test-Path $p)) { New-Item -ItemType Directory -Path $p | Out-Null }
}

function Copy-IfExists([string]$src, [string]$dst) {
  if (Test-Path $src) {
    Ensure-Dir (Split-Path $dst -Parent)
    Copy-Item $src $dst -Force
    return $true
  }
  return $false
}

function Write-ArtifactDigest([string]$label, [string]$path) {
  if (!(Test-Path $path)) {
    Write-Host "$label : not found ($path)"
    return
  }

  $hash = (Get-FileHash $path -Algorithm SHA256).Hash.ToUpperInvariant()
  $vtUrl = "https://www.virustotal.com/gui/file/$($hash.ToLowerInvariant())?nocache=1"
  Write-Host "$label"
  Write-Host "  Path      : $path"
  Write-Host "  SHA256    : $hash"
  Write-Host "  VirusTotal: $vtUrl"
}


Write-Host "== Build Dist =="
Write-Host "Project        : $Project"
Write-Host "Configuration  : $Configuration"
Write-Host "Runtime        : $Runtime"
Write-Host "DistRoot       : $DistRoot"
Write-Host ""

# --- 0) clean dist directory ---
if (Test-Path $DistRoot) { Remove-Item $DistRoot -Recurse -Force }
Ensure-Dir $DistRoot
Write-Host "Effective DistRoot: $DistRoot"

$appDir = Join-Path $DistRoot "app"
Ensure-Dir $appDir

# --- 1) publish app ---
Write-Host "== dotnet publish => $appDir =="
dotnet publish $Project `
  -c $Configuration `
  -r $Runtime `
  --self-contained true `
  -o $appDir `
  /p:PublishSingleFile=false `
  /p:PublishReadyToRun=false `
  /p:PublishTrimmed=false

# validate published exe
$mainExe = Join-Path $appDir "AiStackchanSetup.exe"
if (!(Test-Path $mainExe)) {
  # fallback: choose largest exe if name changed
  $exe = Get-ChildItem $appDir -Filter *.exe | Sort-Object Length -Descending | Select-Object -First 1
  if (-not $exe) { throw "Publish output exe not found in: $appDir" }
  $mainExe = $exe.FullName
  Write-Host "WARN: AiStackchanSetup.exe not found. Using: $($exe.Name)"
}

# --- 2) copy required runtime tools to app/tools ---
$toolsSrc = ".\tools"
$toolsDst = Join-Path $appDir "tools"
$espflashSrc = Join-Path $toolsSrc "espflash.exe"
if (Test-Path $espflashSrc) {
  Ensure-Dir $toolsDst
  Copy-Item $espflashSrc $toolsDst -Force
  Write-Host "Tool copied     : $espflashSrc -> $toolsDst"
} else {
  Write-Host "Tool skipped    : not found ($espflashSrc)"
}

# --- 3) firmware (_public .bin preferred) to dist root ---
$fwSrcDir = ".\firmware"
$rootFwDir = Join-Path $DistRoot "firmware"

if (Test-Path $fwSrcDir) {
  Ensure-Dir $rootFwDir

  $publicFw = Get-ChildItem $fwSrcDir -File -Filter *.bin |
    Where-Object { $_.Name -match "_public" } |
    Select-Object -First 1

  if ($publicFw) {
    Copy-Item $publicFw.FullName $rootFwDir -Force
    $publicMeta = [System.IO.Path]::ChangeExtension($publicFw.FullName, '.meta.json')
    if (Test-Path $publicMeta) {
      Copy-Item $publicMeta $rootFwDir -Force
      Write-Host "Firmware meta    : $([System.IO.Path]::GetFileName($publicMeta))"
    }
    Write-Host "Firmware (public): $($publicFw.Name)"
  } else {
    # Fallback: copy all firmware files when no _public bin exists.
    Copy-Item "$fwSrcDir\*" $rootFwDir -Recurse -Force
    Write-Host "Firmware copied  : all files (no _public found)"
  }
} else {
  Write-Host "Firmware skipped : not found ($fwSrcDir)"
}

# --- 4) copy README to dist root ---
$readmeSrc = ".\README.txt"
$readmeDst = Join-Path $DistRoot "README.txt"
if (Copy-IfExists $readmeSrc $readmeDst) {
  Write-Host "README copied   : $readmeSrc -> $readmeDst"
} else {
  Write-Host "README skipped  : not found ($readmeSrc)"
}

# --- 5) create launcher bat ---
# launch app exe from app dir so dependent DLL resolution stays stable
$batPath = Join-Path $DistRoot "AiStackchanSetup.bat"
@'
@echo off
setlocal

set ROOT=%~dp0
set APP=%ROOT%app
set LOGDIR=%ROOT%log

if not exist "%LOGDIR%" mkdir "%LOGDIR%"
set AISTACKCHAN_LOG_DIR=%LOGDIR%

if not exist "%APP%\AiStackchanSetup.exe" (
  echo [ERROR] AiStackchanSetup.exe not found: "%APP%"
  echo Re-extract the zip and try again.
  pause
  exit /b 1
)

rem launch from app directory
pushd "%APP%"
start "" "%APP%\AiStackchanSetup.exe"
popd

endlocal
'@ | Set-Content -Encoding ASCII $batPath
Write-Host "Launcher created: $batPath"

# --- 6) create zip ---
$zipPath = "$DistRoot.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$DistRoot\*" -DestinationPath $zipPath -Force

$distFwBin = $null
if (Test-Path $rootFwDir) {
  $distFwBin = Get-ChildItem $rootFwDir -File -Filter *.bin |
    Where-Object { $_.Name -match "_public" } |
    Select-Object -First 1
  if (-not $distFwBin) {
    $distFwBin = Get-ChildItem $rootFwDir -File -Filter *.bin | Select-Object -First 1
  }
}

Write-Host ""
Write-Host "== DONE =="
Write-Host "Dist folder: $DistRoot"
Write-Host "Zip       : $zipPath"
Write-Host ""
Write-Host "== Integrity & VirusTotal =="
Write-ArtifactDigest "Distribution zip" $zipPath
Write-Host ""
Write-ArtifactDigest "Setup executable" $mainExe
Write-Host ""
if ($distFwBin) {
  Write-ArtifactDigest "Bundled firmware" $distFwBin.FullName
} else {
  Write-Host "Bundled firmware : not found (*.bin under $rootFwDir)"
}

