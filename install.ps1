<#
==============================================================================
 XeroxGo Agent Setup Manager
 Usage:
   irm https://raw.githubusercontent.com/bibin-benny-cse/campusprint-printer-agent/main/install.ps1 | iex
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

function Get-InstalledPath {
    $uninstallRegKeys = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D37F619C-5582-4BC2-91DE-7A419266F3A1}_is1",
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D37F619C-5582-4BC2-91DE-7A419266F3A1}_is1"
    )
    foreach ($reg in $uninstallRegKeys) {
        if (Test-Path $reg) {
            $loc = (Get-ItemProperty -Path $reg -Name "InstallLocation" -ErrorAction SilentlyContinue).InstallLocation
            if ($loc -and (Test-Path $loc)) { return $loc }
        }
    }
    $candidateDirs = @(
        (Join-Path $env:LOCALAPPDATA "Programs\XeroxGo Agent"),
        (Join-Path $env:ProgramFiles "XeroxGo Agent"),
        (Join-Path $env:LOCALAPPDATA "Programs\XeroxGo Printer Agent"),
        (Join-Path $env:ProgramFiles "XeroxGo Printer Agent")
    )
    foreach ($dir in $candidateDirs) {
        if ($dir -and (Test-Path (Join-Path $dir "XeroxGo.PrinterAgent.exe"))) { return $dir }
    }
    return $null
}

function Invoke-LocalUninstall {
    $installDir = Get-InstalledPath
    $localScript = if ($installDir) { Join-Path $installDir "uninstall.ps1" } else { $null }

    if ($localScript -and (Test-Path $localScript)) {
        & $localScript
        return
    }

    # Fallback uninstallation if local script was removed
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

    # 2. Run native uninstaller
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

    # 3. Remove application files
    Write-Host "Removing application files..."
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

    # 4. Remove configuration and data
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
}

function Invoke-Install {
    Write-Host ""
    Write-Host "XeroxGo Agent Setup" -ForegroundColor Cyan
    Write-Host "-------------------" -ForegroundColor DarkGray

    # 1. Prepare temporary directory
    $tempDir = Join-Path $env:TEMP "XeroxGoInstall"
    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    }

    $installerPath = Join-Path $tempDir "XeroxGoAgent-Setup.exe"
    $certPath      = Join-Path $tempDir "XeroxGo-Publisher-Certificate.cer"

    # 2. Download installer
    $installerUrl = "$ReleaseBaseUrl/XeroxGoAgent-Setup.exe"
    Write-Host "Downloading installer..."
    try {
        Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
    } catch {
        Write-Host "Error: Failed to download installer from: $installerUrl" -ForegroundColor Red
        return
    }

    # 3. Register publisher certificate for current user
    $certUrl = "$ReleaseBaseUrl/XeroxGo-Publisher-Certificate.cer"
    try {
        Invoke-WebRequest -Uri $certUrl -OutFile $certPath -UseBasicParsing
        if (Get-Command Import-Certificate -ErrorAction SilentlyContinue) {
            Import-Certificate -FilePath $certPath -CertStoreLocation "Cert:\CurrentUser\TrustedPublisher" | Out-Null
            Import-Certificate -FilePath $certPath -CertStoreLocation "Cert:\CurrentUser\Root" -ErrorAction SilentlyContinue | Out-Null
        } else {
            & certutil -user -addstore "TrustedPublisher" $certPath | Out-Null
        }
    } catch {}

    # 4. Remove Mark of the Web
    if (Get-Command Unblock-File -ErrorAction SilentlyContinue) {
        Unblock-File -Path $installerPath -ErrorAction SilentlyContinue
    }

    # 5. Launch installer
    Write-Host "Starting setup wizard..." -ForegroundColor Green
    Write-Host ""
    $installArgs = if ($Silent) { @("/SILENT", "/VERYSILENT", "/SUPPRESSMSGBOXES") } else { @() }
    Start-Process -FilePath $installerPath -ArgumentList $installArgs -Wait

    # 6. Ensure local uninstaller exists in installation directory
    $installedDir = Get-InstalledPath
    if ($installedDir) {
        $targetUninstall = Join-Path $installedDir "uninstall.ps1"
        if (-not (Test-Path $targetUninstall)) {
            $uninstallUrl = "$ReleaseBaseUrl/uninstall.ps1"
            $rawUninstallUrl = "https://raw.githubusercontent.com/$RepoOwner/$RepoName/main/XeroxGo.PrinterAgent/Installer/uninstall.ps1"
            try {
                Invoke-WebRequest -Uri $uninstallUrl -OutFile $targetUninstall -UseBasicParsing -ErrorAction Stop
            } catch {
                try {
                    Invoke-WebRequest -Uri $rawUninstallUrl -OutFile $targetUninstall -UseBasicParsing -ErrorAction SilentlyContinue
                } catch {}
            }
        }
    }
}

# Determine Action
$installedPath = Get-InstalledPath

if ($Uninstall -or ($Action -eq 'uninstall') -or ($env:XEROXGO_ACTION -eq 'uninstall') -or ($env:UNINSTALL -eq '1')) {
    Invoke-LocalUninstall
} elseif ($Install -or ($Action -eq 'install')) {
    Invoke-Install
} elseif ($installedPath) {
    Write-Host ""
    Write-Host "XeroxGo Agent Setup" -ForegroundColor Cyan
    Write-Host "-------------------" -ForegroundColor DarkGray
    Write-Host "An existing installation was found." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  1) Reinstall"
    Write-Host "  2) Uninstall"
    Write-Host "  3) Cancel"
    Write-Host ""
    $choice = Read-Host "Select an option [1-3]"
    switch ($choice) {
        "1" {
            Write-Host ""
            Write-Host "Reinstalling XeroxGo Agent..." -ForegroundColor Cyan
            Invoke-LocalUninstall
            Start-Sleep -Seconds 1
            Invoke-Install
        }
        "2" {
            Invoke-LocalUninstall
        }
        default {
            Write-Host "Cancelled." -ForegroundColor DarkGray
        }
    }
} else {
    Invoke-Install
}
