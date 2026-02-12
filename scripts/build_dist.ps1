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

# --- 2) copy tools to app/tools ---
$toolsSrc = ".\tools"
if (Test-Path $toolsSrc) {
  $toolsDst = Join-Path $appDir "tools"
  Ensure-Dir $toolsDst
  Copy-Item "$toolsSrc\*" $toolsDst -Recurse -Force
  Write-Host "Tools copied    : $toolsSrc -> $toolsDst"
} else {
  Write-Host "Tools skipped   : not found ($toolsSrc)"
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

Write-Host ""
Write-Host "== DONE =="
Write-Host "Dist folder: $DistRoot"
Write-Host "Zip       : $zipPath"

