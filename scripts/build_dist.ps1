param(
  [string]$Project = ".\AiStackchanSetup.csproj",
  [string]$Runtime = "win-x64",
  [string]$Configuration = "Release",
  [string]$DistRoot = ".\dist\AiStackchanSetup"
)

$ErrorActionPreference = "Stop"

function New-DirectoryIfMissing($p) {
  if (!(Test-Path $p)) { New-Item -ItemType Directory -Path $p | Out-Null }
}

# --- clean dist ---
if (Test-Path $DistRoot) { Remove-Item $DistRoot -Recurse -Force }
New-DirectoryIfMissing $DistRoot

$appDir = Join-Path $DistRoot "app"
New-DirectoryIfMissing $appDir

Write-Host "== dotnet publish => $appDir =="
dotnet publish $Project `
  -c $Configuration `
  -r $Runtime `
  --self-contained true `
  -o $appDir `
  /p:PublishSingleFile=false `
  /p:PublishReadyToRun=false `
  /p:PublishTrimmed=false

# --- tools -> app/tools ---
if (Test-Path ".\tools") {
  $toolsDst = Join-Path $appDir "tools"
  New-DirectoryIfMissing $toolsDst
  Copy-Item ".\tools\*" $toolsDst -Recurse -Force
}

# --- firmware: prefer root \firmware if present ---
$fwSrcDir = if (Test-Path ".\firmware") { ".\firmware" } else { ".\Resources\firmware" }
$rootFwDir = Join-Path $DistRoot "firmware"
$appFwDir  = Join-Path $appDir "firmware"

if (Test-Path $fwSrcDir) {
  New-DirectoryIfMissing $rootFwDir
  New-DirectoryIfMissing $appFwDir

  $publicFw = Get-ChildItem $fwSrcDir -File | Where-Object { $_.Name -match "_public" } | Select-Object -First 1
  if ($publicFw) {
    Copy-Item $publicFw.FullName $rootFwDir -Force
    Copy-Item $publicFw.FullName $appFwDir -Force
    Write-Host "Firmware (public): $($publicFw.Name)"
  } else {
    # Fallback: copy all if no _public found (safety)
    Copy-Item "$fwSrcDir\*" $rootFwDir -Recurse -Force
    Copy-Item "$fwSrcDir\*" $appFwDir -Recurse -Force
    Write-Host "Firmware: copied all (no _public found)"
  }
} else {
  Write-Host "Firmware source not found: $fwSrcDir (skipped)"
}

# --- README.txt -> dist root ---
if (Test-Path ".\README.txt") {
  Copy-Item ".\README.txt" (Join-Path $DistRoot "README.txt") -Force
}

# --- launcher bat -> dist root ---
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

# --- zip ---
$zipPath = "$DistRoot.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$DistRoot\*" -DestinationPath $zipPath -Force

Write-Host "== DONE =="
Write-Host "Dist folder: $DistRoot"
Write-Host "Zip: $zipPath"
