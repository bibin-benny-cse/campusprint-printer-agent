<#
.SYNOPSIS
    XeroxGo Windows Printer Agent - Automated Web Installer
.DESCRIPTION
    Downloads and launches the latest XeroxGo Printer Agent installer directly
    from GitHub Releases without browser Mark-of-the-Web restrictions.
.EXAMPLE
    irm https://raw.githubusercontent.com/bibin-benny-cse/campusprint-printer-agent/main/install.ps1 | iex
#>

[CmdletBinding()]
param(
    [string]$Version = "latest"
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13

Write-Host ""
Write-Host " =========================================================" -ForegroundColor Cyan
Write-Host "   🖨️  XeroxGo Printer Agent - Automated Installer        " -ForegroundColor Cyan
Write-Host " =========================================================" -ForegroundColor Cyan
Write-Host ""

$repo = "bibin-benny-cse/campusprint-printer-agent"
if ($Version -eq "latest") {
    $downloadUrl = "https://github.com/$repo/releases/latest/download/XeroxGoAgent-Setup.exe"
    Write-Host " [1/3] Target release: Latest Production" -ForegroundColor Yellow
} else {
    $downloadUrl = "https://github.com/$repo/releases/download/$Version/XeroxGoAgent-Setup.exe"
    Write-Host " [1/3] Target release: $Version" -ForegroundColor Yellow
}

$tempFolder = [System.IO.Path]::GetTempPath()
$installerPath = Join-Path $tempFolder "XeroxGoAgent-Setup.exe"

# Clean up any previous installer cached in TEMP
if (Test-Path $installerPath) {
    Remove-Item $installerPath -Force -ErrorAction SilentlyContinue
}

Write-Host " [2/3] Downloading XeroxGoAgent-Setup.exe from GitHub..." -ForegroundColor Yellow
try {
    # Download file cleanly via WebClient without browser Mark-of-the-Web
    $webClient = New-Object System.Net.WebClient
    $webClient.Headers.Add("User-Agent", "XeroxGo-WebInstaller")
    $webClient.DownloadFile($downloadUrl, $installerPath)
    Write-Host "       Download completed successfully." -ForegroundColor Green
} catch {
    Write-Host " [ERROR] Failed to download installer from: $downloadUrl" -ForegroundColor Red
    Write-Host " Details: $_" -ForegroundColor Red
    exit 1
}

# Gracefully close existing agent if running before upgrading
$running = Get-Process -Name "XeroxGo.PrinterAgent" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host " [INFO] Stopping running agent process prior to upgrade..." -ForegroundColor Magenta
    Stop-Process -Name "XeroxGo.PrinterAgent" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

Write-Host " [3/3] Launching XeroxGo Agent Setup Wizard..." -ForegroundColor Yellow
Write-Host ""
Write-Host " =========================================================" -ForegroundColor Green
Write-Host "   Setup launched! Follow the on-screen prompts to finish. " -ForegroundColor Green
Write-Host " =========================================================" -ForegroundColor Green
Write-Host ""

Start-Process -FilePath $installerPath
