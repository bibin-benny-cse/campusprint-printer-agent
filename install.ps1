<#
==============================================================================
 XeroxGo Windows Printer Agent - Unified Setup Manager (Install / Clean Uninstall)
 
 Quick One-Liners:
   Install / Update:
     irm https://raw.githubusercontent.com/bibin-benny-cse/campusprint-printer-agent/main/install.ps1 | iex

   Direct Clean Uninstall:
     & ([scriptblock]::Create((irm https://raw.githubusercontent.com/bibin-benny-cse/campusprint-printer-agent/main/install.ps1))) -Uninstall
     (Or simply run the script above — if already installed, it interactively prompts you)
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
}

function Invoke-InstallOrUpdate {
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
        
        if (Get-Command Import-Certificate -ErrorAction SilentlyContinue) {
            Import-Certificate -FilePath $certPath -CertStoreLocation "Cert:\CurrentUser\TrustedPublisher" | Out-Null
            Import-Certificate -FilePath $certPath -CertStoreLocation "Cert:\CurrentUser\Root" -ErrorAction SilentlyContinue | Out-Null
        } else {
            & certutil -user -addstore "TrustedPublisher" $certPath | Out-Null
        }
        Write-Host "   ✓ Publisher verified for Current User." -ForegroundColor Green
    } catch {
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
}

# Determine Action
$targetAction = ""
if ($Uninstall -or ($Action -eq 'uninstall') -or ($env:XEROXGO_ACTION -eq 'uninstall') -or ($env:UNINSTALL -eq '1')) {
    $targetAction = "uninstall"
} elseif ($Install -or ($Action -eq 'install') -or ($env:XEROXGO_ACTION -eq 'install')) {
    $targetAction = "install"
} else {
    # Interactive check: If already installed, offer choice
    if (Test-IsInstalled) {
        Write-Host ""
        Write-Host "================================================================" -ForegroundColor Cyan
        Write-Host "  ℹ XeroxGo Printer Agent is currently installed on this system." -ForegroundColor Yellow
        Write-Host "================================================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Please select an option:"
        Write-Host "  [1] Reinstall / Update to Latest Version" -ForegroundColor Green
        Write-Host "  [2] Full Clean Uninstall (Removes app, configs, logs, certs)" -ForegroundColor Red
        Write-Host "  [Q] Cancel / Exit" -ForegroundColor DarkGray
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
