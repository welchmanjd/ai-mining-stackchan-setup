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

# --- 0) dist をクリーン作成 ---
if (Test-Path $DistRoot) { Remove-Item $DistRoot -Recurse -Force }
Ensure-Dir $DistRoot
Write-Host "Effective DistRoot: $DistRoot"

$appDir = Join-Path $DistRoot "app"
Ensure-Dir $appDir

# --- 1) publish（安定性優先の構成：single-file/R2Rは無効） ---
Write-Host "== dotnet publish => $appDir =="
dotnet publish $Project `
  -c $Configuration `
  -r $Runtime `
  --self-contained true `
  -o $appDir `
  /p:PublishSingleFile=false `
  /p:PublishReadyToRun=false `
  /p:PublishTrimmed=false

# publish結果のexe確認
$mainExe = Join-Path $appDir "AiStackchanSetup.exe"
if (!(Test-Path $mainExe)) {
  # exe名が変わった場合に備え、最大サイズのexeを拾う
  $exe = Get-ChildItem $appDir -Filter *.exe | Sort-Object Length -Descending | Select-Object -First 1
  if (-not $exe) { throw "Publish output exe not found in: $appDir" }
  $mainExe = $exe.FullName
  Write-Host "WARN: AiStackchanSetup.exe not found. Using: $($exe.Name)"
}

# --- 2) tools を app/tools へコピー ---
$toolsSrc = ".\tools"
if (Test-Path $toolsSrc) {
  $toolsDst = Join-Path $appDir "tools"
  Ensure-Dir $toolsDst
  Copy-Item "$toolsSrc\*" $toolsDst -Recurse -Force
  Write-Host "Tools copied    : $toolsSrc -> $toolsDst"
} else {
  Write-Host "Tools skipped   : not found ($toolsSrc)"
}

# --- 3) firmware（_public を含む .bin を優先）をルート firmware と app/firmware に同梱 ---
$fwSrcDir = ".\Resources\firmware"
$rootFwDir = Join-Path $DistRoot "firmware"
$appFwDir  = Join-Path $appDir  "firmware"

if (Test-Path $fwSrcDir) {
  Ensure-Dir $rootFwDir
  Ensure-Dir $appFwDir

  $publicFw = Get-ChildItem $fwSrcDir -File -Filter *.bin |
    Where-Object { $_.Name -match "_public" } |
    Select-Object -First 1

  if ($publicFw) {
    Copy-Item $publicFw.FullName $rootFwDir -Force
    Copy-Item $publicFw.FullName $appFwDir  -Force
    $publicMeta = [System.IO.Path]::ChangeExtension($publicFw.FullName, '.meta.json')
    if (Test-Path $publicMeta) {
      Copy-Item $publicMeta $rootFwDir -Force
      Copy-Item $publicMeta $appFwDir  -Force
      Write-Host "Firmware meta    : $([System.IO.Path]::GetFileName($publicMeta))"
    }
    Write-Host "Firmware (public): $($publicFw.Name)"
  } else {
    # 念のため：_public が無い場合は全コピー（ただし通常は起きない想定）
    Copy-Item "$fwSrcDir\*" $rootFwDir -Recurse -Force
    Copy-Item "$fwSrcDir\*" $appFwDir  -Recurse -Force
    Write-Host "Firmware copied  : all files (no _public found)"
  }
} else {
  Write-Host "Firmware skipped : not found ($fwSrcDir)"
}

# --- 4) README を dist ルートへ ---
$readmeSrc = ".\README.txt"
$readmeDst = Join-Path $DistRoot "README.txt"
if (Copy-IfExists $readmeSrc $readmeDst) {
  Write-Host "README copied   : $readmeSrc -> $readmeDst"
} else {
  Write-Host "README skipped  : not found ($readmeSrc)"
}

# --- 5) 起動用bat（ルート）を生成 ---
# ルートのbatから app 内の exe を起動する（DLL探索のため pushd 必須）
$batPath = Join-Path $DistRoot "AiStackchanSetup.bat"
@'
@echo off
setlocal

set ROOT=%~dp0
set APP=%ROOT%app

if not exist "%APP%\AiStackchanSetup.exe" (
  echo [ERROR] AiStackchanSetup.exe が見つかりません: "%APP%"
  echo zipを展開し直してください。
  pause
  exit /b 1
)

rem 依存DLL探索のため app に移動して起動
pushd "%APP%"
start "" "%APP%\AiStackchanSetup.exe"
popd

endlocal
'@ | Set-Content -Encoding ASCII $batPath
Write-Host "Launcher created: $batPath"

# --- 6) zip を作る ---
$zipPath = "$DistRoot.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$DistRoot\*" -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "== DONE =="
Write-Host "Dist folder: $DistRoot"
Write-Host "Zip       : $zipPath"
