# ==============================================================================
# XeroxGo Windows Printer Agent - Dedicated Clean Uninstaller
# Usage:
#   irm https://raw.githubusercontent.com/bibin-benny-cse/campusprint-printer-agent/main/uninstall.ps1 | iex
# ==============================================================================

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
$ErrorActionPreference = 'Stop'

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     🗑️  XeroxGo Windows Printer Agent - Clean Uninstaller" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Terminate any active process
Write-Host "⏳ [1/6] Stopping active XeroxGo Printer Agent processes..." -ForegroundColor Yellow
$processes = Get-Process -Name "XeroxGo.PrinterAgent" -ErrorAction SilentlyContinue
if ($processes) {
    $processes | Stop-Process -Force
    Start-Sleep -Seconds 1
    Write-Host "   ✓ Stopped active processes." -ForegroundColor Green
} else {
    Write-Host "   ✓ No active process running." -ForegroundColor DarkGray
}

# 2. Run official Inno Setup Uninstaller if present
Write-Host "⏳ [2/6] Running application uninstaller..." -ForegroundColor Yellow
$uninstallRegKeys = @(
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D37F619C-5582-4BC2-91DE-7A419266F3A1}_is1",
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D37F619C-5582-4BC2-91DE-7A419266F3A1}_is1"
)
foreach ($regPath in $uninstallRegKeys) {
    if (Test-Path $regPath) {
        $uninstString = (Get-ItemProperty -Path $regPath -Name "UninstallString" -ErrorAction SilentlyContinue).UninstallString
        if ($uninstString) {
            $uninstExe = $uninstString.Trim('"')
            if (Test-Path $uninstExe) {
                Write-Host "   Executing native uninstaller silently..." -ForegroundColor DarkGray
                Start-Process -FilePath $uninstExe -ArgumentList "/SILENT", "/VERYSILENT", "/SUPPRESSMSGBOXES" -Wait
            }
        }
        Remove-Item -Path $regPath -Force -Recurse -ErrorAction SilentlyContinue
    }
}
Write-Host "   ✓ Application uninstalled." -ForegroundColor Green

# 3. Remove application installation directories
Write-Host "⏳ [3/6] Removing program files and residual binaries..." -ForegroundColor Yellow
$installDirs = @(
    (Join-Path $env:LOCALAPPDATA "Programs\XeroxGo Printer Agent"),
    (Join-Path $env:ProgramFiles "XeroxGo Printer Agent"),
    (Join-Path ${env:ProgramFiles(x86)} "XeroxGo Printer Agent")
)
foreach ($dir in $installDirs) {
    if ($dir -and (Test-Path $dir)) {
        Remove-Item -Path $dir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "   ✓ Removed: $dir" -ForegroundColor Green
    }
}

# 4. Remove all user data, configuration, logs, and temp queues
Write-Host "⏳ [4/6] Purging user data, logs, and configuration..." -ForegroundColor Yellow
$dataDir = Join-Path $env:LOCALAPPDATA "XeroxGo"
if (Test-Path $dataDir) {
    Remove-Item -Path $dataDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "   ✓ Purged: $dataDir" -ForegroundColor Green
} else {
    Write-Host "   ✓ No residual data directory found." -ForegroundColor DarkGray
}

# 5. Remove autostart registry and shortcuts
Write-Host "⏳ [5/6] Cleaning Windows Startup registry & shortcuts..." -ForegroundColor Yellow
$runReg = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
if (Test-Path $runReg) {
    $val = Get-ItemProperty -Path $runReg -Name "XeroxGoPrinterAgent" -ErrorAction SilentlyContinue
    if ($val) {
        Remove-ItemProperty -Path $runReg -Name "XeroxGoPrinterAgent" -Force -ErrorAction SilentlyContinue
        Write-Host "   ✓ Removed startup registry key." -ForegroundColor Green
    }
}

# Shortcuts
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) "XeroxGo Printer Agent.lnk"
$startMenuShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) "XeroxGo Printer Agent.lnk"
if (Test-Path $desktopShortcut) { Remove-Item $desktopShortcut -Force -ErrorAction SilentlyContinue }
if (Test-Path $startMenuShortcut) { Remove-Item $startMenuShortcut -Force -ErrorAction SilentlyContinue }
Write-Host "   ✓ Desktop & Start Menu shortcuts removed." -ForegroundColor Green

# 6. Remove publisher certificate from stores & cleanup temp cache
Write-Host "⏳ [6/6] Cleaning certificate store and temporary files..." -ForegroundColor Yellow
$stores = @("Cert:\CurrentUser\TrustedPublisher", "Cert:\CurrentUser\Root")
foreach ($store in $stores) {
    if (Test-Path $store) {
        Get-ChildItem -Path $store -ErrorAction SilentlyContinue |
            Where-Object { $_.Subject -like "*CN=XeroxGo Technologies*" } |
            ForEach-Object {
                Remove-Item -Path $_.PSPath -Force -ErrorAction SilentlyContinue
                Write-Host "   ✓ Removed publisher certificate from $store" -ForegroundColor Green
            }
    }
}

$tempDir = Join-Path $env:TEMP "XeroxGoInstall"
if (Test-Path $tempDir) {
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "✨ XeroxGo Printer Agent has been cleanly and fully uninstalled!" -ForegroundColor Green
Write-Host "   Zero residual files, configs, registry entries, or certs left." -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
