# ==============================================================================
# XeroxGo Agent - Local Uninstaller
# Shipped directly with the application in {app}\uninstall.ps1
# ==============================================================================

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
$ErrorActionPreference = 'SilentlyContinue'

Write-Host ""
Write-Host "XeroxGo Agent Uninstaller" -ForegroundColor Cyan
Write-Host "-------------------------" -ForegroundColor DarkGray

# 1. Stop active processes
Write-Host "Stopping processes..."
$processes = Get-Process -Name "XeroxGo.PrinterAgent", "XeroxGoAgent-Setup" -ErrorAction SilentlyContinue
if ($processes) {
    $processes | Stop-Process -Force
    Start-Sleep -Seconds 1
}

# 2. Query registry for installation paths & run native uninstaller
$uninstallRegKeys = @(
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D37F619C-5582-4BC2-91DE-7A419266F3A1}_is1",
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D37F619C-5582-4BC2-91DE-7A419266F3A1}_is1"
)
$discoveredInstallDirs = @()
foreach ($regPath in $uninstallRegKeys) {
    if (Test-Path $regPath) {
        $regProps = Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue
        if ($regProps.InstallLocation) {
            $discoveredInstallDirs += $regProps.InstallLocation
        }
        if ($regProps.UninstallString) {
            $uninstExe = $regProps.UninstallString.Trim('"')
            if (Test-Path $uninstExe) {
                Start-Process -FilePath $uninstExe -ArgumentList "/SILENT", "/VERYSILENT", "/SUPPRESSMSGBOXES" -Wait
                Start-Sleep -Seconds 2
            }
        }
        Remove-Item -Path $regPath -Force -Recurse -ErrorAction SilentlyContinue
    }
}

# 3. Remove program files
Write-Host "Removing application files..."
$installDirs = @(
    (Join-Path $env:LOCALAPPDATA "Programs\XeroxGo Agent"),
    (Join-Path $env:ProgramFiles "XeroxGo Agent"),
    (Join-Path ${env:ProgramFiles(x86)} "XeroxGo Agent"),
    (Join-Path $env:LOCALAPPDATA "Programs\XeroxGo Printer Agent"),
    (Join-Path $env:ProgramFiles "XeroxGo Printer Agent"),
    $PSScriptRoot
) + $discoveredInstallDirs | Select-Object -Unique

foreach ($dir in $installDirs) {
    if ($dir -and (Test-Path $dir)) {
        Remove-Item -Path $dir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 4. Remove user data, config, logs, and temp queues
Write-Host "Removing configuration and data..."
$dataDir = Join-Path $env:LOCALAPPDATA "XeroxGo"
if (Test-Path $dataDir) {
    Remove-Item -Path $dataDir -Recurse -Force -ErrorAction SilentlyContinue
}

# 5. Remove startup registration
Write-Host "Removing startup registration..."
$runRegs = @(
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run",
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run"
)
foreach ($runReg in $runRegs) {
    if (Test-Path $runReg) {
        Remove-ItemProperty -Path $runReg -Name "XeroxGoPrinterAgent" -Force -ErrorAction SilentlyContinue
    }
}

# 6. Remove shortcuts
$shortcuts = @(
    (Join-Path ([Environment]::GetFolderPath('Desktop')) "XeroxGo Agent.lnk"),
    (Join-Path ([Environment]::GetFolderPath('Programs')) "XeroxGo Agent.lnk"),
    (Join-Path ([Environment]::GetFolderPath('CommonDesktop')) "XeroxGo Agent.lnk"),
    (Join-Path ([Environment]::GetFolderPath('CommonPrograms')) "XeroxGo Agent.lnk"),
    (Join-Path ([Environment]::GetFolderPath('Desktop')) "XeroxGo Printer Agent.lnk"),
    (Join-Path ([Environment]::GetFolderPath('Programs')) "XeroxGo Printer Agent.lnk")
)
foreach ($sc in $shortcuts) {
    if ($sc -and (Test-Path $sc)) {
        Remove-Item $sc -Force -ErrorAction SilentlyContinue
    }
}

# 7. Remove certificates
Write-Host "Removing certificates..."
$certStores = @(
    "Cert:\CurrentUser\TrustedPublisher",
    "Cert:\CurrentUser\Root",
    "Cert:\LocalMachine\TrustedPublisher",
    "Cert:\LocalMachine\Root"
)
foreach ($store in $certStores) {
    if (Test-Path $store) {
        Get-ChildItem -Path $store -ErrorAction SilentlyContinue |
            Where-Object { $_.Subject -like "*CN=XeroxGo Technologies*" } |
            ForEach-Object {
                Remove-Item -Path $_.PSPath -Force -ErrorAction SilentlyContinue
            }
    }
}

# 8. Clean temporary files
$tempDir = Join-Path $env:TEMP "XeroxGoInstall"
if (Test-Path $tempDir) {
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Uninstall complete." -ForegroundColor Green
Write-Host ""
