<#
==============================================================================
 XeroxGo Agent - Unified Setup Manager (Install / Clean Uninstall)
 
 Quick One-Liners:
   Install / Update:
     irm https://raw.githubusercontent.com/bibin-benny-cse/campusprint-printer-agent/main/install.ps1 | iex

   Direct Clean Uninstall:
     & ([scriptblock]::Create((irm https://raw.githubusercontent.com/bibin-benny-cse/campusprint-printer-agent/main/install.ps1))) -Uninstall
==============================================================================
#>

param(
    [string]$Action = "",
    [switch]$Uninstall,
    [switch]$Install,
    [switch]$Silent
)

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
$ErrorActionPreference = 'Stop'

$RepoOwner = "bibin-benny-cse"
$RepoName  = "campusprint-printer-agent"
$ReleaseBaseUrl = "https://github.com/$RepoOwner/$RepoName/releases/latest/download"

function Test-IsInstalled {
    $uninstallRegKeys = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D37F619C-5582-4BC2-91DE-7A419266F3A1}_is1",
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D37F619C-5582-4BC2-91DE-7A419266F3A1}_is1"
    )
    foreach ($reg in $uninstallRegKeys) {
        if (Test-Path $reg) { return $true }
    }
    $candidateDirs = @(
        (Join-Path $env:LOCALAPPDATA "Programs\XeroxGo Agent"),
        (Join-Path $env:ProgramFiles "XeroxGo Agent"),
        (Join-Path ${env:ProgramFiles(x86)} "XeroxGo Agent"),
        (Join-Path $env:LOCALAPPDATA "Programs\XeroxGo Printer Agent"),
        (Join-Path $env:ProgramFiles "XeroxGo Printer Agent")
    )
    foreach ($dir in $candidateDirs) {
        if ($dir -and (Test-Path (Join-Path $dir "XeroxGo.PrinterAgent.exe"))) { return $true }
    }
    return $false
}

