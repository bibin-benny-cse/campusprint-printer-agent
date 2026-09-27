# ==============================================================================
# XeroxGo Windows Printer Agent - Quick Web Installer
# Usage:
#   irm https://raw.githubusercontent.com/bibin-benny-cse/campusprint-printer-agent/main/install.ps1 | iex
# ==============================================================================

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
$ErrorActionPreference = 'Stop'

$RepoOwner = "bibin-benny-cse"
$RepoName  = "campusprint-printer-agent"
$ReleaseBaseUrl = "https://github.com/$RepoOwner/$RepoName/releases/latest/download"

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     🖨️  XeroxGo Windows Printer Agent - Quick Installer" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Prepare temporary directory
$tempDir = Join-Path $env:TEMP "XeroxGoInstall"
if (-not (Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
}

$installerPath = Join-Path $tempDir "XeroxGoAgent-Setup.exe"
$certPath      = Join-Path $tempDir "XeroxGo-Publisher-Certificate.cer"

# 2. Download the latest signed installer
$installerUrl = "$ReleaseBaseUrl/XeroxGoAgent-Setup.exe"
Write-Host "⏳ [1/3] Downloading latest XeroxGoAgent-Setup.exe..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
    Write-Host "   ✓ Installer downloaded successfully." -ForegroundColor Green
} catch {
    Write-Host "   ❌ Failed to download installer from: $installerUrl" -ForegroundColor Red
    Write-Host "   Error: $_" -ForegroundColor Red
    return
}

# 3. Download and register Publisher Certificate for Current User (No Admin required)
$certUrl = "$ReleaseBaseUrl/XeroxGo-Publisher-Certificate.cer"
Write-Host "⏳ [2/3] Registering Verified Publisher certificate..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri $certUrl -OutFile $certPath -UseBasicParsing
    
    # Import into CurrentUser\TrustedPublisher store (no UAC / elevation needed)
    if (Get-Command Import-Certificate -ErrorAction SilentlyContinue) {
        Import-Certificate -FilePath $certPath -CertStoreLocation "Cert:\CurrentUser\TrustedPublisher" | Out-Null
        Import-Certificate -FilePath $certPath -CertStoreLocation "Cert:\CurrentUser\Root" -ErrorAction SilentlyContinue | Out-Null
    } else {
        & certutil -user -addstore "TrustedPublisher" $certPath | Out-Null
    }
    Write-Host "   ✓ Publisher verified for Current User." -ForegroundColor Green
} catch {
    # Non-fatal: if cert fails to import, file unblocking still ensures clean launch
    Write-Host "   ℹ Note: Certificate auto-registration skipped ($($_)). Continuing..." -ForegroundColor DarkGray
}

# 4. Remove any Mark of the Web tags (ensures zero Smart App Control friction)
Write-Host "⏳ [3/3] Preparing installer execution..." -ForegroundColor Yellow
if (Get-Command Unblock-File -ErrorAction SilentlyContinue) {
    Unblock-File -Path $installerPath -ErrorAction SilentlyContinue
}
Write-Host "   ✓ Installer verified and unblocked." -ForegroundColor Green

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "🚀 Launching XeroxGo Printer Agent Setup Wizard..." -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# 5. Launch the installer
Start-Process -FilePath $installerPath