function Invoke-CleanUninstall {
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "             🗑️  XeroxGo Agent - Clean Uninstaller" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""

    # 1. Stop active processes
    Write-Host "⏳ [1/5] Stopping processes..." -ForegroundColor Yellow
    $processes = Get-Process -Name "XeroxGo.PrinterAgent", "XeroxGoAgent-Setup" -ErrorAction SilentlyContinue
    if ($processes) {
        $processes | Stop-Process -Force
        Start-Sleep -Seconds 1
        Write-Host "   ✓ Stopped active processes." -ForegroundColor Green
    } else {
        Write-Host "   ✓ No active processes running." -ForegroundColor DarkGray
    }

    # 2. Run native uninstaller if present
    Write-Host "⏳ [2/5] Running uninstaller..." -ForegroundColor Yellow
    $uninstallRegKeys = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D37F619C-5582-4BC2-91DE-7A419266F3A1}_is1",
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D37F619C-5582-4BC2-91DE-7A419266F3A1}_is1"
    )
    $discoveredInstallDirs = @()
    $ranNative = $false
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
                    Start-Sleep -Seconds 1
                    $ranNative = $true
                }
            }
            Remove-Item -Path $regPath -Force -Recurse -ErrorAction SilentlyContinue
        }
    }
    if ($ranNative) {
        Write-Host "   ✓ Uninstaller completed." -ForegroundColor Green
    } else {
        Write-Host "   ℹ Native uninstaller not found; running direct cleanup." -ForegroundColor DarkGray
    }

    # 3. Clean files & directories
    Write-Host "⏳ [3/5] Removing application files..." -ForegroundColor Yellow
    $installDirs = @(
        (Join-Path $env:LOCALAPPDATA "Programs\XeroxGo Agent"),
        (Join-Path $env:ProgramFiles "XeroxGo Agent"),
        (Join-Path ${env:ProgramFiles(x86)} "XeroxGo Agent"),
        (Join-Path $env:LOCALAPPDATA "Programs\XeroxGo Printer Agent"),
        (Join-Path $env:ProgramFiles "XeroxGo Printer Agent")
    ) + $discoveredInstallDirs | Select-Object -Unique

    foreach ($dir in $installDirs) {
        if ($dir -and (Test-Path $dir)) {
            Remove-Item -Path $dir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Host "   ✓ Files removed." -ForegroundColor Green

    # 4. Purge configuration and data
    Write-Host "⏳ [4/5] Removing configuration and cache..." -ForegroundColor Yellow
    $dataDir = Join-Path $env:LOCALAPPDATA "XeroxGo"
    if (Test-Path $dataDir) {
        Remove-Item -Path $dataDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "   ✓ Configuration and cache removed." -ForegroundColor Green
    } else {
        Write-Host "   ✓ No configuration found." -ForegroundColor DarkGray
    }

    # Clean shortcuts and startup keys
    $runRegs = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run",
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run"
    )
    foreach ($runReg in $runRegs) {
        if (Test-Path $runReg) {
            Remove-ItemProperty -Path $runReg -Name "XeroxGoPrinterAgent" -Force -ErrorAction SilentlyContinue
        }
    }
    $shortcuts = @(
        (Join-Path ([Environment]::GetFolderPath('Desktop')) "XeroxGo Agent.lnk"),
        (Join-Path ([Environment]::GetFolderPath('Programs')) "XeroxGo Agent.lnk"),
        (Join-Path ([Environment]::GetFolderPath('CommonDesktop')) "XeroxGo Agent.lnk"),
        (Join-Path ([Environment]::GetFolderPath('CommonPrograms')) "XeroxGo Agent.lnk"),
        (Join-Path ([Environment]::GetFolderPath('Desktop')) "XeroxGo Printer Agent.lnk"),
        (Join-Path ([Environment]::GetFolderPath('Programs')) "XeroxGo Printer Agent.lnk"),
        (Join-Path ([Environment]::GetFolderPath('Desktop')) "XeroxGo.lnk"),
        (Join-Path ([Environment]::GetFolderPath('Programs')) "XeroxGo.lnk")
    )
    foreach ($sc in $shortcuts) {
        if ($sc -and (Test-Path $sc)) {
            Remove-Item $sc -Force -ErrorAction SilentlyContinue
        }
    }

    # 5. Remove certificates
    Write-Host "⏳ [5/5] Removing certificates..." -ForegroundColor Yellow
    $certStores = @(
        "Cert:\CurrentUser\TrustedPublisher",
        "Cert:\CurrentUser\Root",
        "Cert:\LocalMachine\TrustedPublisher",
        "Cert:\LocalMachine\Root"
    )
    $removedCerts = 0
    foreach ($store in $certStores) {
        if (Test-Path $store) {
            try {
                $certs = Get-ChildItem -Path $store -ErrorAction SilentlyContinue |
                    Where-Object { $_.Subject -like "*CN=XeroxGo Technologies*" }
                if ($certs) {
                    $certs | ForEach-Object {
                        Remove-Item -Path $_.PSPath -Force -ErrorAction SilentlyContinue
                        $removedCerts++
                    }
                }
            } catch {}
        }
    }
    if ($removedCerts -gt 0) {
        Write-Host "   ✓ Certificates removed." -ForegroundColor Green
    } else {
        Write-Host "   ✓ No certificates found." -ForegroundColor DarkGray
    }

    # Cleanup temp caches & crash dumps
    $tempDir = Join-Path $env:TEMP "XeroxGoInstall"
    if (Test-Path $tempDir) {
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    $crashDumpDir = Join-Path $env:LOCALAPPDATA "CrashDumps"
    if (Test-Path $crashDumpDir) {
        Get-ChildItem -Path $crashDumpDir -Filter "XeroxGo*.dmp" -ErrorAction SilentlyContinue |
            Remove-Item -Force -ErrorAction SilentlyContinue
    }

    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "✨ XeroxGo Agent has been uninstalled." -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Invoke-InstallOrUpdate {
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "              🖨️  XeroxGo Agent - Quick Installer" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""

    # 1. Prepare temporary directory
    $tempDir = Join-Path $env:TEMP "XeroxGoInstall"
    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    }

    $installerPath = Join-Path $tempDir "XeroxGoAgent-Setup.exe"
    $certPath      = Join-Path $tempDir "XeroxGo-Publisher-Certificate.cer"

    # 2. Download installer
    $installerUrl = "$ReleaseBaseUrl/XeroxGoAgent-Setup.exe"
    Write-Host "⏳ [1/3] Downloading installer..." -ForegroundColor Yellow
    try {
        Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
        Write-Host "   ✓ Download complete." -ForegroundColor Green
    } catch {
        Write-Host "   ❌ Download failed from: $installerUrl" -ForegroundColor Red
        return
    }

    # 3. Register publisher certificate for current user
    $certUrl = "$ReleaseBaseUrl/XeroxGo-Publisher-Certificate.cer"
    Write-Host "⏳ [2/3] Registering publisher certificate..." -ForegroundColor Yellow
    try {
        Invoke-WebRequest -Uri $certUrl -OutFile $certPath -UseBasicParsing
        if (Get-Command Import-Certificate -ErrorAction SilentlyContinue) {
            Import-Certificate -FilePath $certPath -CertStoreLocation "Cert:\CurrentUser\TrustedPublisher" | Out-Null
            Import-Certificate -FilePath $certPath -CertStoreLocation "Cert:\CurrentUser\Root" -ErrorAction SilentlyContinue | Out-Null
        } else {
            & certutil -user -addstore "TrustedPublisher" $certPath | Out-Null
        }
        Write-Host "   ✓ Certificate registered." -ForegroundColor Green
    } catch {}

    # 4. Remove Mark of the Web
    Write-Host "⏳ [3/3] Preparing installer..." -ForegroundColor Yellow
    if (Get-Command Unblock-File -ErrorAction SilentlyContinue) {
        Unblock-File -Path $installerPath -ErrorAction SilentlyContinue
    }
    Write-Host "   ✓ Ready to launch." -ForegroundColor Green

    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "🚀 Launching setup wizard..." -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""

    # 5. Launch installer
    if ($Silent) {
        Start-Process -FilePath $installerPath -ArgumentList "/SILENT", "/VERYSILENT", "/SUPPRESSMSGBOXES"
    } else {
        Start-Process -FilePath $installerPath
    }
}

# Determine Action
$targetAction = ""
if ($Uninstall -or ($Action -eq 'uninstall') -or ($env:XEROXGO_ACTION -eq 'uninstall') -or ($env:UNINSTALL -eq '1')) {
    $targetAction = "uninstall"
} elseif ($Install -or ($Action -eq 'install')) {
    $targetAction = "install"
} else {
    # Interactive check: If already installed, offer choice
    if (Test-IsInstalled) {
        Write-Host ""
        Write-Host "================================================================" -ForegroundColor Cyan
        Write-Host "  ℹ XeroxGo Agent is already installed on this system." -ForegroundColor Yellow
        Write-Host "================================================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Please select an option:"
        Write-Host "  [1] Reinstall / Update" -ForegroundColor Green
        Write-Host "  [2] Uninstall" -ForegroundColor Red
        Write-Host "  [Q] Cancel" -ForegroundColor DarkGray
        Write-Host ""
        $choice = Read-Host "Enter choice (1, 2, or Q)"
        if ($choice -eq '2') {
            $targetAction = "uninstall"
        } elseif ($choice -eq '1') {
            $targetAction = "install"
        } else {
            Write-Host "Operation cancelled." -ForegroundColor Yellow
            return
        }
    } else {
        $targetAction = "install"
    }
}

if ($targetAction -eq 'uninstall') {
    Invoke-CleanUninstall
} else {
    Invoke-InstallOrUpdate
}
